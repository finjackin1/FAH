// Noise suppressor = DeepFilterNet3 (MIT/Apache) streaming ONNX.
// The model does STFT/ERB/deep filtering inside: in 480 samples at 48 kHz, out the clean frame; its state (45 304 floats)
// goes from one call to the next. atten_lim_db caps how much noise is lowered = strength (100% = no cap).
// Model delay 1440 samples + 480 framing = 40 ms. Mono (mic): left channel in, both channels out.
using System.Reflection;
using FAH.Contrato;
using Microsoft.ML.OnnxRuntime;

namespace FAH.Microfone;

public sealed class Supressor : IEfeito, IDisposable
{
    public const int Quadro = 480, Atraso = 1440;
    public string Id => "ruido";
    public Lado Lado => Lado.Mic;
    public int Posicao => 30;

    /// <summary>Wanted state (UI thread); the audio thread fades to it.</summary>
    public volatile bool Ligado;
    /// <summary>0..1 (UI "Força"). 1 = no cap.</summary>
    public volatile float Forca = 0.7f;
    public string? Erro { get; private set; }
    /// <summary>Voice gate (31/10, user: claps/knocks/music/motorbikes opened the level gate): the mic opens only while the model
    /// hears speech (its lsnr above Limiar dB), stays open SoltarMs after the last speech frame, else drops to ChaoDb. The decision
    /// is made on the input, 40 ms ahead of the clean output = built-in lookahead (word starts are not cut).</summary>
    public volatile bool Portao;
    public volatile float Limiar = 5f;
    public volatile int SoltarMs = 300;
    public float ChaoDb = -50f;
    public bool Aberto { get; private set; } = true;
    /// <summary>31/10 speech ear (Silero): when loaded, the gate opens on its speech probability above VozLimiar instead of lsnr.
    /// Ouvido 0 = lsnr only (old), 1 = Silero only, 2 = Silero, or lsnr if Silero is uncertain (bench picks).</summary>
    public readonly Vad Vad = new();
    public volatile int Ouvido = 1;
    public volatile float VozLimiar = 0.5f;
    /// <summary>31/10 impact softener (user: claps while talking): a click/clap/knock is seen in the input 40 ms before it comes out,
    /// so its ~70 ms are lowered ImpactoDb, starting 2 ms ahead. Detect = high-passed fast peak far above its 40 ms mean.</summary>
    public volatile bool Impacto = false;   // off 31/10: bench = 8-35 false hits on voice alone, voice -1..-2 dB, DFN already leaves claps at -54 dB under speech
    public float ImpactoDb = -24f, ImpactoK = 8f;
    public int ImpactoMs = 70;
    /// <summary>Impacts seen since start (bench / log).</summary>
    public long Golpes { get; private set; }

    InferenceSession? sessao;
    RunOptions? opc;
    string[] entNomes = [], saiNomes = [];
    OrtValue[] entA = [], entB = [], saiA = [], saiB = [];
    readonly float[] quadro = new float[Quadro], limpo = new float[Quadro], lim = new float[1], snr = new float[1];
    float[] estA = [], estB = [];
    bool par;
    // fifo in (waiting for a full frame) and out ring (clean samples, read one per input sample)
    readonly float[] fin = new float[Quadro], ro = new float[4096];
    int nin, rw, rr, rn;
    // dry voice on the same delay (switch on/off without a time jump)
    readonly float[] seco = new float[1 << 13];
    int sp;
    float mix;                            // 0 = dry, 1 = clean (fades ~20 ms)
    int resta;                            // voice gate: samples left open
    float gp = 1;                         // voice gate gain
    bool rodando;
    // impact softener: marks per input sample, read Pre samples ahead of the output; gain gi
    readonly Biquad hp = new();
    readonly bool[] marca = new bool[1 << 13];
    float ef, es, gi = 1; int duck, nada;
    const int Pre = 96;

    public Supressor() => ImpactoHz = 1500;
    /// <summary>Impact detector high-pass corner.</summary>
    public float ImpactoHz { set => hp.PassaAlta(48000, value, 0.707); }
    public void Preparar(int taxa) { }
    /// <summary>Model's local SNR of the last frame (dB, about -15..35): high = speech over noise.</summary>
    public float Lsnr => snr[0];

    long quadros;
    /// <summary>One line for the log (live check).</summary>
    public string Estado => $"ligado={Ligado} modelo={(sessao != null ? "sim" : Erro ?? "nao")} rodando={rodando} mix={mix:0.00} quadros={quadros} forca={Forca:0.00} vad={(Vad.Pronto ? "sim" : Vad.Erro ?? "nao")} golpes={Golpes}";

    /// <summary>Loads the model (â‰ˆ0.5 s). Call off the audio thread.</summary>
    public void Carregar()
    {
        if (sessao != null || Erro != null) return;
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("dfn3.onnx") ?? throw new FileNotFoundException("dfn3.onnx");
            var bytes = new byte[s.Length]; s.ReadExactly(bytes);
            Vad.Carregar();
            var so = new SessionOptions { IntraOpNumThreads = 2, InterOpNumThreads = 1, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL };
            var se = new InferenceSession(bytes, so);
            int nEst = 45304;
            if (se.InputMetadata.TryGetValue("states", out var me) && me.Dimensions.Length == 1 && me.Dimensions[0] > 0) nEst = me.Dimensions[0];
            estA = new float[nEst]; estB = new float[nEst];
            entNomes = ["input_frame", "states", "atten_lim_db"];
            saiNomes = [.. se.OutputMetadata.Keys];
            OrtValue V(float[] a, params long[] sh) => OrtValue.CreateTensorValueFromMemory(a, sh);
            var vq = V(quadro, Quadro); var vl = V(lim);
            entA = [vq, V(estA, nEst), vl]; entB = [vq, V(estB, nEst), vl];
            // outputs: clean frame, new state, (lsnr). A reads estA -> writes estB, B the other way
            OrtValue[] Sai(float[] dest)
            {
                var l = new List<OrtValue>();
                foreach (var (nome, md) in se.OutputMetadata)
                {
                    long n = md.Dimensions.Aggregate(1L, (x, d) => x * Math.Max(1, d));
                    l.Add(n == Quadro ? V(limpo, Quadro) : n == nEst ? V(dest, nEst) : n == 1 ? V(snr, [.. md.Dimensions.Select(d => (long)Math.Max(1, d))]) : V(new float[n], [.. md.Dimensions.Select(d => (long)Math.Max(1, d))]));
                }
                return [.. l];
            }
            saiA = Sai(estB); saiB = Sai(estA);
            opc = new RunOptions();
            sessao = se;
        }
        catch (Exception e) { Erro = e.Message; }
    }

    public void Processar(Span<float> lr, int q)
    {
        bool quer = Ligado && sessao != null;
        if (!quer && !rodando) return;                       // fully off: zero latency, no CPU
        if (quer && !rodando) Reiniciar();
        lim[0] = Forca >= 0.995f ? 100f : 40f * Math.Clamp(Forca, 0f, 1f);
        float alvo = quer ? 1 : 0, passo = 1f / 960, chao = MathF.Pow(10, ChaoDb / 20), abre = 1f / 144, fecha = (1 - chao) / 4800;
        bool porta = Portao && quer, ouve = porta && Ouvido > 0 && Vad.Pronto, imp = Impacto && quer;
        float gImp = MathF.Pow(10, ImpactoDb / 20), rf = 0.9930f, cs = 1f / 1920, k = ImpactoK, desce = (1 - gImp) / 48, sobe = (1 - gImp) / 960;
        int segura = Pre + ImpactoMs * 48, hold = (Atraso + Quadro) + SoltarMs * 48;
        for (int i = 0; i < q; i++)
        {
            float x = lr[2 * i];
            fin[nin++] = x;
            seco[sp] = x;
            if (nin == Quadro) { Rodar(); nin = 0; }
            if (ouve && Vad.Empurrar(x) && (Vad.Prob > VozLimiar || (Ouvido == 2 && Vad.Prob > 0.15f && snr[0] > Limiar))) resta = hold;
            // impact detector on the input timeline (fast peak vs 40 ms mean of |high-pass|; 20 ms dead time after a hit)
            float a = MathF.Abs(hp.Passo(x)); ef = a > ef ? a : ef * rf; bool g = false;
            if (nada > 0) nada--; else if (imp && ef > k * es + 0.003f) { g = true; Golpes++; nada = 960; }
            es += (a - es) * cs;
            marca[sp] = g;
            // clean comes out Quadro-1 + Atraso samples late; dry read on the same delay
            float y = 0;
            if (rn > 0) { y = ro[rr]; rr = (rr + 1) & (ro.Length - 1); rn--; }
            float d = seco[(sp - (Atraso + Quadro - 1) + seco.Length) & (seco.Length - 1)];
            if (marca[(sp - (Atraso + Quadro - 1) + Pre + seco.Length) & (seco.Length - 1)]) duck = segura;
            sp = (sp + 1) & (seco.Length - 1);
            float gia = duck > 0 ? gImp : 1; if (duck > 0) duck--;
            gi = gia < gi ? Math.Max(gia, gi - desce) : Math.Min(gia, gi + sobe);
            mix += Math.Clamp(alvo - mix, -passo, passo);
            if (resta > 0) resta--;
            float ga = !porta || resta > 0 ? 1 : chao;
            gp = ga > gp ? Math.Min(ga, gp + abre) : Math.Max(ga, gp - fecha);
            lr[2 * i] = lr[2 * i + 1] = (d + (y - d) * mix) * gp * gi;
        }
        Aberto = gp > 0.5f;
        if (!quer && mix <= 0) rodando = false;
    }

    void Reiniciar()
    {
        Array.Clear(estA); Array.Clear(estB); par = false;
        nin = 0; rw = rr = rn = 0; rodando = true; mix = 0; resta = 0; gp = 1;
        Vad.Zerar(); Array.Clear(marca); ef = es = 0; gi = 1; duck = nada = 0; hp.Zerar();
    }

    void Rodar()
    {
        Array.Copy(fin, quadro, Quadro); quadros++;
        try
        {
            sessao!.Run(opc, entNomes, par ? entB : entA, saiNomes, par ? saiB : saiA);
            par = !par;
        }
        catch { Array.Copy(quadro, limpo, Quadro); snr[0] = 35; }
        if (snr[0] > Limiar && !(Portao && Ouvido > 0 && Vad.Pronto)) resta = (Atraso + Quadro) + SoltarMs * 48;   // covers the 40 ms the output lags + the hold
        for (int k = 0; k < Quadro && rn < ro.Length; k++) { ro[rw] = limpo[k]; rw = (rw + 1) & (ro.Length - 1); rn++; }
    }

    public void Dispose() { sessao?.Dispose(); opc?.Dispose(); Vad.Dispose(); }
}
