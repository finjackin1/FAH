// Reverb (port of the previous Python version): Dattorro plate (diffusers 4.8/3.6/12.7/9.3 ms),
// with the app's 7 controls. Per frame here (the Python block split was only for numpy). Buffers are sized once per rate
// for the biggest room, so changing a control never allocates nor cuts the tail.
namespace FAH.Efeitos;

public sealed class Reverb
{
    const double Base = 29761.0, EscalaMax = 1.45;
    public const double MixMax = 0.3;        // above 0.6 the song hides behind the echo; user asked for half

    /// <summary>Delay line, power-of-2 ring. Escrever then Atras(0) = what was just written.</summary>
    sealed class Linha(int minimo)
    {
        readonly float[] b = new float[(int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, minimo))];
        int p;
        int M => b.Length - 1;
        public void Escrever(float v) { b[p] = v; p = (p + 1) & M; }
        public float Atras(int t) => b[(p - 1 - t) & M];
        /// <summary>Before writing this frame: value written <paramref name="d"/> frames ago.</summary>
        public float Antes(int d) => b[(p - d) & M];
        public float AntesFrac(double d)
        {
            double idx = p - d; int i0 = (int)Math.Floor(idx); double fr = idx - i0;
            float x0 = b[i0 & M], x1 = b[(i0 + 1) & M];
            return (float)(x0 + (x1 - x0) * fr);
        }
        public void Limpar() { Array.Clear(b); p = 0; }
    }

    /// <summary>All-pass: w = x + g*w[d]; y = w[d] - g*w. Optional fractional (modulated) delay.</summary>
    sealed class PassaTudo(int maxAtraso, double g)
    {
        public readonly Linha L = new(maxAtraso + 4);
        public int D;
        public float Processar(float x) { float wd = L.Antes(D); float w = (float)(x + g * wd); L.Escrever(w); return (float)(wd - g * w); }
        public float Processar(float x, double mod) { float wd = L.AntesFrac(D + mod); float w = (float)(x + g * wd); L.Escrever(w); return (float)(wd - g * w); }
    }

    readonly double[] p = [.6020329, .801616, .3168983, .25056827, .3750593, .34842435, .44140986];   // "Reverb leve"
    readonly Lock trava = new();
    volatile bool sujo = true;
    readonly CompensaVolume comp = new();
    int taxa;
    Linha pre = null!, d1a = null!, d2a = null!, d1b = null!, d2b = null!;
    PassaTudo[] dif = null!;
    PassaTudo ap1a = null!, ap2a = null!, ap1b = null!, ap2b = null!;
    // current settings (applied on the processing thread when sujo)
    int preAtr, L1a, L2a, L1b, L2b;
    int tL1, tL2, tL3, tL4, tL5, tL6, tL7, tR1, tR2, tR3, tR4, tR5, tR6, tR7;
    double lpA, hpA, amort, dec, exc, fase, mixAtual, pdm, mixAlvo;
    double lpZ, hpX, hpY, zA, zB;
    bool limpo = true;

    public bool Ligado { get; set; }

    public Reverb(int taxa = 48000) => Preparar(taxa);

    public void Preparar(int t)
    {
        taxa = t;
        comp.Preparar(t);
        int S(double n) => (int)Math.Round(n * t / Base * EscalaMax) + 8;
        exc = 16.0 * t / Base;
        pre = new Linha(2 * ((int)(0.2 * t) + 4));
        dif = [new(S(142), .75), new(S(107), .75), new(S(379), .625), new(S(277), .625)];
        ap1a = new(S(672) + (int)exc + 4, -.7); ap1b = new(S(908) + (int)exc + 4, -.7);
        ap2a = new(S(1913), .5); ap2b = new(S(2656), .5);
        d1a = new(S(4453)); d2a = new(S(3720)); d1b = new(S(4217)); d2b = new(S(3163));
        sujo = true; limpo = false;
    }

    /// <summary>7 values 0..1: size, filter, fade, pre-delay, mix, pre-delay mix, damp. Mix capped at <see cref="MixMax"/>.</summary>
    public void Definir(ReadOnlySpan<double> v)
    {
        lock (trava)
        {
            for (int i = 0; i < p.Length; i++) p[i] = i < v.Length ? Math.Clamp(v[i], 0, 1) : 1;
            p[4] = Math.Min(MixMax, p[4]);
            sujo = true;
        }
    }

    void Montar()
    {
        double e, cLp, a6, pd;
        lock (trava) { e = .55 + .9 * p[0]; cLp = 500 * Math.Pow(40, p[1]); dec = .25 + .72 * p[2]; pd = p[3]; mixAlvo = p[4]; pdm = p[5] * .5; a6 = p[6]; sujo = false; }
        int S(double n, double k = 1) => Math.Max(8, (int)Math.Round(n * taxa / Base * k));
        preAtr = (int)(200 * pd * 0.001 * taxa);
        lpA = Math.Exp(-2 * Math.PI * Math.Min(cLp, taxa * .45) / taxa);
        hpA = Math.Exp(-2 * Math.PI * 60.0 / taxa);
        amort = .05 + .85 * a6;
        dif[0].D = S(142); dif[1].D = S(107); dif[2].D = S(379); dif[3].D = S(277);
        ap1a.D = S(672, e); ap2a.D = S(1800, e); ap1b.D = S(908, e); ap2b.D = S(2656, e);
        L1a = S(4453, e); L2a = S(3720, e); L1b = S(4217, e); L2b = S(3163, e);
        tL1 = S(266, e); tL2 = S(2974, e); tL3 = S(1913, e); tL4 = S(1996, e); tL5 = S(1990, e); tL6 = S(187, e); tL7 = S(1066, e);
        tR1 = S(353, e); tR2 = S(3627, e); tR3 = S(1228, e); tR4 = S(2673, e); tR5 = S(2111, e); tR6 = S(335, e); tR7 = S(121, e);
    }

    void Limpar()
    {
        foreach (var l in new[] { pre, d1a, d2a, d1b, d2b, ap1a.L, ap2a.L, ap1b.L, ap2b.L }) l.Limpar();
        foreach (var d in dif) d.L.Limpar();
        lpZ = hpX = hpY = zA = zB = 0;
        limpo = true;
    }

    public void Processar(Span<float> lr, int quadros)
    {
        if (sujo) Montar();
        double alvo = Ligado ? mixAlvo : 0;
        if (!Ligado && mixAtual < 1e-4 && comp.Neutro) { if (!limpo) Limpar(); mixAtual = 0; return; }
        limpo = false;
        double mix0 = mixAtual, soma = 0, w = 2 * Math.PI / taxa, d = quadros > 1 ? 1.0 / (quadros - 1) : 0;
        mixAtual = alvo;
        for (int i = 0; i < quadros; i++)
        {
            float xl = lr[2 * i], xr = lr[2 * i + 1];
            soma += xl * xl + xr * xr;
            // pre-delay (stereo, 2 lines interleaved in one: write L then R)
            pre.Escrever(xl); pre.Escrever(xr);
            float pl = pre.Atras(2 * preAtr + 1), pr = pre.Atras(2 * preAtr);
            // mono in -> low-pass (filter) -> high-pass 60 Hz
            double ent = (pl + pr) * .5;
            lpZ = (1 - lpA) * ent + lpA * lpZ;
            double hp = (1 + hpA) / 2 * (lpZ - hpX) + hpA * hpY; hpX = lpZ; hpY = hp;
            float v = (float)hp;
            foreach (var ap in dif) v = ap.Processar(v);
            // slow modulation (~1 Hz) of the tank all-passes
            double ma = exc * Math.Sin(fase), mb = exc * Math.Cos(fase);
            fase += w; if (fase > 2 * Math.PI) fase -= 2 * Math.PI;
            float fbB = (float)(d2b.Antes(L2b) * dec), fbA = (float)(d2a.Antes(L2a) * dec);
            // half A
            float a = ap1a.Processar(v + fbB, ma);
            d1a.Escrever(a); a = d1a.Atras(L1a);
            zA = (1 - amort) * a + amort * zA;
            d2a.Escrever(ap2a.Processar((float)(zA * dec)));
            // half B
            float b = ap1b.Processar(v + fbA, mb);
            d1b.Escrever(b); b = d1b.Atras(L1b);
            zB = (1 - amort) * b + amort * zB;
            d2b.Escrever(ap2b.Processar((float)(zB * dec)));
            // outputs (Dattorro tap table)
            double yl = d1a.Atras(tL1) + d1a.Atras(tL2) - ap2a.L.Atras(tL3) + d2a.Atras(tL4) - d1b.Atras(tL5) - ap2b.L.Atras(tL6) - d2b.Atras(tL7);
            double yr = d1b.Atras(tR1) + d1b.Atras(tR2) - ap2b.L.Atras(tR3) + d2b.Atras(tR4) - d1a.Atras(tR5) - ap2a.L.Atras(tR6) - d2a.Atras(tR7);
            double m = quadros > 1 ? mix0 + (alvo - mix0) * i * d : alvo, seco = 1 - .45 * m, mo = .6 * m, pm = pdm * m;
            lr[2 * i] = (float)(xl * seco + yl * mo + pl * pm);
            lr[2 * i + 1] = (float)(xr * seco + yr * mo + pr * pm);
        }
        comp.Aplicar(soma, lr, quadros);
    }
}
