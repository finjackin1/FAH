// Stereo -> virtual speakers, plain matrix (28/10: the per-bin STFT upmix sounded like low-bitrate MP3 and swayed L/R):
// L -> front left, R -> front right, untouched; only the side part S = (L-R)/2 (what isn't centered) feeds the sides
// (5 ms late) and the back pair (12 ms late), low-passed. Centered sound (voice) stays clean in front. Same 512 delay as before.
namespace FAH.Espacial;

public sealed class Upmix
{
    const int H = 512, AtrasoLado = 0, AtrasoTras = 0;           // 01/11: nothing late (was 5 and 12 ms), he: must not be out of sync
    const double GLado = 0.45, GTras = 0.4;
    // engine roles: FL FR C BL BR SL SR
    readonly double[] fila = new double[H + AtrasoTras + H];
    readonly double[] dl = new double[H], dr = new double[H];
    readonly Biquads lpLado = new([Biquads.PassaBaixa(8000)], 1), lpTras = new([Biquads.PassaBaixa(6000)], 1);

    /// <summary>512 stereo frames (l, r) -> 512 frames per speaker role into saida[role] (delay 512).</summary>
    public void Bloco(ReadOnlySpan<double> l, ReadOnlySpan<double> r, double[][] saida)
    {
        for (int c = 0; c < 7; c++) Array.Clear(saida[c], 0, H);
        // front: previous block (fixed 512 delay)
        Array.Copy(dl, saida[0], H); Array.Copy(dr, saida[1], H);
        l.CopyTo(dl); r.CopyTo(dr);
        // side signal history, newest block at the end
        Array.Copy(fila, H, fila, 0, fila.Length - H);
        for (int i = 0; i < H; i++) fila[fila.Length - H + i] = 0.5 * (l[i] - r[i]);
        Lateral(saida[5], saida[6], fila.Length - 2 * H - AtrasoLado, GLado, lpLado);
        Lateral(saida[3], saida[4], fila.Length - 2 * H - AtrasoTras, GTras, lpTras);
    }

    void Lateral(double[] esq, double[] dir, int ini, double g, Biquads lp)
    {
        for (int i = 0; i < H; i++) esq[i] = fila[ini + i] * g;
        lp.Processar(esq, 0, H);
        for (int i = 0; i < H; i++) dir[i] = -esq[i];
    }
}

/// <summary>01/11: a stereo program on the 7.1 cable arrives on FL/FR only (Windows doesn't upmix). First version (S at -7 dB, 5/12 ms late,
/// backup/7.1-2026-11-01-preencher) measured 99% of a real mix on the two fronts, and a left instrument came out of both sides: "one place".
/// Now, per wide band, what only one side has MOVES (doesn't copy) from that front to the speakers on its own side; what both sides share
/// (voice, bass) stays exactly as it was. Band shares differ, so instruments land on different speakers. Slow gains (no per-bin STFT: it sounded
/// like low-bitrate MP3 and swayed, 28/10), complementary bands (they sum back to the input, nothing filters what stays), no delay on the fronts.
/// 01/11 night, he: voices at the sides, a speaker must not need any delay. Measured: moved sound 6.5-8.9 ms late (all-pass), voice 17% out of the
/// fronts with instruments on both sides. Now every speaker is exactly in time (no all-pass), each band pans over TWO neighbour speakers, and what
/// both sides share goes to the real center speaker (0 degrees) instead of a phantom made by the fronts.
/// Off at once when the program really uses the other channels; back only after 10 s without them. Starts on.
/// One front alone with the other silent = a channel test, not stereo: nothing is moved.</summary>
public sealed class Preencher
{
    const int B = 512, NB = 3;
    const double Silencio = 1e-5, Subida = (double)B / 48000;   // 1 s up, 1 block down
    const double Alisa = 0.02;                                   // energy tracking per block: ~500 ms, no sway (voice pauses move the center's share)
    public const int Segurar = 10 * 48000;
    // share of the moved part per band: what stays in front, side, back. In step, so it pans: few speakers per band, and in step they add up louder: sum of squares 0.72
    static readonly (double F, double S, double T)[] Reparte = [(0.68, 0.51, 0.0), (0.47, 0.38, 0.60), (0.51, 0.0, 0.68)];
    /// <summary>Center speaker gain for what leaves the fronts: same voice level at the ear as FL + FR (h11, the fixed head). Timbre: full match up to
    /// 4 kHz (his approved voice), half above (those peaks and notches are what says "straight ahead").</summary>
    public const double GanhoCentro = 1.17;
    static readonly double[][] TomCentro = [Biquads.Pico(300, 0.9, 3.5), Biquads.Pico(2000, 1.2, -2), Biquads.Pico(3500, 1.5, 4.5), Biquads.Pico(6500, 2, 3.5), Biquads.Pico(11000, 2, -3)];
    readonly Biquads tomCentro = new(TomCentro, 1);
    static readonly double[] Corte = [250, 1200, 4500];          // under 250 Hz nothing moves (no direction there)

    /// <summary>Tracks one band of L/R and gives the part only one side has.</summary>
    sealed class Banda
    {
        double el, er, ec;

        public void Medir(double[] l, double[] r)
        {
            double a = 0, b = 0, c = 0;
            for (int i = 0; i < B; i++) { a += l[i] * l[i]; b += r[i] * r[i]; c += l[i] * r[i]; }
            el += Alisa * (a / B - el); er += Alisa * (b / B - er); ec += Alisa * (c / B - ec);
        }

        /// <summary>Side a with partner b: a - w·b, scaled so only the energy a doesn't share with b comes out.</summary>
        static (double W, double G) Calc(double ea, double eb, double ec)
        {
            double com = Math.Max(0, ec), w = eb > 1e-14 ? Math.Min(1, com / eb) : 0;
            double resto = ea - 2 * w * ec + w * w * eb, unico = Math.Max(0, ea - com);
            return (w, resto > 1e-14 ? Math.Min(1, Math.Sqrt(unico / resto)) : 0);
        }

        /// <summary>k = how much of the mean of the two unique parts is really voice leaking in: both sides have their own instruments AND share a voice.
        /// One side only = nothing leaks; nothing shared = no voice.</summary>
        public (double wl, double gl, double wr, double gr, double k) Agora()
        {
            var (a, b) = Calc(el, er, ec); var (c, d) = Calc(er, el, ec);
            double com = Math.Max(0, ec), ul = Math.Max(0, el - com), ur = Math.Max(0, er - com), soma = ul + ur;
            double k = soma > 1e-14 ? com / (com + soma / 4) * 2 * Math.Min(ul, ur) / soma : 0;
            return (a, b, c, d, k);
        }
    }

    readonly Biquads[] divL = new Biquads[NB], divR = new Biquads[NB];
    readonly Banda[] bandas = [new(), new(), new()];
    readonly double[][] bl = [new double[B], new double[B], new double[B]], br = [new double[B], new double[B], new double[B]];
    readonly double[] resL = new double[B], resR = new double[B], grave = new double[B];
    readonly double[] sl = new double[B], sr = new double[B], tl = new double[B], tr = new double[B], fl = new double[B], fr = new double[B], cen = new double[B];
    int quieto = Segurar;
    double g;

    public double Ganho => g;
    /// <summary>07/11: center role goes straight to the ears at 0.707 (Brir.VozPlana) -> the shared part goes in plain at √2 (= the same
    /// level both fronts gave it), no HRTF timbre match.</summary>
    public bool Plano;

    public Preencher()
    {
        for (int k = 0; k < NB; k++)
        {
            double[][] s = [Biquads.PassaBaixa(Corte[k]), Biquads.PassaBaixa(Corte[k])];
            divL[k] = new Biquads(s, 1); divR[k] = new Biquads(s, 1);
        }
    }

    static bool Som(double[] v) { for (int i = 0; i < B; i++) if (Math.Abs(v[i]) > Silencio) return true; return false; }

    /// <summary>x = 512 frames per speaker role (FL FR C BL BR SL SR), lfe = the raw LFE channel. Moves from roles 0,1 to 2..6.</summary>
    public void Bloco(double[][] x, double[] lfe)
    {
        bool outros = Som(lfe); for (int c = 2; c < 7 && !outros; c++) outros = Som(x[c]);
        if (outros || Som(x[0]) != Som(x[1])) quieto = 0;
        else if (quieto < Segurar) quieto += B;
        double g0 = g, g1 = quieto >= Segurar ? Math.Min(1, g + Subida) : 0;
        g = g1;

        // complementary split: under 250 stays out, then each band = low-pass of what's left, the last one = the rest
        Array.Copy(x[0], resL, B); Array.Copy(x[1], resR, B);
        Array.Copy(resL, grave, B); divL[0].Processar(grave, 0, B); for (int i = 0; i < B; i++) resL[i] -= grave[i];
        Array.Copy(resR, grave, B); divR[0].Processar(grave, 0, B); for (int i = 0; i < B; i++) resR[i] -= grave[i];
        Array.Clear(sl); Array.Clear(sr); Array.Clear(tl); Array.Clear(tr); Array.Clear(fl); Array.Clear(fr); Array.Clear(cen);
        for (int k = 0; k < NB; k++)
        {
            double[] a = bl[k], b = br[k];
            Array.Copy(resL, a, B); Array.Copy(resR, b, B);
            if (k < NB - 1)
            {
                divL[k + 1].Processar(a, 0, B); divR[k + 1].Processar(b, 0, B);
                for (int i = 0; i < B; i++) { resL[i] -= a[i]; resR[i] -= b[i]; }
            }
            var t = bandas[k]; var (wl0, gl0, wr0, gr0, k0) = t.Agora();
            t.Medir(a, b);
            var (wl1, gl1, wr1, gr1, k1) = t.Agora();
            var (pf, ps, pt) = Reparte[k];
            for (int i = 0; i < B; i++)
            {
                double u = (i + 1.0) / B;
                double el = (gl0 + (gl1 - gl0) * u) * (a[i] - (wl0 + (wl1 - wl0) * u) * b[i]);
                double er = (gr0 + (gr1 - gr0) * u) * (b[i] - (wr0 + (wr1 - wr0) * u) * a[i]);
                double vaz = (k0 + (k1 - k0) * u) * 0.5 * (el + er); el -= vaz; er -= vaz;
                double meio = 0.5 * (a[i] - el + b[i] - er);          // what both share = center speaker
                cen[i] += meio;
                fl[i] += el * (1 - pf) + meio; fr[i] += er * (1 - pf) + meio;
                sl[i] += el * ps; sr[i] += er * ps; tl[i] += el * pt; tr[i] += er * pt;
            }
        }
        bool plano = Plano;
        if (!plano) tomCentro.Processar(cen, 0, B);
        double gc = plano ? Math.Sqrt(2) : GanhoCentro;
        if (g0 == 0 && g1 == 0) return;
        for (int i = 0; i < B; i++)
        {
            double t = g0 + (g1 - g0) * (i + 1) / B;
            x[0][i] -= fl[i] * t; x[1][i] -= fr[i] * t; x[2][i] += cen[i] * t * gc;
            x[5][i] += sl[i] * t; x[6][i] += sr[i] * t; x[3][i] += tl[i] * t; x[4][i] += tr[i] * t;
        }
    }
}
