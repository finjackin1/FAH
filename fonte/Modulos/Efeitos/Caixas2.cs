// Estéreo+ v2 (07/11/2026) user: "salve o Estéreo+ atual como v1 e aplique o sistema do 7.1 nas duas caixas do estéreo com as correções".
// = the 7.1 "polimento 4" (PERFEITO) + Espalhamento 360 on two speakers, built by the same code (Brir/Conv/Upmix linked read-only from Espacial):
//   Palco re-pan (Abre per profile, as the 7.1 does on its fronts)
//   -> what L and R share (voice) is pulled out per band (Preencher, as for a stereo program on the 7.1 cable): it goes PLAIN to both ears
//      (exactly what headphones give it) + the scattered halo (Brir.Halo, -10 dB, taps outside the 240° cone muted)
//   -> the rest of L / R = virtual speakers at -30 / +30 (raw HRTF h11, flat gain, no EQ, no room, grass ground bounce).
// Below 250 Hz nothing is pulled out (stays on the speakers). Latency: Palco 1024 + block 512. v1 = EstereoMais.V2 = false.
using FAH.Espacial;

namespace FAH.Efeitos;

public sealed class Caixas2
{
    /// <summary>07/11 user: the plain voice sat loose inside the head, apart from the speakers. false = nothing pulled out: L/R go whole to the
    /// ±30 speakers, voice = phantom center in front with the instruments. true = the plain-voice path above.</summary>
    public static bool TirarVoz = false;
    /// <summary>07/11 user: "a voz do Estéreo+ tem que ficar O MAIS PARECIDA POSSÍVEL com a voz dele desativado". true (wins over TirarVoz) =
    /// mid/side: M = (L+R)/2 plain to the ears exactly as with Estéreo+ off (only delayed to meet the speakers), no Palco, no halo, so a centered
    /// voice is untouched. Only S = (L-R)/2 is reshaped: Lado·S stays plain (keeps hard pans in the bass, where speakers can't tell sides) and
    /// Caixa·S goes to the virtual speakers (+S left, -S right). Lado = 1, Caixa = 0 would be Estéreo+ off.</summary>
    public static bool VozIgual = true;
    public static double Lado = 1.0, Caixa = 0.5;   // 07/11 user: "o efeito de estéreo some e o áudio fica focado no centro" (0.5/0.7: hard pan only 5 dB apart) -> full plain stereo + speakers on top (11 dB apart)
    /// <summary>08/11 user slider (0..1, see Espacial\Intensidade.cs): sets Lado/Caixa of v5 live. 0.5 = v5 as approved.</summary>
    public static double Forca = Intensidade.Padrao;
    /// <summary>08/11 user: E+ "som igual on/off" (centered = identical by design; Jogo/Música/Filme were the same sound) -> option A:
    /// more of the side S to the virtual speakers, a different amount per mode. Multiplies the speaker gain of Intensidade.Mapa (v5 only;
    /// the 7.1 keeps its own). Plain side gain stays from Mapa (lowering it pulls hard pans to the middle). 1 = 1.2.3 sound.</summary>
    public static readonly Dictionary<string, double> CaixaModo = new() { ["jogo"] = 1.3, ["musica"] = 1.6, ["filme"] = 2.0 };
    readonly double kModo;
    const int B = Convolucao.B;
    static readonly Lock trava = new();
    /// <summary>07/11 user: "faça [como o Dolby: voz numa caixa virtual central à frente] e vamos ajustando, salve essa config atual como v2".
    /// 2 = VozIgual as approved (M plain = voice identical to off). 3 = M goes through a virtual center speaker at 0° (raw HRTF h11 + ground
    /// bounce, like the ±30 pair) instead of plain, GanhoVoz sets its level; the plain Lado·S and the Caixa·S speakers stay as in 2. Only the
    /// part L and R share (Preencher, per band above 250 Hz) goes there: sending all of M collapsed hard pans (only 2 dB apart).</summary>
    // 07/11 23h user on v3: "ficou bom demais na moral, SALVE ESSA" -> v3 = APPROVED Estéreo+ (do not change unless the user asks).
    // 07/11 23h user: E+ v3 "parece 128 kbps" (freqs cut, voice muffled) next to the new 7.1 ("320 kbps"). Measured: same static spectrum,
    // E+ 0.7 dB lower; what differs is the engine: the 7.1 also moves each side's own part to the side/back speakers (Preencher) and its
    // flat neutral gain comes from all 7 speakers. 4 = the new 7.1's exact engine (Motor, Brir.BaseEstereo) fed with L/R on FL/FR. 3 = v3.
    // 08/11 user on v5 + VozCentroPlana: "agora ficou bom, salve essa versão" -> v5 (plain voice, front fan) = APPROVED Estéreo+ (backup temp fah_backup_eplus_v5_0811). v3 stays available as Versao 3.
    public static int Versao = 5;   // 07/11 23h20 user on v4: "meu deus, volte" -> back to approved v3
    // 5 (07/11 user "Frente larga"): v3, but the Caixa·S speaker feed is split per STFT bin by its pan ψ: |ψ|·S goes ONLY to its own side's
    // speaker on a front fan (±15..60, further out the harder the pan), (1-|ψ|)·S stays v3 style on ±30. Fixes v3 playing each side sound
    // inverted from the opposite speaker too (sounds stacked in 3 spots, masking). +512 latency (STFT N1024 hop 512, sqrt-Hann).
    // center HRTF at the ears = plain level (mono voice -1.3 dB at 1.0 with all of M, -0.6 dB at 1.16 shared part only). 07/11 23h user: voice
    // "mais abafada" / ~0.5-1 dB under the new 7.1's -> 1.45 (= the 7.1's voice within 0.1 dB) was "tenebroso, acabou com o grave, voz estranha": back to 1.24.
    public static double GanhoVoz = 1.24;
    /// <summary>08/11 user on v5: "a voz ainda tá na frente e não no centro igual é com E+ desativado ou no 7.1". true = v5 sends no voice
    /// through the 0° HRTF speaker: all of M stays plain at the ears (= E+ off, = the 7.1's VozPlana), only S goes to the fan.</summary>
    public static bool VozCentroPlana = true;
    /// <summary>07/11 23h25 user: v3 "parece 128 kbps, o 7.1 parece 320": measured (centered noise) v3 sits ~0.7 dB under the 7.1 in EVERY band
    /// from 200 Hz to 14 kHz, same balance (1.45 on the voice alone left the bass behind = "acabou com o grave"). So the whole v3 output goes
    /// up evenly. EstereoMais' CompensaVolume (Fracao 0.5) takes back half the dB, so 1.4 dB here = +0.7 dB at the ears. 0 = v3 as approved.</summary>
    public static double GanhoSaidaDb = 0;
    /// <summary>07/11 23h50 user: E+ still "96 kbps, freqs atropelando", only with E+ on, source YouTube Music (lossy). Lossy codecs starve the
    /// side (L-R) highs; plain, that noise sits where the voice is and stays masked; sent to the ±30 speakers it moves away and unmasks.
    /// So the Caixa·S feed is low-passed here (2 sections): side highs stay only plain = exactly as E+ off. 0 = v3 as before.</summary>
    public static double CorteCaixaHz = 0;
    /// <summary>The low-pass phase lines the speaker S up with the plain S around 1-2 kHz (+2.6 dB bump on the sides): this dip takes it back.</summary>
    public static double PicoCaixaHz = 1600, PicoCaixaQ = 0.7, PicoCaixaDb = -8;
    static Convolucao? modelo, modeloCentro, modelo71, modeloLeque;
    // fan order: [-30, 30, 0] = v3's own (center index 2 = AtrasoCentro), then -60 -45 -15 15 45 60 (Cena.AngLeque). Negative = left (+S).
    static readonly double[] Trio = [-30, 30, 0];

    /// <summary>v5 fan, same raw HRTF + flat gain as v3: the gain (averaged over the speakers by Brir) is put back to v3's exactly, so the
    /// ±30 pair and the center are v3's own responses.</summary>
    static Convolucao ModeloLeque()
    {   // 08/11: built by the shared engine (Espacial\Cena.cs), as the 7.1's speakers
        lock (trava)
        {
            if (modeloLeque == null)
            {
                Brir.Usar("h11");
                var (resp, at) = Cena.Respostas(Cena.Angulos(Frentes.Leque, Trio, []), Trio);
                atrasoCentro = at; modeloLeque = new Convolucao(resp);
            }
            return modeloLeque;
        }
    }
    static int atrasoVoz, atrasoCentro, atraso71;

    /// <summary>08/11 Dev screen: Brir knobs changed -> drop the built speaker models (next core builds them again).</summary>
    public static void Reiniciar() { lock (trava) modelo = modeloCentro = modelo71 = modeloLeque = null; }

    /// <summary>v4: the 7.1 layout (FL FR C BL BR SL SR at Brir.Ang71) exactly as the new 7.1 builds it: raw HRTF center, no halo.</summary>
    static Convolucao Modelo71()
    {
        lock (trava)
        {
            if (modelo71 == null)
            {
                Brir.Usar("h11");
                bool vp = Brir.VozPlana, be = Brir.BaseEstereo;
                Brir.VozPlana = true; Brir.BaseEstereo = true;      // = the 7.1's own flags (no halo on the center)
                try { var (resp, _) = Brir.Caixas(Brir.Ang71, true, 0); atraso71 = Brir.AtrasoCentro; modelo71 = new Convolucao(resp); }
                finally { Brir.VozPlana = vp; Brir.BaseEstereo = be; }
            }
            return modelo71;
        }
    }

    /// <summary>Speaker responses [L, R, center], built once (~0.5 s, off the audio thread). centro = false: center = halo only (VozPlana, as
    /// the 7.1); true: center = real HRTF speaker at 0°. Brir statics here are this assembly's own copy: the 7.1 never sees this switch.</summary>
    static Convolucao Modelo(bool centro = false)
    {
        lock (trava)
        {
            if (centro ? modeloCentro == null : modelo == null)
            {
                Brir.Usar("h11");                                   // the 7.1's fixed head
                Brir.BaseEstereo = false;                           // this copy's halo rule stays as approved (the 7.1 flag isn't ours)
                bool antes = Brir.VozPlana;
                Brir.VozPlana = !centro;
                try
                {
                    var (resp, _) = Brir.Caixas([-30, 30, 0], true, 0);  // Neutro path: raw HRTF + ground bounce
                    if (centro) { atrasoCentro = Brir.AtrasoCentro; modeloCentro = new Convolucao(resp); }
                    else { atrasoVoz = Brir.AtrasoCentro; modelo = new Convolucao(resp); }
                }
                finally { Brir.VozPlana = antes; }
            }
            return centro ? modeloCentro! : modelo!;
        }
    }

    Convolucao conv;
    Preencher preencher = new() { Plano = true };
    readonly Palco? palco;
    readonly double[][] x = [.. Enumerable.Range(0, 7).Select(_ => new double[B])];
    readonly double[][] ent3;
    readonly double[] lfe = new double[B], l0 = new double[B], r0 = new double[B], dl = new double[B], dr = new double[B], vozP = new double[B], vozR = new double[B], vozC = new double[B];
    readonly double[] yl = new double[B], yr = new double[B], sCaixa = new double[B];
    Biquads? corteCaixa = NovoCorte();
    static Biquads? NovoCorte() => CorteCaixaHz > 0 ? new([Biquads.PassaBaixa(CorteCaixaHz), Biquads.PassaBaixa(CorteCaixaHz), Biquads.Pico(PicoCaixaHz, PicoCaixaQ, PicoCaixaDb)], 1) : null;
    readonly double[] linhaVoz = new double[4 * B], linhaL = new double[4 * B], linhaR = new double[4 * B];
    readonly float[] pal = new float[2 * B];
    readonly int dVoz;
    readonly bool v3, v4, v5;
    readonly Frente frente = new(Frentes.Leque, 0);            // 08/11 v5 = the shared engine's fan front (Espacial\Cena.cs)
    readonly double[][] x9 = [.. Enumerable.Range(0, 9).Select(_ => new double[B])], entFrente;
    int cheio, posVoz;

    public Caixas2(double abre, int taxa, string modo = "")
    {
        kModo = CaixaModo.GetValueOrDefault(modo, 1.0);
        v5 = VozIgual && Versao == 5; v4 = Versao == 4; v3 = VozIgual && (Versao == 3 || v5);
        conv = Novo();
        dVoz = Math.Clamp(v4 ? atraso71 : v3 ? atrasoCentro : atrasoVoz, 0, linhaVoz.Length - B);
        ent3 = [x[0], x[1], x[2]]; entFrente = [l0, r0, vozC];
        if (abre < 1 && !VozIgual && !v4) palco = new Palco(abre, taxa);
    }

    public void Limpar()
    {
        conv = Novo(); preencher = new() { Plano = true }; palco?.Limpar(); corteCaixa = NovoCorte();
        frente.Limpar();
        Array.Clear(yl); Array.Clear(yr); Array.Clear(linhaVoz); Array.Clear(linhaL); Array.Clear(linhaR); cheio = 0; posVoz = 0;
    }

    Convolucao Novo() => (v5 ? ModeloLeque() : v4 ? Modelo71() : Modelo(v3)).Clone();

    /// <summary>n &lt;= 512 frames, interleaved L/R in e -> o.</summary>
    public void Processar(ReadOnlySpan<float> e, float[] o, int n)
    {
        var p = pal.AsSpan(0, n * 2);
        e[..(n * 2)].CopyTo(p);
        palco?.Processar(p, n);
        for (int i = 0; i < n; i++)
        {
            l0[cheio] = p[2 * i]; r0[cheio] = p[2 * i + 1];
            o[2 * i] = (float)yl[cheio]; o[2 * i + 1] = (float)yr[cheio];
            if (++cheio == B) { Bloco(); cheio = 0; }
        }
    }

    void Bloco()
    {
        Array.Copy(l0, x[0], B); Array.Copy(r0, x[1], B);
        for (int c = 2; c < 7; c++) Array.Clear(x[c]);
        if (v4)
        {   // = Motor's 7.1 block with Brir.BaseEstereo: Preencher (Plano) moves the shared part to C (·√2) and each side's own part partly
            // to SL/SR/BL/BR; what is left on FL/FR goes plain to its ear (delayed to the HRTF peak) + ±CaixaBase·S into the ±30 speakers
            Array.Clear(lfe);
            preencher.Bloco(x, lfe);
            int m = linhaL.Length; double cx = Brir.CaixaBase, gc = Brir.GanhoCentroBase;
            var fl = x[0]; var fr = x[1]; var c = x[2];
            for (int i = 0; i < B; i++)
            {
                double l = fl[i], r = fr[i], s = 0.5 * (l - r);
                linhaL[posVoz] = l; linhaR[posVoz] = r;
                int k = (posVoz - dVoz + m) % m; vozP[i] = linhaL[k]; vozR[i] = linhaR[k]; posVoz = (posVoz + 1) % m;
                fl[i] = cx * s; fr[i] = -cx * s; c[i] *= gc;
            }
            conv.Bloco(x, dl, dr);
            for (int i = 0; i < B; i++) { yl[i] = vozP[i] + dl[i]; yr[i] = vozR[i] + dr[i]; }
            return;
        }
        if (VozIgual)
        {
            int m = linhaL.Length; double la = Lado, cx = Caixa, gv = GanhoVoz;
            if (v5) { (la, cx) = Intensidade.Mapa(Forca); cx *= kModo; }
            bool centroPlano = v5 && VozCentroPlana;
            if (v3 && !centroPlano)
            {   // only what L and R share (the voice) goes to the center speaker; hard pans keep their plain stereo exactly
                Array.Clear(lfe);
                preencher.Bloco(x, lfe);                                // x[2] = shared part · √2 (Plano)
                for (int i = 0; i < B; i++) vozC[i] = 0.70710678 * x[2][i];
            }
            else Array.Clear(vozC);
            if (v5)
            {   // plain M (minus the center's share) + la·S, cx·S over the fan, gv·shared part on the 0° speaker
                frente.Bloco(entFrente, la, cx, gv, centroPlano ? null : vozC, vozP, vozR, x9, atrasoCentro);
                conv.Bloco(x9, dl, dr);
                for (int i = 0; i < B; i++) { yl[i] = vozP[i] + dl[i]; yr[i] = vozR[i] + dr[i]; }
                return;
            }
            for (int i = 0; i < B; i++)
            {
                double c = vozC[i], mid = 0.5 * (l0[i] + r0[i]) - c, lado = 0.5 * (l0[i] - r0[i]);
                sCaixa[i] = cx * lado; x[2][i] = gv * c;
                linhaL[posVoz] = mid + la * lado; linhaR[posVoz] = mid - la * lado;
                int k = (posVoz - dVoz + m) % m; vozP[i] = linhaL[k]; vozR[i] = linhaR[k]; posVoz = (posVoz + 1) % m;
            }
            if (v3) corteCaixa?.Processar(sCaixa, 0, B);
            for (int i = 0; i < B; i++) { x[0][i] = sCaixa[i]; x[1][i] = -sCaixa[i]; }
            conv.Bloco(ent3, dl, dr);
            double gs = v3 ? Math.Pow(10, GanhoSaidaDb / 20) : 1;
            for (int i = 0; i < B; i++) { yl[i] = gs * (vozP[i] + dl[i]); yr[i] = gs * (vozR[i] + dr[i]); }
            return;
        }
        if (!TirarVoz)
        {
            conv.Bloco(ent3, dl, dr);
            for (int i = 0; i < B; i++) { yl[i] = dl[i]; yr[i] = dr[i]; }
            return;
        }
        preencher.Bloco(x, lfe);                                    // x[2] = shared part · √2 (Plano); its other outputs are ignored
        int n = linhaVoz.Length;
        for (int i = 0; i < B; i++)
        {
            double c = 0.70710678 * x[2][i];                        // = the shared part as it sat in L and in R
            x[0][i] = l0[i] - c; x[1][i] = r0[i] - c;
            linhaVoz[posVoz] = c; vozP[i] = linhaVoz[(posVoz - dVoz + n) % n]; posVoz = (posVoz + 1) % n;
        }
        conv.Bloco(ent3, dl, dr);
        for (int i = 0; i < B; i++) { yl[i] = dl[i] + vozP[i]; yr[i] = dr[i] + vozP[i]; }
    }
}
