// 08/11 user: ONE spatial engine for Som 7.1 and Estéreo+; each mode only picks its speakers. Linked into Efeitos (Estéreo+ v5).
//   Layout  = speaker order [L, R, C] + (Leque) the 6 fan speakers + extras (7.1: BL BR SL SR). Responses = raw HRTF + ground bounce, flat
//             gain (Brir Neutro), no halo, built by Respostas for both modes.
//   Frente  = the approved front stage: what L/R carry goes PLAIN to the ears as M + la·S (delayed to meet the HRTF center peak), only cx·S
//             feeds virtual speakers: Par = +S / -S on the front pair (the 7.1's BaseEstereo), Leque = split per STFT bin by its pan over the
//             front fan ±15..60 (Estéreo+ v5, one hop B later: every feed and the plain part wait one block too). Center = gc·C on the 0° speaker.
// la/cx come from the Intensidade slider (Intensidade.Mapa). Step 1 of the merge kept both sounds bit-identical (temp fah_teste_motor).
namespace FAH.Espacial;

public enum Frentes { Par, Leque }

public static class Cena
{
    /// <summary>Fan speakers right after the front trio (Leque), left (+S) negative.</summary>
    public static readonly double[] AngLeque = [-60, -45, -15, 15, 45, 60];

    /// <summary>Speaker order of a layout: frente3 = [L, R, C] angles, then the fan (Leque), then the extras.</summary>
    public static double[] Angulos(Frentes f, double[] frente3, double[] extras) =>
        f == Frentes.Leque ? [.. frente3, .. AngLeque, .. extras] : [.. frente3, .. extras];

    /// <summary>All speakers of a layout: [speaker][ear][tap], raw HRTF + flat gain, no halo; the center is index 2 (AtrasoCentro = its peak).
    /// refGanho = layout whose flat gain is kept (speaker 0 energy matched to it; Estéreo+ keeps the gain of its [-30,30,0] trio); null = own.
    /// fixarCentro = false (Fonte3D): the engine's static Brir.AtrasoCentro stays; the center peak is measured here.</summary>
    public static (double[][][] Resp, int AtrasoCentro) Respostas(double[] angulos, double[]? refGanho, bool fixarCentro = true)
    {
        double[][][]? refR = refGanho == null ? null : Brir.Caixas(refGanho, true, 0, halo: false, fixarCentro).Resp;
        var (resp, _) = Brir.Caixas(angulos, true, 0, halo: false, fixarCentro);
        int atraso = fixarCentro ? Brir.AtrasoCentro : Pico(resp[2]);
        if (refR != null)
        {
            static double E(double[][] h) { double s = 0; foreach (var x in h) foreach (var v in x) s += v * v; return s; }
            double k = Math.Sqrt(E(refR[0]) / Math.Max(E(resp[0]), 1e-30));
            foreach (var c in resp) foreach (var x in c) for (int i = 0; i < x.Length; i++) x[i] *= k;
        }
        return (resp, atraso);
    }

    /// <summary>Sample where |L| + |R| peaks.</summary>
    public static int Pico(double[][] h)
    {
        int p = 0; double m = -1;
        for (int i = 0; i < h[0].Length; i++) { double v = Math.Abs(h[0][i]) + Math.Abs(h[1][i]); if (v > m) { m = v; p = i; } }
        return p;
    }
}

/// <summary>Front stage of one stream (state: plain delay lines, Leque STFT). See Cena.</summary>
public sealed class Frente(Frentes modo, int extras)
{
    const int B = Convolucao.B, NF = 2 * B, K = B + 1;
    public readonly Frentes Modo = modo;
    public readonly int Extras = extras;
    /// <summary>Feeds Bloco writes: Par 3 + extras, Leque 9 + extras.</summary>
    public int Caixas => (Modo == Frentes.Leque ? 9 : 3) + Extras;
    readonly double[] linhaL = new double[4 * B], linhaR = new double[4 * B]; int pos;
    // Leque (07/11 "Frente larga"): STFT N1024 hop 512, sqrt-Hann
    static readonly int[] ZonaE = [5, 0, 4, 3], ZonaD = [6, 1, 7, 8];   // 15 30 45 60 per side, indices in the layout
    static readonly double[] jan = [.. Enumerable.Range(0, NF).Select(i => Math.Sqrt(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / NF)))];
    readonly Fft? fft = modo == Frentes.Leque ? new(NF) : null;
    readonly double[] hl = new double[NF], hr = new double[NF], lRe = new double[NF], lIm = new double[NF], rRe = new double[NF], rIm = new double[NF];
    readonly double[] pL = new double[K], pR = new double[K], ganhoBin = new double[9 * K], fRe = new double[NF], fIm = new double[NF];
    readonly double[][] cauda = [.. Enumerable.Range(0, 9).Select(_ => new double[B])];
    readonly double[][] antes = [.. Enumerable.Range(0, 1 + extras).Select(_ => new double[B])];   // center + extras, one hop

    public void Limpar()
    {
        Array.Clear(linhaL); Array.Clear(linhaR); pos = 0;
        Array.Clear(hl); Array.Clear(hr); Array.Clear(pL); Array.Clear(pR);
        foreach (var c in cauda) Array.Clear(c);
        foreach (var c in antes) Array.Clear(c);
    }

    /// <summary>x = [L, R, C, extras...] (one block). Out: planoL/R = plain part at the ears; caixas = speaker feeds in layout order.
    /// Par: caixas may be x itself (in place). Leque: caixas must be other arrays. menosMeio (optional) is taken off the plain mid
    /// (what the caller sends through the center instead). atrasoCentro = the center HRTF peak (Brir.AtrasoCentro of the layout).</summary>
    public void Bloco(double[][] x, double la, double cx, double gc, double[]? menosMeio, double[] planoL, double[] planoR, double[][] caixas, int atrasoCentro)
    {
        bool leque = Modo == Frentes.Leque;
        int n = linhaL.Length, d = Math.Clamp(atrasoCentro + (leque ? B : 0), 0, n - B);
        var fl = x[0]; var fr = x[1]; var c = x[2];
        if (!leque)
        {
            var o0 = caixas[0]; var o1 = caixas[1]; var o2 = caixas[2];
            for (int i = 0; i < B; i++)
            {
                double l = fl[i], r = fr[i], s = 0.5 * (l - r), md = 0.5 * (l + r);
                if (menosMeio != null) md -= menosMeio[i];
                linhaL[pos] = md + la * s; linhaR[pos] = md - la * s;
                int k = (pos - d + n) % n; planoL[i] = linhaL[k]; planoR[i] = linhaR[k]; pos = (pos + 1) % n;
                o0[i] = cx * s; o1[i] = -cx * s; o2[i] = c[i] * gc;
            }
            for (int e = 0; e < Extras; e++) if (!ReferenceEquals(caixas[3 + e], x[3 + e])) Array.Copy(x[3 + e], caixas[3 + e], B);
            return;
        }
        for (int i = 0; i < B; i++)
        {
            double l = fl[i], r = fr[i], s = 0.5 * (l - r), md = 0.5 * (l + r);
            if (menosMeio != null) md -= menosMeio[i];
            linhaL[pos] = md + la * s; linhaR[pos] = md - la * s;
            int k = (pos - d + n) % n; planoL[i] = linhaL[k]; planoR[i] = linhaR[k]; pos = (pos + 1) % n;
        }
        Leque(fl, fr, cx, caixas);
        var cen = caixas[2]; var ca = antes[0];
        for (int i = 0; i < B; i++) { cen[i] = ca[i]; ca[i] = gc * c[i]; }
        for (int e = 0; e < Extras; e++) { var a = antes[1 + e]; Array.Copy(a, caixas[9 + e], B); Array.Copy(x[3 + e], a, B); }
    }

    /// <summary>cx·S of this block -> the 8 fan feeds (all but the center) one hop late. Pan per bin ψ from smoothed L/R powers:
    /// (1-|ψ|)·S on the ±30 pair (+S left, -S right), |ψ|·S only on its own side, 15 + 45·|ψ| between fan nodes (constant power).</summary>
    void Leque(double[] l0, double[] r0, double cx, double[][] y9)
    {
        Array.Copy(hl, B, hl, 0, B); Array.Copy(hr, B, hr, 0, B);
        Array.Copy(l0, 0, hl, B, B); Array.Copy(r0, 0, hr, B, B);
        for (int i = 0; i < NF; i++) { lRe[i] = hl[i] * jan[i]; rRe[i] = hr[i] * jan[i]; }
        Array.Clear(lIm); Array.Clear(rIm);
        fft!.Executar(lRe, lIm); fft.Executar(rRe, rIm);
        const double a = 0.7;
        Array.Clear(ganhoBin);
        for (int k = 0; k < K; k++)
        {
            pL[k] = a * pL[k] + (1 - a) * (lRe[k] * lRe[k] + lIm[k] * lIm[k]);
            pR[k] = a * pR[k] + (1 - a) * (rRe[k] * rRe[k] + rIm[k] * rIm[k]);
            double psi = (pL[k] - pR[k]) / (pL[k] + pR[k] + 1e-20), ap = Math.Abs(psi);
            ganhoBin[0 * K + k] += 1 - ap; ganhoBin[1 * K + k] -= 1 - ap;
            double p = 3 * ap; int j = Math.Min((int)p, 2); double t = p - j;
            var z = psi >= 0 ? ZonaE : ZonaD; double sg = psi >= 0 ? ap : -ap;
            ganhoBin[z[j] * K + k] += sg * Math.Cos(t * Math.PI / 2);
            ganhoBin[z[j + 1] * K + k] += sg * Math.Sin(t * Math.PI / 2);
        }
        for (int c = 0; c < 9; c++)
        {
            if (c == 2) continue;
            int o = c * K;
            for (int k = 0; k < K; k++) { double g = 0.5 * cx * ganhoBin[o + k]; fRe[k] = g * (lRe[k] - rRe[k]); fIm[k] = g * (lIm[k] - rIm[k]); }
            fft.RealInversa(fRe, fIm);
            var y = y9[c]; var cd = cauda[c];
            for (int i = 0; i < B; i++) { y[i] = cd[i] + fRe[i] * jan[i]; cd[i] = fRe[B + i] * jan[B + i]; }
        }
    }
}
