// Estéreo+ v4 (05/11/2026): out of the head like the phone's Dolby. User: "voz fica exatamente onde tá", treble/bass "no nível", no echo,
// "parece que o áudio sai um pouco da cabeça", missing = "espacialidade". All time domain, zero latency.
//   m = (L+R)/2 untouched (centre bit-exact: voice never moves)
//   s = (L-R)/2 -> head cue: u = s - g*Sombra(s delayed by Itd)   (each side leaks to the other ear late and shadowed, like a real head)
//   v = u / (1 - g*Sombra(z))   bass back: side at DC = as recorded (v2 lost low mids here), treble untouched (shadow ~0 up high)
//   s' = v + (Largura-1)*HP(v) small flat widening above Corte (v3 doubled side treble = "brilhante")
//   L = m + s', R = m - s', then half-dB loudness compensation. No room, no all-pass.
// History: v1 HRIR+room "sala fechada"; v2 HRIR ±30° "falta uma parte"; v3 M/S x2 + all-pass "diamante bruto" but bright, in the head;
// v3.1-3.3 side-treble filters "HORRÍVEL". v3 source kept at %TEMP%\EstereoMais-v3-1636.cs.
// Profile change = new core + 40 ms crossfade; off = crossfade to dry, then untouched.
// 07/11: v1 = this file as released in 1.2.2 (v4 core; note its Palco was built but never run). v2 = Caixas2.cs (the 7.1 system on two speakers).
namespace FAH.Efeitos;

/// <summary>Itd = interaural delay ms (bigger = speakers further out), Cruza = leak to the other ear (0..0.9), Sombra = head-shadow Hz,
/// Largura = side gain above Corte (1 = as recorded), Abre = re-pan exponent (1 = off, smaller = sounds further apart; see Palco.cs).</summary>
public sealed record PerfilEstereo(double Itd, double Cruza, double Sombra, double Largura, double Corte, double Abre = 1);

public sealed class EstereoMais
{
    public static readonly IReadOnlyDictionary<string, PerfilEstereo> Perfis = new Dictionary<string, PerfilEstereo>
    {
        // 05/11 user: "deixe as caixas no lugar e explore a espacialidade 3d de um alto-falante até o outro, muito maior"
        // analytic search (05/11): side never below recorded, peak <= ~2 dB over Largura, other ear at 400 Hz -10..-13 dB. Physics limit: centre fixed + flat side = only moderate crossfeed
        ["jogo"] = new(0.30, 0.50, 300, 1.00, 100, 0.70),     // v5: widening moved to Palco (no out-of-phase leak)   // precise: speakers just outside the ears
        ["musica"] = new(0.45, 0.60, 300, 1.00, 500, 0.60),
        ["filme"] = new(0.60, 0.40, 300, 1.15, 300, 0.55),  // biggest
    };

    /// <summary>true = Estéreo+ v2 (Caixas2). false = v1 (1.2.2 sound).</summary>
    public static bool V2 = true;

    readonly CompensaVolume comp = new() { Fracao = 0.5 };
    readonly Lock trava = new();
    volatile Nucleo? desejado;
    volatile bool ligado;
    Nucleo? atual, entrando;
    bool trocando; int pos, rampa = 1920, taxa = 48000;
    readonly float[] bufA = new float[Nucleo.Sub * 2], bufB = new float[Nucleo.Sub * 2];
    string modo = "musica";

    public bool Ligado { get => ligado; set { ligado = value; Atualizar(); } }
    public string Modo { get => modo; set { modo = Perfis.ContainsKey(value) ? value : "musica"; Atualizar(); } }
    /// <summary>Last build error: engine stays dry.</summary>
    public string? Erro { get; private set; }
    /// <summary>Core in use or being faded in (tests / diagnostics).</summary>
    public Nucleo? Desejado => desejado;

    public void Preparar(int t)
    {
        taxa = t; comp.Preparar(t); rampa = Math.Max(1, (int)(0.04 * t));
        atual = entrando = null; trocando = false;
        Atualizar();
    }

    void Atualizar()
    {
        if (!ligado) return;                                    // built only when needed
        lock (trava)
        {
            string chave = $"{modo}|{taxa}";
            if (desejado?.Chave == chave) return;
            try { desejado = new Nucleo(chave, Perfis[modo], taxa); Erro = null; }
            catch (Exception e) { Erro = e.Message; desejado = null; }
        }
    }

    public bool Neutro => !ligado && atual == null && !trocando && comp.Neutro;

    public void Processar(Span<float> lr, int quadros)
    {
        var alvo = ligado ? desejado : null;
        if (alvo == null && atual == null && !trocando && comp.Neutro) return;
        double soma = 0;
        for (int i = 0; i < quadros * 2; i++) soma += lr[i] * lr[i];
        for (int ini = 0; ini < quadros; ini += Nucleo.Sub)
        {
            int n = Math.Min(Nucleo.Sub, quadros - ini);
            var bloco = lr.Slice(ini * 2, n * 2);
            if (!trocando && alvo != atual) { entrando = alvo; entrando?.Limpar(); trocando = true; pos = 0; }
            Saida(atual, bloco, bufA, n);
            if (trocando)
            {
                Saida(entrando, bloco, bufB, n);
                for (int i = 0; i < n; i++)
                {
                    float t = Math.Min(1f, (pos + i) / (float)rampa);
                    bloco[2 * i] = bufA[2 * i] + (bufB[2 * i] - bufA[2 * i]) * t;
                    bloco[2 * i + 1] = bufA[2 * i + 1] + (bufB[2 * i + 1] - bufA[2 * i + 1]) * t;
                }
                pos += n;
                if (pos >= rampa) { atual = entrando; entrando = null; trocando = false; }
            }
            else bufA.AsSpan(0, n * 2).CopyTo(bloco);
        }
        comp.Aplicar(soma, lr, quadros);
    }

    static void Saida(Nucleo? n, Span<float> entrada, float[] saida, int quadros)
    {
        if (n == null) entrada[..(quadros * 2)].CopyTo(saida);
        else n.Processar(entrada, saida, quadros);
    }
}

/// <summary>One profile's DSP with its own state. Built off the audio thread; Processar = audio thread only.</summary>
public sealed class Nucleo
{
    public const int Sub = 512;
    public readonly string Chave;
    public readonly PerfilEstereo Perfil;
    readonly double g, bS, bR, gb, kLarg, hpA;
    readonly double[] atraso; int ap;
    double sombraU, sombraV, hpX, hpY;
    readonly Palco? palco;
    readonly Caixas2? caixas;

    public Nucleo(string chave, PerfilEstereo p, int taxa)
    {
        if (p.Cruza < 0 || p.Cruza > 0.9 || p.Itd <= 0 || p.Itd > 1 || p.Largura < 1) throw new ArgumentException("Estéreo+: perfil fora da faixa");
        Chave = chave; Perfil = p; g = p.Cruza;
        bS = 1 - Math.Exp(-2 * Math.PI * p.Sombra / taxa);                     // one-pole head shadow
        bR = bS; gb = g * bR;
        kLarg = p.Largura - 1; hpA = 1 / (1 + 2 * Math.PI * p.Corte / taxa);
        atraso = new double[Math.Max(1, (int)Math.Round(p.Itd * taxa / 1000))];
        if (EstereoMais.V2) caixas = new Caixas2(p.Abre, taxa);
        else if (p.Abre < 1) palco = new Palco(p.Abre, taxa);
    }

    public void Limpar() { sombraU = sombraV = hpX = hpY = 0; Array.Clear(atraso); ap = 0; palco?.Limpar(); caixas?.Limpar(); }

    public void Processar(ReadOnlySpan<float> e, float[] o, int n)
    {
        if (caixas != null) { caixas.Processar(e, o, n); return; }
        for (int i = 0; i < n; i++)
        {
            double l = e[2 * i], r = e[2 * i + 1], m = 0.5 * (l + r), s = 0.5 * (l - r);
            double d = atraso[ap]; atraso[ap] = s; if (++ap == atraso.Length) ap = 0;
            sombraU += bS * (d - sombraU);
            double u = s - g * sombraU;
            // v = u + g*Sombra(v), one-pole solved for this sample: lp = lp1 + b(v - lp1)
            double v = (u + g * sombraV * (1 - bR)) / (1 - gb);
            sombraV += bR * (v - sombraV);
            double hi = hpA * (hpY + v - hpX); hpX = v; hpY = hi;
            double s2 = v + kLarg * hi;
            o[2 * i] = (float)(m + s2); o[2 * i + 1] = (float)(m - s2);
        }
        if (Math.Abs(sombraU) < 1e-25) sombraU = 0;
        if (Math.Abs(sombraV) < 1e-25) sombraV = 0;
        if (Math.Abs(hpY) < 1e-25) hpY = 0;
        for (int i = 0; i < atraso.Length; i++) if (Math.Abs(atraso[i]) < 1e-25) atraso[i] = 0;
    }
}
