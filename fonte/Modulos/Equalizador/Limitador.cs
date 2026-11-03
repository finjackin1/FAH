// Peak limiter (port of the previous Python version): gain per 32-frame sub-block, looks one
// sub-block ahead, smooth release. Streaming in place: fixed delay of 2 sub-blocks (64 frames = 1.3 ms).
namespace FAH.Equalizador;

public sealed class Limitador
{
    const int S = 32, D = 2 * S;
    readonly float[] anel = new float[D * 2];       // delay line, stereo
    readonly double[] gIni = new double[4], gFim = new double[4];
    double teto = Math.Pow(10, -0.3 / 20), rel, g = 1, reqAnt = 1, pico;
    long n;

    public bool Ligado { get; set; }
    public double ReducaoDb { get; private set; }

    public Limitador(int taxa = 48000, double soltaMs = 60) { Preparar(taxa, soltaMs); Array.Fill(gIni, 1); Array.Fill(gFim, 1); }

    public void Preparar(int taxa, double soltaMs = 60) => rel = 1 - Math.Exp(-S / (soltaMs * 0.001 * taxa));

    public double TetoDb { set => teto = Math.Pow(10, value / 20); }

    public void Processar(Span<float> lr, int quadros)
    {
        if (!Ligado) return;
        double menor = 1;
        for (int i = 0; i < quadros; i++, n++)
        {
            int p = (int)(n % D) * 2;
            float l = lr[2 * i], r = lr[2 * i + 1];
            // output = input from D frames ago, with the ramp of its sub-block (already decided)
            long m = n - D;
            float ol = 0, or = 0;
            if (m >= 0)
            {
                int sub = (int)((m / S) & 3), k = (int)(m % S);
                double gg = gIni[sub] + (gFim[sub] - gIni[sub]) * (k + 1) / S;
                ol = (float)(anel[p] * gg); or = (float)(anel[p + 1] * gg);
            }
            anel[p] = l; anel[p + 1] = r;
            pico = Math.Max(pico, Math.Max(Math.Abs(l), Math.Abs(r)));
            if ((n + 1) % S == 0)
            {
                // sub-block j done: decide the ramp of sub-block j-1 (it sees j-1 and j: one ahead)
                long j = n / S;
                double req = Math.Min(1, teto / Math.Max(pico, 1e-12)), solto = g + (1 - g) * rel;
                double novo = Math.Min(reqAnt, Math.Min(req, solto));
                int s = (int)((j - 1) & 3);
                gIni[s] = g; gFim[s] = novo; g = novo; reqAnt = req; pico = 0;
                menor = Math.Min(menor, novo);
            }
            lr[2 * i] = ol; lr[2 * i + 1] = or;
        }
        ReducaoDb = 20 * Math.Log10(Math.Max(menor, 1e-6));
    }
}
