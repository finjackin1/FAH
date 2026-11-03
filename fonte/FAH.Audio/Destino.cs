// A real output (or cable) that pulls from one or more readers, summing them (port of sistema/destino.py Destino).
// Several readers = N->1 mix, each on its own clock. Device format: whatever Windows mixes in (shared mode);
// engine side is always 48 kHz stereo float.
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FAH.Audio;

public sealed class Destino : IDisposable
{
    public const int Taxa = 48000;
    public readonly string Nome;
    public string NomeReal = "";
    public float Volume = 1;
    public bool Mudo;
    volatile Leitor[] leitores;
    WasapiOut? saida;
    public long Blocos;
    public float Pico;                 // peak since last read by the UI
    public int LatenciaMs;
    public double MaiorVaoMs;          // longest gap between two pulls from Windows (output thread stalled)
    long ultimoPedido;

    public Destino(string nome, double alvoMs = 30) { Nome = nome; leitores = [new Leitor(nome, alvoMs)]; }

    public Leitor Principal => leitores[0];
    public IReadOnlyList<Leitor> Leitores => leitores;

    public Leitor NovoLeitor(string rotulo, double alvoMs = 30)
    {
        var lt = new Leitor(rotulo, alvoMs);
        leitores = [.. leitores, lt];          // swap whole array: the audio thread never sees half a list
        return lt;
    }

    public void Remover(Leitor lt) { if (lt != leitores[0]) leitores = [.. leitores.Where(x => x != lt)]; }

    public void Empurrar(ReadOnlySpan<float> lr) => leitores[0].Empurrar(lr);

    public int CanaisReais { get; private set; }

    public bool Aberto => saida?.PlaybackState == PlaybackState.Playing;

    public void Abrir()
    {
        var ap = Aparelhos.Achar(true, Nome) ?? throw new InvalidOperationException($"saída não encontrada: {Nome}");
        var dev = Aparelhos.Abrir(ap.Id);
        var mix = dev.AudioClient.MixFormat;
        CanaisReais = mix.Channels;
        if (mix.BitsPerSample != 32) throw new NotSupportedException($"{ap.Nome}: formato {mix} (esperado float 32)");
        ISampleProvider fonte = new Soma(this);
        if (mix.SampleRate != Taxa) fonte = new WdlResamplingSampleProvider(fonte, mix.SampleRate);
        // never 0 latency: NAudio's loop waits 3x latency; 0 = busy wait (1 full core per output, measured in old FAH)
        var o = new WasapiOut(dev, AudioClientShareMode.Shared, true, 20);
        o.Init(new ParaCanais(fonte, mix));
        // only a stop FAH did not ask for (device unplugged / invalidated): Fechar clears "saida" first, so its own stops are ignored
        o.PlaybackStopped += (_, e) => { if (ReferenceEquals(saida, o)) AoPerder?.Invoke(e.Exception); };
        o.Play();
        saida = o; NomeReal = ap.Nome; LatenciaMs = 20;
    }

    /// <summary>Raised (once per loss) when the device stops by itself. Handler must be cheap: it runs on NAudio's playback thread.</summary>
    public Action<Exception?>? AoPerder;

    public void Fechar()
    {
        var o = saida; saida = null;
        try { o?.Stop(); o?.Dispose(); } catch { }
    }

    public void Dispose() => Fechar();

    /// <summary>Audio thread: sum of all readers at 48 kHz stereo.</summary>
    internal void Preencher(Span<float> outp, int quadros, Span<float> tmp)
    {
        Blocos++;
        outp[..(quadros * 2)].Clear();
        bool algum = false;
        foreach (var lt in leitores)
        {
            if (!lt.Ler(tmp, quadros) || lt.Mudo) continue;
            float v = (float)lt.Volume;
            for (int i = 0; i < quadros * 2; i++) outp[i] += tmp[i] * v;
            algum = true;
        }
        if (!algum) return;
        float g = Mudo ? 0 : Volume, pk = Pico;
        for (int i = 0; i < quadros * 2; i++) { outp[i] *= g; float a = Math.Abs(outp[i]); if (a > pk) pk = a; }
        Pico = pk;
    }

    sealed class Soma(Destino d) : ISampleProvider
    {
        float[] tmp = new float[4096];
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Taxa, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            if (!Mmcss.Feito) Mmcss.Registrar();
            long agora = System.Diagnostics.Stopwatch.GetTimestamp();
            if (d.ultimoPedido != 0) d.MaiorVaoMs = Math.Max(d.MaiorVaoMs, (agora - d.ultimoPedido) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            d.ultimoPedido = agora;
            int q = count / 2;
            if (tmp.Length < count) tmp = new float[count];
            try { d.Preencher(buffer.AsSpan(offset, q * 2), q, tmp); }
            catch { Array.Clear(buffer, offset, count); }
            return count;
        }
    }

    /// <summary>Stereo float -> device mix format (L/R on the first two channels, rest silent).</summary>
    sealed class ParaCanais(ISampleProvider fonte, WaveFormat mix) : IWaveProvider
    {
        float[] lr = new float[4096];
        public WaveFormat WaveFormat => mix;

        public int Read(byte[] buffer, int offset, int count)
        {
            int ch = mix.Channels, q = count / (4 * ch);
            if (lr.Length < q * 2) lr = new float[q * 2];
            fonte.Read(lr, 0, q * 2);
            var dst = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, q * ch * 4));
            if (ch == 2) lr.AsSpan(0, q * 2).CopyTo(dst);
            else
            {
                dst.Clear();
                for (int i = 0; i < q; i++)
                {
                    if (ch == 1) dst[i] = 0.5f * (lr[2 * i] + lr[2 * i + 1]);
                    else { dst[i * ch] = lr[2 * i]; dst[i * ch + 1] = lr[2 * i + 1]; }
                }
            }
            return q * ch * 4;
        }
    }
}

/// <summary>Opening a device failed: plain sentence for the user (raw detail stays in the log). null = no known cause.</summary>
public static class ErroAudio
{
    const int EmUso = unchecked((int)0x8889000A), ExclusivoNegado = unchecked((int)0x8889000E), FormatoNaoSuportado = unchecked((int)0x88890008),
        Negado = unchecked((int)0x80070005);

    public static string? Amigo(Exception e, bool mic = false)
    {
        for (var x = e; x != null; x = x.InnerException)
        {
            if (x.HResult == EmUso || x.HResult == ExclusivoNegado) return "Outro programa está usando esse aparelho em modo exclusivo.";
            if (x.HResult == FormatoNaoSuportado || x is NotSupportedException) return "Esse aparelho usa um formato que o FAH não aceita.";
            if (mic && (x is UnauthorizedAccessException || x.HResult == Negado)) return "O Windows bloqueou o microfone. Abra Configurações > Privacidade > Microfone e permita apps da área de trabalho.";
        }
        return null;
    }
}

/// <summary>Pro Audio thread priority (MMCSS) for audio threads.</summary>
public static class Mmcss
{
    [System.Runtime.InteropServices.DllImport("avrt.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr AvSetMmThreadCharacteristics(string tarefa, ref uint indice);

    [ThreadStatic] static bool feito;
    public static bool Feito => feito;
    public static void Registrar() { feito = true; try { uint i = 0; AvSetMmThreadCharacteristics("Pro Audio", ref i); } catch { } }
}
