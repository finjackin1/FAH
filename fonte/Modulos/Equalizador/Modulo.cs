// Equalizer module: Som side = UI state "eq2" (ui\modulos\equalizador.js) -> EQ + tone + limiter;
// Mic side = "eqmic" (ui\modulos\equalizador-mic.js) -> same EQ + tone on the voice (the mic chain has its own limiter).
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Equalizador;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    // graphic bands of the approved screen (10 bands)
    public static readonly double[] Hz = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    readonly Equalizador eq = new();
    readonly Limitador lim = new();
    readonly Equalizador eqMic = new();
    IHost? host;

    public string Id => "eq";
    public string Nome => "Equalizador";
    public IReadOnlyList<IEfeito> Efeitos { get; }

    public Modulo() => Efeitos = [new Efeito("eq", 40, eq.MudarTaxa, eq.Processar), new Efeito("limit", 95, t => lim.Preparar(t), lim.Processar),
        new Efeito("eqmic", 40, eqMic.MudarTaxa, eqMic.Processar, Lado.Mic)];

    public Equalizador Eq => eq;
    public Equalizador EqMic => eqMic;
    public Limitador Limitador => lim;

    public void Iniciar(IHost h) => host = h;

    // module switch (UI "ligado".eq): off = EQ, tone, AutoEQ and limiter all bypassed
    bool modOn = true, micOn = true;
    (Ajuste aj, bool limite, double teto) ultimo = (new Ajuste { Ligado = false, TomLigado = false }, false, -1);
    Ajuste? ultimoMic;

    public bool Receber(string chave, JsonElement v)
    {
        if (chave == "ligado")
        {
            modOn = !v.TryGetProperty("eq", out var x) || x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0);
            micOn = !v.TryGetProperty("eqmic", out var xm) || xm.ValueKind == JsonValueKind.True || (xm.ValueKind == JsonValueKind.Number && xm.GetDouble() != 0);
            Aplicar();
            return false;                       // shared key
        }
        if (chave == "eqmic") { ultimoMic = Ler(v).Item1; Aplicar(); return false; }   // shared: the mic module takes LIMIT/teto for its limiter
        if (chave != "eq2") return false;
        ultimo = Ler(v);
        Aplicar();
        return true;
    }

    void Aplicar()
    {
        eq.Definir(modOn ? ultimo.aj : new Ajuste { Ligado = false, TomLigado = false });
        lim.TetoDb = ultimo.teto; lim.Ligado = modOn && ultimo.limite;
        eqMic.Definir(micOn && ultimoMic != null ? ultimoMic : new Ajuste { Ligado = false, TomLigado = false });
    }

    /// <summary>UI state -> engine settings. Fields: modo, equ, tone, limit, pre, g[], bass, treble, teto, bandas[{t,f,g,q,on}].</summary>
    public static (Ajuste, bool limite, double teto) Ler(JsonElement v)
    {
        bool B(string k, bool pad) => v.TryGetProperty(k, out var x) ? x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0) : pad;
        double N(JsonElement o, string k, double pad = 0) => o.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : pad;
        var bandas = new List<Banda>();
        bool grafico = !v.TryGetProperty("modo", out var mo) || mo.GetString() != "param";
        if (grafico && v.TryGetProperty("g", out var g))
        {
            double q = Ajuste.QGrafico(Hz.Length); int i = 0;
            foreach (var x in g.EnumerateArray()) { if (i < Hz.Length) bandas.Add(new(Tipo.PassaFaixa, Hz[i], q, x.GetDouble())); i++; }
        }
        else if (!grafico && v.TryGetProperty("bandas", out var bs))
            foreach (var b in bs.EnumerateArray())
            {
                var t = b.TryGetProperty("t", out var tt) ? tt.GetString() : "pico";
                var tipo = t switch { "grave" => Tipo.PratGrave, "agudo" => Tipo.PratAguda, "pb" => Tipo.PassaBaixa, "pa" => Tipo.PassaAlta, _ => Tipo.Pico };
                bool on = !b.TryGetProperty("on", out var o) || o.ValueKind == JsonValueKind.True || (o.ValueKind == JsonValueKind.Number && o.GetDouble() != 0);
                bandas.Add(new(tipo, N(b, "f", 1000), N(b, "q", 1), N(b, "g"), Canais.Ambos, on));
            }
        // autoeq: {nome, fonte, on, pre, b:[[type, Hz, Q, dB]]} = headphone correction, own preamp
        var corr = new List<Banda>(); double corrPre = 0;
        if (v.TryGetProperty("autoeq", out var aq) && aq.ValueKind == JsonValueKind.Object && aq.TryGetProperty("on", out var aon) && aon.ValueKind == JsonValueKind.True
            && aq.TryGetProperty("b", out var ab) && ab.ValueKind == JsonValueKind.Array)
        {
            corrPre = N(aq, "pre");
            foreach (var b in ab.EnumerateArray())
            {
                if (b.ValueKind != JsonValueKind.Array || b.GetArrayLength() < 4) continue;
                var tipo = b[0].GetString() switch { "grave" => Tipo.PratGrave, "agudo" => Tipo.PratAguda, "pico" => Tipo.Pico, _ => (Tipo?)null };
                if (tipo is { } t) corr.Add(new(t, b[1].GetDouble(), b[2].GetDouble(), b[3].GetDouble()));
            }
        }
        var aj = new Ajuste { Ligado = B("equ", true), Preamp = N(v, "pre"), TomLigado = B("tone", true), Graves = N(v, "bass"), Agudos = N(v, "treble"), Bandas = bandas, Correcao = corr, CorrecaoPre = corrPre };
        return (aj, B("limit", false), N(v, "teto", -1));
    }

    sealed class Efeito(string id, int pos, Action<int> preparar, SpanAcao proc, Lado lado = Lado.Som) : IEfeito
    {
        public string Id => id;
        public Lado Lado => lado;
        public int Posicao => pos;
        public void Preparar(int taxa) => preparar(taxa);
        public void Processar(Span<float> lr, int quadros) => proc(lr, quadros);
    }

    delegate void SpanAcao(Span<float> lr, int quadros);
}
