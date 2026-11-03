// EQ model (equ5), port of the previous Python version (audio/filtros.py).
// RBJ biquads in double, state per channel. Types 0-2 (LP/HP/BP) = PARALLEL branch y = x + Σ(G-1)·H(x);
// types 3-5 (peak/low shelf/high shelf) in SERIES after the parallel sum.
using System.Numerics;

namespace FAH.Equalizador;

public enum Tipo { PassaBaixa, PassaAlta, PassaFaixa, Pico, PratGrave, PratAguda }

public enum Canais { Ambos, Esq, Dir }

public record struct Banda(Tipo Tipo, double Freq, double Q, double Ganho, Canais Canais = Canais.Ambos, bool Ligada = true)
{
    public readonly bool Paralela => Tipo <= Tipo.PassaFaixa;
    public readonly Banda Limitada() => this with { Freq = Math.Clamp(Freq, 10, 24000), Q = Math.Clamp(Q, 0.1, 12), Ganho = Math.Clamp(Ganho, -15, 15) };
}

/// <summary>Full EQ state (what a preset stores).</summary>
public sealed record Ajuste
{
    public bool Ligado { get; init; } = true;
    public double Preamp { get; init; }
    public bool TomLigado { get; init; } = true;
    public double Graves { get; init; }      // dB, LPF 90 Hz Q 0.8 parallel
    public double Agudos { get; init; }      // dB, HPF 10 kHz Q 0.6 parallel
    public IReadOnlyList<Banda> Bandas { get; init; } = [];
    /// <summary>AutoEQ headphone correction: series bands + its own preamp, independent of Ligado (EQU).</summary>
    public IReadOnlyList<Banda> Correcao { get; init; } = [];
    public double CorrecaoPre { get; init; }

    public const double GravesFreq = 90, GravesQ = 0.8, AgudosFreq = 10000, AgudosQ = 0.6;

    public static double QGrafico(int n) => 1.084095 + (3.33 - 1.084095) * (Math.Clamp(n, 5, 32) - 5) / 27.0;
}

public readonly record struct Coef(double B0, double B1, double B2, double A1, double A2)
{
    /// <summary>RBJ cookbook, same as equ5_calc_coefs (normalized by a0).</summary>
    public static Coef De(Tipo tipo, double freq, double q, double ganhoDb, int taxa)
    {
        double f = Math.Min(freq, taxa / 2.0 - 1.0), w0 = 2 * Math.PI * f / taxa, c = Math.Cos(w0), s = Math.Sin(w0);
        double al = s / (2 * Math.Max(q, 1e-3)), b0, b1, b2, a0, a1, a2;
        switch (tipo)
        {
            case Tipo.PassaBaixa: b0 = (1 - c) / 2; b1 = 1 - c; b2 = b0; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
            case Tipo.PassaAlta: b0 = (1 + c) / 2; b1 = -(1 + c); b2 = b0; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
            case Tipo.PassaFaixa: b0 = al; b1 = 0; b2 = -al; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
            default:
                double A = Math.Pow(10, ganhoDb / 40);
                if (tipo == Tipo.Pico) { b0 = 1 + al * A; b1 = -2 * c; b2 = 1 - al * A; a0 = 1 + al / A; a1 = -2 * c; a2 = 1 - al / A; break; }
                double r = 2 * Math.Sqrt(A) * al;
                if (tipo == Tipo.PratGrave)
                {
                    b0 = A * ((A + 1) - (A - 1) * c + r); b1 = 2 * A * ((A - 1) - (A + 1) * c); b2 = A * ((A + 1) - (A - 1) * c - r);
                    a0 = (A + 1) + (A - 1) * c + r; a1 = -2 * ((A - 1) + (A + 1) * c); a2 = (A + 1) + (A - 1) * c - r;
                }
                else
                {
                    b0 = A * ((A + 1) + (A - 1) * c + r); b1 = -2 * A * ((A - 1) + (A + 1) * c); b2 = A * ((A + 1) + (A - 1) * c - r);
                    a0 = (A + 1) - (A - 1) * c + r; a1 = 2 * ((A - 1) - (A + 1) * c); a2 = (A + 1) - (A - 1) * c - r;
                }
                break;
        }
        return new(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    public Complex H(double f, int taxa)
    {
        var z1 = Complex.FromPolarCoordinates(1, -2 * Math.PI * f / taxa);
        var z2 = z1 * z1;
        return (B0 + B1 * z1 + B2 * z2) / (1 + A1 * z1 + A2 * z2);
    }
}

/// <summary>One biquad section, transposed direct form II, 2 channels.</summary>
internal sealed class Secao(Coef k, double fator, Canais canais)
{
    public readonly Coef K = k;
    public readonly double Fator = fator;          // parallel: G-1; series: unused
    public readonly Canais Canais = canais;
    double z1L, z2L, z1R, z2R;

    public bool Usa(int ch) => Canais == Canais.Ambos || (int)Canais - 1 == ch;

    public double Passo(double x, int ch)
    {
        double y;
        if (ch == 0) { y = K.B0 * x + z1L; z1L = K.B1 * x - K.A1 * y + z2L; z2L = K.B2 * x - K.A2 * y; }
        else { y = K.B0 * x + z1R; z1R = K.B1 * x - K.A1 * y + z2R; z2R = K.B2 * x - K.A2 * y; }
        return y;
    }

    public bool Igual(Secao o) => o.K == K && o.Fator == Fator && o.Canais == Canais;

    public void Herdar(Secao o) { if (o.Canais != Canais) return; z1L = o.z1L; z2L = o.z2L; z1R = o.z1R; z2R = o.z2R; }
}

/// <summary>Filters ready for one rate.</summary>
internal sealed class Conjunto
{
    public readonly Secao[] Par, Ser;
    public readonly double Ganho;
    public bool Vazio => Par.Length == 0 && Ser.Length == 0;

    public Conjunto(Ajuste aj, int taxa)
    {
        var par = new List<Secao>(); var ser = new List<Secao>();
        if (aj.TomLigado)
        {
            if (Math.Abs(aj.Graves) > 0.01) par.Add(new(Coef.De(Tipo.PassaBaixa, Ajuste.GravesFreq, Ajuste.GravesQ, 0, taxa), Math.Pow(10, aj.Graves / 20) - 1, Canais.Ambos));
            if (Math.Abs(aj.Agudos) > 0.01) par.Add(new(Coef.De(Tipo.PassaAlta, Ajuste.AgudosFreq, Ajuste.AgudosQ, 0, taxa), Math.Pow(10, aj.Agudos / 20) - 1, Canais.Ambos));
        }
        foreach (var b in aj.Correcao)
            if (b.Ligada && !b.Paralela && Math.Abs(b.Ganho) >= 0.005) ser.Add(new(Coef.De(b.Tipo, b.Freq, Math.Clamp(b.Q, 0.1, 12), Math.Clamp(b.Ganho, -24, 24), taxa), 0, Canais.Ambos));
        if (aj.Ligado)
            foreach (var b0 in aj.Bandas)
            {
                var b = b0.Limitada();
                if (!b.Ligada || Math.Abs(b.Ganho) < 0.005) continue;
                if (b.Paralela) par.Add(new(Coef.De(b.Tipo, b.Freq, b.Q, 0, taxa), Math.Pow(10, b.Ganho / 20) - 1, b.Canais));
                else ser.Add(new(Coef.De(b.Tipo, b.Freq, b.Q, b.Ganho, taxa), 0, b.Canais));
            }
        Par = [.. par]; Ser = [.. ser];
        Ganho = (aj.Ligado ? Math.Pow(10, aj.Preamp / 20) : 1.0) * Math.Pow(10, Math.Min(aj.CorrecaoPre, 0) / 20);
    }

    /// <summary>Keeps the state only of filters that did not change (same coefficients). State of a DIFFERENT
    /// filter (old code: same slot) makes a burst when the AutoEQ headphone changes; changed ones start at rest
    /// and the crossfade hides their start.</summary>
    public void Herdar(Conjunto o)
    {
        static void Casar(Secao[] novo, Secao[] velho)
        {
            var usado = new bool[velho.Length];
            foreach (var n in novo)
                for (int j = 0; j < velho.Length; j++)
                    if (!usado[j] && velho[j].Igual(n)) { n.Herdar(velho[j]); usado[j] = true; break; }
        }
        Casar(Par, o.Par); Casar(Ser, o.Ser);
    }

    public double Amostra(double x, int ch)
    {
        double y = x;
        foreach (var s in Par) if (s.Usa(ch)) y += s.Fator * s.Passo(x, ch);
        foreach (var s in Ser) if (s.Usa(ch)) y = s.Passo(y, ch);
        return y;
    }

    public double RespostaDb(double f, int taxa, int ch)
    {
        Complex tot = 1, ser = 1;
        foreach (var s in Par) if (s.Usa(ch)) tot += s.Fator * s.K.H(f, taxa);
        foreach (var s in Ser) if (s.Usa(ch)) ser *= s.K.H(f, taxa);
        return 20 * Math.Log10(Math.Max((tot * ser).Magnitude * Ganho, 1e-9));
    }
}

/// <summary>Processes stereo blocks. New settings from any thread; old and new run together for 30 ms and crossfade
/// (one block was too short: cut heard on AutoEQ changes, 27/10/2026). A request during a fade waits for it to end.</summary>
public sealed class Equalizador(int taxa = 48000)
{
    int taxa = taxa;
    Ajuste? novo = new();
    Conjunto? atual, velho;
    double ganhoAtual = 1, ganhoVelho = 1;
    int fadeN, fadePos;

    public Ajuste Ajuste { get; private set; } = new();

    public void Definir(Ajuste aj) { Ajuste = aj; Volatile.Write(ref novo, aj); }

    public void MudarTaxa(int t) { if (t == taxa) return; taxa = t; atual = null; Volatile.Write(ref novo, Ajuste); }

    /// <summary>Real curve (what is heard), dB at f.</summary>
    public static double RespostaDb(Ajuste aj, double f, int taxa = 48000, int ch = 0) => new Conjunto(aj, taxa).RespostaDb(f, taxa, ch);

    public void Processar(Span<float> lr, int quadros)
    {
        if (velho == null)
        {
            var pedido = Interlocked.Exchange(ref novo, null);
            if (pedido != null)
            {
                var conj = new Conjunto(pedido, taxa);
                if (atual != null) { conj.Herdar(atual); velho = atual; ganhoVelho = ganhoAtual; fadeN = Math.Max(1, taxa * 30 / 1000); fadePos = 0; }
                atual = conj; ganhoAtual = conj.Ganho;
            }
        }
        var c = atual;
        if (c == null) return;
        int i = 0;
        if (velho != null)
        {
            for (; i < quadros && fadePos < fadeN; i++, fadePos++)
            {
                double t = (double)fadePos / fadeN;
                for (int ch = 0; ch < 2; ch++)
                {
                    double x = lr[2 * i + ch];
                    lr[2 * i + ch] = (float)(velho.Amostra(x, ch) * ganhoVelho * (1 - t) + c.Amostra(x, ch) * ganhoAtual * t);
                }
            }
            if (fadePos >= fadeN) velho = null;
        }
        if (c.Vazio && ganhoAtual == 1) return;
        for (; i < quadros; i++)
        {
            lr[2 * i] = (float)(c.Amostra(lr[2 * i], 0) * ganhoAtual);
            lr[2 * i + 1] = (float)(c.Amostra(lr[2 * i + 1], 1) * ganhoAtual);
        }
    }
}
