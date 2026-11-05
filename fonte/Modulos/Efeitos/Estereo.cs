// Stereo (port of the previous Python version): balance, width (mid/side), mono.
// Every change ramps across one block; width change is volume-compensated (wider must not just mean louder).
namespace FAH.Efeitos;

/// <summary>Keeps an effect's output as loud as its input (~1.5 s mean energy). Gain -12..+6 dB, ramped per block; holds in silence.</summary>
public sealed class CompensaVolume
{
    static readonly double GMax = Math.Pow(10, 6 / 20.0), GMin = Math.Pow(10, -12 / 20.0);
    double a, eIn, eOut, g = 1;
    /// <summary>Share of the level difference taken back (1 = all, 0.5 = half the dB: Estéreo+ keeps the centre near unity).</summary>
    public double Fracao { get; init; } = 1;

    public CompensaVolume(int taxa = 48000, double segundos = 1.5) => Preparar(taxa, segundos);
    public void Preparar(int taxa, double segundos = 1.5) => a = Math.Exp(-1.0 / (segundos * taxa / 480.0));   // per 480-frame block

    /// <summary>Gain back at 1: the effect may leave the path without a step.</summary>
    public bool Neutro => Math.Abs(g - 1) < 2e-3;

    /// <summary><paramref name="somaEntrada"/> = sum of squares of the input block (L and R). Scales <paramref name="lr"/> in place.</summary>
    public void Aplicar(double somaEntrada, Span<float> lr, int quadros)
    {
        if (quadros <= 0) return;
        double so = 0;
        for (int i = 0; i < quadros * 2; i++) so += lr[i] * lr[i];
        double ab = Math.Pow(a, quadros / 480.0);
        eIn = ab * eIn + (1 - ab) * somaEntrada / (quadros * 2);
        eOut = ab * eOut + (1 - ab) * so / (quadros * 2);
        double ant = g;
        if (eIn > 1e-8 && eOut > 1e-12) g = Math.Clamp(Math.Pow(eIn / eOut, 0.5 * Fracao), GMin, GMax);   // above ~-80 dB: real sound
        double passo = quadros > 1 ? (g - ant) / (quadros - 1) : 0;
        for (int i = 0; i < quadros; i++)
        {
            float k = (float)(quadros > 1 ? ant + passo * i : g);
            lr[2 * i] *= k; lr[2 * i + 1] *= k;
        }
    }
}

public sealed class Estereo
{
    public const double LarguraMax = 1.25;   // above 1.5 the side goes out of phase; user asked for half the extra
    readonly CompensaVolume comp = new();
    double la = 1, gaL = 1, gaR = 1, hpA, hpX, hpY;

    /// <summary>-1 left .. +1 right.</summary>
    public double Balanco { get; set; }
    /// <summary>0 mono .. 1.25 wide.</summary>
    public double Largura { get; set; } = 1;
    public bool Mono { get; set; }
    /// <summary>Estéreo+: widen only the side above ~120 Hz (bass stays centered, no phasey boom). 0 = whole side.</summary>
    public double CorteLadoHz { get; set; }

    public void Preparar(int taxa) { comp.Preparar(taxa); taxaHz = taxa; }
    int taxaHz = 48000;

    public bool Neutro => Math.Abs(Balanco) < 1e-4 && Math.Abs(Largura - 1) < 1e-4 && !Mono
        && Math.Abs(gaL - 1) < 1e-9 && Math.Abs(gaR - 1) < 1e-9 && Math.Abs(la - 1) < 1e-4 && comp.Neutro;

    public void Processar(Span<float> lr, int quadros)
    {
        if (Neutro || quadros <= 0) return;
        bool mono = Mono, corte = !mono && CorteLadoHz > 0;
        if (corte) hpA = 1 / (1 + 2 * Math.PI * CorteLadoHz / taxaHz);
        double larg = mono ? 0 : Math.Clamp(Largura, 0, LarguraMax), b = Math.Clamp(Balanco, -1, 1);
        double nl = Math.Min(1, 1 - b), nr = Math.Min(1, 1 + b), d = quadros > 1 ? 1.0 / (quadros - 1) : 0, soma = 0;
        for (int i = 0; i < quadros; i++)
        {
            double t = quadros > 1 ? i * d : 1, l = lr[2 * i], r = lr[2 * i + 1];
            soma += l * l + r * r;
            double m = (l + r) * 0.5, s0 = (l - r) * 0.5, k = la + (larg - la) * t, s;
            if (corte)
            {   // one-pole highpass of the side: only that part gets the extra width
                double y = hpA * (hpY + s0 - hpX); hpX = s0; hpY = y;
                s = s0 + (k - 1) * y;
            }
            else s = s0 * k;
            lr[2 * i] = (float)(m + s); lr[2 * i + 1] = (float)(m - s);
        }
        if (!mono) comp.Aplicar(soma, lr, quadros);
        for (int i = 0; i < quadros; i++)
        {
            double t = quadros > 1 ? i * d : 1;
            lr[2 * i] *= (float)(gaL + (nl - gaL) * t); lr[2 * i + 1] *= (float)(gaR + (nr - gaR) * t);
        }
        la = larg; gaL = nl; gaR = nr;
    }
}
