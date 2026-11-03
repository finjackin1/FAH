// DSP pieces of Som 7.1: FFT, partitioned convolution (OLS, block 512), RBJ biquads.
namespace FAH.Espacial;

/// <summary>In-place complex radix-2 FFT (numpy sign: forward e^-i; inverse scaled 1/N).</summary>
public sealed class Fft
{
    public readonly int N;
    readonly int[] rev;
    readonly double[] cs, sn;

    public Fft(int n)
    {
        N = n; int lg = 0; while ((1 << lg) < n) lg++;
        rev = new int[n];
        for (int i = 0; i < n; i++) { int r = 0; for (int b = 0; b < lg; b++) if ((i & (1 << b)) != 0) r |= 1 << (lg - 1 - b); rev[i] = r; }
        cs = new double[n / 2]; sn = new double[n / 2];
        for (int k = 0; k < n / 2; k++) { cs[k] = Math.Cos(2 * Math.PI * k / n); sn[k] = Math.Sin(2 * Math.PI * k / n); }
    }

    public void Executar(double[] re, double[] im, bool inversa = false)
    {
        int n = N;
        for (int i = 0; i < n; i++) { int j = rev[i]; if (j > i) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); } }
        for (int len = 2; len <= n; len <<= 1)
        {
            int h = len >> 1, passo = n / len;
            for (int i = 0; i < n; i += len)
                for (int k = 0; k < h; k++)
                {
                    double wr = cs[k * passo], wi = inversa ? sn[k * passo] : -sn[k * passo];
                    int a = i + k, b = a + h;
                    double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi; re[a] += xr; im[a] += xi;
                }
        }
        if (inversa) { double s = 1.0 / n; for (int i = 0; i < n; i++) { re[i] *= s; im[i] *= s; } }
    }

    /// <summary>rfft of x (zero-padded/truncated to N): bins 0..N/2 into re/im (length N used as work).</summary>
    public void Real(ReadOnlySpan<double> x, double[] re, double[] im)
    {
        int m = Math.Min(x.Length, N);
        x[..m].CopyTo(re); Array.Clear(re, m, N - m); Array.Clear(im);
        Executar(re, im);
    }

    /// <summary>irfft: bins 0..N/2 in re/im -> real signal in re (imag of DC/Nyquist ignored, like numpy).</summary>
    public void RealInversa(double[] re, double[] im)
    {
        int h = N / 2; im[0] = 0; im[h] = 0;
        for (int k = 1; k < h; k++) { re[N - k] = re[k]; im[N - k] = -im[k]; }
        Executar(re, im, true);
    }
}

/// <summary>Uniform partitioned convolution (overlap-save, block B, FFT 2B): C inputs -> 2 outputs (ears).
/// Respostas[c][e] = response of input c to ear e. Clone() = same filters, fresh state.</summary>
public sealed class Convolucao
{
    public const int B = 512, N = 2 * B, K = B + 1;
    public readonly int C, P;
    readonly double[][] hr, hi;                 // [(p*C+c)*2+e][K]
    double[][] xr, xi;                          // [slot*C+c][K] spectra of the last P input frames
    double[][] ant;                             // previous block of each input
    int pos;
    readonly Fft fft = new(N);
    readonly double[] wr = new double[N], wi = new double[N], yl = new double[K], yli = new double[K], yr = new double[K], yri = new double[K];

    public Convolucao(double[][][] respostas)
    {
        C = respostas.Length;
        int n = 1; foreach (var r in respostas) foreach (var e in r) n = Math.Max(n, e.Length);
        P = (n + B - 1) / B;
        hr = new double[P * C * 2][]; hi = new double[P * C * 2][];
        for (int p = 0; p < P; p++)
            for (int c = 0; c < C; c++)
                for (int e = 0; e < 2; e++)
                {
                    var src = respostas[c][e]; int ini = p * B, m = Math.Max(0, Math.Min(B, src.Length - ini));
                    fft.Real(m > 0 ? src.AsSpan(ini, m) : [], wr, wi);
                    int i = (p * C + c) * 2 + e;
                    hr[i] = wr[..K]; hi[i] = wi[..K];
                }
        (xr, xi, ant) = Estado(P, C);
    }

    Convolucao(Convolucao b) { C = b.C; P = b.P; hr = b.hr; hi = b.hi; (xr, xi, ant) = Estado(P, C); }

    static (double[][], double[][], double[][]) Estado(int p, int c)
    {
        var a = new double[p * c][]; var b = new double[p * c][]; var d = new double[c][];
        for (int i = 0; i < a.Length; i++) { a[i] = new double[K]; b[i] = new double[K]; }
        for (int i = 0; i < c; i++) d[i] = new double[B];
        return (a, b, d);
    }

    public Convolucao Clone() => new(this);

    /// <summary>Copies the input history of <paramref name="o"/> (same C and P): a new filter set starts already "warm".</summary>
    public void CopiarEstado(Convolucao o)
    {
        if (o.C != C || o.P != P) return;
        for (int i = 0; i < xr.Length; i++) { o.xr[i].CopyTo(xr[i], 0); o.xi[i].CopyTo(xi[i], 0); }
        for (int i = 0; i < C; i++) o.ant[i].CopyTo(ant[i], 0);
        pos = o.pos;
    }

    /// <summary>x[c] = B samples of input c -> B samples per ear into l/r.</summary>
    public void Bloco(double[][] x, Span<double> l, Span<double> r)
    {
        pos = (pos + 1) % P;
        for (int c = 0; c < C; c++)
        {
            Array.Copy(ant[c], 0, wr, 0, B); Array.Copy(x[c], 0, wr, B, B); Array.Clear(wi);
            Array.Copy(x[c], ant[c], B);
            fft.Executar(wr, wi);
            Array.Copy(wr, xr[pos * C + c], K); Array.Copy(wi, xi[pos * C + c], K);
        }
        Array.Clear(yl); Array.Clear(yli); Array.Clear(yr); Array.Clear(yri);
        for (int p = 0; p < P; p++)
        {
            int slot = (pos - p + P) % P;
            for (int c = 0; c < C; c++)
            {
                double[] ar = xr[slot * C + c], ai = xi[slot * C + c];
                int h = (p * C + c) * 2;
                double[] lr = hr[h], li = hi[h], rr = hr[h + 1], ri = hi[h + 1];
                for (int k = 0; k < K; k++)
                {
                    double a = ar[k], b = ai[k];
                    yl[k] += a * lr[k] - b * li[k]; yli[k] += a * li[k] + b * lr[k];
                    yr[k] += a * rr[k] - b * ri[k]; yri[k] += a * ri[k] + b * rr[k];
                }
            }
        }
        Array.Copy(yl, wr, K); Array.Copy(yli, wi, K); fft.RealInversa(wr, wi);
        wr.AsSpan(B, B).CopyTo(l);
        Array.Copy(yr, wr, K); Array.Copy(yri, wi, K); fft.RealInversa(wr, wi);
        wr.AsSpan(B, B).CopyTo(r);
    }
}

/// <summary>RBJ biquad cascade, own state per channel (double, direct form I).</summary>
public sealed class Biquads
{
    readonly double[][] k;                       // b0 b1 b2 a1 a2
    readonly double[,] z;                        // [section*canais + ch, 4]
    readonly int canais;

    public Biquads(IEnumerable<double[]> secoes, int canais) { k = [.. secoes]; this.canais = canais; z = new double[Math.Max(1, k.Length * canais), 4]; }

    public bool Vazio => k.Length == 0;

    public static double[] Pico(double f, double q, double db, double taxa = 48000)
    {
        double w = 2 * Math.PI * Math.Min(f, taxa / 2 - 1) / taxa, c = Math.Cos(w), al = Math.Sin(w) / (2 * q), A = Math.Pow(10, db / 40), a0 = 1 + al / A;
        return [(1 + al * A) / a0, -2 * c / a0, (1 - al * A) / a0, -2 * c / a0, (1 - al / A) / a0];
    }

    public static double[] PassaBaixa(double f, double q = 0.707, double taxa = 48000)
    {
        double w = 2 * Math.PI * f / taxa, c = Math.Cos(w), al = Math.Sin(w) / (2 * q), a0 = 1 + al;
        return [(1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, -2 * c / a0, (1 - al) / a0];
    }

    /// <summary>In place on channel <paramref name="ch"/> of an interleaved buffer (stride = canais).</summary>
    public void Processar(Span<double> x, int ch, int n)
    {
        for (int s = 0; s < k.Length; s++)
        {
            var q = k[s]; int zi = s * canais + ch;
            double x1 = z[zi, 0], x2 = z[zi, 1], y1 = z[zi, 2], y2 = z[zi, 3];
            for (int i = 0; i < n; i++)
            {
                int j = i * canais + ch; double v = x[j];
                double y = q[0] * v + q[1] * x1 + q[2] * x2 - q[3] * y1 - q[4] * y2;
                x2 = x1; x1 = v; y2 = y1; y1 = y; x[j] = y;
            }
            z[zi, 0] = x1; z[zi, 1] = x2; z[zi, 2] = y1; z[zi, 3] = y2;
        }
    }

    /// <summary>Offline: whole mono array (fresh state).</summary>
    public static void Filtrar(IEnumerable<double[]> secoes, double[] x)
    {
        var b = new Biquads(secoes, 1); b.Processar(x, 0, x.Length);
    }
}
