// Sound of ONE program (and its child processes), straight from Windows (process loopback, Windows 10 2004+).
// The program keeps playing where it plays; this is a copy, 48 kHz stereo float, pushed into a reader (e.g. the FAH mic mix).
using System.IO;
using System.Runtime.InteropServices;

namespace FAH.Audio;

public sealed class CapturaPrograma : IDisposable
{
    public readonly uint Pid;
    readonly Leitor destino;
    IAudioClient? cliente;
    IAudioCaptureClient? cap;
    readonly AutoResetEvent evento = new(false);
    Thread? th;
    volatile bool vivo;
    public float Volume = 1;
    /// <summary>01/11 copies sent into the mic: leveled to the voice before Volume (null = raw copy).</summary>
    public NivelPrograma? Nivel;
    public float Pico;
    /// <summary>02/11 (copy with own volume): per packet E = undo the program's session volume (loopback carries it), V = copy volume.
    /// Null = Volume only.</summary>
    public Func<(float E, float V)>? Ganho;
    float eAt = 1, vAt = 1; long eSobe = -1;                 // E in use; when a higher E was first asked (ms)
    readonly System.Diagnostics.Stopwatch rel = System.Diagnostics.Stopwatch.StartNew();

    public CapturaPrograma(uint pid, Leitor destino) { Pid = pid; this.destino = destino; }

    public void Abrir()
    {
        var ap = new ActivationParams { Tipo = 1, Pid = Pid, Modo = 0 };          // PROCESS_LOOPBACK, include the process tree
        int tam = Marshal.SizeOf<ActivationParams>();
        IntPtr pAp = Marshal.AllocHGlobal(tam), pPv = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.StructureToPtr(ap, pAp, false);
            // PROPVARIANT VT_BLOB { cbSize, pBlobData }
            for (int i = 0; i < 24; i += 4) Marshal.WriteInt32(pPv, i, 0);
            Marshal.WriteInt16(pPv, 0, 65); Marshal.WriteInt32(pPv, 8, tam); Marshal.WriteIntPtr(pPv, 16, pAp);
            var fim = new Pronto();
            ActivateAudioInterfaceAsync("VAD\\Process_Loopback", typeof(IAudioClient).GUID, pPv, fim, out var op);
            if (!fim.Feito.WaitOne(3000)) throw new TimeoutException("o Windows não respondeu");
            op.GetActivateResult(out int hr, out object o);
            Marshal.ThrowExceptionForHR(hr);
            cliente = (IAudioClient)o;
        }
        finally { Marshal.FreeHGlobal(pAp); Marshal.FreeHGlobal(pPv); }
        var wf = new Formato { Tag = 3, Canais = 2, Taxa = 48000, Bps = 48000 * 8, Bloco = 8, Bits = 32 };
        const uint Loopback = 0x00020000, Evento = 0x00040000, AutoPcm = 0x80000000, Qualidade = 0x08000000;
        Marshal.ThrowExceptionForHR(cliente.Initialize(0, Loopback | Evento | AutoPcm | Qualidade, 2_000_000, 0, ref wf, IntPtr.Zero));
        Marshal.ThrowExceptionForHR(cliente.SetEventHandle(evento.SafeWaitHandle.DangerousGetHandle()));
        var iid = typeof(IAudioCaptureClient).GUID;
        Marshal.ThrowExceptionForHR(cliente.GetService(ref iid, out var c));
        cap = (IAudioCaptureClient)c;
        vivo = true;
        th = new Thread(Laco) { IsBackground = true, Name = "programa-" + Pid, Priority = ThreadPriority.AboveNormal };
        th.Start();
        Marshal.ThrowExceptionForHR(cliente.Start());
    }

    void Laco()
    {
        Mmcss.Registrar();
        var buf = new float[48000];
        while (vivo)
        {
            evento.WaitOne(100);
            try
            {
                while (vivo && cap!.GetNextPacketSize(out uint n) == 0 && n > 0)
                {
                    if (cap.GetBuffer(out IntPtr p, out uint q, out uint fl, out _, out _) != 0) break;
                    int k = (int)Math.Min(q * 2, (uint)buf.Length);
                    if ((fl & 2) != 0) Array.Clear(buf, 0, k); else Marshal.Copy(p, buf, 0, k);   // 2 = AUDCLNT_BUFFERFLAGS_SILENT
                    cap.ReleaseBuffer(q);
                    float v = Volume, pk = Pico;
                    if (Ganho != null) { (float e, v) = Ganho(); Volume = v; Desfazer(buf.AsSpan(0, k), e); }
                    if (Nivel != null) Nivel.Processar(buf.AsSpan(0, k), v);
                    else { float d = (v - vAt) / Math.Max(1, k / 2); for (int i = 0; i + 1 < k; i += 2) { vAt += d; buf[i] *= vAt; buf[i + 1] *= vAt; } vAt = v; }
                    if (Ganho != null) for (int i = 0; i < k; i++) buf[i] = Math.Clamp(buf[i], -1f, 1f);
                    for (int i = 0; i < k; i++) { float a = Math.Abs(buf[i]); if (a > pk) pk = a; }
                    Pico = pk;
                    destino.Empurrar(buf.AsSpan(0, k));
                }
            }
            catch { Thread.Sleep(50); }
        }
    }

    /// <summary>buf *= E, ramped over the packet. A lower E applies at once; a higher one only after 120 ms (the samples already in
    /// flight still carry the old, louder session volume for ~50 ms, measured by FAH.Testes --piso: raising at once would blast them).</summary>
    void Desfazer(Span<float> b, float e)
    {
        e = float.IsFinite(e) ? Math.Clamp(e, 0, 1f / Mesa.Piso) : 1;
        long t = rel.ElapsedMilliseconds; float alvo = eAt;
        if (e <= eAt * 1.001f) { alvo = e; eSobe = -1; }
        else if (eSobe < 0) eSobe = t;
        else if (t - eSobe >= 120) { alvo = e; eSobe = -1; }
        int q = b.Length / 2; float d = (alvo - eAt) / Math.Max(1, q), g = eAt;
        for (int i = 0; i < q; i++) { g += d; b[2 * i] *= g; b[2 * i + 1] *= g; }
        eAt = alvo;
    }

    public void Dispose()
    {
        vivo = false; evento.Set();
        th?.Join(1000);
        try { cliente?.Stop(); } catch { }
        if (cap != null) Marshal.ReleaseComObject(cap);
        if (cliente != null) Marshal.ReleaseComObject(cliente);
        cap = null; cliente = null;
    }

    /// <summary>Top processes of a program (name without .exe): those whose parent is not the same program. Capturing their
    /// tree takes every child (browser tabs, Discord helpers) once, with the Mixer closed too.</summary>
    public static uint[] Raizes(string nome)
    {
        var mapa = new Dictionary<uint, (uint pai, string exe)>();
        IntPtr h = CreateToolhelp32Snapshot(2, 0);                     // TH32CS_SNAPPROCESS
        if (h == IntPtr.Zero || h == new IntPtr(-1)) return [];
        try
        {
            var e = new Entrada { Tam = (uint)Marshal.SizeOf<Entrada>() };
            for (bool ok = Process32FirstW(h, ref e); ok; ok = Process32NextW(h, ref e)) mapa[e.Pid] = (e.Pai, Path.GetFileNameWithoutExtension(e.Exe));
        }
        finally { CloseHandle(h); }
        bool E(uint pid) => mapa.TryGetValue(pid, out var x) && x.exe.Equals(nome, StringComparison.OrdinalIgnoreCase);
        return [.. mapa.Where(k => E(k.Key) && !E(k.Value.pai)).Select(k => k.Key)];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Entrada
    {
        public uint Tam, Uso, Pid; public IntPtr Heap; public uint Modulo, Threads, Pai; public int Prioridade; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint f, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr h, ref Entrada e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr h, ref Entrada e);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    // ---- interop
    [StructLayout(LayoutKind.Sequential)] struct ActivationParams { public int Tipo; public uint Pid; public int Modo; }
    [StructLayout(LayoutKind.Sequential, Pack = 2)] struct Formato { public ushort Tag, Canais; public uint Taxa, Bps; public ushort Bloco, Bits, Extra; }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void ActivateAudioInterfaceAsync([MarshalAs(UnmanagedType.LPWStr)] string caminho, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        IntPtr parametros, IActivateAudioInterfaceCompletionHandler fim, out IActivateAudioInterfaceAsyncOperation op);

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateAudioInterfaceCompletionHandler { void ActivateCompleted(IActivateAudioInterfaceAsyncOperation op); }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IActivateAudioInterfaceAsyncOperation { void GetActivateResult(out int hr, [MarshalAs(UnmanagedType.IUnknown)] out object iface); }

    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAgileObject { }

    sealed class Pronto : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public readonly ManualResetEvent Feito = new(false);
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation op) => Feito.Set();
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int modo, uint flags, long duracao, long periodo, ref Formato f, IntPtr sessao);
        [PreserveSig] int GetBufferSize(out uint q);
        [PreserveSig] int GetStreamLatency(out long l);
        [PreserveSig] int GetCurrentPadding(out uint q);
        [PreserveSig] int IsFormatSupported(int modo, IntPtr f, out IntPtr perto);
        [PreserveSig] int GetMixFormat(out IntPtr f);
        [PreserveSig] int GetDevicePeriod(out long a, out long b);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr h);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object s);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr dados, out uint quadros, out uint flags, out ulong pos, out ulong qpc);
        [PreserveSig] int ReleaseBuffer(uint quadros);
        [PreserveSig] int GetNextPacketSize(out uint quadros);
    }
}
