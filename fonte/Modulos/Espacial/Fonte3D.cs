// 08/11 3D screen (ui\telas\espaco3d.js): one test source the user flies around a head, rendered LIVE into the headphones.
// UI key "f3d" {tocar, fonte:'ruido'|'arquivo', arq, az, el, dist, pos (UI only), modo:'direto'|'caixas', caixas:[angles], vol, sala, silenciar}.
//   Direto       = the HRTF of that exact direction (3 nearest measured points mixed, Brir.RespostaSuave), level of the Cena center speaker.
//   Pelas caixas = what the current mode would do: sin/cos pan between the 2 neighbour speakers of the list, each through its Cena response
//                  (a gap wider than 180° = stuck on the nearest speaker); empty list = plain L/R pan (ordinary headphones).
//   Distance 0.5..10 m (12x12x6 m room in the UI): level 1/d, air low-pass 20 kHz @1 m -> 10 kHz @5 m, floor 6 kHz, optional room (Brir.SalaComum(0), -12 dB vs direct @1 m, fixed).
// Chain place 94 (after boost 90, before the limiter 95). Own source -> 512-sample blocks -> output FIFO (the host block size varies).
// A move builds a new Convolucao on a worker; the audio thread copies the input history into it and crossfades over one block.
using System.Text.Json;
using FAH.Contrato;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FAH.Espacial;

public sealed class Fonte3D
{
    const int B = Convolucao.B, L = Brir.LDCaixa, Taxa = 48000, MaxSeg = 180, Anel = 8192;
    IHost? host;
    readonly Lock trava = new();
    // what the UI wants (UI thread writes, worker / audio read)
    double az, el, dist = 1.5; bool modoCaixas; double[] caixas = [];
    volatile bool tocar, silenciar, comSala = true, usaArquivo;
    volatile float vol = 0.5f, ganhoDist = 1 / 1.5f;
    volatile double[] lpK = Biquads.PassaBaixa(20000);
    string arq = "", carregado = "", carregando = ""; bool primeiro = true;
    volatile float[]? arquivo;
    static readonly float[] Ruido = Pulsos();
    // filters (worker builds, audio takes)
    volatile Convolucao? pendente, salaPronta;
    volatile float ganhoSala;
    int ocupado, sujo;

    public IEfeito Efeito { get; }
    public Fonte3D() => Efeito = new Ef(this);
    public void Iniciar(IHost h) => host = h;

    public void Receber(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Object) return;
        bool primeira = primeiro; primeiro = false;
        tocar = !primeira && Bool(v, "tocar", false);              // saved state replayed at start never plays by itself
        silenciar = Bool(v, "silenciar", false); comSala = Bool(v, "sala", true);
        usaArquivo = v.TryGetProperty("fonte", out var fo) && fo.ValueKind == JsonValueKind.String && fo.GetString() == "arquivo";
        vol = (float)Math.Pow(Math.Clamp(Num(v, "vol", 70), 0, 100) / 100, 2);
        double d = Math.Clamp(Num(v, "dist", 1.5), 0.5, 10);
        ganhoDist = (float)(1 / d);
        lpK = Biquads.PassaBaixa(d <= 1 ? 20000 : Math.Max(6000, 20000 * Math.Pow(2, -(d - 1) / 4)), 0.707, Taxa);
        string novoArq = v.TryGetProperty("arq", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! : "";
        lock (trava)
        {
            az = Num(v, "az", 0); el = Math.Clamp(Num(v, "el", 0), -40, 90); dist = d;
            modoCaixas = v.TryGetProperty("modo", out var m) && m.ValueKind == JsonValueKind.String && m.GetString() == "caixas";
            caixas = v.TryGetProperty("caixas", out var c) && c.ValueKind == JsonValueKind.Array
                ? [.. c.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetDouble())] : [];
            arq = novoArq;
        }
        if (novoArq != "" && novoArq != carregado && novoArq != carregando && (!primeira || (tocar && usaArquivo))) Carregar(novoArq);
        Pedir();
    }

    // ---------------------------------------------------------------- worker: builds the filters for the current position
    public void Pedir()
    {
        Interlocked.Exchange(ref sujo, 1);
        if (Interlocked.Exchange(ref ocupado, 1) == 0) Task.Run(Trabalhar);
    }

    void Trabalhar()
    {
        while (true)
        {
            Interlocked.Exchange(ref sujo, 0);
            try { Construir(); } catch (Exception e) { host?.Log("f3d: " + e.Message); }
            Volatile.Write(ref ocupado, 0);
            if (Volatile.Read(ref sujo) == 0 || Interlocked.Exchange(ref ocupado, 1) == 1) return;
        }
    }

    string refOuvido = ""; double energiaCentro = 1, ganhoDireto = 1; int atrasoCentro;
    string chaveLista = ""; double[] listaAng = []; double[][][] listaResp = [];

    void Construir()
    {
        if (!Brir.Pronto) return;
        double a, e; bool cx; double[] lista;
        lock (trava) { a = az; e = el; cx = modoCaixas; lista = caixas; }
        if (refOuvido != Brir.Ouvido)
        {   // level reference: the center speaker of the plain [-30, 30, 0] trio
            var (r, at) = Cena.Respostas([-30, 30, 0], null, fixarCentro: false);
            energiaCentro = E(r[2]); atrasoCentro = at;
            ganhoDireto = Math.Sqrt(energiaCentro / Math.Max(E(Brir.RespostaSuave(0, 0)), 1e-30));
            refOuvido = Brir.Ouvido; chaveLista = "";
        }
        if (salaPronta == null)
        {
            var s = Brir.SalaComum(0);
            salaPronta = new Convolucao([[s[0], s[1]]]);
        }
        ganhoSala = (float)(Math.Sqrt(energiaCentro) * Math.Pow(10, -12 / 20.0));
        double[][] h = [new double[L], new double[L]];
        if (!cx)
        {
            var r = Brir.RespostaSuave(a, e);
            for (int o = 0; o < 2; o++) for (int i = 0; i < Math.Min(L, r[o].Length); i++) h[o][i] = r[o][i] * ganhoDireto;
        }
        else if (lista.Length < 3)
        {   // ordinary headphones: constant-power L/R pan, both ears timed like the center speaker
            double p = Math.Sin(a * Math.PI / 180) * Math.Cos(e * Math.PI / 180), t = (p + 1) * Math.PI / 4, k = Math.Sqrt(energiaCentro);
            int at = Math.Clamp(atrasoCentro, 0, L - 1);
            h[0][at] = k * Math.Cos(t); h[1][at] = k * Math.Sin(t);
        }
        else
        {
            string ch = string.Join(',', lista);
            if (ch != chaveLista) { listaResp = Cena.Respostas(lista, null, fixarCentro: false).Resp; listaAng = lista; chaveLista = ch; }
            var g = Pan(listaAng, a);
            for (int c = 0; c < g.Length; c++)
            {
                if (g[c] == 0) continue;
                for (int o = 0; o < 2; o++) { var x = listaResp[c][o]; for (int i = 0; i < Math.Min(L, x.Length); i++) h[o][i] += g[c] * x[i]; }
            }
        }
        if (!cx || lista.Length >= 3) Externalizar(h, a, e);
        pendente = new Convolucao([h]);
    }

    // 08/11 user: "the front center sits at my ear, not in front". The 0° HRTF is the same in both ears, and with nothing else the brain
    // puts such a sound inside the head. The 7.1 MUSIC keeps that on purpose (its center = "voice like stereo", Brir.VozPlana/BaseEstereo):
    // untouched. Only this test source gets, near the front: Blauert front bands + 5 early bounces (sides, ceiling, behind-sides) through
    // their own HRTF, different time per ear side -> the two ears differ and the sound sits out in front. Weight fades out by 70° (az or el),
    // so sides/back stay as approved. Total energy kept (same loudness).
    static readonly (double Daz, double El, double Ms, double G)[] Rebotes = [(-60, 0, 3.5, 0.40), (60, 0, 5.5, 0.35), (0, 40, 8, 0.25), (-115, 0, 11, 0.20), (115, 0, 13.5, 0.18)];
    // Also used by the 7.1 fronts in Filme/Jogo (Ajustes.Externo): there the front must sit out in front, not in the head.
    internal static void Externalizar(double[][] h, double az, double el)
    {
        double a = Math.Abs(((az + 180) % 360 + 360) % 360 - 180);
        double w = Math.Clamp((70 - a) / 40, 0, 1) * Math.Clamp((70 - Math.Abs(el)) / 40, 0, 1);
        if (w < 1e-3) return;
        double e0 = E(h);
        if (e0 < 1e-30) return;
        double kRef = Math.Sqrt(e0 / Math.Max(E(Brir.RespostaSuave(az, el)), 1e-30));
        double[][] rb = [new double[L], new double[L]];
        foreach (var (daz, rel, ms, g) in Rebotes)
        {
            var r = Brir.RespostaSuave(az + daz, Math.Clamp(el + rel, -40, 90));
            int at = (int)Math.Round(ms * Taxa / 1000);
            for (int o = 0; o < 2; o++) for (int i = 0; i < r[o].Length && at + i < L; i++) rb[o][at + i] += r[o][i] * g * w * kRef;
        }
        foreach (var x in rb) Biquads.Filtrar([Biquads.PassaBaixa(7000)], x);
        foreach (var x in h) Biquads.Filtrar([Biquads.Pico(400, 2.0, 3 * w), Biquads.Pico(1100, 2.5, -2 * w), Biquads.Pico(3600, 1.2, 1.5 * w)], x);
        for (int o = 0; o < 2; o++) for (int i = 0; i < L; i++) h[o][i] += rb[o][i];
        double k = Math.Sqrt(e0 / Math.Max(E(h), 1e-30));
        foreach (var x in h) for (int i = 0; i < L; i++) x[i] *= k;
    }

    /// <summary>Gains per speaker (list order): sin/cos between the 2 speakers around az; inside a gap wider than 180° only the nearest one.</summary>
    static double[] Pan(double[] ang, double az)
    {
        static double N(double x) => ((x + 180) % 360 + 360) % 360 - 180;
        var g = new double[ang.Length];
        var ord = Enumerable.Range(0, ang.Length).OrderBy(i => N(ang[i])).ToArray();
        double a = N(az);
        for (int k = 0; k < ord.Length; k++)
        {
            int i = ord[k], j = ord[(k + 1) % ord.Length];
            double ai = N(ang[i]), gap = (N(ang[j]) - ai + 360) % 360, off = (a - ai + 360) % 360;
            if (gap < 1e-6 || off > gap) continue;
            if (gap > 180) { if (off < gap - off) g[i] = 1; else g[j] = 1; return g; }
            double t = off / gap * Math.PI / 2;
            g[i] = Math.Cos(t); g[j] = Math.Sin(t); return g;
        }
        g[ord[0]] = 1; return g;
    }

    static double E(double[][] h) { double s = 0; foreach (var x in h) foreach (var v in x) s += v * v; return s; }

    // ---------------------------------------------------------------- audio file: any format Windows plays, mono 48 kHz, max 3 min, RMS -20 dBFS
    void Carregar(string caminho)
    {
        carregando = caminho;
        string nome = Path.GetFileName(caminho);
        host?.Enviar("f3dinfo", new { nome, carregando = true, erro = "" });
        Task.Run(() =>
        {
            try
            {
                var x = Decodificar(caminho);
                if (carregando != caminho) return;
                arquivo = x; carregado = caminho; carregando = "";
                host?.Enviar("f3dinfo", new { nome, carregando = false, erro = "", segundos = Math.Round(x.Length / (double)Taxa, 1) });
            }
            catch (Exception e)
            {
                host?.Log("f3d arquivo: " + e.Message);
                if (carregando == caminho) carregando = "";
                host?.Enviar("f3dinfo", new { nome, carregando = false, erro = "Não consegui abrir esse arquivo. Escolha outra música (mp3, wav, m4a, flac...)." });
            }
        });
    }

    static float[] Decodificar(string caminho)
    {
        using var rd = new MediaFoundationReader(caminho);
        ISampleProvider sp = new Mono(rd.ToSampleProvider());
        if (sp.WaveFormat.SampleRate != Taxa) sp = new WdlResamplingSampleProvider(sp, Taxa);
        var buf = new float[Taxa * MaxSeg]; int n = 0;
        while (n < buf.Length) { int k = sp.Read(buf, n, Math.Min(Taxa, buf.Length - n)); if (k <= 0) break; n += k; }
        if (n < Taxa / 10) throw new InvalidDataException("arquivo sem som");
        Array.Resize(ref buf, n);
        double s = 0, pico = 0; foreach (var v in buf) { s += v * v; pico = Math.Max(pico, Math.Abs(v)); }
        double rms = Math.Sqrt(s / n), g = rms > 1e-6 ? 0.1 / rms : 1;
        if (pico * g > 4) g = 4 / pico;                                      // very spiky file: keep peaks sane (limiter does the rest)
        int f = Math.Min(n / 2, Taxa / 100);                                  // 10 ms fades: the loop point doesn't click
        for (int i = 0; i < n; i++) { double w = i < f ? i / (double)f : i >= n - f ? (n - 1 - i) / (double)f : 1; buf[i] = (float)(buf[i] * g * w); }
        return buf;
    }

    sealed class Mono(ISampleProvider src) : ISampleProvider
    {
        readonly int c = src.WaveFormat.Channels; float[] tmp = [];
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(src.WaveFormat.SampleRate, 1);
        public int Read(float[] buffer, int offset, int count)
        {
            if (c == 1) return src.Read(buffer, offset, count);
            if (tmp.Length < count * c) tmp = new float[count * c];
            int k = src.Read(tmp, 0, count * c) / c;
            for (int i = 0; i < k; i++) { float s = 0; for (int j = 0; j < c; j++) s += tmp[i * c + j]; buffer[offset + i] = s / c; }
            return k;
        }
    }

    /// <summary>Built-in test sound: 4 bursts of pink noise (250 ms on, 250 ms off), RMS of the bursts -20 dBFS.</summary>
    static float[] Pulsos()
    {
        int n = Taxa * 2, on = Taxa / 4, r = Taxa / 200;
        var o = new float[n]; var rng = new Random(3);
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0, s = 0; int m = 0;
        for (int i = 0; i < n; i++)
        {
            double w = rng.NextDouble() * 2 - 1;                             // Paul Kellet's pink filter
            b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
            double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
            int q = i % (2 * on);
            double env = q >= on ? 0 : Math.Min(1, Math.Min(q, on - 1 - q) / (double)r);
            o[i] = (float)(p * env);
            if (q < on) { s += p * p; m++; }
        }
        float g = (float)(0.1 / Math.Sqrt(s / Math.Max(m, 1)));
        for (int i = 0; i < n; i++) o[i] *= g;
        return o;
    }

    static bool Bool(JsonElement o, string k, bool pad) => o.TryGetProperty(k, out var x)
        ? x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0) : pad;
    static double Num(JsonElement o, string k, double pad) => o.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : pad;

    // ---------------------------------------------------------------- audio thread
    sealed class Ef : IEfeito
    {
        static readonly double KFade = 1 - Math.Exp(-1 / (0.015 * Taxa)), KDist = 1 - Math.Exp(-1 / (0.03 * Taxa)),
            KDuck = 1 - Math.Exp(-1 / (0.06 * Taxa)), KSala = 1 - Math.Exp(-1 / (0.05 * Taxa));
        Convolucao? conv, sala;
        float[] fonte = []; int pos;
        double gF, gD = 1 / 1.5, gS, duck = 1, x1, x2, y1, y2;
        int cauda;
        readonly double[] xb = new double[B], xs = new double[B], l = new double[B], r = new double[B], l2 = new double[B], r2 = new double[B], sl = new double[B], sr = new double[B];
        readonly double[][] ent, entS;
        readonly float[] anel = new float[2 * Anel]; int rd, n;

        readonly Fonte3D f;
        public Ef(Fonte3D dono) { f = dono; ent = [xb]; entS = [xs]; }

        public string Id => "f3d";
        public Lado Lado => Lado.Som;
        public int Posicao => 94;
        public void Preparar(int taxa) { n = 0; rd = 0; gF = 0; cauda = 0; duck = 1; }

        public void Processar(Span<float> lr, int quadros)
        {
            bool ativo = f.tocar || gF > 1e-5 || cauda > 0;
            double alvoDuck = f.silenciar && f.tocar ? 0 : 1;
            if (!ativo && n == 0 && duck > 0.9999) { duck = 1; return; }
            if (ativo) while (n < quadros && n + B <= Anel) Render();
            for (int i = 0; i < quadros; i++)
            {
                duck += (alvoDuck - duck) * KDuck;
                float ol = 0, or = 0;
                if (n > 0) { ol = anel[2 * rd]; or = anel[2 * rd + 1]; rd = (rd + 1) % Anel; n--; }
                lr[2 * i] = (float)(lr[2 * i] * duck) + ol;
                lr[2 * i + 1] = (float)(lr[2 * i + 1] * duck) + or;
            }
        }

        void Render()
        {
            var nova = f.pendente;
            if (nova != null && Interlocked.CompareExchange(ref f.pendente, null, nova) != nova) nova = null;
            sala ??= f.salaPronta;
            var src = f.usaArquivo && f.arquivo is { } ar ? ar : Ruido;
            if (!ReferenceEquals(src, fonte)) { fonte = src; pos = 0; }
            double alvoF = f.tocar ? f.vol : 0, alvoD = f.ganhoDist, alvoS = f.comSala ? f.ganhoSala : 0;
            var k = f.lpK;
            for (int i = 0; i < B; i++)
            {
                gF += (alvoF - gF) * KFade; gD += (alvoD - gD) * KDist;
                double v = fonte[pos]; if (++pos >= fonte.Length) pos = 0;
                double y = k[0] * v + k[1] * x1 + k[2] * x2 - k[3] * y1 - k[4] * y2;
                x2 = x1; x1 = v; y2 = y1; y1 = y;
                xs[i] = y * gF; xb[i] = xs[i] * gD;
            }
            if (Math.Abs(y1) < 1e-20) { y1 = 0; y2 = 0; }
            if (conv == null && nova != null) { conv = nova; nova = null; }
            if (nova != null) nova.CopiarEstado(conv!);                    // same input history (before this block): starts warm
            if (conv != null) conv.Bloco(ent, l, r); else { Array.Clear(l); Array.Clear(r); }
            if (nova != null)
            {   // both ran this block: linear crossfade old -> new
                nova.Bloco(ent, l2, r2);
                for (int i = 0; i < B; i++) { double t = (i + 0.5) / B; l[i] += (l2[i] - l[i]) * t; r[i] += (r2[i] - r[i]) * t; }
                conv = nova;
            }
            bool salaViva = sala != null && (alvoS > 0 || gS > 1e-6);
            if (salaViva) sala!.Bloco(entS, sl, sr);
            for (int i = 0; i < B; i++)
            {
                double ga = 0;
                if (salaViva) { gS += (alvoS - gS) * KSala; ga = gS; }
                int w = (rd + n) % Anel;
                anel[2 * w] = (float)(l[i] + ga * sl[i]); anel[2 * w + 1] = (float)(r[i] + ga * sr[i]);
                n++;
            }
            if (f.tocar) cauda = (sala?.P ?? 0) + (conv?.P ?? 0) + 2;
            else if (gF <= 1e-5 && cauda > 0) cauda--;
        }
    }
}
