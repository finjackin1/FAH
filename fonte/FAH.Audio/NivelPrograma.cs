// 01/11 (user: program sent into the mic must sound as loud as the voice; friends: dense music at the voice's peak level sounded louder).
// Both sides are measured by loudness, as the ear does: K-weighted (BS.1770) mono energy of 100 ms windows, gated mean of the last windows with sound.
// The meter shows peaks, so music (few peaks) stays lower on it than a voice of the same loudness.
// The program's gain = voice level - program level: first 4 windows of sound jump straight to it, then 6 dB/s (down 12 dB/s).
// -24..+40 dB. Then own volume, then a -1 dBFS limiter. Sudden volume changes: see Processar (02/11).
// 02/11 (user: "volume sobe e desce sozinho", "não fica alto"): the target no longer follows the live voice measurement. Quiet
// moments were read as "voice" (log: voz -47 dB while silent, -18 when speaking), so the program fell ~25 dB and came back.
// Fixed target = the loudness FAH's mic chain gives a normal voice (-18, test "programa mandado pro mic").
namespace FAH.Audio;

/// <summary>BS.1770 K-weighting at 48 kHz.</summary>
public sealed class FiltroK
{
    double a1, a2, b1, b2;

    public double Passar(double x)
    {
        double y = 1.53512485958697 * x + a1;
        a1 = -2.69169618940638 * x + 1.69065929318241 * y + a2;
        a2 = 1.19839281085285 * x - 0.73248077421585 * y;
        double z = y + b1;
        b1 = -2 * y + 1.99004745483398 * z + b2;
        b2 = y - 0.99007225036621 * z;
        return z;
    }

    public void Limpar()
    {
        if (Math.Abs(a1) < 1e-25) a1 = 0; if (Math.Abs(a2) < 1e-25) a2 = 0;
        if (Math.Abs(b1) < 1e-25) b1 = 0; if (Math.Abs(b2) < 1e-25) b2 = 0;
    }
}

/// <summary>Loudness of the last blocks kept (400 ms ending at a window, pauses inside it count, as in BS.1770):
/// mean energy, then mean again without blocks 10 dB under it (relative gate).</summary>
sealed class Sonoridade(int blocos)
{
    readonly FiltroK k = new();
    readonly double[] r = new double[blocos], h = new double[4];
    double e;
    int nj, n, pos, hp, hn;
    public int Guardadas => n;
    public float Db { get; private set; } = -100;
    /// <summary>Last 400 ms block, dB.</summary>
    public float Bloco { get; private set; } = -100;
    public void Zerar() { n = 0; pos = 0; }

    /// <summary>True at the end of a window: db = that window's level.</summary>
    public bool Somar(float x, out float db)
    {
        double y = k.Passar(x); e += y * y; db = 0;
        if (++nj < NivelPrograma.Janela) return false;
        double ej = e / nj; e = 0; nj = 0; k.Limpar();
        h[hp] = ej; hp = (hp + 1) % h.Length; if (hn < h.Length) hn++;
        db = (float)(-0.691 + 10 * Math.Log10(ej + 1e-12));
        return true;
    }

    /// <summary>Keeps the block ending at the window just ended.</summary>
    public void Guardar()
    {
        double b = 0; for (int i = 0; i < hn; i++) b += h[i];
        Bloco = (float)(-0.691 + 10 * Math.Log10(b / hn + 1e-12));
        r[pos] = b / hn; pos = (pos + 1) % r.Length; if (n < r.Length) n++;
        double s = 0; for (int i = 0; i < n; i++) s += r[i];
        double lim = s / n * 0.1, s2 = 0; int c = 0;
        for (int i = 0; i < n; i++) if (r[i] >= lim) { s2 += r[i]; c++; }
        Db = (float)(-0.691 + 10 * Math.Log10(s2 / c + 1e-12));
    }
}

public sealed class NivelPrograma
{
    public const int Taxa = 48000, Janela = 4800;
    public const float AlvoVoz = -18;
    /// <summary>Wanted loudness, dB (fixed: AlvoVoz).</summary>
    public volatile float AlvoDb = AlvoVoz;
    public float GanhoDb { get; private set; }
    /// <summary>Program's own loudness (before gain), dB.</summary>
    public float NivelDb { get; private set; } = -100;
    readonly Sonoridade son = new(80);
    float g = 1, gl = 1;
    int janelas, baixo;
    static readonly float Solta = 1 - MathF.Exp(-1f / (0.08f * Taxa)), Teto = MathF.Pow(10, -1f / 20);

    /// <summary>Interleaved stereo. vol = own volume (after leveling).</summary>
    public void Processar(Span<float> lr, float vol)
    {
        int q = lr.Length / 2; if (q == 0) return;
        for (int i = 0; i < q; i++)
        {
            if (!son.Somar(0.5f * (lr[2 * i] + lr[2 * i + 1]), out float db)) continue;
            if (db < -60) continue;                                       // silence: keep the gain
            son.Guardar(); NivelDb = son.Db; float bl = son.Bloco;
            if (janelas >= 4)
            {
                // 02/11 (user: raising the program's volume makes the mic jump; log: +33 dB, gain fell 12 dB/s = ~3 s into the limiter):
                // the last 400 ms over the target cuts the gain at once
                float teto = Math.Clamp(AlvoDb, -40, -6) + 2 - bl; if (GanhoDb > teto) GanhoDb = Math.Max(teto, -24);
                // volume lowered: the gate keeps the old loud blocks ~8 s, so 1.5 s of blocks 10 dB under = start over
                if (bl < NivelDb - 10) { if (++baixo >= 15) { son.Zerar(); janelas = 0; baixo = 0; } } else baixo = 0;
            }
            if (janelas < 4 && ++janelas == 4) GanhoDb = Quer();
        }
        if (janelas >= 4) GanhoDb += Math.Clamp(Quer() - GanhoDb, -12f * q / Taxa, 6f * q / Taxa);
        float g1 = MathF.Pow(10, GanhoDb / 20) * vol, d = (g1 - g) / q;
        for (int i = 0; i < q; i++)
        {
            g += d;
            float l = lr[2 * i] * g, r = lr[2 * i + 1] * g, a = Math.Max(Math.Abs(l), Math.Abs(r));
            float alvo = a > Teto ? Teto / a : 1;
            if (alvo < gl) gl = alvo; else gl += (1 - gl) * Solta;
            lr[2 * i] = l * gl; lr[2 * i + 1] = r * gl;
        }
        g = g1;
    }

    float Quer() => Math.Clamp(Math.Clamp(AlvoDb, -40, -6) - NivelDb, -24, 40);
}

/// <summary>Diagnosis only since 02/11 (log line "mic nível"). 01/11 the voice's loudness in the call (processed voice), over the last ~10 s of speech; from the 5th speech window on.
/// Speech window = peak above -40 dBFS and 15 dB over the quietest windows.</summary>
public sealed class NivelVoz
{
    public volatile float Db = -20;
    readonly Sonoridade son = new(100);
    float pj, piso = -60;

    /// <summary>Interleaved stereo (left read).</summary>
    public void Medir(ReadOnlySpan<float> lr)
    {
        for (int i = 0; i < lr.Length; i += 2)
        {
            float a = Math.Abs(lr[i]); if (a > pj) pj = a;
            if (!son.Somar(lr[i], out _)) continue;
            float db = 20 * MathF.Log10(pj + 1e-9f); pj = 0;
            piso = db < piso ? db : piso + 0.1f;                          // background: drops at once, rises 1 dB/s
            if (db > -40 && db > piso + 15) { son.Guardar(); if (son.Guardadas >= 5) Db = son.Db; }
        }
    }
}
