// Voice changer (UI "Alterar voz"): tone (semitones) and timbre (formants) moved apart by a phase vocoder with the spectral
// envelope kept (cepstrum), robot (zero phase = monotone buzz at 48000/512 = 94 Hz), radio (300-3400 Hz band + drive),
// echo, and the mix with the real voice. Natural voice with no extras = bypass (no delay, no CPU).
// STFT 2048 / hop 512 (Hann, 4x overlap): 43 ms delay while the tone/timbre/robot part runs.
using FAH.Contrato;

namespace FAH.Microfone;

public sealed class Voz : IEfeito
{
    const int N = 2048, H = 512, K = N / 2 + 1, Lifter = 60;
    public string Id => "voz";
    public Lado Lado => Lado.Mic;
    public int Posicao => 50;

    public volatile bool Ligado;
    /// <summary>Semitones -12..12, timbre -100..100, robot/echo/radio/mix 0..1 (mix = altered share, 1 = only altered).</summary>
    public volatile float Tom, Timbre, Robo, Eco, Radio, Mix = 1;

    readonly Fft fft = new(N);
    readonly float[] win = new float[N], ent = new float[N], acc = new float[N], re = new float[N], im = new float[N],
        cr = new float[N], ci = new float[N], mag = new float[K], fase = new float[K], ult = new float[K], soma = new float[K],
        env = new float[K], fina = new float[K], m2 = new float[K], w2 = new float[K], ro = new float[8192], seco = new float[8192];
    int ep, cont, rw, rr, rn, sp;
    float mixV;                     // fade of the STFT part (0 = dry) ~20 ms
    bool rodando;
    readonly Biquad[] banda = [new(), new(), new(), new()];
    readonly float[] eco = new float[48000];
    int epos;
    int taxa = 48000;

    public Voz() { for (int i = 0; i < N; i++) win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N)); }

    public void Preparar(int t)
    {
        taxa = t;
        banda[0].PassaAlta(t, 320, 0.707); banda[1].PassaAlta(t, 320, 0.707); banda[2].PassaBaixa(t, 3300, 0.707); banda[3].PassaBaixa(t, 3300, 0.707);
    }

    bool PrecisaStft => Math.Abs(Tom) > 0.01f || Math.Abs(Timbre) > 0.5f || Robo > 0.005f;

    public void Processar(Span<float> lr, int q)
    {
        bool on = Ligado;
        float rad = on ? Radio : 0, ec = on ? Eco : 0, mx = on ? Mix : 0;
        bool stft = on && PrecisaStft;
        if (!stft && !rodando && rad <= 0.005f && ec <= 0.005f && epos == 0) return;
        if (stft && !rodando) { rodando = true; Array.Clear(ent); Array.Clear(acc); Array.Clear(soma); Array.Clear(ult); cont = 0; rw = rr = rn = 0; mixV = 0; }
        float passo = 1f / 960, drive = 1 + 6 * rad, nd = (float)Math.Tanh(drive);
        float fb = 0.25f + 0.35f * ec;
        int atraso = (int)(0.28 * taxa);
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i], y = x;
            if (rodando)
            {
                ent[ep] = x; ep = (ep + 1) & (N - 1);
                seco[sp] = x;
                if (++cont == H) { cont = 0; Quadro(); }
                float w = 0; if (rn > 0) { w = ro[rr]; rr = (rr + 1) & (ro.Length - 1); rn--; }
                float d = seco[(sp - (N - 1) + seco.Length) & (seco.Length - 1)];
                sp = (sp + 1) & (seco.Length - 1);
                mixV += Math.Clamp((stft ? 1 : 0) - mixV, -passo, passo);
                x = d; y = d + (w - d) * mixV;
            }
            if (rad > 0.005f)
            {
                float b = y; foreach (var f in banda) b = f.Passo(b);
                b = (float)Math.Tanh(b * drive) / nd;
                y += (b - y) * rad;
            }
            float e = eco[epos];
            eco[epos] = y + e * fb * (ec > 0.005f ? 1 : 0);
            epos = ec > 0.005f || Math.Abs(e) > 1e-6f ? (epos + 1) % atraso : 0;
            y += e * 0.6f * ec;
            lr[2 * i] = lr[2 * i + 1] = x + (y - x) * mx;
        }
        if (!stft && mixV <= 0) rodando = false;
    }

    /// <summary>One hop: analysis of the last N samples, tone/timbre/robot, synthesis, overlap-add, H samples out.</summary>
    void Quadro()
    {
        float p = MathF.Pow(2, Tom / 12f), f = MathF.Pow(2, Timbre / 100f * 0.6f), rob = Robo;
        for (int i = 0; i < N; i++) { re[i] = ent[(ep + i) & (N - 1)] * win[i]; im[i] = 0; }
        fft.Fazer(re, im);
        float esp = 2 * MathF.PI * H / N;
        for (int k = 0; k < K; k++)
        {
            float m = MathF.Sqrt(re[k] * re[k] + im[k] * im[k]), ph = MathF.Atan2(im[k], re[k]);
            float d = ph - ult[k] - k * esp; ult[k] = ph;
            d -= 2 * MathF.PI * MathF.Round(d / (2 * MathF.PI));
            mag[k] = m; fase[k] = k * 2 * MathF.PI / N + d / H;      // true frequency, rad/sample
        }
        // spectral envelope: low quefrencies of log|X|
        for (int k = 0; k < N; k++) { int kk = k < K ? k : N - k; cr[k] = MathF.Log(mag[kk] + 1e-9f); ci[k] = 0; }
        fft.Fazer(cr, ci, true);
        for (int k = Lifter; k <= N - Lifter; k++) { cr[k] = 0; ci[k] = 0; }
        fft.Fazer(cr, ci);
        for (int k = 0; k < K; k++) { env[k] = MathF.Exp(cr[k]); fina[k] = mag[k] / (env[k] + 1e-9f); m2[k] = 0; w2[k] = 0; }
        // fine structure (harmonics) moved by p
        for (int k = 0; k < K; k++)
        {
            int j = (int)MathF.Round(k * p); if (j >= K) break;
            if (fina[k] > m2[j]) { w2[j] = fase[k] * p; }
            m2[j] += fina[k];
        }
        // envelope moved by f (timbre), sampled at j/f
        for (int j = 0; j < K; j++)
        {
            float pos = j / f; int a = (int)pos; float fr = pos - a;
            float e = a + 1 < K ? env[a] * (1 - fr) + env[a + 1] * fr : env[K - 1] * 0.1f;
            float m = m2[j] * e;
            soma[j] += w2[j] * H;
            soma[j] -= 2 * MathF.PI * MathF.Floor(soma[j] / (2 * MathF.PI));
            float c = MathF.Cos(soma[j]), s = MathF.Sin(soma[j]);
            re[j] = m * ((1 - rob) * c + rob); im[j] = m * (1 - rob) * s;
        }
        for (int j = 1; j < K - 1; j++) { re[N - j] = re[j]; im[N - j] = -im[j]; }
        fft.Fazer(re, im, true);
        const float norma = 1f / 1.5f;                              // Hann² at 4x overlap sums to 1.5
        for (int i = 0; i < N; i++) acc[i] += re[i] * win[i] * norma;
        for (int i = 0; i < H && rn < ro.Length; i++) { ro[rw] = acc[i]; rw = (rw + 1) & (ro.Length - 1); rn++; }
        Array.Copy(acc, H, acc, 0, N - H); Array.Clear(acc, N - H, H);
    }
}

/// <summary>RBJ biquad, direct form I, one channel.</summary>
public sealed class Biquad
{
    float b0, b1, b2, a1, a2, x1, x2, y1, y2;

    void Coef(double c0, double c1, double c2, double d0, double d1, double d2)
    { b0 = (float)(c0 / d0); b1 = (float)(c1 / d0); b2 = (float)(c2 / d0); a1 = (float)(d1 / d0); a2 = (float)(d2 / d0); }

    public void PassaAlta(int t, double f, double q)
    {
        double w = 2 * Math.PI * f / t, al = Math.Sin(w) / (2 * q), c = Math.Cos(w);
        Coef((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + al, -2 * c, 1 - al);
    }

    public void PassaBaixa(int t, double f, double q)
    {
        double w = 2 * Math.PI * f / t, al = Math.Sin(w) / (2 * q), c = Math.Cos(w);
        Coef((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + al, -2 * c, 1 - al);
    }

    public void PassaFaixa(int t, double f, double q)
    {
        double w = 2 * Math.PI * f / t, al = Math.Sin(w) / (2 * q), c = Math.Cos(w);
        Coef(al, 0, -al, 1 + al, -2 * c, 1 - al);
    }

    public float Passo(float x)
    {
        float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1; x1 = x; y2 = y1; y1 = y;
        if (!float.IsFinite(y)) { x1 = x2 = y1 = y2 = 0; return 0; }
        return y;
    }

    public void Zerar() { x1 = x2 = y1 = y2 = 0; }
}
