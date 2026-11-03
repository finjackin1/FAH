// Effects module (Som side): UI state "efeitos" (ui\modulos\efeitos.js) -> stereo (70), crossfeed (20), reverb (80).
// Also reads "ligado" (module switches on the device screen) without owning it.
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Efeitos;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    /// <summary>Som 7.1 engine exists (Modulosspacial): its switch blocks crossfeed (same flag in efeitos.js).</summary>
    public const bool Som71Existe = true;

    readonly Estereo est = new();
    readonly Crossfeed cf = new();
    readonly Reverb rv = new();
    readonly double[] rp = new double[7];
    bool modOn = true, s71, estOn = true, cfOn = true, rvOn;
    double bal, larg = 1; bool mono;

    public string Id => "efeitos";
    public string Nome => "Efeitos";
    public IReadOnlyList<IEfeito> Efeitos { get; }

    public Modulo() => Efeitos = [new Efeito("crossfeed", 20, cf.Preparar, cf.Processar), new Efeito("estereo", 70, est.Preparar, est.Processar), new Efeito("reverb", 80, rv.Preparar, rv.Processar)];

    public Estereo Estereo => est;
    public Crossfeed Crossfeed => cf;
    public Reverb Reverb => rv;

    public void Iniciar(IHost h) { }

    public bool Receber(string chave, JsonElement v)
    {
        if (chave == "ligado")
        {
            modOn = Bool(v, "efeitos", true);
            s71 = Som71Existe && Bool(v, "som71", true);
            Aplicar();
            return false;                       // shared key: other modules may read it too
        }
        if (chave != "efeitos") return false;
        if (v.TryGetProperty("est", out var e))
        {
            estOn = Bool(e, "on", true); bal = Num(e, "bal", 0) / 100; larg = Num(e, "larg", 100) / 100; mono = Bool(e, "mono", false);
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
        // 30/10: width over the 7.1 binaural put the ears out of phase (corr +0.06 -> -0.27 on a back speaker): sound "from the
        // whole area", not from the speaker. Paused like the crossfeed; balance and mono still apply.
        est.Balanco = eOn ? bal : 0; est.Largura = eOn && !s71 ? larg : 1; est.Mono = eOn && mono;
        cf.Ligado = modOn && cfOn; cf.Permitido = !s71;
        rv.Ligado = modOn && rvOn;
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
