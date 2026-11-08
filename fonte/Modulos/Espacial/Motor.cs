// Som 7.1 engine (port of espacial.py MotorEspacial): stereo or 7.1 in -> binaural stereo out, fixed delay of one
// block (512) plus the upmix hop for stereo. Settings are built off the audio thread (Ajustes) and picked up per block;
// a new set fades in over 200 ms (no click), switching on/off ramps over 50 ms.
namespace FAH.Espacial;

/// <summary>One finished filter set (immutable): what a speaker layout + room + type sounds like.</summary>
public sealed class Ajustes
{
    public required bool Modo71 { get; init; }
    public required int Tipo { get; init; }
    public required int Sala { get; init; }
    public required Convolucao Direto { get; init; }       // 7 inputs, responses already /norma
    public required Convolucao Rev { get; init; }          // 1 input (weighted mono sum), the room
    public required double[] Peso { get; init; }           // k/norma per speaker into the room
    public required double[][] Corr { get; init; }
    /// <summary>08/11 user: in Filme/Jogo the 7.1 fronts are 3D too (a far enemy in front must not sit in the head): FL/FR/C go whole
    /// through their HRTF speakers with Fonte3D.Externalizar, nothing plain. Música keeps the approved plain front (voice like stereo).</summary>
    public bool Externo { get; init; }

    static readonly Convolucao?[] revCache = new Convolucao?[3];

    /// <summary>angulos = 7.1 layout (FL FR C BL BR SL SR); stereo content always uses the upmix layout.</summary>
    public static Ajustes Construir(bool modo71, int tipo, int sala, double[] angulos)
    {
        // 08/11 one engine: the approved 7.1 (Neutro + BaseEstereo) gets its speakers from Cena, as the Estéreo+ does
        bool baseE = modo71 && Brir.Neutro && Brir.BaseEstereo, externo = baseE && tipo != 0;
        var (resp, peso) = baseE
            ? (Cena.Respostas(Cena.Angulos(Frentes.Par, angulos[..3], angulos[3..]), null).Resp, new double[angulos.Length])
            : Brir.Caixas(modo71 ? angulos : Brir.AngEst, modo71, sala);
        if (externo) for (int c = 0; c < 3; c++)
        {   // +1.46 dB = same loudness as the Música front (measured, noise on FL/FR/C)
            Fonte3D.Externalizar(resp[c], angulos[c], 0);
            foreach (var o in resp[c]) for (int i = 0; i < o.Length; i++) o[i] *= 1.183;
        }
        var rev = revCache[sala] ??= new Convolucao([Brir.SalaComum(sala)]);
        return new() { Modo71 = modo71, Tipo = tipo, Sala = sala, Direto = new Convolucao(resp), Rev = rev, Peso = peso, Corr = Brir.Correcao(modo71, tipo), Externo = externo };
    }
}

public sealed class Motor
{
    const int B = Convolucao.B, Cruzar = 9600, Rampa = 2400;

    sealed class Vivo(Ajustes a)
    {
        public readonly Ajustes A = a;
        public readonly Convolucao Dir = a.Direto.Clone(), Rev = a.Rev.Clone();
        public readonly Biquads Corr = new(a.Corr, 2);
        public int Aquecer, Feito;
    }

    // settings (written by the module thread, read per block)
    public volatile Ajustes? PedidoEst, Pedido71;
    public volatile bool Ligado;
    public readonly double[] Ganho = [1, 1, 1, 1, 1, 1, 1];  // per speaker role (volume/mute)
    public volatile bool Sub = true, Voz = true;
    public double GanhoLfe = 1;
    /// <summary>08/11 user: the sub must play everything too, leaning on the lows. SubAgudo = share of the sub feed above its 120 Hz low-pass
    /// (0 = lows only, as before); SubEstereo = a stereo program on the 7.1 cable has no LFE, so the sub gets the fronts' mid while filling
    /// (0 = silent sub, as before). Dev screen keys mSubAgudo / mSubEstereo.</summary>
    public static volatile float SubAgudo = 0.25f, SubEstereo = 0.7f;
    readonly double[] lfeCru = new double[B], meioFrente = new double[B];

    /// <summary>Sub low-pass + its full-range share (SubAgudo).</summary>
    void FiltrarSub()
    {
        Array.Copy(lfe, lfeCru, B);
        lfeLp.Processar(lfe, 0, B);
        double a = SubAgudo; if (a > 0) for (int i = 0; i < B; i++) lfe[i] += a * (lfeCru[i] - lfe[i]);
    }
    /// <summary>08/11 "Intensidade" slider (0..1, Intensidade.cs) of the fronts' side part; read every block.</summary>
    public double Forca = Intensidade.Padrao;
    // "Far sounds don't vanish" (29/10, Jogo/Filme only): linked upward leveler on all speakers before the HRTF, so positions
    // don't move. Below -30 dBFS it lifts half the gap (max +9 dB); silence/hiss under -65 untouched. Loud = drops at once
    // (64-sample ramp), quiet = rises slowly (0.4 s), no pumping. Asked instead of more echo.
    const double NivLimiar = -30, NivMax = 9, NivPiso = -65, NivSobe = 0.4;
    double nivG = 1;

    /// <summary>lfe (on time) * g -> lfe d samples later (d = blocks the HRTF/room path takes + the head's own offset).</summary>
    void AtrasarSub(double g, int d)
    {
        int n = linhaSub.Length; d = Math.Clamp(d, 0, n - B);
        for (int i = 0; i < B; i++) { linhaSub[posSub] = lfe[i] * g; lfe[i] = linhaSub[(posSub - d + n) % n]; posSub = (posSub + 1) % n; }
    }

    void Nivelar(bool ligado)
    {
        double alvo = 1;
        if (ligado)
        {
            double p = 0; for (int c = 0; c < 7; c++) { var xc = x[c]; for (int i = 0; i < B; i++) p += xc[i] * xc[i]; }
            double db = 10 * Math.Log10(p / B + 1e-20), boost = Math.Min(NivMax, Math.Max(0, (NivLimiar - db) * 0.5));
            boost *= Math.Clamp((db - NivPiso) / 10, 0, 1);
            alvo = Math.Pow(10, boost / 20);
        }
        double g0 = nivG, g1 = alvo < g0 ? alvo : g0 + (alvo - g0) * (1 - Math.Exp(-B / (NivSobe * Brir.Taxa)));
        int rampa = alvo < g0 ? 64 : B;
        if (g0 == 1 && g1 == 1) return;
        for (int c = 0; c < 7; c++) { var xc = x[c]; for (int i = 0; i < B; i++) xc[i] *= i < rampa ? g0 + (g1 - g0) * (i + 1) / rampa : g1; }
        nivG = g1;
    }
    volatile int testeCaixa = -1, testeResta;

    public int Trocas;                                         // sets crossfaded (tests)
    public bool Modo71Ativo => modo71 == true;

    // block state
    bool? modo71;
    readonly List<Vivo> vivos = [];
    Upmix upmix = new();
    Preencher preencher = new();
    readonly Frente frente = new(Frentes.Par, 4);               // 08/11 shared front stage (Cena.cs), BaseEstereo path
    Biquads? dialogo, dialogoSala;
    readonly double[] cenSala = new double[B], flSala = new double[B], frSala = new double[B];
    readonly double[][] xSala = new double[7][];
    readonly Biquads lfeLp = new([Biquads.PassaBaixa(120), Biquads.PassaBaixa(120)], 1);
    // "Reforçar voz" = voice enhancer (28/10), same for every type, only on the center / mid: lifts a voice buried in the
    // mix (male or female) without pushing it in front of the instruments. Two wide gentle curves, always on (28/10):
    // warmth ~200 Hz (body of male and female voices) and presence ~2.2 kHz (strength + words). Narrow peaks at 220 Hz
    // and 1.2 kHz sounded boxy/nasal, a level ride flattened the singing, a room cut sounded detached: all dropped.
    // 43 (+1.0 @200 / +1.2 @2.2k, Q .6) = "giant stage pointed at me": less body, narrower presence, lower
    // 44 = "two giant plates left and right aiming it at me": the boost also fed the room (its side-wall reflections).
    // Now the room gets the center through the exact inverse (RBJ peak -dB), so only the direct voice is lifted.
    static readonly (double Hz, double Q, double Db)[] RealceP = [(200, 0.7, 0.6), (2000, 0.8, 1.0)];
    static readonly double[][] Realce = [.. RealceP.Select(p => Biquads.Pico(p.Hz, p.Q, p.Db))], RealceInv = [.. RealceP.Select(p => Biquads.Pico(p.Hz, p.Q, -p.Db))];
    readonly double[] extra = new double[B];
    readonly double[][] x = [.. Enumerable.Range(0, 7).Select(_ => new double[B])];
    readonly double[] linhaVoz = new double[4 * B], vozP = new double[B]; int posVoz;   // 07/11 plain center (Brir.VozPlana) / plain L (BaseEstereo, via Frente)
    readonly double[] vozR = new double[B];                                            // plain R (BaseEstereo, via Frente)
    readonly double[][] xExt = new double[7][]; readonly double[] extL = new double[B], extR = new double[B], extC = new double[B], zero = new double[B];   // Externo feeds
    readonly double[] linhaSub = new double[4 * B]; int posSub;   // sub feed delay line (29/10: per head, Brir.AjusteSub)
    readonly double[] mono = new double[B], lfe = new double[B], dl = new double[B], dr = new double[B], rl = new double[B], rr = new double[B];
    readonly double[][] monoIn;
    readonly double[] yv = new double[B * 2], y0 = new double[B * 2], el = new double[B], er = new double[B];
    readonly Random rnd = new(7);

    // frame queues (adapter between any host block size and 512)
    double[] ent = new double[B * 8]; int cheio, largura = 2;
    readonly double[] sai = new double[(B * 4) * 2]; int nSai, lido;     // frames in sai / already read
    double mix;                                                 // 0 = dry, 1 = binaural

    public Motor() { monoIn = [mono]; Reiniciar(2); }

    public bool Ativo => Ligado || mix > 0;

    /// <summary>Plays a short noise burst through one virtual speaker (0..6), over whatever is playing.</summary>
    public void Testar(int caixa) { testeResta = (int)(0.7 * Brir.Taxa); testeCaixa = caixa; }

    void Reiniciar(int ch)
    {
        largura = ch; cheio = 0; nSai = B; lido = 0; Array.Clear(sai);
        vivos.Clear(); modo71 = null; upmix = new(); dialogo = null; dialogoSala = null;
    }

    /// <summary>In place: lr (stereo) or src (8 ch) -> lr. Dry path follows the ramp.</summary>
    public void Processar(ReadOnlySpan<float> src, int ch, Span<float> lr, int q)
    {
        bool alvo = Ligado && (ch == 8 ? Pedido71 : PedidoEst) != null;
        if (!alvo && mix <= 0)
        {
            if (ch == 8) Descer(src, lr, q);
            if (vivos.Count > 0 || lido > 0 || cheio > 0) Reiniciar(largura);
            return;
        }
        if (ch != largura) Reiniciar(ch);
        double passo = 1.0 / Rampa;
        for (int i = 0; i < q; i++)
        {
            for (int c = 0; c < ch; c++) ent[cheio * ch + c] = src[i * ch + c];
            if (++cheio == B) { Bloco(ch); cheio = 0; }
            double wl = sai[lido * 2], wr = sai[lido * 2 + 1]; lido++;
            float dLft, dRgt;
            if (ch == 8) { int b = i * 8; float c = 0.707f * src[b + 2]; dLft = src[b] + c + 0.5f * (src[b + 4] + src[b + 6]); dRgt = src[b + 1] + c + 0.5f * (src[b + 5] + src[b + 7]); }
            else { dLft = src[i * 2]; dRgt = src[i * 2 + 1]; }
            mix = Math.Clamp(mix + (alvo ? passo : -passo), 0, 1);
            lr[i * 2] = (float)(dLft * (1 - mix) + wl * mix);
            lr[i * 2 + 1] = (float)(dRgt * (1 - mix) + wr * mix);
        }
    }

    static void Descer(ReadOnlySpan<float> s, Span<float> lr, int q)
    {
        for (int i = 0; i < q; i++)
        {
            int b = i * 8; float c = 0.707f * s[b + 2];
            lr[i * 2] = s[b] + c + 0.5f * (s[b + 4] + s[b + 6]); lr[i * 2 + 1] = s[b + 1] + c + 0.5f * (s[b + 5] + s[b + 7]);
        }
    }

    Vivo NovoVivo(Ajustes a, Vivo? de)
    {
        var v = new Vivo(a);
        if (de != null)
        {
            if (de.A.Externo == a.Externo) v.Dir.CopiarEstado(de.Dir);   // inputs are the same: the direct part is warm at once
            else v.Aquecer = v.Dir.P * B;                       // Música <-> Filme/Jogo: other front feeds, warm up first
            if (de.Rev.P == v.Rev.P) v.Rev.CopiarEstado(de.Rev);
            else v.Aquecer = Math.Max(v.Aquecer, v.Rev.P * B);
        }
        return v;
    }

    /// <summary>extra = the enhancer's boost for center/mid c.</summary>
    void Realcar(double[] c)
    {
        Array.Copy(c, extra, B); dialogo!.Processar(extra, 0, B);
        for (int i = 0; i < B; i++) extra[i] -= c[i];
    }

    void Bloco(int ch)
    {
        if (lido > 0) { Array.Copy(sai, lido * 2, sai, 0, (nSai - lido) * 2); nSai -= lido; lido = 0; }
        bool m71 = ch == 8;
        var ped = m71 ? Pedido71 : PedidoEst;
        if (ped == null) { Array.Clear(sai, nSai * 2, B * 2); nSai += B; return; }
        if (modo71 != m71) { modo71 = m71; upmix = new(); preencher = new(); Array.Clear(linhaSub); Array.Clear(linhaVoz); frente.Limpar(); vivos.Clear(); vivos.Add(NovoVivo(ped, null)); }
        else if (vivos.Count == 1 && vivos[0].A != ped) vivos.Add(NovoVivo(ped, vivos[0]));

        bool voz = Voz && !Brir.Neutro;
        if (voz != (dialogo != null)) { dialogo = voz ? new Biquads(Realce, 1) : null; dialogoSala = voz ? new Biquads(RealceInv, 1) : null; }

        if (m71)
        {
            for (int c = 0; c < 7; c++) { var xc = x[c]; int k = Brir.Canal71[c]; for (int i = 0; i < B; i++) xc[i] = ent[i * 8 + k]; }
            for (int i = 0; i < B; i++) lfe[i] = ent[i * 8 + 3];
            preencher.Plano = Brir.Neutro && (Brir.VozPlana || Brir.BaseEstereo);
            for (int i = 0; i < B; i++) meioFrente[i] = 0.5 * (x[0][i] + x[1][i]);
            preencher.Bloco(x, lfe);                            // 01/11: before the voice boost: its center is the voice now
            double ge = preencher.Ganho * SubEstereo;          // 08/11: filled stereo -> the sub gets the fronts' mid (no LFE there)
            if (ge > 0) for (int i = 0; i < B; i++) lfe[i] += ge * meioFrente[i];
            if (dialogo != null) { Realcar(x[2]); for (int i = 0; i < B; i++) x[2][i] += extra[i]; }
            FiltrarSub();
            double gl = Sub ? 0.25 * GanhoLfe : 0;
            // one block late = in step with the convolution (28/10: on time it was -2.5 dB at 50 Hz, cancelling the fronts)
            AtrasarSub(gl, B + Brir.AjusteSub);
        }
        else
        {
            for (int i = 0; i < B; i++) { el[i] = ent[i * 2]; er[i] = ent[i * 2 + 1]; }
            if (dialogo != null)
            {
                for (int i = 0; i < B; i++) mono[i] = 0.5 * (el[i] + er[i]);
                Realcar(mono);
                for (int i = 0; i < B; i++) { el[i] += extra[i]; er[i] += extra[i]; }
            }
            upmix.Bloco(el, er, x);
            // "Graves no subwoofer" on stereo (28/10): no LFE channel, so the low end of the mid feeds both ears straight
            // (no HRTF/room), two blocks late = in step with the upmix + convolution (measured: 1 block = -2 dB at 50 Hz)
            double gs = Sub && !Brir.Neutro ? 0.25 * GanhoLfe : 0;
            for (int i = 0; i < B; i++) lfe[i] = 0.5 * (el[i] + er[i]);
            FiltrarSub();
            AtrasarSub(gs, 2 * B + Brir.AjusteSub);
        }
        for (int c = 0; c < 7; c++) { double g = Ganho[c]; if (g != 1) { var xc = x[c]; for (int i = 0; i < B; i++) xc[i] *= g; } }
        Nivelar(vivos[^1].A.Tipo != 0);
        int tc = testeCaixa;
        if (tc >= 0 && testeResta > 0)
        {
            int total = (int)(0.7 * Brir.Taxa);
            for (int i = 0; i < B && testeResta > 0; i++, testeResta--)
            {
                int t = total - testeResta; double env = Math.Min(1.0, Math.Min(t, testeResta) / 960.0);
                x[tc][i] += 0.1 * env * (rnd.NextDouble() * 2 - 1);
            }
            if (testeResta <= 0) testeCaixa = -1;
        }

        // 07/11 voice like stereo: the center role skips the head, 0.707 to each ear, as late as the HRTF center peak
        // 07/11 new 7.1 (Brir.BaseEstereo) = Estéreo+ v3 on the fronts: FL/FR plain to their ear, only their side part feeds the ±30 HRTF pair,
        // the center is a real HRTF speaker; sides/backs untouched (HRTF)
        bool baseE = m71 && Brir.Neutro && Brir.BaseEstereo;
        bool plana = m71 && Brir.Neutro && Brir.VozPlana && !baseE;
        {
            int n = linhaVoz.Length, d = Math.Clamp(Brir.AtrasoCentro, 0, n - B);   // measured: the 7.1 convolution adds no block
            if (baseE)
            {   // 08/11 slider; 0.5 = plain 1, CaixaBase 0.5 as approved. In place: x = the 7 speaker feeds after
                if (vivos.Exists(v => v.A.Externo))
                {   // Filme/Jogo: whole FL/FR + gc·C to their (externalized) speakers; the plain stage below keeps running for a crossfade
                    Array.Copy(x[0], extL, B); Array.Copy(x[1], extR, B);
                    double gc = Brir.GanhoCentroBase; for (int i = 0; i < B; i++) extC[i] = gc * x[2][i];
                    xExt[0] = extL; xExt[1] = extR; xExt[2] = extC; for (int c = 3; c < 7; c++) xExt[c] = x[c];
                }
                var (la, cx) = Intensidade.Mapa(Forca);
                frente.Bloco(x, la, cx, Brir.GanhoCentroBase, null, vozP, vozR, x, Brir.AtrasoCentro);
            }
            else
            {
                for (int i = 0; i < B; i++)
                {
                    linhaVoz[posVoz] = plana ? 0.7071 * x[2][i] : 0;
                    vozP[i] = vozR[i] = linhaVoz[(posVoz - d + n) % n]; posVoz = (posVoz + 1) % n;
                }
                if (plana && !Brir.VozHalo) Array.Clear(x[2]);   // with the halo the center's engine response IS the halo (Brir.Halo)
            }
        }

        // room feed without the voice boost (the boost stays on the direct sound only): 7.1 = the center; stereo = the
        // mid of the fronts (the upmix leaves the center empty, the boost rides on L and R alike)
        for (int c = 0; c < 7; c++) xSala[c] = x[c];
        if (dialogoSala != null)
        {
            if (m71) { Array.Copy(x[2], cenSala, B); dialogoSala.Processar(cenSala, 0, B); xSala[2] = cenSala; }
            else
            {
                for (int i = 0; i < B; i++) cenSala[i] = 0.5 * (x[0][i] + x[1][i]);
                dialogoSala.Processar(cenSala, 0, B);
                for (int i = 0; i < B; i++) { double ld = 0.5 * (x[0][i] - x[1][i]); flSala[i] = cenSala[i] + ld; frSala[i] = cenSala[i] - ld; }
                xSala[0] = flSala; xSala[1] = frSala;
            }
        }

        for (int vi = 0; vi < vivos.Count; vi++)
        {
            var v = vivos[vi];
            bool ext = v.A.Externo && baseE && xExt[0] != null;
            v.Dir.Bloco(ext ? xExt : x, dl, dr);
            var pL = ext ? zero : vozP; var pR = ext ? zero : vozR;
            var p = v.A.Peso;
            for (int i = 0; i < B; i++) { double s = 0; for (int c = 0; c < 7; c++) s += p[c] * xSala[c][i]; mono[i] = s; }
            v.Rev.Bloco(monoIn, rl, rr);
            var y = vi == 0 ? y0 : yv;
            for (int i = 0; i < B; i++) { y[i * 2] = dl[i] + rl[i] + lfe[i] + pL[i]; y[i * 2 + 1] = dr[i] + rr[i] + lfe[i] + pR[i]; }
            v.Corr.Processar(y, 0, B); v.Corr.Processar(y, 1, B);
        }
        if (vivos.Count == 2)
        {
            var novo = vivos[1];
            if (novo.Aquecer > 0) novo.Aquecer -= B;
            else
            {
                for (int i = 0; i < B; i++)
                {
                    double t = Math.Clamp((novo.Feito + i + 1) / (double)Cruzar, 0, 1);
                    y0[i * 2] = y0[i * 2] * (1 - t) + yv[i * 2] * t; y0[i * 2 + 1] = y0[i * 2 + 1] * (1 - t) + yv[i * 2 + 1] * t;
                }
                novo.Feito += B;
                if (novo.Feito >= Cruzar) { vivos.RemoveAt(0); Trocas++; }
            }
        }
        Array.Copy(y0, 0, sai, nSai * 2, B * 2); nSai += B;
    }
}
