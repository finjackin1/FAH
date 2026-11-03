// Mic chain tools (mono inside: left channel processed, both channels written). Each one bypasses itself when off.
// Order: 5 mono > 10 low cut > [30 suppressor] > 35 gate > [40 EQ mic] > [50 voice] > 60 de-esser > 70 leveler > 80 auto volume > 95 limiter > 99 meter.
using FAH.Contrato;

namespace FAH.Microfone;

public abstract class Ferramenta(string id, int pos) : IEfeito
{
    public string Id => id;
    public Lado Lado => Lado.Mic;
    public int Posicao => pos;
    protected int Taxa = 48000;
    public virtual void Preparar(int taxa) => Taxa = taxa;
    public abstract void Processar(Span<float> lr, int q);
    protected float Coef(double ms) => (float)Math.Exp(-1.0 / (Math.Max(0.01, ms) * 0.001 * Taxa));
    protected static float Db(float x) => 20 * MathF.Log10(x + 1e-12f);
    protected static float Lin(float db) => MathF.Pow(10, db / 20);
}

/// <summary>Stereo mic -> mono. A dead side (some USB mics send one real channel) is dropped instead of halving the voice.</summary>
public sealed class Mono() : Ferramenta("mono", 5)
{
    double eL, eR; int lado;            // 0 = average, 1 = left only, 2 = right only
    float gL = 0.5f, gR = 0.5f;
    public override void Processar(Span<float> lr, int q)
    {
        double sl = 0, sr = 0;
        for (int i = 0; i < q; i++) { sl += lr[2 * i] * lr[2 * i]; sr += lr[2 * i + 1] * lr[2 * i + 1]; }
        eL = eL * 0.95 + sl * 0.05; eR = eR * 0.95 + sr * 0.05;
        if (eL > 1e-9 || eR > 1e-9)
        {
            if (lado != 1 && eL > 100 * eR) lado = 1; else if (lado != 2 && eR > 100 * eL) lado = 2;
            else if (lado != 0 && eL < 30 * eR && eR < 30 * eL) lado = 0;
        }
        float tL = lado == 0 ? 0.5f : lado == 1 ? 1 : 0, tR = lado == 0 ? 0.5f : lado == 2 ? 1 : 0, p = 1f / q;
        float dL = (tL - gL) * p, dR = (tR - gR) * p;
        for (int i = 0; i < q; i++) { gL += dL; gR += dR; float m = lr[2 * i] * gL + lr[2 * i + 1] * gR; lr[2 * i] = lr[2 * i + 1] = m; }
        gL = tL; gR = tR;
    }
}

/// <summary>"Cortar graves": 80 Hz high-pass, 24 dB/oct (desk knocks, hum, wind).</summary>
public sealed class Corte() : Ferramenta("corte", 10)
{
    public volatile bool Ligado = true;
    readonly Biquad a = new(), b = new();
    float g;                            // 0 = through, 1 = filtered (fade)
    public override void Preparar(int t) { base.Preparar(t); a.PassaAlta(t, 80, 0.541); b.PassaAlta(t, 80, 1.307); }
    public override void Processar(Span<float> lr, int q)
    {
        float alvo = Ligado ? 1 : 0;
        if (alvo == 0 && g == 0) return;
        float p = 1f / 2400;
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i], y = b.Passo(a.Passo(x));
            g += Math.Clamp(alvo - g, -p, p);
            lr[2 * i] = lr[2 * i + 1] = x + (y - x) * g;
        }
        if (g == 0) { a.Zerar(); b.Zerar(); }
    }
}

/// <summary>Noise gate ("Abafar quando você para de falar"): below the threshold for longer than "soltar", the mic drops 30 dB.</summary>
public sealed class Portao() : Ferramenta("gate", 35)
{
    public volatile bool Ligado;
    /// <summary>0..1 (UI "Limite"): threshold -70..-30 dBFS.</summary>
    public volatile float Limite = 0.35f;
    public volatile int SoltarMs = 250;
    public bool Aberto { get; private set; } = true;
    float env, g = 1; int resta;
    public override void Processar(Span<float> lr, int q)
    {
        if (!Ligado && g >= 1) { Aberto = true; return; }
        float thr = Lin(-70 + 40 * Limite), ca = Coef(1), cr = Coef(60), abre = 1f / (0.002f * Taxa), fecha = 1f / (0.08f * Taxa), chao = Lin(-30);
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i], a = Math.Abs(x);
            env = a > env ? a + (env - a) * ca : a + (env - a) * cr;
            if (!Ligado || env > thr) { resta = SoltarMs * Taxa / 1000; Aberto = true; }
            else if (resta > 0) resta--;
            else Aberto = false;
            float alvo = Aberto ? 1 : chao;
            g = alvo > g ? Math.Min(alvo, g + abre) : Math.Max(alvo, g - fecha * (1 - chao));
            lr[2 * i] = lr[2 * i + 1] = x * g;
        }
    }
}

/// <summary>"Suavizar o S": 1st-order split at 4.5 kHz; the top band is pulled down while it dominates (s, x, ch), up to -12 dB.</summary>
public sealed class DeEsser() : Ferramenta("sss", 60)
{
    public volatile float Forca;        // 0..1
    float lp, eA, eT, g = 1, k;
    public override void Preparar(int t) { base.Preparar(t); k = (float)Math.Exp(-2 * Math.PI * 4500 / t); }
    public override void Processar(Span<float> lr, int q)
    {
        float f = Forca;
        if (f <= 0.005f && g >= 0.999f) return;
        float c = Coef(8), lim = 0.62f - 0.45f * f, piso = Lin(-12 * f);
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i];
            lp = x + (lp - x) * k;
            float hp = x - lp;
            eA = Math.Abs(hp) + (eA - Math.Abs(hp)) * c; eT = Math.Abs(x) + (eT - Math.Abs(x)) * c;
            float r = eA / (eT + 1e-9f), alvo = f <= 0.005f || r <= lim ? 1 : Math.Max(piso, lim / r);
            g += (alvo - g) * (alvo < g ? 0.02f : 0.002f);
            lr[2 * i] = lr[2 * i + 1] = lp + hp * g;
        }
    }
}

/// <summary>"Nivelar a voz": RMS compressor, more amount = lower threshold and higher ratio, with make-up.</summary>
public sealed class Nivelador() : Ferramenta("nivel", 70)
{
    public volatile float Forca = 0.5f; // 0..1
    float env, g = 1;
    public override void Processar(Span<float> lr, int q)
    {
        float f = Forca;
        if (f <= 0.005f && Math.Abs(g - 1) < 1e-3f) return;
        float thr = -18 - 14 * f, ratio = 1 + 4 * f, makeup = Lin(-thr * (1 - 1 / ratio) * 0.45f * (f > 0.005f ? 1 : 0)), ca = Coef(10), cr = Coef(180);
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i], p = x * x;
            env = p > env ? p + (env - p) * ca : p + (env - p) * cr;
            float db = 10 * MathF.Log10(env + 1e-12f), red = f <= 0.005f || db <= thr ? 0 : (db - thr) * (1 - 1 / ratio);
            float alvo = Lin(-red) * makeup;
            g += (alvo - g) * 0.01f;
            lr[2 * i] = lr[2 * i + 1] = x * g;
        }
    }
}

/// <summary>"Ajustar volume sozinho": while you speak, slowly brings the voice level (RMS ~0.4 s) to the target. -12..+18 dB, 3 dB/s.</summary>
public sealed class AutoVolume() : Ferramenta("agc", 80)
{
    public volatile bool Ligado;
    /// <summary>Target, dBFS RMS (UI "Volume alvo": -60 + 0.6 * %).</summary>
    public volatile float AlvoDb = -18;
    public float GanhoDb { get; private set; }
    float env, rap, g = 1, piso = -60;
    public override void Processar(Span<float> lr, int q)
    {
        if (!Ligado && Math.Abs(GanhoDb) < 0.05f) { GanhoDb = 0; g = 1; return; }
        float c = Coef(400), cr = Coef(30), alvo = Math.Clamp(AlvoDb, -36, -9), passo = 3f * q / Taxa;
        for (int i = 0; i < q; i++) { float x = lr[2 * i]; env = x * x + (env - x * x) * c; rap = x * x + (rap - x * x) * cr; }
        float nivel = 10 * MathF.Log10(env + 1e-12f), agora = 10 * MathF.Log10(rap + 1e-12f);   // levels coming in (before this gain)
        // background level (fast level): follows drops at once, rises 1 dB/s (speech never counts as background)
        piso = agora < piso ? agora : piso + 1f * q / Taxa;
        bool fala = agora > -50 && agora > piso + 12;
        // only adapts while you speak; never lifts the background above -55 dBFS
        float quer = !Ligado ? 0 : fala ? Math.Clamp(alvo - nivel, -12, 18) : GanhoDb;
        quer = Math.Min(quer, Math.Max(0, -55 - piso));
        GanhoDb += Math.Clamp(quer - GanhoDb, -passo, passo);
        float g1 = Lin(GanhoDb), d = (g1 - g) / q;
        for (int i = 0; i < q; i++) { g += d; lr[2 * i] = lr[2 * i + 1] = lr[2 * i] * g; }
        g = g1;
    }
}

/// <summary>Safety peak limiter, -1 dBFS, 2 ms look-ahead (the voice never clips into the call).</summary>
public sealed class LimitadorMic() : Ferramenta("limitmic", 95)
{
    /// <summary>Ceiling, dBFS (mic EQ screen LIMIT on = its "Teto"; off = -1).</summary>
    public volatile float TetoDb = -1;
    const int Ante = 96;
    readonly float[] atr = new float[Ante];
    int p; float g = 1;
    public override void Processar(Span<float> lr, int q)
    {
        float teto = Lin(Math.Clamp(TetoDb, -24, -0.1f)), solta = 1 - Coef(80);
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i], a = Math.Abs(x), y = atr[p];
            atr[p] = x; p = (p + 1) % Ante;
            float alvo = a > teto ? teto / a : 1;
            if (alvo < g) g = alvo; else g += (1 - g) * solta;
            float o = y * g;
            lr[2 * i] = lr[2 * i + 1] = Math.Clamp(o, -teto, teto);
        }
    }
}

/// <summary>Last: voice level for the UI meter (RMS 50 ms, dBFS) and peak.</summary>
public sealed class Medidor() : Ferramenta("medidor", 99)
{
    float env;
    public float RmsDb => 10 * MathF.Log10(env + 1e-12f);
    public float Pico;
    public override void Processar(Span<float> lr, int q)
    {
        float c = Coef(50), pk = Pico;
        for (int i = 0; i < q; i++) { float x = lr[2 * i]; env = x * x + (env - x * x) * c; if (Math.Abs(x) > pk) pk = Math.Abs(x); }
        Pico = pk;
    }
}
