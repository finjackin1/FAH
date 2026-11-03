// One sound path: input (cable or mic) -> chain -> one or more outputs (port of sistema/fluxo.py).
// The capture callback only copies into a ring; an own thread processes and hands out, so a hiccup in
// processing never loses input samples (each output's ring absorbs the difference).
using FAH.Contrato;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace FAH.Audio;

/// <summary>Effects in fixed order. Desviar = "compare" button: everything bypassed while true.</summary>
public sealed class Cadeia
{
    volatile IEfeito[] efeitos = [];
    public bool Desviar;
    /// <summary>7.1 switch off while the cable stays 8 ch (28/10, no format change = no cut): fold to stereo, Som 7.1 runs its stereo path.</summary>
    public volatile bool Estereo;
    public int Taxa { get; private set; } = 48000;

    public void Definir(IEnumerable<IEfeito> lst)
    {
        var a = lst.OrderBy(e => e.Posicao).ToArray();
        foreach (var e in a) e.Preparar(Taxa);
        efeitos = a;
    }

    /// <summary>New order of the same (already prepared) effects; swapped in one go between blocks.</summary>
    public void Ordenar(IEnumerable<IEfeito> lst) => efeitos = [.. lst];

    public IReadOnlyList<IEfeito> Efeitos => efeitos;

    public void Processar(Span<float> lr, int quadros)
    {
        if (Desviar) return;
        foreach (var e in efeitos) e.Processar(lr, quadros);
    }

    /// <summary>7.1 cable: an effect that wants the 8 channels (Som 7.1) gets them; otherwise downmix + normal chain.</summary>
    public void Processar8(ReadOnlySpan<float> src, Span<float> lr, int quadros)
    {
        var a = efeitos;
        IEfeitoMulticanal? m = null;
        if (!Desviar && !Estereo) foreach (var e in a) if (e is IEfeitoMulticanal mc && mc.QuerMulticanal) { m = mc; break; }
        if (m == null) { Fluxo.Descer(src, lr, quadros, 8); Processar(lr, quadros); return; }
        m.ProcessarMulticanal(src, lr, quadros);
        foreach (var e in a) if (e != m) e.Processar(lr, quadros);
    }
}

public sealed class Fluxo : IDisposable
{
    public const int Taxa = 48000, Bloco = 1024;
    const int CapturaMs = 50;           // WASAPI capture buffer: headroom only (data still comes every ~10 ms); 10 lost input on any 10 ms stall
    public readonly string Entrada;
    public string NomeReal = "";
    public readonly Cadeia Cadeia;
    volatile Destino[] destinos = [];
    /// <summary>Extra listeners of the processed sound (mic: "Ouvir minha voz" reader on the headphones, 5 s test recording).</summary>
    public volatile Action<ReadOnlySpan<float>>[] Escutas = [];
    Anel anel = new(Taxa);
    int largura = 2;                    // channels kept in the ring: 8 = 7.1 cable (raw, for Som 7.1), else stereo
    readonly AutoResetEvent evento = new(false);
    WasapiCapture? cap;
    Thread? th;
    volatile bool vivo;
    WdlResampler? rs;
    int canaisDev, taxaDev;
    public int Canais => canaisDev;
    float[] conv = new float[8192], rsOut = new float[8192];
    // measures
    public long Blocos, Erros;
    public volatile string? UltimoErro;
    public double TempoProc, AudioProc, PiorMs;
    public float Pico;
    public double MaiorVaoMs;           // longest gap between two capture deliveries (cable/Windows side)
    long ultimaChegada;
    public long Transbordos => anel.Transbordos;

    /// <summary>loopback = record what plays into the output named <paramref name="entrada"/> (Voicemeeter Input).</summary>
    public readonly bool Loopback;
    public Fluxo(string entrada, Cadeia cadeia, bool loopback = false) { Entrada = entrada; Cadeia = cadeia; Loopback = loopback; }

    public IReadOnlyList<Destino> Destinos => destinos;
    public void DefinirDestinos(IEnumerable<Destino> d) => destinos = [.. d];
    public double UsoPct => AudioProc > 0 ? 100 * TempoProc / AudioProc : 0;

    /// <summary>ap = already found (cable switch: lookup done before the silence).</summary>
    public void Abrir(Aparelho? ap = null)
    {
        ap ??= Aparelhos.Achar(Loopback, Entrada) ?? throw new InvalidOperationException($"entrada não encontrada: {Entrada}");
        var c = Loopback ? new WasapiLoopbackCapture(Aparelhos.Abrir(ap.Id)) : new WasapiCapture(Aparelhos.Abrir(ap.Id), true, CapturaMs);
        var wf = c.WaveFormat;
        if (wf.BitsPerSample != 32) throw new NotSupportedException($"{ap.Nome}: formato {wf} (esperado float 32)");
        canaisDev = wf.Channels; taxaDev = wf.SampleRate;
        largura = canaisDev == 8 ? 8 : 2;
        anel = new(Taxa, largura);
        if (taxaDev != Taxa)
        {
            rs = new WdlResampler();
            rs.SetMode(true, 2, false); rs.SetFilterParms(); rs.SetFeedMode(true); rs.SetRates(taxaDev, Taxa);
        }
        c.DataAvailable += Chegou;
        // only a stop FAH did not ask for (device unplugged / invalidated): Fechar clears "cap" first, so its own stops are ignored
        c.RecordingStopped += (_, e) => { if (ReferenceEquals(cap, c)) AoPerder?.Invoke(e.Exception); };
        NomeReal = ap.Nome;
        vivo = true;
        th = new Thread(Laco) { IsBackground = true, Name = "fluxo-" + Entrada, Priority = ThreadPriority.AboveNormal };
        th.Start();
        c.StartRecording();
        cap = c;
    }

    /// <summary>Raised when the input stops by itself (device unplugged / invalidated). Handler must be cheap: capture thread.</summary>
    public Action<Exception?>? AoPerder;

    public void Fechar()
    {
        vivo = false; evento.Set();
        var c = cap; cap = null;
        try { c?.StopRecording(); c?.Dispose(); } catch { }
        th?.Join(2000);
    }

    public void Dispose() => Fechar();

    void Chegou(object? s, WaveInEventArgs e)
    {
        if (!Mmcss.Feito) Mmcss.Registrar();
        long agora = System.Diagnostics.Stopwatch.GetTimestamp();
        if (ultimaChegada != 0) MaiorVaoMs = Math.Max(MaiorVaoMs, (agora - ultimaChegada) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        ultimaChegada = agora;
        var src = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));
        int q = src.Length / canaisDev, L = largura;
        if (conv.Length < q * L) conv = new float[q * L];
        if (L == 8) src[..(q * 8)].CopyTo(conv); else Descer(src, conv, q, canaisDev);
        if (rs == null) { anel.Escrever(conv.AsSpan(0, q * L)); evento.Set(); return; }
        int pede = rs.ResamplePrepare(q, L, out var ib, out var io);
        Array.Copy(conv, 0, ib, io, pede * L);
        int max = (int)((long)q * Taxa / taxaDev) + 16;
        if (rsOut.Length < max * L) rsOut = new float[max * L];
        int saiu = rs.ResampleOut(rsOut, 0, pede, max, L);
        anel.Escrever(rsOut.AsSpan(0, saiu * L));
        evento.Set();
    }

    /// <summary>Any channel count -> stereo. 7.1/5.1: L/R + 0.707·C + 0.5·LFE + 0.5·surrounds (Som 7.1 off; on, it gets the 8 channels).</summary>
    public static void Descer(ReadOnlySpan<float> src, Span<float> lr, int q, int ch)
    {
        if (ch == 2) { src[..(q * 2)].CopyTo(lr); return; }
        for (int i = 0; i < q; i++)
        {
            int b = i * ch;
            if (ch == 1) { lr[2 * i] = lr[2 * i + 1] = src[b]; continue; }
            float l = src[b], r = src[b + 1];
            if (ch >= 6)
            {
                float c = 0.707f * src[b + 2] + 0.5f * src[b + 3];
                l += c; r += c;
                for (int k = 4; k + 1 < ch; k += 2) { l += 0.5f * src[b + k]; r += 0.5f * src[b + k + 1]; }
            }
            lr[2 * i] = l; lr[2 * i + 1] = r;
        }
    }

    void Laco()
    {
        Mmcss.Registrar();
        var buf = new float[Bloco * 2];
        var bruto = new float[Bloco * 8];
        var sw = new System.Diagnostics.Stopwatch();
        while (vivo)
        {
            evento.WaitOne(100);
            int q;
            var a = anel;
            if (a.Canais == 8) while (vivo && (q = a.Ler(bruto)) > 0) ProcessarBloco8(bruto, buf, q, sw);
            else while (vivo && (q = a.Ler(buf)) > 0) ProcessarBloco(buf, q, sw);
        }
    }

    /// <summary>7.1 block (8 channels in <paramref name="bruto"/>) -> stereo in <paramref name="buf"/> -> hand out.</summary>
    public void ProcessarBloco8(float[] bruto, float[] buf, int q, System.Diagnostics.Stopwatch? sw = null)
    {
        sw ??= new(); sw.Restart();
        try { Cadeia.Processar8(bruto.AsSpan(0, q * 8), buf.AsSpan(0, q * 2), q); }
        catch { Erros++; }
        Contar(buf, q, sw);
    }

    /// <summary>Chain + hand out (also used by the offline tests).</summary>
    public void ProcessarBloco(float[] buf, int q, System.Diagnostics.Stopwatch? sw = null)
    {
        sw ??= new(); sw.Restart();
        try { Cadeia.Processar(buf.AsSpan(0, q * 2), q); }
        catch (Exception e) { Erros++; UltimoErro = e.ToString(); }
        Contar(buf, q, sw);
    }

    void Contar(float[] buf, int q, System.Diagnostics.Stopwatch sw)
    {
        double ms = sw.Elapsed.TotalMilliseconds;
        Blocos++; TempoProc += ms / 1000; AudioProc += q / (double)Taxa; PiorMs = Math.Max(PiorMs, ms);
        float pk = Pico;
        for (int i = 0; i < q * 2; i++) { float a = Math.Abs(buf[i]); if (a > pk) pk = a; }
        Pico = pk;
        foreach (var d in destinos) d.Empurrar(buf.AsSpan(0, q * 2));
        foreach (var e in Escutas) e(buf.AsSpan(0, q * 2));
    }
}
