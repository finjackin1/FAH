// Mic path (B5, docs\contexto\plano-mic.md): real mic -> Fluxo -> Mic chain -> render side of the 2nd cable (VAC Line 2);
// Windows default mic (all roles) = capture side of that cable, so Discord/games get the clean voice. Guard file restores it.
// "Ouvir minha voz" = processed voice also on the headphones; "Gravar 5 s e ouvir" = record the processed voice, play it back.
using System.IO;
using System.Text.Json;
using FAH.Audio;
using FAH.Contrato;

namespace FAH;

sealed partial class Motor
{
    public readonly Cadeia Mic = new();
    readonly PadraoWindows guardaMic = new(Path.Combine(Base.Dados, "padrao-mic-original.json"), false);
    Fluxo? fluxoMic;
    Destino? destMic;
    Cabo? caboMic;
    public bool MicLigado => fluxoMic != null;
    public string? ErroMic { get; private set; }
    /// <summary>Real mic FAH records now (null = mic path off).</summary>
    public string? MicAtual => fluxoMic?.NomeReal;
    /// <summary>Endpoint id FAH plays the voice (+ programs sent into the mic) to; null = mic path off.</summary>
    public string? CaboMicAtual => fluxoMic != null ? caboMic?.Tocar.Id : null;
    /// <summary>Endpoint programs record the FAH mic from; null = mic path off.</summary>
    public string? CaboMicEntrada => fluxoMic != null ? caboMic?.Captar.Id : null;
    const string ChaveMic = "micliga";

    void IniciarMic()
    {
        Mic.Definir(Modulos.SelectMany(m => m.Efeitos).Where(e => e.Lado == Lado.Mic));
        Log("ordem mic: " + string.Join(" > ", Mic.Efeitos.Select(e => e.Id)));
    }

    /// <summary>At start: the mic path always comes on (31/10, user: Line 2 = Windows default mic whenever FAH is open).</summary>
    public void MicInicio() => LigarMic();

    public bool LigarMic() => LigarMic(false);

    bool LigarMic(bool religar)
    {
        lock (trava)
        {
            if (fluxoMic != null) { StatusMic(); return true; }
            if (encerrando) return false;
            ErroMic = null;
            if (!religar && guardaMic.Devolver()) Log("microfone padrão do Windows devolvido (a última sessão caiu)");
            micCaiu = false;
            Desestacionar("mic");
            var som = Cabo?.Cabo ?? ConferirCabo().Cabo;
            var ach = Cabos.DetectarMic(som);
            micPodeLite = ach.PodeLite;
            if (ach.Cabo is not { } cb) { semCaboMic = true; return FalhaMic(SemSegundoCabo(som)); }
            semCaboMic = false;
            var reais = Aparelhos.Listar(false).Where(a => !a.Virtual).ToList();
            var ultimo = estado.TryGetValue("mic", out var um) && um.ValueKind == JsonValueKind.String ? um.GetString() : null;
            var mic = reais.FirstOrDefault(a => a.Padrao) ?? reais.FirstOrDefault(a => a.Nome == ultimo) ?? reais.FirstOrDefault();
            if (mic == null) return FalhaMic("Nenhum microfone encontrado.");
            try
            {
                var d = new Destino(cb.Tocar.Nome); d.AoPerder = x => PerdeuCaboMic(d, x); d.Abrir(); destMic = d;
                var f = new Fluxo(mic.Nome, Mic); f.AoPerder = x => PerdeuMic(f, x); f.DefinirDestinos([d]); f.Escutas = [.. f.Escutas, MedirVoz]; f.Abrir(mic); fluxoMic = f; caboMic = cb;
                guardaMic.Trocar(cb.Captar.Id, mic.Id);
                if (encerrando) guardaMic.Devolver();       // exit's restore ran while we were switching: undo ours
                LancarGuardiao();
                lock (tEstado) { estado["mic"] = JsonSerializer.SerializeToElement(mic.Nome); estado[ChaveMic] = JsonSerializer.SerializeToElement(true); Base.GravarEstado(estado); }
            }
            catch (Exception e)
            {
                FecharMic();
                guardaMic.Devolver();
                Log("o microfone não ligou: " + e);
                return FalhaMic(ErroAudio.Amigo(e, true) ?? "O microfone não ligou. Os detalhes ficaram no relatório do FAH.");
            }
            Log($"microfone ligado: {mic.Nome} ({fluxoMic?.Canais} canais) -> {cb.Tocar.Nome}; programas gravam {cb.Captar.Nome}");
            SincronizarInjecoes();
        }
        StatusMic();
        return true;
    }

    public void DesligarMic()
    {
        lock (trava)
        {
            micCaiu = false; var c = fluxoMic != null ? caboMic : null;
            FecharMic(); guardaMic.Devolver(); Estacionar("mic", c);
            lock (tEstado) { estado[ChaveMic] = JsonSerializer.SerializeToElement(false); Base.GravarEstado(estado); }
        }
        Log("microfone desligado: o Windows voltou a usar o microfone direto");
        StatusMic();
    }

    void FecharMic()
    {
        PararOuvir(); PararOuvirSaida();
        FecharInjecoes();
        fluxoMic?.Fechar(); fluxoMic = null; errosMic = 0;
        destMic?.Fechar(); destMic = null; caboMic = null;
    }

    bool FalhaMic(string msg) { ErroMic = msg; Log(msg); StatusMic(); return false; }

    /// <summary>No 2nd cable: the mic switch is greyed (semCabo) and this says which cable solves it, by the Som cable's brand.</summary>
    bool semCaboMic;
    /// <summary>Mic switch greyed for lack of a 2nd cable (re-checked when the Som cable is checked again).</summary>
    public bool SemCaboMic => semCaboMic && !MicLigado;

    /// <summary>No VAC at all and a Som cable: FAH can install VAC Lite for the mic (button in the mic notice).</summary>
    public bool PodeInstalarMic => SemCaboMic && micPodeLite && Cabo?.Cabo != null;
    volatile bool micPodeLite;

    string SemSegundoCabo(Cabo? som) => som != null && micPodeLite
        ? "Falta um cabo virtual para o microfone. Clique em Instalar cabo do microfone: o FAH baixa o Virtual Audio Cable Lite, que é grátis."
        : SemSegundoFixo(som);

    static string SemSegundoFixo(Cabo? som) => som switch
    {
        { Vac: true } c when Surround71.Lite(c.Tocar.Nome) => "O Virtual Audio Cable Lite tem um cabo só, e ele já leva o som do PC. Para o microfone, instale o VB-CABLE A (VB-Audio) ou a versão completa do Virtual Audio Cable.",
        { Vac: true } when !Surround71.TemApi() => "Falta um segundo cabo para o microfone. Na versão completa do Virtual Audio Cable, mude Cables para 2 no painel. A versão Lite tem um cabo só: instale o VB-CABLE A (VB-Audio).",
        { Vac: true } => "Falta a Line 2 para o microfone. Abra o painel do Virtual Audio Cable, mude Cables para 2 e clique em Set.",
        { Marca: "VB-CABLE" } => "Falta o CABLE-A para o microfone. Instale o VB-CABLE A (VB-Audio) e abra o FAH de novo.",
        { Marca: "Voicemeeter" } => "O Voicemeeter não tem um cabo livre que o FAH consiga usar para o microfone. Instale o VB-CABLE A (VB-Audio) ou o Virtual Audio Cable.",
        { } => "Falta um segundo cabo virtual para o microfone. Instale o VB-CABLE A (VB-Audio) ou aumente Cables para 2 no Virtual Audio Cable.",
        null => "O microfone precisa de um cabo virtual só para ele, além do cabo do som do PC. Sem cabo nenhum, instale um para o som e outro para o microfone.",
    };

    /// <summary>Real mic unplugged while running (capture thread callback: only queue). Notice + the normal retry, no auto-reconnect.</summary>
    void PerdeuMic(Fluxo f, Exception? ex)
    {
        Log("microfone perdeu o aparelho " + f.NomeReal + ": " + (ex?.Message ?? "parou sozinho"));
        Task.Run(() => { lock (trava) { if (fluxoMic != f) return; FecharMic(); guardaMic.Devolver(); FalhaMic("O microfone foi desconectado."); } });
    }

    /// <summary>Second cable (where the clean voice is played) lost while running.</summary>
    void PerdeuCaboMic(Destino d, Exception? ex)
    {
        Log("cabo do microfone perdeu o aparelho: " + (ex?.Message ?? "parou sozinho"));
        Task.Run(() => { lock (trava) { if (destMic != d) return; FecharMic(); guardaMic.Devolver(); micCaiu = true; FalhaMic("O cabo virtual do microfone foi desconectado."); } });
    }

    /// <summary>Mixer "Padrão do Windows" on the mic side while the mic path is on: FAH records another real mic (Windows stays on the cable).</summary>
    void TrocarMicReal(Aparelho a)
    {
        lock (tEstado) { estado["mic"] = JsonSerializer.SerializeToElement(a.Nome); Base.GravarEstado(estado); }
        guardaMic.NovoOriginal(a.Id);
        bool ouvia = leitorOuvir != null, ouviaSai = fluxoSai != null;
        lock (trava) FecharMic();
        Log("microfone do FAH trocado para " + a.Nome);
        LigarMicCom(a);
        if (ouvia) Ouvir(true);
        if (ouviaSai) OuvirSaida(true);
    }

    void LigarMicCom(Aparelho a)
    {
        lock (trava)
        {
            if (fluxoMic != null || encerrando) return;
            var achM = Cabos.DetectarMic(Cabo?.Cabo); var cb = achM.Cabo; micPodeLite = achM.PodeLite;
            if (cb == null) { semCaboMic = true; FalhaMic(SemSegundoCabo(Cabo?.Cabo)); return; }
            semCaboMic = false;
            try
            {
                var d = new Destino(cb.Tocar.Nome); d.AoPerder = x => PerdeuCaboMic(d, x); d.Abrir(); destMic = d;
                var f = new Fluxo(a.Nome, Mic); f.AoPerder = x => PerdeuMic(f, x); f.DefinirDestinos([d]); f.Escutas = [.. f.Escutas, MedirVoz]; f.Abrir(a); fluxoMic = f; caboMic = cb;
                guardaMic.Trocar(cb.Captar.Id, a.Id);
                if (encerrando) guardaMic.Devolver();       // exit's restore ran while we were switching: undo ours
            }
            catch (Exception e)
            {
                FecharMic(); guardaMic.Devolver();
                Log("o microfone não ligou: " + e);
                FalhaMic(ErroAudio.Amigo(e, true) ?? "O microfone não ligou. Os detalhes ficaram no relatório do FAH."); return;
            }
            SincronizarInjecoes();
        }
        StatusMic();
    }

    public void StatusMic() => Enviar("mic", new
    {
        ligado = MicLigado, erro = SemCaboMic ? instalaMicErro ?? ErroMic : ErroMic, semCabo = SemCaboMic, podeInstalar = PodeInstalarMic && !InstalandoCabo, instalaErro = SemCaboMic ? instalaMicErro : null, entrada = fluxoMic?.NomeReal, saida = caboMic?.Captar.Nome, ouvir = leitorOuvir != null, ouvirSai = fluxoSai != null, injeta = Injetados(),
        uso = Math.Round(fluxoMic?.UsoPct ?? 0, 1),
    });

    // ---- hearing yourself
    Destino? destOuvir;                 // own output when the Som path is off
    Leitor? leitorOuvir;
    Action<ReadOnlySpan<float>>? escutaOuvir;

    /// <summary>Output for hearing the mic: FAH's Som output when on, else the Windows default real output.</summary>
    (Destino d, bool proprio)? SaidaOuvir()
    {
        if (destinos.Count > 0) return (destinos[0], false);
        var reais = Aparelhos.Listar(true).Where(a => !a.Virtual).ToList();
        var ultima = estado.TryGetValue("saida", out var us) && us.ValueKind == JsonValueKind.String ? us.GetString() : null;
        var s = reais.FirstOrDefault(a => a.Padrao) ?? reais.FirstOrDefault(a => a.Nome == ultima) ?? reais.FirstOrDefault();
        if (s == null) return null;
        var d = new Destino(s.Nome); d.Abrir(); return (d, true);
    }

    public void Ouvir(bool on)
    {
        lock (trava)
        {
            PararOuvir();
            if (on && fluxoMic is { } f)
            {
                try
                {
                    if (SaidaOuvir() is not var (d, proprio)) { Log("ouvir: nenhuma saída real"); Aviso("Sem saída de som para ouvir."); }
                    else
                    {
                        var lt = d.NovoLeitor("ouvir-mic", 40);
                        escutaOuvir = lr => lt.Empurrar(lr);
                        f.Escutas = [.. f.Escutas, escutaOuvir];
                        leitorOuvir = lt; destOuvir = proprio ? d : null; donoOuvir = d;
                    }
                }
                catch (Exception e) { Log("ouvir: " + e.Message); }
            }
        }
        StatusMic();
    }

    Destino? donoOuvir;

    void PararOuvir()
    {
        if (fluxoMic is { } f && escutaOuvir != null) f.Escutas = [.. f.Escutas.Where(x => x != escutaOuvir)];
        if (leitorOuvir != null) donoOuvir?.Remover(leitorOuvir);
        destOuvir?.Fechar();
        destOuvir = null; donoOuvir = null; leitorOuvir = null; escutaOuvir = null;
    }

    // ---- 01/11 "Ouvir o que sai no microfone": records the cable's capture side (= exactly what Discord gets: voice + programs) onto the headphones
    Fluxo? fluxoSai;
    Leitor? leitorSai;
    Destino? donoSai, destSai;

    public void OuvirSaida(bool on)
    {
        lock (trava)
        {
            PararOuvirSaida();
            if (on && fluxoMic != null && caboMic is { } cb)
            {
                try
                {
                    if (SaidaOuvir() is not var (d, proprio)) { Log("ouvir saída do mic: nenhuma saída real"); Aviso("Sem saída de som para ouvir."); }
                    else
                    {
                        var lt = d.NovoLeitor("ouvir-saida-mic", 60);
                        leitorSai = lt; donoSai = d; destSai = proprio ? d : null;
                        var f = new Fluxo(cb.Captar.Nome, new Cadeia()) { Escutas = [lr => lt.Empurrar(lr)] };
                        f.Abrir(cb.Captar); fluxoSai = f;
                    }
                }
                catch (Exception e) { Log("ouvir saída do mic: " + e.Message); PararOuvirSaida(); }
            }
        }
        StatusMic();
    }

    void PararOuvirSaida()
    {
        try { fluxoSai?.Fechar(); } catch { }
        if (leitorSai != null) donoSai?.Remover(leitorSai);
        destSai?.Fechar();
        fluxoSai = null; leitorSai = null; donoSai = null; destSai = null;
    }

    // ---- 5 s test: record the processed voice, then play it on the headphones
    int testando;

    public void GravarTeste()
    {
        if (fluxoMic is not { } f || Interlocked.Exchange(ref testando, 1) == 1) return;
        Task.Run(() =>
        {
            try
            {
                const int Seg = 5;
                var buf = new float[Fluxo.Taxa * Seg * 2]; int n = 0;
                Action<ReadOnlySpan<float>> grava = lr => { int k = Math.Min(lr.Length, buf.Length - n); if (k > 0) { lr[..k].CopyTo(buf.AsSpan(n)); n += k; } };
                f.Escutas = [.. f.Escutas, grava];
                for (int s = Seg; s > 0; s--) { Enviar("micteste", new { fase = "gravando", resta = s }); Thread.Sleep(1000); }
                f.Escutas = [.. f.Escutas.Where(x => x != grava)];
                Enviar("micteste", new { fase = "tocando", resta = Seg });
                (Destino d, bool proprio)? alvo; lock (trava) alvo = SaidaOuvir();
                if (alvo is var (d, proprio))
                {
                    try
                    {
                        var lt = d.NovoLeitor("teste-mic", 60);
                        try { for (int p = 0; p < n; p += 4800) { lt.Empurrar(buf.AsSpan(p, Math.Min(4800, n - p))); Thread.Sleep(50); } Thread.Sleep(300); }
                        finally { d.Remover(lt); }
                    }
                    finally { if (proprio) d.Fechar(); }   // own output never left open (it held the headphones)
                }
            }
            catch (Exception e) { Log("teste do mic: " + e.Message); }
            finally { Interlocked.Exchange(ref testando, 0); Enviar("micteste", new { fase = "pronto" }); }
        });
    }

    // ---- program sound sent into the mic (Mixer: "Mandar para o microfone"). One process-loopback capture per process,
    // each on its own reader of the cable output (the Destino sums them with the voice). Programs restart = new PIDs: resynced every 1.5 s.
    public Func<string, uint[]>? PidsDe = CapturaPrograma.Raizes;
    readonly Dictionary<uint, (CapturaPrograma c, Leitor l)> capturas = [];
    readonly HashSet<uint> recusados = [];
    readonly HashSet<string> injAvisados = [];
    System.Threading.Timer? relInj;
    int tiqueInj;

    string[] Injetados()
    {
        lock (tEstado) return estado.TryGetValue("micinjeta", out var v) && v.ValueKind == JsonValueKind.Array
            ? [.. v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)] : [];
    }

    public void Injetar(string x, bool on)
    {
        var l = Injetados().ToList();
        if (on && !l.Contains(x)) l.Add(x); else if (!on) l.Remove(x);
        lock (tEstado) { estado["micinjeta"] = JsonSerializer.SerializeToElement(l); Base.GravarEstado(estado); }
        Log((on ? "mandando para o microfone: " : "parou de mandar para o microfone: ") + x);
        SincronizarInjecoes();
        StatusMic();
    }

    void SincronizarInjecoes()
    {
        lock (trava)
        {
            relInj ??= new System.Threading.Timer(_ => { try { SincronizarInjecoes(); } catch { } }, null, 1500, 1500);
            var d = destMic;
            var quer = d == null || PidsDe == null ? [] : Injetados().SelectMany(x => PidsDe(x).Select(p => (p, x))).DistinctBy(q => q.p).ToDictionary(q => q.p, q => q.x);
            foreach (var pid in capturas.Keys.Where(p => !quer.ContainsKey(p)).ToList())
            {
                var (c, l) = capturas[pid]; c.Dispose(); d?.Remover(l); capturas.Remove(pid); xInj.Remove(pid);
            }
            // 01/11 diagnosis (friends: music louder than voice, meter: music low): levels every ~15 s while something is sent
            if (capturas.Count > 0 && ++tiqueInj % 10 == 0)
                Log($"mic nível: alvo {NivelPrograma.AlvoVoz:F0} dB, voz medida {VozDb:F1} dB; " + string.Join("; ", capturas.Select(k => $"{xInj.GetValueOrDefault(k.Key)} {k.Key}: programa {k.Value.c.Nivel?.NivelDb:F1} dB, ganho {k.Value.c.Nivel?.GanhoDb:F1} dB, vol {k.Value.c.Volume:F2}")));
            if (d == null) { AtualizarVivas(true); return; }
            foreach (var (pid, x) in quer.Where(q => !capturas.ContainsKey(q.Key) && !recusados.Contains(q.Key)))
            {
                var l = d.NovoLeitor("programa-" + pid, 60);
                var xx = x; var c = new CapturaPrograma(pid, l) { Ganho = () => GanhoDe("mic", xx), Nivel = new() };
                try { c.Abrir(); capturas[pid] = (c, l); xInj[pid] = x; }
                catch (Exception e)
                {
                    c.Dispose(); d.Remover(l); recusados.Add(pid); Log($"mandar para o microfone (processo {pid}): {e.Message}");
                    if (injAvisados.Add(x)) Aviso(TextoCopia($"Não deu para mandar o som de {x} para o microfone."));   // once per program per run
                }
            }
            AtualizarVivas(true);
        }
    }

    // voice loudness in the call: only logged (02/11: programs go to the fixed NivelPrograma.AlvoVoz, see NivelPrograma.cs)
    readonly NivelVoz nivelVoz = new();
    float VozDb => nivelVoz.Db;
    void MedirVoz(ReadOnlySpan<float> lr) => nivelVoz.Medir(lr);

    void FecharInjecoes()
    {
        foreach (var (c, l) in capturas.Values) { c.Dispose(); destMic?.Remover(l); }
        capturas.Clear(); xInj.Clear(); recusados.Clear(); AtualizarVivas(true);
    }
}
