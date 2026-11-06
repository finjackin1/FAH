// Host side of the engine: modules + Som chain, cable capture -> chain -> real output, Windows default guard.
// Path: Windows default = cable "Saída (VAC)" -> capture "Saída -_NS" -> chain -> real output(s).
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FAH.Audio;
using FAH.Contrato;

namespace FAH;

sealed partial class Motor : IHost, IDisposable
{
    /// <summary>Last cable check (UI veil "cabo"); null = still checking.</summary>
    public Achado? Cabo { get; private set; }
    public readonly List<IModulo> Modulos;
    public readonly Cadeia Som = new();
    readonly PadraoWindows guarda = new(Path.Combine(Base.Dados, "padrao-original.json"));
    readonly Dictionary<string, JsonElement> estado = Base.LerEstado();
    readonly Lock trava = new(), tEstado = new();   // tEstado: estado is written from the UI thread and from Ligar
    Fluxo? fluxo;
    readonly List<Destino> destinos = [];
    public Action<string, object>? AoEnviar;          // -> UI
    public bool Ligado => fluxo != null;
    public string? Erro { get; private set; }
    public int Taxa => Fluxo.Taxa;
    readonly System.Threading.Timer saude;
    long[] antes = new long[8];
    long errosMic;

    public Motor()
    {
        PadraoWindows.Log = Log;
        AtivarCabos();
        var erros = new List<string>();
        Modulos = Carregador.Carregar(Base.Modulos, erros);
        foreach (var e in erros) Log("módulo recusado: " + e);
        foreach (var m in Modulos) { try { m.Iniciar(this); } catch (Exception e) { Log($"{m.Id}: {e.Message}"); } ObservarPreparo(m); }
        Som.Definir(Modulos.SelectMany(m => m.Efeitos).Where(e => e.Lado == Lado.Som));
        Ordenar();
        IniciarMic();
        foreach (var (k, v) in estado) Entregar(k, v);
        SincronizarClones();
        Task.Run(SincronizarBypass);                  // MTA: its device objects are used later from timer threads
        saude = new(_ => Saude(), null, 15000, 15000);
        VigiarPadrao();
        VigiarChamadas();
    }

    /// <summary>Every 15 s: one log line only if something went wrong (dropout, overflow, long gap, slow block).</summary>
    void Saude()
    {
        try
        {
            if (fluxoMic is { } fm && fm.Erros > errosMic)
            {
                Log($"mic: cadeia falhou em {fm.Erros - errosMic} blocos (voz passa crua): {fm.UltimoErro}");
                errosMic = fm.Erros;
            }
            var f = fluxo; var ds = destinos.ToArray();
            if (f == null || ds.Length == 0) return;
            var d = ds[0]; var lt = d.Principal;
            long[] agora = [lt.Subcargas, lt.Descartes, lt.Anel.Transbordos, f.Transbordos, f.Erros, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
            long D(int i) => Math.Max(0, agora[i] - antes[i]);
            bool ruim = D(0) + D(1) + D(2) + D(3) + D(4) > 0 || f.MaiorVaoMs > 25 || d.MaiorVaoMs > 25 || f.PiorMs > 8;
            if (ruim)
                Log($"saúde: faltas +{D(0)} (alvo {lt.AlvoMs:0} ms, fila {lt.FilaMs:0} ms), descartes +{D(1)}, transb saída +{D(2)} entrada +{D(3)}, erros +{D(4)}, " +
                    $"vão entrada {f.MaiorVaoMs:0} ms, vão saída {d.MaiorVaoMs:0} ms, bloco pior {f.PiorMs:0.0} ms, uso {f.UsoPct:0}%, gc +{D(5)}/+{D(6)}/+{D(7)}");
            antes = agora; f.MaiorVaoMs = 0; d.MaiorVaoMs = 0; f.PiorMs = 0;
        }
        catch { }
    }

    public IReadOnlyDictionary<string, JsonElement> Estado => estado;

    // ---- IHost
    public void Enviar(string tipo, object dados) => AoEnviar?.Invoke(tipo, dados);
    public string PastaDados(string id) => Base.Pasta(Path.Combine("dados", id));
    public void Log(string t) => Base.Log(t);

    /// <summary>UI changed a state key: modules get it, saved for next run.</summary>
    public void Receber(string chave, JsonElement valor)
    {
        lock (tEstado) estado[chave] = valor.Clone();
        if (chave == "ordem") Ordenar(); else Entregar(chave, valor);
        lock (tEstado) Base.GravarEstado(estado);
    }

    void Entregar(string chave, JsonElement v)
    {
        foreach (var m in Modulos) { try { if (m.Receber(chave, v)) return; } catch (Exception e) { Log($"{m.Id} {chave}: {e.Message}"); } }
    }

    /// <summary>User order of the Som modules (UI key "ordem", e.g. ["efeitos","eq"]). Som 7.1 always first (it needs the
    /// 8 raw channels), limiter (95+) always last; inside a module its effects keep their own Posicao.</summary>
    void Ordenar()
    {
        List<string> ordem = estado.TryGetValue("ordem", out var o) && o.ValueKind == JsonValueKind.Array
            ? [.. o.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)] : ["eq", "efeitos"];
        int Grupo(IModulo m, IEfeito e) { if (e.Posicao >= 90) return 99; if (m.Id == "som71") return 0; int i = ordem.IndexOf(m.Id); return i < 0 ? 50 : 1 + i; }
        Som.Ordenar(Modulos.SelectMany(m => m.Efeitos.Where(e => e.Lado == Lado.Som).Select(e => (e, g: Grupo(m, e))))
            .OrderBy(x => x.g).ThenBy(x => x.e.Posicao).Select(x => x.e));
        Log("ordem: " + string.Join(" > ", Som.Efeitos.Select(e => e.Id)));
    }

    /// <summary>VAC pair for the 7.1 switch (only VAC has a channel setting FAH can change); (null, "") otherwise.</summary>
    internal static (Aparelho? cabo, string captar) AcharCabo()
    {
        var a = Cabos.Detectar();
        return a.Cabo is { Vac: true } c ? (c.Tocar, c.Captar.Nome) : (null, "");
    }

    /// <summary>Cable check for the veil (start, Ligar, "Verificar de novo"). Logs once per change.</summary>
    public Achado ConferirCabo()
    {
        var antes = Cabo;
        Cabos.Log = antes == null ? Log : null;
        var a = LigarOutroLado(Cabos.Detectar());
        Cabos.Log = null;
        if (antes != null && (antes.Estado != a.Estado || antes.Cabo?.Tocar.Nome != a.Cabo?.Tocar.Nome)) Log($"cabo: {a.Estado} {a.Cabo?.Tocar.Nome ?? a.Marca} {a.Detalhe}".TrimEnd());
        Cabo = a;
        if (a.Estado == "falta" && !instalaTentou)   // no cable at all: install (once until a cable shows up again)
        {
            if (TomarInstala()) { instalaTentou = true; Task.Run(ExecutarInstala); }
            else if (Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) instalaErro ??= "Simulação: o FAH não instala cabos.";   // locked screen needs a button
        }
        CaboStatus();
        return a;
    }

    public void CaboStatus() => EnviarCabo();

    void EnviarCabo(string? fase = null, int? pct = null)
    {
        var a = Cabo;
        if (a == null) return;
        if (a.Estado == "achou") { instalaReiniciar = false; instalaErro = null; instalaTentou = false; }   // cable removed later = install again
        var (estado, erro) = Cabos.EstadoParaUi(a.Estado, Volatile.Read(ref instalaOcupado) == 1, instalaReiniciar, instalaErro);
        if (estado == "instalando") { if (fase != null) { instalaFase = fase; instalaPct = pct; } fase = instalaFase; pct = instalaPct; } else { fase = null; pct = null; }
        Enviar("cabo", new { estado, marca = a.Cabo?.Marca ?? a.Marca, nome = a.Cabo?.Tocar.Nome, desativados = a.Desativados, vac = a.Cabo?.Vac ?? false, erro, fase, pct });
    }

    string? caboLigado;     // cable Som opened last (only read while Som is on; the 7.1 switch reopens the same one)

    /// <summary>"Verificar de novo" / cable screen: re-detects and applies the difference. Som the user turned off stays off;
    /// a new cable takes over only what depends on it (Cabos.Replanejar).</summary>
    public void RefazerCabo()
    {
        lock (trava)
        {
            if (encerrando) return;
            bool tinha = Cabo?.Cabo != null;
            var a = ConferirCabo();
            var p = Cabos.Replanejar(tinha, a.Cabo?.Tocar.Id, Ligado, caboLigado, caboMic?.Tocar.Id, SemCaboMic);
            if (p.RefazerMic) { Log("cabo do microfone passou para o som, procurando outro"); FecharMic(); guardaMic.Devolver(); }
            if (p.TrocarSom) { Log($"cabo do som trocado para {a.Cabo!.Tocar.Nome}"); Fechar(); guarda.Devolver(); }
            if (p.TrocarSom || p.LigarSom) Ligar();
            if (p.LigarMic) LigarMic();
        }
        Cabo71Status();
        StatusMic();
    }

    public bool Ligar() => Ligar(false);

    bool Ligar(bool religar)
    {
        lock (trava)
        {
            if (fluxo != null) return true;
            if (encerrando) return false;
            Erro = null;
            if (!religar && guarda.Devolver()) Log("padrão do Windows devolvido (a última sessão caiu)");
            var ach = ConferirCabo();
            if (ach.Cabo is not { } cb) { Erro = null; Status(); return false; }   // the veil explains it (no extra error line)
            var cabo = cb.Tocar; string captar = cb.Captar.Nome;
            // real output = current Windows default (if it is not the cable), else the last one we played to
            // (reconnects run with the cable as default: 7.1 switch), else first real one
            var reais = Aparelhos.Listar(true).Where(a => !a.Virtual).ToList();
            var pad = reais.FirstOrDefault(a => a.Padrao);
            var ultima = estado.TryGetValue("saida", out var us) && us.ValueKind == JsonValueKind.String ? us.GetString() : null;
            var saida = pad ?? reais.FirstOrDefault(a => a.Nome == ultima) ?? reais.FirstOrDefault();
            if (saida == null) return Falha("Nenhuma saída de som real encontrada.");
            if (!religar && cb.Vac) Garantir71(cabo, captar);
            try
            {
                var d = new Destino(saida.Nome); d.AoPerder = x => PerdeuSaida(d, x); d.Abrir(); destinos.Add(d);
                if (!religar && d.CanaisReais > 2) Aviso("Essa saída tem mais de 2 canais: o som toca só na frente esquerda e direita.");
                if (saida.Nome != ultima) lock (tEstado) { estado["saida"] = JsonSerializer.SerializeToElement(saida.Nome); Base.GravarEstado(estado); }
                var f = new Fluxo(captar, Som, cb.Loopback); f.AoPerder = x => PerdeuCabo(f, x); f.DefinirDestinos(destinos); f.Abrir(); fluxo = f; caboLigado = cabo.Id;
                PrenderChamadas(saida);
                guarda.Trocar(cabo.Id, saida.Id);
                if (encerrando) guarda.Devolver();          // exit's restore ran while we were switching: undo ours
                LancarGuardiao();
            }
            catch (Exception e)
            {
                Fechar();
                guarda.Devolver();
                Log("não ligou: " + e);
                return Falha(ErroAudio.Amigo(e) ?? "Não deu para ligar o som. Os detalhes ficaram no relatório do FAH.");
            }
            Log($"ligado: {captar} ({fluxo?.Canais} canais) -> {string.Join(", ", destinos.Select(d => d.NomeReal))}");
            if (!religar && cb.Vac) Task.Run(() => Cabo71Status());
            Status();
            return true;
        }
    }

    /// <summary>Cable always 7.1 (switch removed 28/10), both sides checked on every open. 01/11: play side stuck at 2 ch while
    /// capture was 8, and only capture was looked at: games got stereo, for days. Runs before FAH opens the cable and makes it the
    /// default, so nothing is playing through it and nothing gets cut. Not in simulation (Windows untouched there).</summary>
    void Garantir71(Aparelho cabo, string captar)
    {
        if (Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) return;
        try
        {
            // 04/11: VAC Lite (stereo only) and a refused/failed switch asked again on every start. Lite: never tried.
            // Failed once on this cable: not at start any more (status explains it, the 7.1 card can still retry).
            if (!Surround71.Suporta(cabo.Nome)) { Log("cabo 7.1: este cabo não tem 7.1 (" + cabo.Nome + "), sem tentar"); return; }
            Surround71.Log = Log;
            var e = Surround71.Estado(cabo.Nome, captar);
            if (e.Tocar == 8 && e.Captar == 8) { Marcar71(cabo.Id, true); return; }
            if (Falhou71(cabo.Id)) { Log("cabo 7.1: administrador recusado antes neste cabo, não pergunto de novo ao abrir"); return; }
            Log($"cabo 7.1: Windows com tocar {e.Tocar} e captar {e.Captar} canais, corrigindo");
            Aviso("O FAH está colocando o cabo em 7.1. Se o Windows pedir permissão, aceite.", "toast");
            var r = Surround71.Ativar(cabo.Nome, captar, true);
            Log("cabo 7.1: " + r.msg);
            Lembrar71(cabo.Id, r);
        }
        catch (Exception x) { Log("cabo 7.1: " + x.Message); }
    }

    /// <summary>Only a refused/failed admin step is remembered (transient failures retry on the next start); success clears it.</summary>
    void Lembrar71(string id, (bool ok, string msg) r)
    {
        if (r.ok) Marcar71(id, true); else if (r.msg == Surround71.SemAdmin) Marcar71(id, false);
    }

    const string ChaveCabo = "cabo71liga", ChaveFalhou = "cabo71falhou";

    bool Falhou71(string id)
    {
        lock (tEstado) return estado.TryGetValue(ChaveFalhou, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() == id;
    }

    void Marcar71(string id, bool ok)
    {
        lock (tEstado)
        {
            if (ok == !Falhou71(id)) return;
            if (ok) estado.Remove(ChaveFalhou); else estado[ChaveFalhou] = JsonSerializer.SerializeToElement(id);
            Base.GravarEstado(estado);
        }
    }

    /// <summary>Why this cable gets no 7.1 from games (null = it can). Shown greyed on the 7.1 card, never as an error toast.</summary>
    string? Motivo71(Cabo? c)
    {
        if (c == null) return null;
        if (!c.Vac) return $"O {c.Marca} só passa estéreo. Para os jogos mandarem 7.1, instale o Virtual Audio Cable (versão completa). O Som 7.1 continua funcionando a partir do estéreo.";
        if (Surround71.Lite(c.Tocar.Nome)) return "O Virtual Audio Cable Lite só passa estéreo. A versão completa do Virtual Audio Cable recebe 7.1 dos jogos. O Som 7.1 continua funcionando a partir do estéreo.";
        // no API: Lite under another name, or the full driver not answering
        if (!Surround71.TemApi()) return "Este Virtual Audio Cable não deixa o FAH mudar para 7.1. Se for a versão Lite, ela só passa estéreo; se for a completa, reinstale o Virtual Audio Cable. O Som 7.1 continua funcionando a partir do estéreo.";
        return null;
    }

    /// <summary>"Jogos e filmes mandam 7.1" switch. Cable stays 8 ch once there (28/10, user wanted no cut at all): on/off only
    /// flips Som.Estereo (fold to stereo inside FAH), no Windows format change, so the playing app is never interrupted.
    /// Cable still stereo: first "on" switches it to 7.1 (below), and it stays.</summary>
    public void Cabo71(bool on)
    {
        lock (tEstado) { estado[ChaveCabo] = JsonSerializer.SerializeToElement(on); Base.GravarEstado(estado); }
        Som.Estereo = !on;
        if (Motivo71(Cabo?.Cabo) != null) { Log("cabo 7.1: este cabo não muda para 7.1 (" + Cabo!.Cabo!.Tocar.Nome + ")"); Cabo71Status(); return; }
        var (cb, cp) = AcharCabo();
        var e = cb == null ? null : Surround71.Estado(cb.Nome, cp);
        if (!on || (e != null && e.Tocar == 8 && e.Captar == 8))
        {
            Log("cabo 7.1: " + (on ? "ligado (cabo já em 7.1, sem troca)" : "desligado: o FAH junta os canais em estéreo (cabo fica em 7.1)"));
            Cabo71Status();
            return;
        }
        Trocar71(true);
    }

    /// <summary>Cable format change (Windows format + VAC channels). Silence kept to the format change itself (28/10, was ~2 s):
    /// lookups before, only our capture closes (the real output stays open), capture reopens at once, check after.</summary>
    void Trocar71(bool on)
    {
        (bool ok, string msg) r;
        var sw = Stopwatch.StartNew(); long tPrep, tFechar, tCabo, tReabrir;
        bool religar = false;
        lock (trava)
        {
            var (cabo, captar) = AcharCabo();
            if (cabo == null) { Enviar("cabo71", new { on = false, erro = "Cabo virtual não encontrado." }); return; }
            Surround71.Log = Log;
            var prep = Surround71.Preparar(cabo.Nome, captar);
            if (prep == null) { Cabo71Status("Não achei os dois lados do cabo no Windows"); return; }
            tPrep = sw.ElapsedMilliseconds;
            bool estava = fluxo != null;
            fluxo?.Fechar(); fluxo = null;
            tFechar = sw.ElapsedMilliseconds;
            bool trocou;
            try { trocou = Surround71.Trocar(prep, on); } catch (Exception e) { trocou = false; Log("cabo 7.1: " + e.Message); }
            tCabo = sw.ElapsedMilliseconds;
            if (estava && destinos.Count > 0)
            {
                try { var f = new Fluxo(captar, Som); f.AoPerder = x => PerdeuCabo(f, x); f.DefinirDestinos(destinos); f.Abrir(prep.Captar); fluxo = f; }
                catch (Exception e) { Log("cabo 7.1: captura não reabriu, religando tudo: " + e.Message); Fechar(); religar = true; }
            }
            else if (estava) { Fechar(); religar = true; }
            tReabrir = sw.ElapsedMilliseconds;
            r = Surround71.Conferir(cabo.Nome, captar, on);
            if (!r.ok && !trocou && on) r = (false, Surround71.SemAdmin);
            if (on) Lembrar71(cabo.Id, r);
        }
        if (religar) Ligar(true);
        Log($"cabo 7.1: {r.msg} (silêncio {tReabrir - tPrep} ms: fechar {tFechar - tPrep}, formato {tCabo - tFechar}, reabrir {tReabrir - tCabo}; antes {tPrep}, total {sw.ElapsedMilliseconds})");
        Cabo71Status(r.ok ? null : r.msg, r.msg);
    }

    public void Cabo71Status(string? erro = null, string? msg = null)
    {
        try
        {
            if (Motivo71(Cabo?.Cabo) is { } motivo) { Enviar("cabo71", new { on = false, canais = 2, suporte = false, motivo }); return; }
            var (cabo, captar) = AcharCabo();
            if (cabo == null) return;
            var e = Surround71.Estado(cabo.Nome, captar);
            bool on = e.Tocar == 8 && e.Captar == 8;
            Enviar("cabo71", new { on, canais = e.Tocar, erro = erro ?? (on ? null : "O cabo do FAH não está em 7.1 no Windows"), msg });
        }
        catch (Exception x) { Log("cabo71: " + x.Message); }
    }

    /// <summary>Mixer "Padrão do Windows" (user OK 28/10: real switch). Mic, or FAH off = Windows default. Som with FAH on =
    /// FAH's real output moves (Windows default stays on the cable, else the EQ would be bypassed) and exit restores to it.</summary>
    public void TrocarPadrao(bool mic, string id)
    {
        try
        {
            var a = Aparelhos.Listar(!mic).FirstOrDefault(x => x.Id == id);
            if (a == null || a.Virtual) return;
            if (mic && fluxoMic != null) { TrocarMicReal(a); return; }
            lock (trava)
            {
                if (mic || fluxo == null) { PadraoWindows.Definir(id, !mic); Log($"padrão do Windows ({(mic ? "microfone" : "som")}): {a.Nome}"); return; }
                lock (tEstado) { estado["saida"] = JsonSerializer.SerializeToElement(a.Nome); Base.GravarEstado(estado); }
                guarda.NovoOriginal(id);
                Fechar();
            }
            Log("saída do FAH trocada para " + a.Nome);
            Ligar(true);
        }
        catch (Exception e) { Log("trocar padrão: " + e.Message); }
    }

    public string? SaidaAtual => fluxo == null ? null : destinos.FirstOrDefault()?.NomeReal is { Length: > 0 } n ? n : destinos.FirstOrDefault()?.Nome;
    /// <summary>Endpoint programs play into while FAH routes sound; null = off.</summary>
    public string? CaboSomEntrada => fluxo != null ? Cabo?.Cabo?.Tocar.Id : null;

    public void Desligar()
    {
        lock (trava) { Fechar(); guarda.Devolver(); Status(); }
    }

    void Fechar()
    {
        fluxo?.Fechar(); fluxo = null;
        foreach (var d in destinos) d.Fechar();
        destinos.Clear();
    }

    bool Falha(string msg) { Erro = msg; Log(msg); Status(); return false; }

    /// <summary>Notice for the UI (ui\base\ponte.js): tipo "toast" = short transient balloon, "erro" = red banner (click = retry).</summary>
    public void Aviso(string texto, string tipo = "toast") => Enviar("aviso", new { texto, tipo });

    readonly List<string> pendentes = [];    // toasts raised before the page loaded (AoEnviar drops them)
    bool paginaPronta;
    /// <summary>Same as Aviso, but kept until the page is up (Janela calls PaginaPronta on NavigationCompleted).</summary>
    void AvisoGuardado(string texto)
    {
        lock (pendentes) { if (!paginaPronta) { pendentes.Add(texto); return; } }
        Aviso(texto);
    }
    public void PaginaPronta()
    {
        string[] fila;
        lock (pendentes) { paginaPronta = true; fila = [.. pendentes]; pendentes.Clear(); }
        foreach (var t in fila) Aviso(t);
    }

    /// <summary>A module may expose a public "Task Preparar()" (background load, e.g. Som 7.1 HRTF set). A fault there would be
    /// lost silently, so the host watches the task: log line + toast. Reflection keeps it out of the contract and the module.</summary>
    void ObservarPreparo(IModulo m)
    {
        if (m.GetType().GetMethod("Preparar", Type.EmptyTypes) is not { } mi || mi.ReturnType != typeof(Task)) return;
        try { if (mi.Invoke(m, null) is Task t) t.ContinueWith(x => FalhaPreparo(m, x.Exception!.GetBaseException()), TaskContinuationOptions.OnlyOnFaulted); }
        catch (Exception e) { FalhaPreparo(m, e.InnerException ?? e); }
    }
    void FalhaPreparo(IModulo m, Exception e)
    {
        Log($"{m.Id}: preparo falhou: {e.GetType().Name}: {e.Message}");
        AvisoGuardado($"O {m.Nome} não carregou, então o som vai tocar sem ele. O motivo ficou no log do FAH.");
    }

    /// <summary>Real output unplugged/invalidated while running (callback from NAudio's playback thread: only queue the work).
    /// No auto-reconnect: path closed, Windows default given back, red banner whose click is the normal "ligar" retry.</summary>
    void PerdeuSaida(Destino d, Exception? ex)
    {
        Log("saída perdeu o aparelho " + d.NomeReal + ": " + (ex?.Message ?? "parou sozinha"));
        Task.Run(() =>
        {
            lock (trava)
            {
                if (fluxo == null || !destinos.Contains(d)) return;
                Fechar(); guarda.Devolver();
                Falha("A saída de som foi desconectada. Clique para tentar de novo.");
            }
        });
    }

    /// <summary>Input cable stopped by itself: same teardown, then the cable check (veil comes back if it is gone).</summary>
    void PerdeuCabo(Fluxo f, Exception? ex)
    {
        Log("cabo perdeu a captura " + f.NomeReal + ": " + (ex?.Message ?? "parou sozinha"));
        Task.Run(() =>
        {
            lock (trava)
            {
                if (fluxo != f) return;
                Fechar(); guarda.Devolver();
                if (ConferirCabo().Cabo != null) Falha("O cabo virtual do FAH parou de enviar som. Clique para tentar de novo.");
                else { Erro = null; Status(); }                       // the veil explains it
            }
        });
    }

    public void Status() => Enviar("motor", new { ligado = Ligado, erro = Erro, entrada = fluxo?.NomeReal, saidas = destinos.Select(d => d.NomeReal).ToArray() });

    /// <summary>Levels for the UI meters (0..1 peaks since last call) + health.</summary>
    public object Niveis()
    {
        var f = fluxo;
        var fm = fluxoMic;
        var r = new { entrada = f?.Pico ?? 0, uso = Math.Round(f?.UsoPct ?? 0, 2), mic = fm?.Pico ?? 0, micSai = destMic?.Pico ?? 0,
            saidas = destinos.Select(d => new { nome = d.NomeReal, pico = d.Pico, fila = Math.Round(d.Principal.FilaMs, 1), faltas = d.Principal.Subcargas }).ToArray() };
        if (f != null) f.Pico = 0;
        if (fm != null) fm.Pico = 0;
        if (destMic != null) destMic.Pico = 0;
        foreach (var d in destinos) d.Pico = 0;
        return r;
    }

    /// <summary>Separate process waits for our PID; if we die with the guard pending, it gives the default back.</summary>
    bool guardiao;

    void LancarGuardiao()
    {
        if (guardiao) return;
        guardiao = true;
        try { Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--guardiao {Environment.ProcessId}") { UseShellExecute = false, CreateNoWindow = true }); }
        catch (Exception e) { Log("guardião: " + e.Message); }
    }

    public static void Guardiao(int pid)
    {
        try { Process.GetProcessById(pid).WaitForExit(); } catch { }
        Thread.Sleep(500);
        PadraoWindows.Log = Base.Log;
        if (new PadraoWindows(Path.Combine(Base.Dados, "padrao-original.json")).Devolver()) Base.Log("guardião devolveu o padrão do Windows");
        if (new PadraoWindows(Path.Combine(Base.Dados, "padrao-mic-original.json"), false).Devolver()) Base.Log("guardião devolveu o microfone padrão do Windows");
        SoltarChamadasOrfas();
    }

    int devolvido;

    /// <summary>Windows defaults (sound + mic) back to what they were before FAH, right now. Exit, logoff/shutdown (Janela:
    /// WM_QUERYENDSESSION) and Dispose call it; runs once, without the engine lock (a stuck audio call must not keep Windows on the
    /// cable). Watching stops first so nothing pulls the default back to the cable afterwards.</summary>
    public void DevolverPadroes(string motivo)
    {
        if (Interlocked.Exchange(ref devolvido, 1) == 1) return;
        PararVigiaPadrao();
        // wait (bounded) for a Tratar/Ligar already inside the lock to finish, so it can't put the cable back after the restore
        if (trava.TryEnter(500)) trava.Exit();
        try { Log($"saída ({motivo}): devolvendo o padrão do Windows"); guarda.Devolver(); } catch (Exception e) { Log("devolver padrão: " + e.Message); }
        try { guardaMic.Devolver(); } catch (Exception e) { Log("devolver microfone padrão: " + e.Message); }
        Log("saída: padrão do Windows devolvido");
    }

    public void Dispose()
    {
        // if exit hangs (even inside the restore), end the process anyway (cables then stay on: harmless, next exit turns them off;
        // the guardian process restores a guard that is still pending)
        new Thread(() => { Thread.Sleep(8000); Base.Log("saída travou: encerrando à força"); Process.GetCurrentProcess().Kill(); }) { IsBackground = true }.Start();
        DevolverPadroes("fechar");                              // 02/11: first, before any teardown that could hang
        saude.Dispose();
        PararChamadas();
        var som = Cabo?.Cabo; var mic = caboMic;
        relInj?.Dispose(); relInj = null;
        lock (trava) { FecharMic(); guardaMic.Devolver(); }      // "micliga" stays: the mic comes back on next start
        Log("saída: microfone fechado");
        FecharClones();
        FecharBypass();
        Desligar();
        Log("saída: som fechado");
        foreach (var m in Modulos) { try { m.Parar(); } catch { } }
        mic ??= som == null ? null : Cabos.DetectarMic(som).Cabo;
        DesativarCabos(som, mic);
        Log("saída: pronto");
    }
}
