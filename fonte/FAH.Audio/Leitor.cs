// Ring + read with clock correction (port of sistema/destino.py Leitor). Each card has its own clock: the reader
// follows the queue level with speed 1±0.5% and 4-point Hermite interpolation (does not dull the highs like linear).
namespace FAH.Audio;

public sealed class Leitor
{
    public const double DesvioMax = 0.005, AlvoMaxMs = 60, ExcessoMs = 120;
    readonly int taxa;
    public readonly Anel Anel;
    public string Rotulo;
    public double AlvoMs, AtrasoMs;
    public bool AlvoAuto = true, CorrigirRelogio = true, Mudo;
    public double VelocidadeFixa = 1, Volume = 1;

    float[] src = new float[4096];       // pending source frames (stereo); src[0] = "previous" Hermite point
    int nsrc;
    double frac, r = 1;
    bool preparando = true;
    public long Lidos, Subcargas, Descartes;
    public double RMin = 1, RMax = 1;
    public readonly List<double> FaltasEm = [];

    public Leitor(string rotulo, double alvoMs = 30, int taxa = 48000)
    {
        Rotulo = rotulo; AlvoMs = alvoMs; this.taxa = taxa; Anel = new Anel(taxa * 2);
    }

    public int Alvo => (int)((AlvoMs + AtrasoMs) * taxa / 1000);
    public double FilaMs => Anel.Count * 1000.0 / taxa;
    public double Velocidade => r;

    public void Empurrar(ReadOnlySpan<float> lr) => Anel.Escrever(lr);

    void Garantir(int quadros) { if (src.Length < quadros * 2) Array.Resize(ref src, quadros * 2); }

    /// <summary>Fills out (frames*2). Returns false = nothing to play (filling up): caller plays silence.</summary>
    public bool Ler(Span<float> outp, int quadros)
    {
        Lidos++;
        int alvo = Alvo;
        bool entrar = false;
        if (preparando)
        {
            // first ~3 s the streams start in jerks: +15 ms reserve, clock correction spends it slowly
            if (Anel.Count < alvo + (Lidos < 300 ? (int)(0.015 * taxa) : 0)) return false;
            preparando = false;
            entrar = Lidos > 1;                          // back from an underrun: ramp in (no click)
            nsrc = 1; src[0] = src[1] = 0; frac = 0;
        }
        int sobra = Anel.Count - alvo - (int)(ExcessoMs * taxa / 1000);
        if (sobra > 0 && CorrigirRelogio)                // accumulated delay (output dropped and came back): back to target at once
        {
            Anel.Descartar(Anel.Count - alvo); Descartes++;
            src[0] = src[(nsrc - 1) * 2]; src[1] = src[(nsrc - 1) * 2 + 1]; nsrc = 1; frac = 0;
        }
        if (CorrigirRelogio)
        {
            double erro = (double)(Anel.Count - alvo) / Math.Max(alvo, 1);
            double rAlvo = 1 + Math.Clamp(0.004 * erro, -DesvioMax, DesvioMax);
            r += (rAlvo - r) * 0.02;                     // smooth: no audible pitch change
        }
        else r = VelocidadeFixa;
        RMin = Math.Min(RMin, r); RMax = Math.Max(RMax, r);
        int precisa = (int)Math.Floor(frac + (quadros - 1) * r) + 4;
        int falta = precisa - nsrc;
        Garantir(precisa + 8);
        if (falta > 0)
        {
            if (Anel.Count < falta)                      // ran out: wait to fill again
            {
                Subcargas++;
                FaltasEm.Add(Math.Round(Lidos * quadros / (double)taxa, 1));
                if (FaltasEm.Count > 50) FaltasEm.RemoveAt(0);
                preparando = true;
                if (AlvoAuto && AlvoMs < AlvoMaxMs && Lidos > 100) AlvoMs = Math.Min(AlvoMaxMs, AlvoMs + 5);
                // what is left plays and fades to silence (cutting dry = click + hole)
                int tem = nsrc - 1;
                tem += Anel.Ler(src.AsSpan(nsrc * 2, Math.Min(Anel.Count, src.Length / 2 - nsrc) * 2));
                int k = Math.Min(tem, quadros);
                if (k < 8) return false;
                outp[..(quadros * 2)].Clear();
                for (int i = 0; i < k; i++)
                {
                    float w = MathF.Cos(MathF.PI / 2 * i / Math.Max(k - 1, 1));
                    outp[2 * i] = src[(i + 1) * 2] * w; outp[2 * i + 1] = src[(i + 1) * 2 + 1] * w;
                }
                return true;
            }
            nsrc += Anel.Ler(src.AsSpan(nsrc * 2, falta * 2));
        }
        for (int i = 0; i < quadros; i++)
        {
            double s = frac + 1.0 + i * r;
            int ii = (int)Math.Floor(s);
            float t = (float)(s - ii);
            for (int ch = 0; ch < 2; ch++)
            {
                float p0 = src[(ii - 1) * 2 + ch], p1 = src[ii * 2 + ch], p2 = src[(ii + 1) * 2 + ch], p3 = src[(ii + 2) * 2 + ch];
                float c1 = 0.5f * (p2 - p0), c2 = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3, c3 = 0.5f * (p3 - p0) + 1.5f * (p1 - p2);
                outp[2 * i + ch] = ((c3 * t + c2) * t + c1) * t + p1;
            }
        }
        int consumido = (int)Math.Floor(frac + quadros * r);
        frac = frac + quadros * r - consumido;
        nsrc -= consumido;
        src.AsSpan(consumido * 2, nsrc * 2).CopyTo(src);
        if (entrar)
        {
            int m = Math.Min(quadros, 96);               // 2 ms ramp coming back
            for (int i = 0; i < m; i++) { float w = MathF.Sin(MathF.PI / 2 * i / Math.Max(m - 1, 1)); outp[2 * i] *= w; outp[2 * i + 1] *= w; }
        }
        return true;
    }
}
