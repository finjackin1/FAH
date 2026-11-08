// Effects module (Som side). "efeitos".est {on, modo auto|jogo|musica|filme, niv} = Estéreo+ card (Início, switch "ligado".efeitos); niv = "Nivelar volume" (leveler, 90, only while Estéreo+ is really on);
// "efeitos".cf / .rv = crossfeed (20) and reverb (80), shown on the EQ screen since 04/11 -> follow the EQ switch ("ligado".eq).
// Also reads "ligado" (module switches on the device screen) without owning it.
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Efeitos;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    /// <summary>Som 7.1 engine exists (Modulos/Espacial): its switch blocks crossfeed (same flag in efeitos.js).</summary>
    public const bool Som71Existe = true;

    readonly EstereoMais est = new();
    readonly Crossfeed cf = new();
    readonly Reverb rv = new();
    readonly Nivelador nv = new();
    bool nivOn;
    readonly double[] rp = new double[7];
    bool modOn = true, eqOn = true, s71, estOn = true, cfOn = true, rvOn;
    // Estéreo+ (05/11 v4): out of the head, centre untouched (EstereoMais), profile Jogo/Música/Filme; Auto follows what is playing (same detector as Som 7.1, own copy)
    static readonly Dictionary<string, string> ModoDoTipo = new() { ["Jogo"] = "jogo", ["Música"] = "musica", ["Filme"] = "filme" };
    string modo = "auto", modoAuto = "musica";
    IHost? host; Timer? relogio; FAH.Espacial.Conteudo.Detector? detector; int lendo;
    /// <summary>Profile in use now (Auto resolved).</summary>
    public string ModoEfetivo => modo == "auto" ? modoAuto : modo;

    public string Id => "efeitos";
    public string Nome => "Efeitos";
    public IReadOnlyList<IEfeito> Efeitos { get; }

    public Modulo() => Efeitos = [new Efeito("crossfeed", 20, cf.Preparar, cf.Processar), new Efeito("estereo", 70, est.Preparar, est.Processar), new Efeito("reverb", 80, rv.Preparar, rv.Processar), new Efeito("nivelar", 90, nv.Preparar, nv.Processar)];

    public EstereoMais Estereo => est;
    public Crossfeed Crossfeed => cf;
    public Reverb Reverb => rv;
    public Nivelador Nivelador => nv;

    public void Iniciar(IHost h) { host = h; relogio = new Timer(_ => Observar(), null, 2000, 2000); }
    public void Parar() => relogio?.Dispose();

    void Observar()
    {
        if (modo != "auto" || !modOn || !estOn || s71 || Interlocked.Exchange(ref lendo, 1) == 1) return;
        try { Observar(FAH.Espacial.Conteudo.Tocando(), Environment.TickCount64 / 1000.0); }
        catch (Exception e) { host?.Log("estéreo+ automático: " + e.Message); }
        finally { lendo = 0; }
    }

    /// <summary>Auto step (testable): feeds the detector; on a change applies the profile and tells the UI ("estauto").</summary>
    public bool Observar(IReadOnlyList<(string tipo, string exe)> achados, double agora)
    {
        var d = detector ??= new("Música");
        if (!d.Observar(achados, agora)) return false;
        modoAuto = ModoDoTipo.GetValueOrDefault(d.Tipo, "musica");
        host?.Log($"estéreo+ automático: {d.Tipo} ({d.Fonte})");
        Aplicar();
        host?.Enviar("estauto", new { modo = modoAuto, fonte = d.Fonte });
        return true;
    }

    public bool Receber(string chave, JsonElement v)
    {
        if (chave == "ligado")
        {
            modOn = Bool(v, "efeitos", true); eqOn = Bool(v, "eq", true);
            s71 = Som71Existe && Bool(v, "som71", true);
            Aplicar();
            return false;                       // shared key: other modules may read it too
        }
        if (chave == "dev") { Dev(v); return false; }      // shared key (Som 7.1 reads its part); host only sends it in dev mode
        if (chave != "efeitos") return false;
        if (v.TryGetProperty("est", out var e))
        {
            estOn = Bool(e, "on", true); nivOn = Bool(e, "niv", false);
            if (e.TryGetProperty("modo", out var mo) && mo.ValueKind == JsonValueKind.String) modo = mo.GetString() is "jogo" or "musica" or "filme" ? mo.GetString()! : "auto";
        }
        if (v.TryGetProperty("cf", out var c))
        {
            cfOn = Bool(c, "on", true);
            cf.Nivel = c.TryGetProperty("nivel", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "medio";
        }
        if (v.TryGetProperty("rv", out var r))
        {
            rvOn = Bool(r, "on", false);
            if (r.TryGetProperty("p", out var ps) && ps.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var x in ps.EnumerateArray()) { if (i < rp.Length && x.ValueKind == JsonValueKind.Number) rp[i] = x.GetDouble() / 100; i++; }
                rp[4] *= Reverb.MixMax;          // UI Mix 0..100% = 0..MixMax
                rv.Definir(rp);
            }
        }
        Aplicar();
        return true;
    }

    void Aplicar()
    {
        bool eOn = modOn && estOn;
        // 04/11: Som 7.1 on = Estéreo+ paused (user decided, no combo). crossfeed would narrow what Estéreo+ widens -> paused too
        est.Modo = ModoEfetivo; est.Ligado = eOn && !s71;
        if (est.Erro != null) host?.Log("estéreo+: " + est.Erro);
        cf.Ligado = eqOn && cfOn; cf.Permitido = !s71 && !est.Ligado;
        rv.Ligado = eqOn && rvOn;
        nv.Ligado = est.Ligado && nivOn;
    }

    // ---- 08/11 Dev screen (ui/telas/dev.js, only with usuario/config/dev): every Estéreo+ / leveler knob. Missing key = the shipped value.
    static readonly (int Versao, bool V2, bool VozCentroPlana, double GanhoVoz, double Lado, double Caixa, double GanhoSaidaDb, double CorteCaixaHz,
        double PicoHz, double PicoQ, double PicoDb, double Espalhamento, double HaloDb) PadE = (Caixas2.Versao, EstereoMais.V2, Caixas2.VozCentroPlana,
        Caixas2.GanhoVoz, Caixas2.Lado, Caixas2.Caixa, Caixas2.GanhoSaidaDb, Caixas2.CorteCaixaHz, Caixas2.PicoCaixaHz, Caixas2.PicoCaixaQ,
        Caixas2.PicoCaixaDb, FAH.Espacial.Brir.Espalhamento, FAH.Espacial.Brir.HaloDb);
    static readonly (double Alvo, double Razao, double GanhoMax, double GuardaDb, double Intensidade) PadN =
        (Nivelador.Alvo, Nivelador.Razao, Nivelador.GanhoMax, Nivelador.GuardaDb, Nivelador.Intensidade);
    string estrutura = "";
    Task refazendo = Task.CompletedTask;

    void Dev(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Object) return;
        Caixas2.Forca = Math.Clamp(Num(v, "eInt", FAH.Espacial.Intensidade.Padrao * 100) / 100, 0, 1);
        Caixas2.GanhoVoz = Num(v, "eGanhoVoz", PadE.GanhoVoz); Caixas2.Lado = Num(v, "eLado", PadE.Lado); Caixas2.Caixa = Num(v, "eCaixa", PadE.Caixa);
        Caixas2.GanhoSaidaDb = Num(v, "eGanhoSaidaDb", PadE.GanhoSaidaDb); Caixas2.VozCentroPlana = Bool(v, "eVozCentroPlana", PadE.VozCentroPlana);
        Nivelador.Alvo = Num(v, "nAlvo", PadN.Alvo); Nivelador.Razao = Math.Max(1, Num(v, "nRazao", PadN.Razao));
        Nivelador.GanhoMax = Num(v, "nGanhoMax", PadN.GanhoMax); Nivelador.GuardaDb = Num(v, "nGuardaDb", PadN.GuardaDb);
        Nivelador.Intensidade = Num(v, "nIntensidade", PadN.Intensidade);
        // structural: version / cut / Brir knobs change how the core is built -> rebuild off the audio thread (crossfade)
        int versao = (int)Num(v, "eVersao", PadE.Versao); if (versao is not (3 or 4 or 5)) versao = PadE.Versao;
        var e = (versao, Bool(v, "eV2", PadE.V2), Num(v, "eCorteCaixaHz", PadE.CorteCaixaHz), Num(v, "ePicoHz", PadE.PicoHz), Math.Max(0.1, Num(v, "ePicoQ", PadE.PicoQ)),
            Num(v, "ePicoDb", PadE.PicoDb), Math.Clamp(Num(v, "eEspalhamento", PadE.Espalhamento), 10, 360), Math.Max(0, Num(v, "eHaloDb", PadE.HaloDb)));
        string chave = e.ToString();
        if (chave == estrutura) return;
        bool primeira = estrutura == ""; estrutura = chave;
        if (primeira && e == (PadE.Versao, PadE.V2, PadE.CorteCaixaHz, PadE.PicoHz, PadE.PicoQ, PadE.PicoDb, PadE.Espalhamento, PadE.HaloDb)) return;
        refazendo = refazendo.ContinueWith(_ =>
        {
            try
            {
                Caixas2.Versao = e.versao; EstereoMais.V2 = e.Item2; Caixas2.CorteCaixaHz = e.Item3;
                Caixas2.PicoCaixaHz = e.Item4; Caixas2.PicoCaixaQ = e.Item5; Caixas2.PicoCaixaDb = e.Item6;
                FAH.Espacial.Brir.Espalhamento = e.Item7; FAH.Espacial.Brir.HaloDb = e.Item8;
                Caixas2.Reiniciar(); est.Refazer();
                host?.Log($"dev: estéreo+ refeito {chave}");
            }
            catch (Exception x) { host?.Log("dev estéreo+: " + x.Message); }
        }, TaskScheduler.Default);
    }

    static bool Bool(JsonElement o, string k, bool pad) => o.TryGetProperty(k, out var x)
        ? x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0) : pad;
    static double Num(JsonElement o, string k, double pad) => o.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : pad;

    sealed class Efeito(string id, int pos, Action<int> preparar, SpanAcao proc) : IEfeito
    {
        public string Id => id;
        public Lado Lado => Lado.Som;
        public int Posicao => pos;
        public void Preparar(int taxa) => preparar(taxa);
        public void Processar(Span<float> lr, int quadros) => proc(lr, quadros);
    }

    delegate void SpanAcao(Span<float> lr, int quadros);
}
