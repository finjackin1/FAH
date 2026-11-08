// Effects module (Som side). "efeitos".est {on, modo auto|jogo|musica|filme, niv} = Estéreo+ card (Início, switch "ligado".efeitos); niv = "Nivelar volume" (leveler, 90, only while Estéreo+ is really on);
// "efeitos".cf / .rv = crossfeed (20) and reverb (80), shown on the EQ screen since 04/11 -> follow the EQ switch ("ligado".eq).
// Also reads "ligado" (module switches on the device screen) without owning it.
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Efeitos;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    /// <summary>Som 7.1 engine exists (Modulosspacial): its switch blocks crossfeed (same flag in efeitos.js).</summary>
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
        if (chave != "efeitos") return false;
        if (v.TryGetProperty("est", out var e))
        {
            estOn = Bool(e, "on", true); nivOn = Bool(e, "niv", false);
            Caixas2.Forca = Num(e, "int", FAH.Espacial.Intensidade.Padrao * 100) / 100;
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
