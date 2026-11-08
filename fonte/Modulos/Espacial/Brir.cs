// Virtual speakers: HRTF (MIT KEMAR, or a SADIE II head he picks: Ouvidos) + room (approved 14-20/10/2026).
// Each speaker = direct HRIR of its direction (diffuse-field EQ, speaker-set EQ, front/back cues) + ONE shared room
// reverb (mono sum -> stereo room FIR), drr dB under the direct, whole response at energy 1.
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace FAH.Espacial;

public static partial class Brir
{
    public const int Taxa = 48000;

    // Raw 7.1 (28/10, user asked): every automatic EQ off, only HRTF + room. true = bring that stage back
    // (state before: backup6-10-28-som71-antes-cru).
    // EqDifuso back on 28/10: without it the KEMAR ear resonance (2-4 kHz) made mids/voices honky ("som laranja")
    // 28/10 later: full strength (±12 dB) made it muffled/low-quality (AutoEQ target already has ear gain) → half, ±6 dB
    public static double ForcaDifuso = 0.5;
    /// <summary>His colors (28/10): laranja ~1.6 kHz, amarelo ~3.2 kHz (both too high), 6.5 kHz lift; branco = treble/brilho (~11 kHz; +2.5 then too bright on Rocket Man → +1, 6.5k +2 → +1). Peak sections.</summary>
    public static (double Hz, double Q, double Db)[] Cores = [(1600, 1.0, -1.5), (3200, 1.0, -1.0), (6500, 1.0, 1.0), (11000, 0.7, 2.0)];   // 01/11: 11k +1 → +2, still "abafado", room treble alone moved the ear only +0.1 dB; +2.5 was too bright
    public static bool EqDifuso = true, EqCaixas = false, PistasLigadas = true, CorrecaoLigada = false;
    /// <summary>29/10: front/back sounded swapped (KEMAR ±30 and ±150 share ear cues) → cues back on; front at half (its 3.6 kHz lift = his amarelo).</summary>
    /// 01/11: music sounded from behind ("sentado de costas") → front cues full; the 3.6 kHz lift kept at its old size (factor 0.25 below).
    /// 01/11 12:52 half again = "piorou muito tudo" → full kept; muffling handled by narrower front bands (PistasFrenteTras).
    public static double ForcaFrente = 1.0;
    const double CSom = 343.0;

    /// <summary>name, size W x D x H (m), wall reflection, RT60 (s), DRR (dB), pre-delay (ms).</summary>
    /// <summary>PbEco/PbCauda = low-pass (Hz) of the wall reflections / the diffuse noise tail. EcoDb > 0 = each speaker has its own reflections
    /// (through the HRTF, EcoDb under the direct); only the tail is shared. 0 = all in the shared room.</summary>
    public sealed record Sala(string Nome, double W, double D, double H, double Refl, double Rt60, double Drr, double PreMs, double PbEco = 6000, double PbCauda = 4500, double EcoDb = 0);
    /// <summary>07/11 user: "ainda tem eco no 7.1, isso não" -> no room at all (no reflections, no tail), like Estéreo+. false = approved room back.</summary>
    public static bool SemEco = true;
    /// <summary>07/11 user chose "EQ neutra": raw HRIR per speaker (no set EQ, cues, contrast, centered-voice fix, reflections, room, colors),
    /// then ONE min-phase EQ that flattens the layout average (|HL|²+|HR|²) to 0 dB. Motor: voice boost and stereo sub off. false = previous chain.
    /// (The tiny "pertinho" room tried before it was reverted: "eq bizarra".)</summary>
    public static bool Neutro = true;
    // 07/11 "polimento 3" (user: "ficou muito bom") = Neutro + VozPlana + SemEq71 + SalaReal71 + AoArLivre, all true.
    // 07/11 "polimento 2" = this neutral 7.1 as released in 1.2.2 (approved: "essa versão atual tá muito boa"). Back to it: VozPlana = false.
    /// <summary>07/11 user: the voice in 7.1 must sound like stereo; only the rest gives the 3D. With Neutro: the 7.1 center role skips the head,
    /// 0.707·C straight to both ears (Motor), timed with the HRTF center (AtrasoCentro). Panned voices still move (they live in the fronts/sides).</summary>
    public static bool VozPlana = true;
    /// <summary>07/11 test: 7.1 with the raw HRTF, no neutral EQ (flat gain only). Back to polimento 2: SemEq71 = false.</summary>
    public static bool SemEq71 = true;
    /// <summary>07/11 user: speakers felt all aimed at him; each must radiate 360° like real life -> wall bounces per speaker (SomarParedes). false = off.</summary>
    public static bool SalaReal71 = true;
    /// <summary>07/11 user: not a room, outdoors with nothing in the way -> free field, only the grass ground bounce (0.6, low-passed 2.5 kHz). false = living room.</summary>
    public static bool AoArLivre = true;
    /// <summary>07/11 after polimento 3: plain voice + scattered 360° halo (Halo). false = polimento 3. HaloDb = halo under the plain voice.</summary>
    public static bool VozHalo = true;
    public static double HaloDb = 10;
    // 07/11 "polimento 4" (user: "PERFEITO, parece ao redor") = polimento 3 + VozHalo.
    /// <summary>07/11 test: every other speaker gets its own halo too (own random taps, HaloDb under its direct). false = polimento 4.</summary>
    public static bool HaloTodas = false;
    /// <summary>07/11: each speaker spreads this many degrees, aimed at the head (Cobertura). 360 = polimento 4.</summary>
    public static double Espalhamento = 360;
    /// <summary>07/11 user: new 7.1 = the approved Estéreo+ v3 as base + the other speakers for separation (Motor): fronts go PLAIN to the ears
    /// (delayed to meet the HRTF) + CaixaBase·S / -CaixaBase·S into the ±30 HRTF speakers; center = raw HRTF speaker at 0° (no halo, no plain
    /// voice) at GanhoCentroBase; sides/backs stay HRTF. false = polimento 4 ("PERFEITO") exactly.</summary>
    public static bool BaseEstereo = true;
    public static double GanhoCentroBase = 1.0, CaixaBase = 0.5;
    /// <summary>Samples from a 7.1 center input to its peak at the ear through the neutral HRTF (without the engine block).</summary>
    public static volatile int AtrasoCentro;
    public static readonly Sala[] Salas =
    [
        // 01/11: Música (4 x 3.6 m, RT60 0.18) = "bolha", space reached a point then cut off → same fix as 29/10 media:
        // huge space (late first reflections), short dry tail. Old: ("pequena", 4.0, 3.6, 2.6, 0.45, 0.18, 15.0, 8.0)
        // 01/11 later: big room = speakers not separate (one "area"), sound from behind, too big for music → mid room, more direct
        // 01/11 15h: still "abafado"; he: muffled = space = brilho → the room had no treble. 28/10 9k/7k on both = hiss, low quality
        // (noise tail) → reflections bright (clean echoes, no noise), tail only a small step. Música room only.
        // 01/11 16h: "ainda parece um local só": the shared mono room gave every speaker the same space → own reflections per speaker
        // 01/11 night: own reflections -12 dB = lateral and late (out of sync) -> -20, whole room back to the approved -17 dB
        new("pequena", 7.0, 6.0, 3.0, 0.40, 0.28, 17.0, 10.0, 12000, 5500, 20.0),
        // 29/10: games/films felt like a small room (mid and far sounds vanished fast) → huge spaces, more room vs direct
        // later: "space good, MUCH echo" → same size (late first reflections = space), short dry tail
        new("media", 30.0, 25.0, 10.0, 0.50, 0.45, 15.5, 22.0),
        new("grande", 40.0, 32.0, 14.0, 0.50, 0.55, 15.0, 28.0),
    ];

    /// <summary>Content type: room and extra voice (Hz, dB). The voice switch is the same enhancer for all (Motor.Realce).</summary>
    public sealed record Tipo(string Nome, int Sala, (double Hz, double Db)[] Voz);
    public static readonly Tipo[] Tipos =
    [
        new("musica", 0, []),
        new("filme", 2, []),
        new("jogo", 1, [(4000, 1.0), (5657, 1.0)]),
    ];

    // Timbre correction measured in pink noise (ferramentas\calibrar_espacial.py), half-octave peaks, by (mode, type)
    public static readonly double[] FreqsCorr = [.. Enumerable.Range(0, 17).Select(k => Math.Round(63 * Math.Pow(2, k / 2.0)))];
    public const double QCorr = 2.1;
    static readonly double[][] CorrEst =
    [
        [-1.6, -1.1, -1.4, -1.4, -1.3, -1.0, -0.8, -0.2, 1.2, 1.8, 1.7, 0.8, 1.0, -1.5, 5.0, 3.2, 4.1],
        [-1.2, -0.4, -1.2, -1.5, -1.1, -0.9, -0.9, -0.0, 1.3, 1.7, 1.5, 0.7, 1.1, -1.3, 5.0, 3.4, 4.4],
        [-1.2, -1.1, -1.4, -1.3, -1.4, -1.2, -1.0, -0.1, 1.2, 1.7, 1.6, 0.7, 1.0, -1.6, 5.0, 3.1, 4.1],
    ];
    static readonly double[][] Corr71 =
    [
        [0.2, 0.0, -0.2, -0.3, -0.3, -0.0, -0.1, -0.1, 0.9, 1.2, 0.4, -0.8, -1.1, 0.1, 4.3, 1.4, -0.3],
        [0.5, 0.7, -0.1, -0.4, -0.0, 0.1, -0.3, -0.1, 1.0, 1.3, 0.4, -0.7, -0.9, 0.1, 4.5, 1.6, -0.1],
        [0.4, 0.0, -0.2, -0.3, -0.0, -0.1, -0.3, 0.0, 0.9, 1.2, 0.3, -0.7, -1.0, 0.0, 4.5, 1.5, -0.2],
    ];

    /// <summary>Speaker roles (engine order): FL FR C BL BR SL SR. 7.1 cable channel of each.</summary>
    public static readonly string[] Papeis = ["FL", "FR", "C", "BL", "BR", "SL", "SR"];
    public static readonly int[] Canal71 = [0, 1, 2, 4, 5, 6, 7];
    /// <summary>Default 7.1 angles (±30, ±150, ±90). Stereo content: L/R at ±40, side part at ±90 and ±135.</summary>
    // 29/10: cinema 7.1 (Dolby/ITU): sides a bit behind the ears (±100), backs a bit wider (±140); was ±90/±150
    // 01/11: "um local só": side and back were the most alike pair (similarity 0.75 → 0.56 at ±90/±145), fronts ±35
    // 01/11 night: too lateral: fronts back to +-30, sides a bit behind the ears, backs stay well behind
    public static readonly double[] Ang71 = [-30, 30, 0, -145, 145, -100, 100];
    public static readonly double[] AngEst = [-40, 40, 0, -135, 135, -90, 90];

    // ---------------------------------------------------------------- HRTF
    static double[] elev = [], az = [];
    static double[][][] hrir = [];               // [dir][ear][tap], diffuse-field equalized
    static double[]? eq71, eqEst;
    static readonly double[]?[] reverbs = new double[]?[3];
    static readonly Lock trava = new();
    public static bool Pronto { get; private set; }

    /// <summary>29/10: generic KEMAR swaps front/back for him → he picks the head whose ears sound right.
    /// Id = resource hrtf_{id}.npz. kemar = MIT dummy (old default); SADIE II: the 5 people with the biggest 30°/150° spectral
    /// difference out of 18 (ferramentas\sadie_para_npz.py: 12.5..8.9 dB, KEMAR 7.0) + d1 = Neumann KU100 dummy.</summary>
    public static readonly string[] Ouvidos = ["kemar", "h20", "h11", "h16", "h6", "h14", "d1"];
    public static string Ouvido { get; private set; } = "";

    /// <summary>Loads the KEMAR set and builds everything shared (~0.1 s). Thread-safe, once.</summary>
    public static void Preparar()
    {
        lock (trava)
        {
            if (Pronto) return;
            Usar("kemar");
            for (int s = 0; s < 3; s++) reverbs[s] = Reverb(Salas[s]);
            Pronto = true;
        }
    }

    /// <summary>Switches the head (HRIR set) used by Caixa/Resposta; unknown id = kemar. Same id = nothing.</summary>
    public static void Usar(string id)
    {
        if (!Ouvidos.Contains(id)) id = "kemar";
        lock (trava)
        {
            if (id == Ouvido) return;
            var (el, a, novo) = Carregar(id);
            // 29/10: the sub feed was timed for KEMAR (Motor "blocks late"); on the SADIE heads it landed out of phase (h11: +0.1 dB
            // at 50 Hz). Swept -600..+450: every SADIE head peaks at +275 samples (stereo +2.0 dB, 7.1 cable +1.1 dB)
            AjusteSub = id == "kemar" ? 0 : 275;
            elev = el; az = a; hrir = novo;
            if (EqDifuso) EqualizarDifuso();
            eq71 = EqCaixas ? EqDasCaixas(Ang71) : [1.0];
            eqEst = EqCaixas ? EqDasCaixas(AngEst) : [1.0];
            Ouvido = id;
        }
    }

    /// <summary>Sub feed timing for the current head, in samples (+ = later), added to Motor's block delay.</summary>
    public static volatile int AjusteSub;

    static (double[] el, double[] az, double[][][] h) Carregar(string id)
    {
        using var st = typeof(Brir).Assembly.GetManifestResourceStream($"hrtf_{id}.npz")!;
        using var zip = new ZipArchive(st, ZipArchiveMode.Read);
        var el = Npy(zip, "elev.npy", out _); var a = Npy(zip, "az.npy", out _);
        var h = Npy(zip, "hrir.npy", out var forma);
        int n = forma[0], taps = forma[1];
        var novo = new double[n][][];
        for (int i = 0; i < n; i++)
        {
            novo[i] = [new double[taps], new double[taps]];
            for (int t = 0; t < taps; t++) { novo[i][0][t] = h[(i * taps + t) * 2]; novo[i][1][t] = h[(i * taps + t) * 2 + 1]; }
        }
        return (el, a, novo);
    }

    [GeneratedRegex(@"'descr':\s*'([<>|])([fi])(\d)'.*'shape':\s*\(([\d,\s]*)\)")]
    private static partial Regex CabecalhoNpy();

    static double[] Npy(ZipArchive z, string nome, out int[] forma)
    {
        using var s = z.GetEntry(nome)!.Open(); using var ms = new MemoryStream(); s.CopyTo(ms);
        var b = ms.ToArray();
        int ver = b[6], hl = ver == 1 ? BitConverter.ToUInt16(b, 8) : BitConverter.ToInt32(b, 8), ini = ver == 1 ? 10 + hl : 12 + hl;
        var cab = System.Text.Encoding.ASCII.GetString(b, ver == 1 ? 10 : 12, hl);
        var m = CabecalhoNpy().Match(cab);
        if (!m.Success) throw new InvalidDataException($"{nome}: {cab}");
        bool real = m.Groups[2].Value == "f";
        forma = [.. m.Groups[4].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse)];
        int tam = int.Parse(m.Groups[3].Value), n = (b.Length - ini) / tam;
        var r = new double[n];
        for (int i = 0; i < n; i++)
            r[i] = (real, tam) switch
            {
                (true, 4) => BitConverter.ToSingle(b, ini + i * 4),
                (true, _) => BitConverter.ToDouble(b, ini + i * 8),
                (false, 4) => BitConverter.ToInt32(b, ini + i * 4),
                _ => BitConverter.ToInt64(b, ini + i * 8),
            };
        return r;
    }

    /// <summary>(ear L, ear R) of the nearest measured direction; az + = right, left = mirror (ears swapped).</summary>
    public static double[][] Resposta(double azGraus, double elGraus = 0)
    {
        double a = ((azGraus + 180.0) % 360.0 + 360.0) % 360.0 - 180.0, lado = Math.Abs(a), el = Math.Clamp(elGraus, -40, 90);
        double ta = lado * Math.PI / 180, te = el * Math.PI / 180, melhor = double.MinValue; int im = 0;
        for (int i = 0; i < az.Length; i++)
        {
            double ra = az[i] * Math.PI / 180, re = elev[i] * Math.PI / 180;
            double cd = Math.Sin(re) * Math.Sin(te) + Math.Cos(re) * Math.Cos(te) * Math.Cos(ra - ta);
            if (cd > melhor) { melhor = cd; im = i; }
        }
        var h = hrir[im];
        return a < 0 ? [(double[])h[1].Clone(), (double[])h[0].Clone()] : [(double[])h[0].Clone(), (double[])h[1].Clone()];
    }

    // ---------------------------------------------------------------- EQ helpers (n = 1024)
    const int NF = 1024, KF = NF / 2 + 1;
    static double Fk(int k) => k * (double)Taxa / NF;
    static int Bin(double f) { int b = 0; double d = double.MaxValue; for (int k = 0; k < KF; k++) if (Math.Abs(Fk(k) - f) < d) { d = Math.Abs(Fk(k) - f); b = k; } return b; }

    static double[] Suavizar(double[] p, double meiaOitava)
    {
        var l = new double[KF];
        for (int k = 0; k < KF; k++)
        {
            double a = Fk(k) / Math.Pow(2, meiaOitava), b = Fk(k) * Math.Pow(2, meiaOitava), s = 0; int c = 0;
            for (int j = 0; j < KF; j++) if (Fk(j) >= a && Fk(j) <= b) { s += p[j]; c++; }
            l[k] = c > 0 ? s / c : p[k];
        }
        return l;
    }

    /// <summary>Minimum-phase FIR (real cepstrum) with the gain asked at rfftfreq(1024); 256 taps, half Hann fade.</summary>
    public static double[] FirFaseMinima(double[] ganhoDb)
    {
        var f = new Fft(NF); double[] re = new double[NF], im = new double[NF];
        for (int k = 0; k < KF; k++) { re[k] = Math.Log(Math.Max(Math.Pow(10, ganhoDb[k] / 20), 1e-6)); im[k] = 0; }
        f.RealInversa(re, im);                                     // cepstrum
        var cep = new double[NF];
        cep[0] = re[0]; cep[NF / 2] = re[NF / 2];
        for (int i = 1; i < NF / 2; i++) cep[i] = 2 * re[i];
        f.Real(cep, re, im);
        for (int k = 0; k < KF; k++) { double m = Math.Exp(re[k]); (re[k], im[k]) = (m * Math.Cos(im[k]), m * Math.Sin(im[k])); }
        f.RealInversa(re, im);
        var r = new double[256];
        for (int i = 0; i < 256; i++) r[i] = re[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * (256 + i) / 511));
        return r;
    }

    static double[] Convolve(double[] a, double[] b)
    {
        var r = new double[a.Length + b.Length - 1];
        for (int i = 0; i < a.Length; i++) { double v = a[i]; if (v == 0) continue; for (int j = 0; j < b.Length; j++) r[i + j] += v * b[j]; }
        return r;
    }

    static double[] Potencia(IEnumerable<double[]> sinais, bool media)
    {
        var f = new Fft(NF); double[] re = new double[NF], im = new double[NF], p = new double[KF]; int n = 0;
        foreach (var s in sinais) { f.Real(s, re, im); for (int k = 0; k < KF; k++) p[k] += re[k] * re[k] + im[k] * im[k]; n++; }
        if (media) for (int k = 0; k < KF; k++) p[k] /= n;
        return p;
    }

    /// <summary>Diffuse-field EQ (what commercial virtualizers do): removes the KEMAR's own coloration, keeps what changes with direction.</summary>
    static void EqualizarDifuso()
    {
        var p = Potencia(hrir.SelectMany(h => h), true);
        var liso = Suavizar(p, 1.0 / 6);
        double rf = liso[Bin(1000)];
        var g = new double[KF];
        for (int k = 0; k < KF; k++) g[k] = Math.Clamp(-10 * Math.Log10(liso[k] / rf) * ForcaDifuso, -6, 6);
        double g60 = g[Bin(60)], g18 = g[Bin(18000)];
        for (int k = 0; k < KF; k++) { if (Fk(k) < 60) g[k] = g60; if (Fk(k) > 18000) g[k] = g18; }
        var fir = FirFaseMinima(g);
        foreach (var h in hrir) for (int e = 0; e < 2; e++) h[e] = Convolve(h[e], fir);
    }

    /// <summary>EQ of the speaker set in use (20/10): flattens the sum of the used directions above ~1.4 kHz, ±6 dB.</summary>
    static double[] EqDasCaixas(double[] angulos)
    {
        var hs = angulos.Select(a => Resposta(a)).ToArray();
        var p = new double[KF];
        foreach (var h in hs) { var pe = Potencia(h, false); for (int k = 0; k < KF; k++) p[k] += pe[k] / hs.Length; }
        var liso = Suavizar(p, 1.0 / 12);
        double rf = liso[Bin(1000)];
        var g = new double[KF];
        for (int k = 0; k < KF; k++)
            g[k] = Math.Clamp(-10 * Math.Log10(liso[k] / rf), -6, 6) * Math.Clamp(Math.Log2(Math.Max(Fk(k), 1.0) / 1414.0), 0, 1);
        double g16 = g[Bin(16000)];
        for (int k = 0; k < KF; k++) if (Fk(k) > 16000) g[k] = g16;
        return FirFaseMinima(g);
    }

    // Blauert directional bands: front = more ~400 Hz/3.6 kHz, back = ~1.1/11 kHz; only on the direct sound
    static readonly (double F, double Q, double Db)[] Pistas = [(400, 1.0, 3.0), (3600, 1.2, 3.0), (1100, 1.4, -2.0), (11000, 1.2, -2.0)];

    static void PistasFrenteTras(double[][] h, double azGraus)
    {
        if (!PistasLigadas) return;
        double a = Math.Abs(((azGraus + 180.0) % 360.0 + 360.0) % 360.0 - 180.0), peso;
        // 29/10 night: cos(2a) left FL/FR (±30) at 1/4 and stereo L/R (±40) near 0, back capped at 2/3 → front/back didn't reach far.
        // Now flat front to 40° (fade by 60°), back full from 140° (starts at 110°).
        if (a <= 60) peso = ForcaFrente * Math.Clamp((60 - a) / 20, 0, 1);
        else if (a >= 110) peso = -Math.Min(1.0, (a - 110) / 30);
        else return;
        if (Math.Abs(peso) < 1e-3) return;
        // 01/11: full front cue = -2 dB at 11 kHz on the fronts → "abafado" → no treble cut on the front (back keeps it)
        // 29/10: front 3.6 kHz lift made close sounds too loud (ear's most sensitive band) → half of it on the front only
        // 01/11 12:49 version = "muito bom" but a bit muffled: front 400 Hz +3 / 1.1 kHz -2 spread into the low mids →
        // same gain, narrower on the front only (Q 1.0→2.0, 1.4→2.5); back cues untouched
        foreach (var e in h) Biquads.Filtrar(Pistas.Select(p => Biquads.Pico(p.F,
            peso > 0 && p.F == 400 ? 2.0 : peso > 0 && p.F == 1100 ? 2.5 : p.Q,
            p.Db * peso * (peso > 0 && p.F == 3600 ? 0.25 : peso > 0 && p.F == 11000 ? 0 : 1))), e);
    }

    /// <summary>The room reverb, one for all speakers: 1st-order image reflections (side by wall), damped (6k), + diffuse (4.5k)
    /// tail decorrelated per ear. [ear][n], energy 1, no HRTF.</summary>
    public static double[] Reverb(Sala s, bool soCauda = false)
    {
        var rng = new Random(11);
        double[] ouv = [s.W * 0.5, s.D * 0.42, 1.2], fonte = [ouv[0], ouv[1] + 2.0, ouv[2]], tam = [s.W, s.D, s.H];
        const double d0 = 2.0;
        int n = (int)(s.Rt60 * Taxa) + 256;
        double[] l = new double[n], r = new double[n];
        var imgs = new List<double[]>();
        for (int eixo = 0; eixo < 3; eixo++)
            for (int lado = 0; lado < 2; lado++)
            {
                var p = (double[])fonte.Clone();
                p[eixo] = lado == 0 ? -fonte[eixo] : 2 * tam[eixo] - fonte[eixo];
                imgs.Add([p[0] - ouv[0], p[1] - ouv[1], p[2] - ouv[2]]);
            }
        static double Norma(double[] v) => Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        double cedo = imgs.Min(v => Norma(v) - d0) / CSom * Taxa, folga = Math.Max(0, s.PreMs / 1000 * Taxa - cedo);
        foreach (var v in imgs)
        {
            double dist = Norma(v); int at = (int)Math.Round((dist - d0) / CSom * Taxa + folga);
            double pan = 0.5 + 0.5 * v[0] / dist, g = s.Refl * d0 / dist;
            l[at] += g * Math.Sqrt(1 - pan); r[at] += g * Math.Sqrt(pan);
        }
        Biquads.Filtrar([Biquads.PassaBaixa(s.PbEco)], l); Biquads.Filtrar([Biquads.PassaBaixa(s.PbEco)], r);
        int ini = (int)(0.02 * Taxa), m = n - ini;
        double[] cl = new double[m], cr = new double[m];
        for (int i = 0; i < m; i++)
        {
            double t = i / (double)Taxa, env = Math.Exp(-6.91 * t / s.Rt60) * Math.Min(1.0, t / 0.015);
            cl[i] = Gauss(rng) * env; cr[i] = Gauss(rng) * env;
        }
        Biquads.Filtrar([Biquads.PassaBaixa(s.PbCauda)], cl); Biquads.Filtrar([Biquads.PassaBaixa(s.PbCauda)], cr);
        double eEco = Energia(l) + Energia(r), eCauda = Energia(cl) + Energia(cr), k = Math.Sqrt(eEco / Math.Max(1e-12, eCauda));
        if (soCauda) { Array.Clear(l); Array.Clear(r); }
        for (int i = 0; i < m; i++) { l[ini + i] += cl[i] * k; r[ini + i] += cr[i] * k; }
        double tot = Math.Sqrt(Energia(l) + Energia(r));
        var o = new double[n * 2];
        for (int i = 0; i < n; i++) { o[i] = l[i] / tot; o[n + i] = r[i] / tot; }
        return o;
    }

    static double Gauss(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());
    public static double Energia(double[] x) { double s = 0; foreach (var v in x) s += v * v; return s; }

    /// <summary>Room reverb [ear][n] (energy 1 over both ears).</summary>
    public static double[][] ReverbDe(int sala)
    {
        var o = reverbs[sala]!; int n = o.Length / 2;
        return [o[..n], o[n..]];
    }

    const int LD = 3 * Convolucao.B;                               // IIR cue tails die well before 32 ms
    /// <summary>Length of the per-speaker response in the engine (64 ms): direct + its own reflections. Same for every room so a switch stays warm.</summary>
    public const int LDCaixa = 6 * Convolucao.B;

    static double[][] Direto(double azGraus, bool modo71)
    {
        var h = Resposta(azGraus);
        var eq = modo71 ? eq71! : eqEst!;
        var d = new double[2][];
        for (int e = 0; e < 2; e++) { var c = Convolve(h[e], eq); d[e] = new double[LD]; Array.Copy(c, d[e], Math.Min(LD, c.Length)); }
        PistasFrenteTras(d, azGraus);
        return d;
    }

    /// <summary>Spectral contrast (01/11): what makes one direction differ from the others at the ear, above ~1 kHz, gets 60% bigger.
    /// A generic head (not his ears) carries weaker cues than his own, and exaggerating them is what keeps speakers apart. Both ears alike. Mean over all
    /// speakers stays, and a correction common to all keeps a centered voice (FL + FR at the ear) sounding as approved.</summary>
    public const double Contraste = 0.6, ContrasteMax = 8;

    static double[] PotSuave(double[] x, double oit)
    {
        var f = new Fft(NF); double[] re = new double[NF], im = new double[NF], p = new double[KF];
        f.Real(x, re, im);
        for (int k = 0; k < KF; k++) p[k] = re[k] * re[k] + im[k] * im[k] + 1e-14;
        return Suavizar(p, oit);
    }

    static double[] Somar(double[] a, double[] b) { var r = new double[a.Length]; for (int i = 0; i < r.Length; i++) r[i] = a[i] + b[i]; return r; }

    static void Contrastar(double[][][] d)
    {
        int n = d.Length;
        var p = new double[n * 2][]; var media = new double[KF];
        for (int c = 0; c < n; c++) for (int e = 0; e < 2; e++)
        {
            var q = PotSuave(d[c][e], 1.0 / 6); p[c * 2 + e] = q;
            for (int k = 0; k < KF; k++) media[k] += 10 * Math.Log10(q[k]) / (n * 2);
        }
        // 01/11 night: per ear this made left/right differences 60% bigger: fronts at 35 degrees measured like 85 (voices from the sides).
        // Same gain on both ears = direction color only; left/right stays what the head gives. Fronts and center untouched: they keep the approved tone
        // (with them in, the fix below lifted all speakers 2 dB).
        for (int c = 3; c < n; c++)
        {
            var g = new double[KF];
            for (int k = 0; k < KF; k++)
                g[k] = Math.Clamp((5 * Math.Log10(p[c * 2][k] * p[c * 2 + 1][k]) - media[k]) * Contraste, -ContrasteMax, ContrasteMax) * Math.Clamp(Math.Log2(Math.Max(Fk(k), 1.0) / 800), 0, 1);
            double g16 = g[Bin(16000)];
            for (int k = 0; k < KF; k++) if (Fk(k) > 16000) g[k] = g16;
            var fir = FirFaseMinima(g);
            Aplicar(d[c][0], fir); Aplicar(d[c][1], fir);
        }
    }

    /// <summary>A centered voice (FL + FR at each ear), per ear.</summary>
    static double[][] Centro(double[][] fl, double[][] fr, double gl, double gr, double oit)
    {
        var o = new double[2][];
        for (int e = 0; e < 2; e++)
        {
            var x = new double[fl[e].Length];
            for (int i = 0; i < x.Length; i++) x[i] = fl[e][i] * gl + fr[e][i] * gr;
            o[e] = PotSuave(x, oit);
        }
        return o;
    }

    static void Aplicar(double[] x, double[] fir) { var c = Convolve(x, fir); Array.Copy(c, x, x.Length); }

    /// <summary>This speaker's own first reflections: 6 walls, each through the HRTF of the direction it really arrives from. Different
    /// timing and directions per speaker = the ear can tell speakers apart. [ear][LDCaixa], energy = direct energy − EcoDb.</summary>
    static double[][] Ecos(double azGraus, Sala s, double energiaDireto)
    {
        double[] ouv = [s.W * 0.5, s.D * 0.42, 1.2], tam = [s.W, s.D, s.H];
        const double d0 = 2.0;
        double ar = azGraus * Math.PI / 180;
        double[] fonte = [ouv[0] + d0 * Math.Sin(ar), ouv[1] + d0 * Math.Cos(ar), ouv[2]];
        var imgs = new List<double[]>();
        for (int eixo = 0; eixo < 3; eixo++)
            for (int lado = 0; lado < 2; lado++)
            {
                var q = (double[])fonte.Clone();
                q[eixo] = lado == 0 ? -fonte[eixo] : 2 * tam[eixo] - fonte[eixo];
                imgs.Add([q[0] - ouv[0], q[1] - ouv[1], q[2] - ouv[2]]);
            }
        static double Norma(double[] v) => Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        double cedo = imgs.Min(v => Norma(v) - d0) / CSom * Taxa, folga = Math.Max(0, s.PreMs / 1000 * Taxa - cedo);
        double[][] o = [new double[LDCaixa], new double[LDCaixa]];
        foreach (var v in imgs)
        {
            double dist = Norma(v), g = s.Refl * d0 / dist;
            int at = (int)Math.Round((dist - d0) / CSom * Taxa + folga);
            var h = Resposta(Math.Atan2(v[0], v[1]) * 180 / Math.PI, Math.Atan2(v[2], Math.Sqrt(v[0] * v[0] + v[1] * v[1])) * 180 / Math.PI);
            for (int e = 0; e < 2; e++) for (int i = 0; i < h[e].Length && at + i < LDCaixa; i++) o[e][at + i] += h[e][i] * g;
        }
        for (int e = 0; e < 2; e++) Biquads.Filtrar([Biquads.PassaBaixa(s.PbEco)], o[e]);
        double k = Math.Sqrt(energiaDireto / Math.Max(1e-12, Energia(o[0]) + Energia(o[1]))) * Math.Pow(10, -s.EcoDb / 20);
        for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa; i++) o[e][i] *= k;
        return o;
    }

    static readonly double[]?[] caudas = new double[]?[3];

    /// <summary>The shared room input: whole room, or only its diffuse tail when each speaker has its own reflections.</summary>
    public static double[][] SalaComum(int sala)
    {
        if (Salas[sala].EcoDb <= 0) return ReverbDe(sala);
        var o = caudas[sala] ??= Reverb(Salas[sala], soCauda: true); int n = o.Length / 2;
        return [o[..n], o[n..]];
    }

    /// <summary>All speakers of a layout as the engine uses them: [speaker][ear][LDCaixa], energy 1 together with the room, + room gain per speaker.</summary>
    public static (double[][][] Resp, double[] Peso) Caixas(double[] angulos, bool modo71, int sala)
    {
        if (Neutro) return (CaixasNeutras(angulos, modo71), new double[angulos.Length]);
        var s = Salas[sala]; int n = angulos.Length;
        var d = new double[n][][]; var peso = new double[n];
        for (int c = 0; c < n; c++) d[c] = Direto(angulos[c], modo71);
        // the approved sound of a centered voice: same fronts, same level as before contrast and own reflections
        double N0(double az) => Caixa(az, modo71, sala).Norma;
        var alvo = Centro(d[0], d[1], 1 / N0(angulos[0]), 1 / N0(angulos[1]), 1.0 / 3);
        Contrastar(d);
        var rev = SalaComum(sala);
        for (int c = 0; c < n; c++)
        {
            double ed = Energia(d[c][0]) + Energia(d[c][1]);
            // shared part = tail only, -20 dB (as in the old room, Drr + 3). Own reflections at -20 dB: total room -17 dB, as approved
            double k = SemEco ? 0 : Math.Sqrt(ed) * Math.Pow(10, -(s.Drr + (s.EcoDb > 0 ? 3 : 0)) / 20);
            double[][] t = [new double[LDCaixa], new double[LDCaixa]];
            for (int e = 0; e < 2; e++) Array.Copy(d[c][e], t[e], LD);
            if (s.EcoDb > 0 && !SemEco) { var ec = Ecos(angulos[c], s, ed); for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa; i++) t[e][i] += ec[e][i]; }
            double et = Energia(t[0]) + Energia(t[1]), cruz = 0;
            for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa && i < rev[e].Length; i++) cruz += t[e][i] * rev[e][i];
            double norma = Math.Sqrt(et + k * k + 2 * k * cruz);
            for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa; i++) t[e][i] /= norma;
            d[c] = t; peso[c] = k / norma;
        }
        var agora = Centro(d[0], d[1], 1, 1, 1.0 / 3);
        var cor = new double[KF];
        for (int k = 0; k < KF; k++) cor[k] = Math.Clamp(5 * Math.Log10(alvo[0][k] / agora[0][k] * alvo[1][k] / agora[1][k]), -6, 6);
        var fc = FirFaseMinima(cor);
        foreach (var h in d) foreach (var x in h) Aplicar(x, fc);
        return (d, peso);
    }

    /// <summary>07/11 SalaReal71: the speaker radiates all around in a furnished living room; every wall bounce (1st and 2nd order, image method)
    /// reaches the ears through the HRTF of the direction it really comes from, with its true delay and 1/distance level; no artificial tail.</summary>
    static void SomarParedes(double[][] d, double azGraus)
    {
        double[] tam = [6.0, 5.5, 2.7], ouv = [3.0, 2.75, 1.2];
        const double d0 = 2.0, refl = 0.7;                      // pressure reflection of a furnished room (~50% energy absorbed per bounce)
        double ar = azGraus * Math.PI / 180;
        double[] fonte = [ouv[0] + d0 * Math.Sin(ar), ouv[1] + d0 * Math.Cos(ar), ouv[2]];
        double[][] o = [new double[LDCaixa], new double[LDCaixa]];
        for (int nx = -2; nx <= 2; nx++) for (int ny = -2; ny <= 2; ny++) for (int nz = -2; nz <= 2; nz++)
        {
            int ordem = Math.Abs(nx) + Math.Abs(ny) + Math.Abs(nz);
            if (ordem == 0 || ordem > 2) continue;
            if (AoArLivre && !(nx == 0 && ny == 0 && nz == -1)) continue;   // outdoors: only the ground bounce
            static double Img(int k, double f, double L) => k % 2 == 0 ? k * L + f : (k + 1) * L - f;
            double[] v = [Img(nx, fonte[0], tam[0]) - ouv[0], Img(ny, fonte[1], tam[1]) - ouv[1], Img(nz, fonte[2], tam[2]) - ouv[2]];
            double dist = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]), g = Math.Pow(AoArLivre ? 0.6 : refl, ordem) * d0 / dist;
            int at = (int)Math.Round((dist - d0) / CSom * Taxa);
            if (at >= LDCaixa) continue;
            var h = Resposta(Math.Atan2(v[0], v[1]) * 180 / Math.PI, Math.Atan2(v[2], Math.Sqrt(v[0] * v[0] + v[1] * v[1])) * 180 / Math.PI);
            for (int e = 0; e < 2; e++) for (int i = 0; i < h[e].Length && at + i < LDCaixa; i++) o[e][at + i] += h[e][i] * g;
        }
        for (int e = 0; e < 2; e++) { Biquads.Filtrar([Biquads.PassaBaixa(AoArLivre ? 2500 : 9000)], o[e]); for (int i = 0; i < LDCaixa; i++) d[e][i] += o[e][i]; }
    }

    /// <summary>Neutral mode: raw HRIRs [speaker][ear][LDCaixa] through one EQ that makes the average over speakers of |HL|²+|HR|² flat at 0 dB.</summary>
    static double[][][] CaixasNeutras(double[] angulos, bool modo71)
    {
        int n = angulos.Length;
        var d = new double[n][][];
        var p = new double[KF];
        for (int c = 0; c < n; c++)
        {
            var h = Resposta(angulos[c]);
            d[c] = new double[2][];
            for (int e = 0; e < 2; e++) { d[c][e] = new double[LDCaixa]; Array.Copy(h[e], d[c][e], Math.Min(LDCaixa, h[e].Length)); }
            if (modo71 && SalaReal71) SomarParedes(d[c], angulos[c]);
            var pe = Potencia(modo71 && SalaReal71 ? d[c] : h, false);
            for (int k = 0; k < KF; k++) p[k] += pe[k] / n;
        }
        var liso = Suavizar(p, 1.0 / 6);                           // 1/3 octave
        var g = new double[KF];
        for (int k = 0; k < KF; k++) g[k] = Math.Clamp(-10 * Math.Log10(Math.Max(liso[k], 1e-12)), -15, 15);
        double g60 = g[Bin(60)], g18 = g[Bin(18000)];
        for (int k = 0; k < KF; k++) { if (Fk(k) < 60) g[k] = g60; if (Fk(k) > 18000) g[k] = g18; }
        if (modo71 && SemEq71)
        {   // raw HRTF, only a flat gain = the EQ's average 500 Hz-2 kHz, so loudness stays close
            double sg = 0; int ng = 0;
            for (int k = 0; k < KF; k++) if (Fk(k) >= 500 && Fk(k) <= 2000) { sg += g[k]; ng++; }
            double ganho = Math.Pow(10, sg / Math.Max(ng, 1) / 20);
            foreach (var h in d) foreach (var x in h) for (int i = 0; i < x.Length; i++) x[i] *= ganho;
        }
        else
        {
            var fir = FirFaseMinima(g);
            foreach (var h in d) foreach (var x in h) Aplicar(x, fir);
        }
        if (modo71)
        {
            int pico = 0; double m = -1;
            for (int i = 0; i < LDCaixa; i++) { double v = Math.Abs(d[2][0][i]) + Math.Abs(d[2][1][i]); if (v > m) { m = v; pico = i; } }
            AtrasoCentro = pico;
            if (HaloTodas)
                for (int c = 0; c < n; c++)
                {
                    if (c == 2) continue;
                    var ha = Halo(PicoDe(d[c]), 100 + c, Energia(d[c][0]) + Energia(d[c][1]), angulos[c]);
                    for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa; i++) d[c][e][i] += ha[e][i];
                }
            if (VozPlana && VozHalo && !BaseEstereo) d[2] = Halo(pico);
        }
        return d;
    }

    /// <summary>07/11 user: the plain voice still felt aimed at him, must be "360" too. The center's HRTF response is replaced by a soft
    /// scattered halo: 16 directions all around (some up, some down), 3 random taps each 2-18 ms after the plain voice, through the HRTF of
    /// each direction, low-passed 7 kHz, energy HaloDb under the plain voice. Many random taps = no comb; arriving after = voice stays at the ears.</summary>
    static double[][] Halo(int pico, int semente = 7, double energia = 1, double azCaixa = 0)
    {
        var rng = new Random(semente);
        double[][] o = [new double[LDCaixa], new double[LDCaixa]];
        for (int j = 0; j < 16; j++)
        {
            double az = j * 22.5 + rng.NextDouble() * 10 - 5, el = (j % 3) switch { 0 => 0, 1 => 25, _ => -25 };
            var h = Resposta(az, el);
            for (int t = 0; t < 3; t++)
            {
                int at = pico + (int)((0.002 + rng.NextDouble() * 0.016) * Taxa) - Math.Max(0, PicoDe(h));
                double g = (rng.NextDouble() < 0.5 ? -1 : 1) * (0.5 + rng.NextDouble()) * Cobertura(az, el, azCaixa);
                if (g == 0) continue;
                for (int e = 0; e < 2; e++) for (int i = 0; i < h[e].Length; i++) { int q = at + i; if (q >= 0 && q < LDCaixa) o[e][q] += h[e][i] * g; }
            }
        }
        for (int e = 0; e < 2; e++) Biquads.Filtrar([Biquads.PassaBaixa(7000)], o[e]);
        double k = Math.Sqrt(energia * Math.Pow(10, -HaloDb / 10) / Math.Max(1e-12, Energia(o[0]) + Energia(o[1])));
        for (int e = 0; e < 2; e++) for (int i = 0; i < LDCaixa; i++) o[e][i] *= k;
        return o;
    }

    /// <summary>07/11 user: speakers spread 240°, facing him. A halo tap from (az, el) = a reflector ~4 m away in that direction; the speaker
    /// (2 m away at azCaixa, aimed at the head) only feeds it if the reflector is within Espalhamento/2 of its axis (soft ±10° edge).</summary>
    static double Cobertura(double az, double el, double azCaixa)
    {
        if (Espalhamento >= 360) return 1;
        double a = az * Math.PI / 180, e = el * Math.PI / 180, c = azCaixa * Math.PI / 180;
        double[] p = [4 * Math.Cos(e) * Math.Sin(a), 4 * Math.Cos(e) * Math.Cos(a), 4 * Math.Sin(e)], s = [2 * Math.Sin(c), 2 * Math.Cos(c), 0];
        double[] v = [p[0] - s[0], p[1] - s[1], p[2]];
        double cos = -(v[0] * s[0] + v[1] * s[1]) / (Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) * 2);   // axis = speaker -> head = -s
        double fora = Math.Acos(Math.Clamp(cos, -1, 1)) * 180 / Math.PI, meio = Espalhamento / 2;
        return Math.Clamp((meio + 10 - fora) / 20, 0, 1);
    }

    static int PicoDe(double[][] h)
    {
        int p = 0; double m = -1;
        for (int i = 0; i < h[0].Length; i++) { double v = Math.Abs(h[0][i]) + Math.Abs(h[1][i]); if (v > m) { m = v; p = i; } }
        return p;
    }

    /// <summary>Direct part of one speaker (HRIR + set EQ + cues), [ear][LD], and its room gain k (reverb = k·rev):
    /// full response = (direct + k·rev) / norma, energy 1. Without contrast or own reflections (Caixas has them).</summary>
    public static (double[][] Direto, double K, double Norma) Caixa(double azGraus, bool modo71, int sala)
    {
        var d = Direto(azGraus, modo71);
        var rev = ReverbDe(sala);
        double ed = Energia(d[0]) + Energia(d[1]), k = SemEco ? 0 : Math.Sqrt(ed) * Math.Pow(10, -Salas[sala].Drr / 20), cruz = 0;
        for (int e = 0; e < 2; e++) for (int i = 0; i < LD && i < rev[e].Length; i++) cruz += d[e][i] * rev[e][i];
        return (d, k, Math.Sqrt(ed + k * k + 2 * k * cruz));
    }

    /// <summary>Timbre correction + type voice as peak sections (energy-neutral in pink noise).</summary>
    public static double[][] Correcao(bool modo71, int tipo)
    {
        if (Neutro) return [];
        double[][] presenca = [.. Cores.Where(c => c.Db != 0).Select(c => Biquads.Pico(c.Hz, c.Q, c.Db))];
        if (!CorrecaoLigada) return presenca;
        var db = (double[])(modo71 ? Corr71 : CorrEst)[tipo].Clone();
        double m = db.Average(v => Math.Pow(10, v / 10)), off = 10 * Math.Log10(m);
        for (int i = 0; i < db.Length; i++) db[i] -= off;
        foreach (var (hz, g) in Tipos[tipo].Voz)
        {
            int b = 0; for (int i = 1; i < FreqsCorr.Length; i++) if (Math.Abs(FreqsCorr[i] - hz) < Math.Abs(FreqsCorr[b] - hz)) b = i;
            db[b] += g;
        }
        return [.. presenca, .. FreqsCorr.Select((f, i) => (f, g: db[i])).Where(x => Math.Abs(x.g) > 0.05).Select(x => Biquads.Pico(x.f, QCorr, x.g))];
    }
}
