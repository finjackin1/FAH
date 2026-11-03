// Speech detector = Silero VAD v5 (MIT, github snakers4/silero-vad), ONNX. 31/10: the voice gate's ear (DFN's lsnr also rises
// on music/TV). Runs at 16 kHz: input 48 kHz -> 4th-order low-pass 7 kHz -> every 3rd sample. Chunk 512 (32 ms) + 64 samples of
// context from the previous chunk; recurrent state [2,1,128] goes from one call to the next. Prob = speech chance of the last chunk.
using System.Reflection;
using Microsoft.ML.OnnxRuntime;

namespace FAH.Microfone;

public sealed class Vad : IDisposable
{
    public const int Pedaco = 512, Contexto = 64, Fator = 3;
    public string? Erro { get; private set; }
    public bool Pronto => sessao != null;
    /// <summary>Speech probability of the last 32 ms chunk (0..1).</summary>
    public float Prob { get; private set; }
    /// <summary>Counts chunks run (bench / log).</summary>
    public long Pedacos { get; private set; }
    /// <summary>First run error (log / bench).</summary>
    public string? Falha { get; private set; }
    public string Formato => sessao == null ? "" : string.Join("; ", sessao.InputMetadata.Select(k => $"{k.Key} {k.Value.ElementDataType} [{string.Join(",", k.Value.Dimensions)}]")) + " -> " + string.Join("; ", sessao.OutputMetadata.Select(k => $"{k.Key} [{string.Join(",", k.Value.Dimensions)}]"));

    InferenceSession? sessao;
    RunOptions? opc;
    string[] entNomes = [], saiNomes = [];
    OrtValue[] entA = [], entB = [], saiA = [], saiB = [];
    readonly float[] janela = new float[Contexto + Pedaco], estA = new float[2 * 128], estB = new float[2 * 128], prob = new float[1];
    readonly long[] taxa = [16000];
    bool par;
    readonly Biquad f1 = new(), f2 = new();
    int fase, n;

    public Vad() { f1.PassaBaixa(48000, 7000, 0.5412); f2.PassaBaixa(48000, 7000, 1.3066); }

    /// <summary>Loads the model (≈50 ms). Off the audio thread.</summary>
    public void Carregar()
    {
        if (sessao != null || Erro != null) return;
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("silero_vad.onnx") ?? throw new FileNotFoundException("silero_vad.onnx");
            var bytes = new byte[s.Length]; s.ReadExactly(bytes);
            var so = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL };
            var se = new InferenceSession(bytes, so);
            string Ent(string quer, int i) => se.InputMetadata.ContainsKey(quer) ? quer : se.InputMetadata.Keys.ElementAt(i);
            string ei = Ent("input", 0), ee = Ent("state", 1), et = Ent("sr", 2);
            entNomes = [ei, ee, et];
            var srDims = se.InputMetadata[et].Dimensions;
            OrtValue V(float[] a, params long[] sh) => OrtValue.CreateTensorValueFromMemory(a, sh);
            var vj = V(janela, 1, Contexto + Pedaco);
            var vt = OrtValue.CreateTensorValueFromMemory(taxa, srDims.Length == 0 ? [] : [1]);
            entA = [vj, V(estA, 2, 1, 128), vt]; entB = [vj, V(estB, 2, 1, 128), vt];
            // outputs: prob [1,1] and new state [2,1,128]; A reads estA -> writes estB, B the other way
            saiNomes = [.. se.OutputMetadata.Keys];
            OrtValue[] Sai(float[] dest) => [.. se.OutputMetadata.Values.Select(md => md.Dimensions.Length == 3 ? V(dest, 2, 1, 128) : V(prob, 1, 1))];   // state dims are all -1: by rank
            saiA = Sai(estB); saiB = Sai(estA);
            opc = new RunOptions();
            sessao = se;
        }
        catch (Exception e) { Erro = e.Message; }
    }

    public void Zerar() { Array.Clear(estA); Array.Clear(estB); Array.Clear(janela); par = false; n = 0; fase = 0; Prob = 0; f1.Zerar(); f2.Zerar(); }

    /// <summary>One 48 kHz sample in. Returns true when a chunk just ran (Prob updated).</summary>
    public bool Empurrar(float x)
    {
        float y = f2.Passo(f1.Passo(x));
        if (++fase < Fator) return false;
        fase = 0;
        janela[Contexto + n++] = y;
        if (n < Pedaco) return false;
        n = 0;
        try { sessao!.Run(opc, entNomes, par ? entB : entA, saiNomes, par ? saiB : saiA); par = !par; Prob = prob[0]; Pedacos++; }
        catch (Exception e) { Prob = 1; Falha ??= e.Message; }   // model failed: never close the gate on a broken ear
        Array.Copy(janela, Pedaco, janela, 0, Contexto);     // last 64 samples = next chunk's context
        return true;
    }

    public void Dispose() { sessao?.Dispose(); opc?.Dispose(); }
}
