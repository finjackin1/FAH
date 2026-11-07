// Estéreo+ v5 (07/11/2026) user: "parece que os alto-falantes estão virados pra fora... mas algumas coisas ainda não parecem estar lá
// naturalmente, elas se misturam com as outras". Cause: v4 widened with side gain > 1 = hard-panned sounds leak OUT OF PHASE into the other
// speaker (diffuse, smeared) and in-between sounds barely move, so they stay clustered. Fix = direction-preserving re-pan per frequency bin:
//   q = (|L|²-|R|²)/(|L|²+|R|²)  (where the sound sits, -1..+1)   q' = sign(q)|q|^Abre  (Abre < 1 spreads in-between positions apart)
//   centre (q=0) and hard sides (q=±1) keep gain 1; never phase-inverted; total power per bin kept. Bass below ~150 Hz untouched.
// STFT 1024 / hop 256, sqrt-Hann: fixed latency 1024 samples (~21 ms at 48 kHz).
namespace FAH.Efeitos;

public sealed class Palco
{
    const int N = 1024, H = 256;
    readonly FftE fft = new(N);
    readonly float[] jan = new float[N], inL = new float[N], inR = new float[N], olaL = new float[N], olaR = new float[N];
    readonly float[] outL = new float[H], outR = new float[H], re = new float[N], im = new float[N];
    readonly double[] pL = new double[N / 2 + 1], pR = new double[N / 2 + 1], gL = new double[N / 2 + 1], gR = new double[N / 2 + 1];
    readonly double[] peso = new double[N / 2 + 1];
    readonly double abre, aP;
    int fill;

    public Palco(double abre, int taxa)
    {
        this.abre = abre;
        for (int i = 0; i < N; i++) jan[i] = (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N));
        for (int k = 0; k <= N / 2; k++)
        {
            double f = (double)k * taxa / N;
            peso[k] = Math.Clamp((f - 100) / 100, 0, 1);                       // 100..200 Hz fade-in: bass stays as recorded
        }
        aP = Math.Exp(-H / (0.03 * taxa));                                     // ~30 ms power smoothing: stable positions, no warble
        Limpar();
    }

    public void Limpar()
    {
        Array.Clear(inL); Array.Clear(inR); Array.Clear(olaL); Array.Clear(olaR); Array.Clear(outL); Array.Clear(outR);
        Array.Clear(pL); Array.Clear(pR); Array.Fill(gL, 1.0); Array.Fill(gR, 1.0); fill = 0;
    }

    /// <summary>In place on interleaved L/R. Output is the input delayed by N samples, re-panned.</summary>
    public void Processar(Span<float> lr, int n)
    {
        for (int i = 0; i < n; i++)
        {
            inL[N - H + fill] = lr[2 * i]; inR[N - H + fill] = lr[2 * i + 1];
            lr[2 * i] = outL[fill]; lr[2 * i + 1] = outR[fill];
            if (++fill == H) { Quadro(); fill = 0; }
        }
    }

    void Quadro()
    {
        for (int i = 0; i < N; i++) { re[i] = inL[i] * jan[i]; im[i] = inR[i] * jan[i]; }   // X = L + jR: one FFT for both
        fft.Fazer(re, im);
        for (int k = 0; k <= N / 2; k++)
        {
            int j = (N - k) & (N - 1);
            double lr_ = 0.5 * (re[k] + re[j]), li = 0.5 * (im[k] - im[j]);     // L_k
            double rr = 0.5 * (im[k] + im[j]), ri = -0.5 * (re[k] - re[j]);     // R_k
            double el = lr_ * lr_ + li * li, er = rr * rr + ri * ri;
            pL[k] = aP * pL[k] + (1 - aP) * el; pR[k] = aP * pR[k] + (1 - aP) * er;
            double p = pL[k] + pR[k], a = 1, b = 1;
            if (p > 1e-14 && peso[k] > 0)
            {
                double q = (pL[k] - pR[k]) / p, q2 = Math.Sign(q) * Math.Pow(Math.Abs(q), abre);
                q2 = q + peso[k] * (q2 - q);
                a = Math.Clamp(Math.Sqrt((1 + q2) / Math.Max(1 + q, 1e-9)), 0.25, 2);
                b = Math.Clamp(Math.Sqrt((1 - q2) / Math.Max(1 - q, 1e-9)), 0.25, 2);
            }
            gL[k] = 0.5 * (gL[k] + a); gR[k] = 0.5 * (gR[k] + b);              // light gain smoothing between hops
            double nlr = gL[k] * lr_, nli = gL[k] * li, nrr = gR[k] * rr, nri = gR[k] * ri;
            // Y = L' + jR'  ->  Y_k = L'_k + j R'_k,  Y_j = conj(L'_k) + j conj(R'_k)
            re[k] = (float)(nlr - nri); im[k] = (float)(nli + nrr);
            if (j != k) { re[j] = (float)(nlr + nri); im[j] = (float)(-nli + nrr); }
        }
        fft.Fazer(re, im, true);
        for (int i = 0; i < N; i++) { olaL[i] += 0.5f * re[i] * jan[i]; olaR[i] += 0.5f * im[i] * jan[i]; }   // sqrt-Hann² at 75% sums to 2
        Array.Copy(olaL, outL, H); Array.Copy(olaR, outR, H);
        Array.Copy(olaL, H, olaL, 0, N - H); Array.Clear(olaL, N - H, H);
        Array.Copy(olaR, H, olaR, 0, N - H); Array.Clear(olaR, N - H, H);
        Array.Copy(inL, H, inL, 0, N - H); Array.Copy(inR, H, inR, 0, N - H);
        for (int k = 0; k < pL.Length; k++) { if (pL[k] < 1e-30) pL[k] = 0; if (pR[k] < 1e-30) pR[k] = 0; }
    }
}

/// <summary>In-place radix-2 complex FFT (copy of FAH.Microfone.Fft: modules don't reference each other).</summary>
public sealed class FftE
{
    readonly int n;
    readonly int[] rev;
    readonly float[] cos, sin;

    public FftE(int n)
    {
        this.n = n; rev = new int[n]; cos = new float[n / 2]; sin = new float[n / 2];
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++) { int r = 0; for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b); rev[i] = r; }
        for (int i = 0; i < n / 2; i++) { cos[i] = (float)Math.Cos(2 * Math.PI * i / n); sin[i] = (float)Math.Sin(2 * Math.PI * i / n); }
    }

    public void Fazer(float[] re, float[] im, bool inverso = false)
    {
        for (int i = 0; i < n; i++) { int j = rev[i]; if (j > i) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); } }
        float sg = inverso ? 1 : -1;
        for (int tam = 2; tam <= n; tam <<= 1)
        {
            int meio = tam >> 1, passo = n / tam;
            for (int i = 0; i < n; i += tam)
                for (int k = 0; k < meio; k++)
                {
                    float wr = cos[k * passo], wi = sg * sin[k * passo];
                    int a = i + k, b = a + meio;
                    float tr = re[b] * wr - im[b] * wi, ti = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                }
        }
        if (inverso) { float s = 1f / n; for (int i = 0; i < n; i++) { re[i] *= s; im[i] *= s; } }
    }
}
