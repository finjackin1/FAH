// Volume leveler (Smart Volume of PowerampPC, 05/11): slow broadband loudness leveler + fast guard. One gain for all bands (timbre intact).
//  - program level: K-weighted energy (BS.1770 ear curve, sidechain only: bass no longer hides quiet voices) follower, rises in 250 ms / falls in 800 ms, down to 120 ms when the sound dropped >12 dB
//    (silence below -55 dB does not measure: gain stays);
//  - gain: toward the target of the "medio" level (-20 dB; ratio 3 above it, 0.6 x gap up to +9 below it), scaled by Intensidade (0.5 = TETOS smart);
//    reduces in 150 ms, recovers in 1.2 s when close, down to 400 ms when the gap is >5.5 dB
//    (08/11 user: "doesn't follow the audio": quiet part after a loud one took 5-7 s to level, now 2-3 s; simulated pumping <0.2 dB);
//  - guard: 3 ms peak follower; a blast above target+10 dB takes back the gain the leveler had added (never below 0 dB), attack ~1 ms, release 300 ms;
//    audio is delayed 5 ms so the guard is already down when the blast arrives.
// Control runs once per ms (no per-sample log/pow); gain is ramped between control steps. Off = bit-identical (after a 50 ms fade out). No allocations in Processar.
// The delay line keeps running while off, so on/off is a 50 ms crossfade direct <-> delayed (no 5 ms hole, no jump).
namespace FAH.Efeitos;

public sealed class Nivelador
{
    public static double Alvo = -20, Razao = 3, GanhoMax = 9, GuardaDb = 10, Intensidade = 0.5;   // static (not const) since 08/11: Dev screen

    int taxa = 48000, sub = 48, atraso = 240;
    float[] anel = new float[480];
    int pos, cont;
    double soma, pico;                       // accumulators of the current ms
    double env = -1, ef, g, gf;              // level energy (<0 = none yet), guard energy, slow gain dB, guard gain dB (<=0)
    int medidas;
    double gAtual = 1, gPasso;               // linear gain now / step per frame
    double cEnvSobe, cEnvDesce, cEnvDesceRap, cGRed, cGRec, cGRecRap, cEf, cGfAt, cGfSolta, passoFade;
    // K-weighting biquads (shelf + high-pass), per channel: coefficients and state
    double sb0, sb1, sb2, sa1, sa2, hb0, hb1, hb2, ha1, ha2;
    double s1l, s2l, h1l, h2l, s1r, s2r, h1r, h2r;
    bool ativo;                              // processing (on, or fading out)
    double fade = 1;                         // 1 = full effect, 0 = off

    public bool Ligado { get; set; }
    /// <summary>Total gain now (dB), for tests/diagnostics.</summary>
    public double GanhoDb => g + gf;

    static double C(double ms, double passoMs) => Math.Exp(-passoMs / ms);

    public void Preparar(int t)
    {
        taxa = t; sub = Math.Max(1, t / 1000); atraso = (int)(0.005 * t);
        anel = new float[atraso * 2];
        double p = sub * 1000.0 / t;         // control step in ms (1)
        cEnvSobe = C(250, p); cEnvDesce = C(800, p); cEnvDesceRap = C(120, p); cGRed = C(150, p); cGRec = C(1200, p); cGRecRap = C(400, p);
        {   // BS.1770 pre-filter (high shelf +4 dB at 1.68 kHz) and RLB high-pass 38 Hz, recomputed for this rate (same formulas as pyloudnorm/libebur128)
            double K = Math.Tan(Math.PI * 1681.974450955533 / t), Q = 0.7071752369554196, Vh = Math.Pow(10, 3.999843853973347 / 20), Vb = Math.Pow(Vh, 0.4996667741545416);
            double a0 = 1 + K / Q + K * K;
            sb0 = (Vh + Vb * K / Q + K * K) / a0; sb1 = 2 * (K * K - Vh) / a0; sb2 = (Vh - Vb * K / Q + K * K) / a0; sa1 = 2 * (K * K - 1) / a0; sa2 = (1 - K / Q + K * K) / a0;
            K = Math.Tan(Math.PI * 38.13547087602444 / t); Q = 0.5003270373238773; a0 = 1 + K / Q + K * K;
            hb0 = 1; hb1 = -2; hb2 = 1; ha1 = 2 * (K * K - 1) / a0; ha2 = (1 - K / Q + K * K) / a0;
        }
        cEf = C(3, p); cGfAt = C(1, p); cGfSolta = C(300, p); passoFade = 1.0 / (0.05 * t);
        Reiniciar();
    }

    void Reiniciar() { Array.Clear(anel); pos = 0; Recomecar(); fade = 1; ativo = false; }

    // switching on: fresh measurement, ring kept (already holds the last 5 ms), fade in from direct
    void Recomecar() { s1l = s2l = h1l = h2l = s1r = s2r = h1r = h2r = 0; cont = medidas = 0; soma = pico = 0; env = -1; ef = 0; g = gf = 0; gAtual = 1; gPasso = 0; fade = 0; }

    void Controle()
    {
        double e = soma / sub;
        if (!(e < 1e30) || !(pico < 1e30)) { e = 0; pico = 0; s1l = s2l = h1l = h2l = s1r = s2r = h1r = h2r = 0; }    // NaN/inf input must not freeze the guard
        if (e > ef) ef = e; else ef = ef * cEf + e * (1 - cEf);
        if (pico > ef) ef = pico;
        if (e > 3.16e-6)                                           // -55 dB: there is sound -> measure
        {
            medidas++;
            double c = cEnvSobe;
            if (e <= env) { double q = env > 0 ? 10 * Math.Log10(env / e) : 0; c = cEnvDesce + (cEnvDesceRap - cEnvDesce) * Math.Clamp((q - 6) / 6, 0, 1); }
            c = Math.Min(c, 1.0 - 1.0 / medidas);
            env = env < 0 ? e : c * env + (1 - c) * e;
        }
        if (env >= 0)
        {
            double lu = 10 * Math.Log10(env + 1e-12);
            double alvoG = (lu > Alvo ? (Alvo - lu) * (1 - 1 / Razao) : Math.Min(GanhoMax, (Alvo - lu) * 0.6)) * Intensidade;
            double falta = alvoG - g;                              // recovery speeds up with the gap: 1.2 s at <=1.5 dB, 0.4 s at >=5.5 dB
            double c = falta < 0 ? cGRed : cGRec + (cGRecRap - cGRec) * Math.Clamp((falta - 1.5) / 4, 0, 1);
            g = c * g + (1 - c) * alvoG;
        }
        double sobra = 10 * Math.Log10(ef + 1e-12) + g - (Alvo + GuardaDb);
        double alvoF = sobra > 0 ? -Math.Min(sobra, Math.Max(g, 0)) : 0;
        double cf = alvoF < gf ? cGfAt : cGfSolta;
        gf = cf * gf + (1 - cf) * alvoF;
        double alvoLin = Math.Pow(10, (g + gf) * fade / 20);
        gPasso = (alvoLin - gAtual) / sub;
        soma = pico = 0; cont = 0;
    }

    public void Processar(Span<float> lr, int quadros)
    {
        int n = anel.Length / 2;
        quadros = Math.Min(quadros, lr.Length / 2);   // interleaved stereo only: never read past the buffer
        if (!Ligado && !ativo)
        {
            for (int i = 0; i < quadros; i++) { anel[2 * pos] = lr[2 * i]; anel[2 * pos + 1] = lr[2 * i + 1]; if (++pos >= n) pos = 0; }   // output untouched
            return;
        }
        if (Ligado && !ativo) { Recomecar(); ativo = true; }
        for (int i = 0; i < quadros; i++)
        {
            float l = lr[2 * i], r = lr[2 * i + 1];
            double pl = (double)l * l, pr = (double)r * r;
            if (pl > pico) pico = pl; if (pr > pico) pico = pr;                     // guard: raw peaks
            double y = sb0 * l + s1l; s1l = sb1 * l - sa1 * y + s2l; s2l = sb2 * l - sa2 * y;   // level: K-weighted (TDF-II)
            double k = hb0 * y + h1l; h1l = hb1 * y - ha1 * k + h2l; h2l = hb2 * y - ha2 * k; soma += k * k * 0.5;
            y = sb0 * r + s1r; s1r = sb1 * r - sa1 * y + s2r; s2r = sb2 * r - sa2 * y;
            k = hb0 * y + h1r; h1r = hb1 * y - ha1 * k + h2r; h2r = hb2 * y - ha2 * k; soma += k * k * 0.5;
            if (++cont >= sub) Controle();
            if (!Ligado && fade > 0) fade = Math.Max(0, fade - passoFade);
            else if (Ligado && fade < 1) fade = Math.Min(1, fade + passoFade);
            gAtual += gPasso;
            float dl = anel[2 * pos], dr = anel[2 * pos + 1];
            anel[2 * pos] = l; anel[2 * pos + 1] = r;
            if (++pos >= n) pos = 0;
            double gm = gAtual * fade, gd = 1 - fade;
            lr[2 * i] = (float)(l * gd + dl * gm); lr[2 * i + 1] = (float)(r * gd + dr * gm);
        }
        if (!Ligado && fade <= 0) { ativo = false; gAtual = 1; }
    }
}
