// In-place radix-2 complex FFT (tables made once; no allocation per call).
namespace FAH.Microfone;

public sealed class Fft
{
    public readonly int N;
    readonly int[] rev;
    readonly float[] cos, sin;

    public Fft(int n)
    {
        N = n; rev = new int[n]; cos = new float[n / 2]; sin = new float[n / 2];
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++) { int r = 0; for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b); rev[i] = r; }
        for (int i = 0; i < n / 2; i++) { cos[i] = (float)Math.Cos(2 * Math.PI * i / n); sin[i] = (float)Math.Sin(2 * Math.PI * i / n); }
    }

    /// <summary>inverso = true: e^{+j}, scaled by 1/N.</summary>
    public void Fazer(float[] re, float[] im, bool inverso = false)
    {
        int n = N;
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
