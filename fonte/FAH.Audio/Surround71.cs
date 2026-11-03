// 7.1 on the system cable without the VAC panel (port of sistema/surround71.py):
// 1. VAC cable 1 channel range 1..8 (IOCTL SetDataRange, no admin).
// 2. Windows: play side = 7.1 speakers (PhysicalSpeakers 0x63F) and BOTH sides 8 ch / 48 kHz (IPolicyConfig.SetDeviceFormat,
//    device PCM32 + mix float32). Games ask Windows for the format and start sending 7.1.
// 3. Fallback when Windows keeps the old format list: registry + driver restart in an elevated process (one UAC).
// Off = back to 2 channels. All reversible.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FAH.Audio;

public static class Surround71
{
    const uint Mascara71 = 0x63F, Mascara20 = 0x3;
    const string GuidProduto = "{83ed7f0e-2028-4956-b0b4-39c76fdaef1d}", GuidApi = "0d13c528-a6fc-47f1-8fee-c5dd46f1fbb1";
    static uint apiVer = 7;
    public static Action<string>? Log;

    public sealed record Situacao(int VacMax, int Tocar, int Captar, int Gravando, int Tocando);

    // ------------------------------------------------------------------ VAC driver
    [StructLayout(LayoutKind.Sequential)] struct IfData { public int cb; public Guid g; public int flags; public IntPtr res; }
    [StructLayout(LayoutKind.Sequential)] struct DevInfo { public int cb; public Guid g; public int inst; public IntPtr res; }
    [StructLayout(LayoutKind.Sequential)] struct PropChange { public int cb, dif, estado, escopo, perfil; }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SetupDiGetClassDevsW(ref Guid g, string? e, IntPtr h, int f);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr l, IntPtr d, ref Guid g, int i, ref IfData o);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr l, ref IfData d, IntPtr buf, int n, IntPtr req, ref DevInfo di);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiSetClassInstallParamsW(IntPtr l, ref DevInfo di, ref PropChange p, int n);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiCallClassInstaller(int dif, IntPtr l, ref DevInfo di);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateFileW(string p, uint a, int s, IntPtr sa, int c, int f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool DeviceIoControl(IntPtr h, uint c, byte[] i, int ni, byte[]? o, int no, out int r, IntPtr ov);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    /// <summary>Device interface path + DevInfo (for the driver restart). null = VAC not installed.</summary>
    static string? CaminhoApi(out DevInfo di, bool reiniciar = false)
    {
        di = new DevInfo { cb = Marshal.SizeOf<DevInfo>() };
        var g = new Guid(GuidProduto);
        var l = SetupDiGetClassDevsW(ref g, null, IntPtr.Zero, 0x12);
        if (l == IntPtr.Zero || l == new IntPtr(-1)) return null;
        var buf = Marshal.AllocHGlobal(4 + 2 * 1024);
        try
        {
            var d = new IfData { cb = Marshal.SizeOf<IfData>() };
            if (!SetupDiEnumDeviceInterfaces(l, IntPtr.Zero, ref g, 0, ref d)) return null;
            Marshal.WriteInt32(buf, 8);
            if (!SetupDiGetDeviceInterfaceDetailW(l, ref d, buf, 4 + 2 * 1024, IntPtr.Zero, ref di)) return null;
            var p = Marshal.PtrToStringUni(buf + 4)!;
            if (reiniciar)
            {
                var pc = new PropChange { cb = 8, dif = 0x12, estado = 3, escopo = 1 };
                if (!SetupDiSetClassInstallParamsW(l, ref di, ref pc, Marshal.SizeOf<PropChange>())) { Log?.Invoke($"SetClassInstallParams: {Marshal.GetLastWin32Error()}"); return null; }
                if (!SetupDiCallClassInstaller(0x12, l, ref di)) { Log?.Invoke($"CallClassInstaller: {Marshal.GetLastWin32Error()}"); return null; }
            }
            return p + "\\" + GuidApi;
        }
        finally { Marshal.FreeHGlobal(buf); SetupDiDestroyDeviceInfoList(l); }
    }

    static uint Ctl(int func, int acesso) => (0x22u << 16) | ((uint)acesso << 14) | ((uint)func << 2);

    /// <summary>CableInfo (API 7, 328 bytes): faixa at 12 (6 uint: sr_min sr_max bps_min bps_max nc_min nc_max), entradas 76, saidas 80.</summary>
    static byte[]? InfoCabo(IntPtr h, int cabo)
    {
        var o = new byte[328];
        foreach (var v in new[] { apiVer, 7u, 6u, 8u, 5u }.Distinct())
        {
            if (!DeviceIoControl(h, Ctl(0x801, 1), BitConverter.GetBytes(v), 4, new byte[80], 80, out _, IntPtr.Zero)) continue;
            apiVer = v;
            var pedido = BitConverter.GetBytes(v).Concat(BitConverter.GetBytes(cabo - 1)).ToArray();
            return DeviceIoControl(h, Ctl(0x802, 1), pedido, 8, o, o.Length, out _, IntPtr.Zero) ? o : null;
        }
        return null;
    }

    static IntPtr AbrirVac(bool escrita)
    {
        var c = CaminhoApi(out _);
        if (c == null) return IntPtr.Zero;
        var h = CreateFileW(c, 0x80000000 | (escrita ? 0x40000000u : 0), 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
        return h == new IntPtr(-1) ? IntPtr.Zero : h;
    }

    static int bitsMax = 32;

    static (int max, int grav, int toc) LerVac()
    {
        var h = AbrirVac(false);
        if (h == IntPtr.Zero) return (0, 0, 0);
        try { var i = InfoCabo(h, 1); if (i != null) bitsMax = BitConverter.ToInt32(i, 24); return i == null ? (0, 0, 0) : (BitConverter.ToInt32(i, 32), BitConverter.ToInt32(i, 76), BitConverter.ToInt32(i, 80)); }
        finally { CloseHandle(h); }
    }

    static bool VacCanais(int maximo)
    {
        var h = AbrirVac(true);
        if (h == IntPtr.Zero) return false;
        try
        {
            var i = InfoCabo(h, 1);
            if (i == null) return false;
            var p = new List<byte>();
            foreach (var x in new[] { (int)apiVer, 0, BitConverter.ToInt32(i, 12), BitConverter.ToInt32(i, 16), BitConverter.ToInt32(i, 20), BitConverter.ToInt32(i, 24), 1, maximo })
                p.AddRange(BitConverter.GetBytes(x));
            if (!DeviceIoControl(h, Ctl(0x806, 2), [.. p], p.Count, null, 0, out _, IntPtr.Zero)) { Log?.Invoke($"VAC SetDataRange recusado: {Marshal.GetLastWin32Error()}"); return false; }
            var j = InfoCabo(h, 1);
            return j != null && BitConverter.ToInt32(j, 32) == maximo;
        }
        finally { CloseHandle(h); }
    }

    // ------------------------------------------------------------------ Windows (IPolicyConfig)
    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] class PolicyConfigObj { }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string d, out IntPtr f);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d, int pad, out IntPtr f);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d, [MarshalAs(UnmanagedType.LPArray)] byte[] aparelho, [MarshalAs(UnmanagedType.LPArray)] byte[] mistura);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string d, int pad, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr a);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr m);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr m);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string d, int fx, IntPtr k, IntPtr v);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string d, int fx, [MarshalAs(UnmanagedType.LPArray)] byte[] k, [MarshalAs(UnmanagedType.LPArray)] byte[] v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string d, int papel);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string d, int v);
    }

    static IPolicyConfig Pc() => (IPolicyConfig)new PolicyConfigObj();

    /// <summary>WAVEFORMATEXTENSIBLE 40 bytes, 48 kHz, 32 bit (bits = PCM width for the device side).</summary>
    static byte[] Formato(int canais, uint mascara, bool flutuante, int bits = 32)
    {
        var b = new byte[40];
        void W16(int o, int v) => BitConverter.GetBytes((ushort)v).CopyTo(b, o);
        void W32(int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
        W16(0, 0xFFFE); W16(2, canais); W32(4, 48000); W32(8, (uint)(48000 * canais * bits / 8)); W16(12, canais * bits / 8); W16(14, bits);
        W16(16, 22); W16(18, bits); W32(20, mascara);
        new Guid(flutuante ? "00000003-0000-0010-8000-00aa00389b71" : "00000001-0000-0010-8000-00aa00389b71").ToByteArray().CopyTo(b, 24);
        return b;
    }

    static bool DefinirFormato(string id, int canais, uint mascara)
    {
        // device = PCM int at the widest the cable allows (32 on 14/10, 16 on 27/10: VAC range bps_max); mix = float 32.
        // Float for the device gave UNSUPPORTED_FORMAT.
        int hr = 1;
        foreach (var bits in new[] { 32, 24, 16 }.Where(b => b <= Math.Max(16, bitsMax)))
            if ((hr = Pc().SetDeviceFormat(id, Formato(canais, mascara, false, bits), Formato(canais, mascara, true))) == 0) break;
        if (hr != 0) Log?.Invoke($"SetDeviceFormat {canais} canais: 0x{hr:x8}");
        return hr == 0;
    }

    static bool DefinirAltoFalantes(string id, uint mascara)
    {
        var k = new byte[20]; new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e").ToByteArray().CopyTo(k, 0); BitConverter.GetBytes(3).CopyTo(k, 16);
        var v = new byte[24]; BitConverter.GetBytes((ushort)19).CopyTo(v, 0); BitConverter.GetBytes(mascara).CopyTo(v, 8);   // VT_UI4
        int hr = Pc().SetPropertyValue(id, 0, k, v);
        if (hr != 0) Log?.Invoke($"PhysicalSpeakers: 0x{hr:x8}");
        return hr == 0;
    }

    static int CanaisDoAparelho(string id)
    {
        try
        {
            using var d = Aparelhos.Abrir(id);
            var blob = d.Properties[new NAudio.CoreAudioApi.PropertyKey(new Guid("f19f064d-082c-4e27-bc73-6882a1bb8e4c"), 0)].Value as byte[];
            return blob != null && blob.Length >= 4 ? BitConverter.ToUInt16(blob, 2) : 0;
        }
        catch { return 0; }
    }

    // ------------------------------------------------------------------ public
    public static Situacao Estado(string tocar, string captar)
    {
        var (max, g, t) = LerVac();
        var dt = Aparelhos.Achar(true, tocar); var dc = Aparelhos.Achar(false, captar);
        return new(max, dt == null ? 0 : CanaisDoAparelho(dt.Id), dc == null ? 0 : CanaisDoAparelho(dc.Id), g, t);
    }

    static void EsperarLivre(double s)
    {
        var fim = DateTime.UtcNow.AddSeconds(s);
        while (DateTime.UtcNow < fim) { var (_, g, t) = LerVac(); if (g == 0 && t == 0) return; Thread.Sleep(300); }
    }

    /// <summary>Lookups done while the audio still plays (28/10: they were ~350 ms of the silence): both cable sides + VAC.</summary>
    public sealed record Preparo(Aparelho Tocar, Aparelho Captar, int VacMax);

    public static Preparo? Preparar(string tocar, string captar)
    {
        var dt = Aparelhos.Achar(true, tocar); var dc = Aparelhos.Achar(false, captar);
        return dt == null || dc == null ? null : new(dt, dc, LerVac().max);
    }

    /// <summary>Only the Windows format change (the part that needs the cable free; ~50 ms when direct).
    /// Caller must have closed its own capture of the cable. false = could not, even after waiting/admin.</summary>
    public static bool Trocar(Preparo p, bool ligar)
    {
        int canais = ligar ? 8 : 2; uint mascara = ligar ? Mascara71 : Mascara20;
        var sw = Stopwatch.StartNew();
        string dt = p.Tocar.Id, dc = p.Captar.Id;
        bool ok;
        if (ligar)
        {
            if (p.VacMax < 8) VacCanais(8);
            ok = DefinirFormato(dt, canais, mascara) && DefinirFormato(dc, canais, mascara);
            if (!ok)
            {
                EsperarLivre(6);
                ok = DefinirFormato(dt, canais, mascara) && DefinirFormato(dc, canais, mascara);
                if (!ok)
                {
                    if (!PedirAdmin(8)) { Log?.Invoke("surround: administrador recusado"); return false; }
                    Thread.Sleep(3000);
                    var nt = Aparelhos.Achar(true, p.Tocar.Nome); var nc = Aparelhos.Achar(false, p.Captar.Nome);
                    if (nt == null || nc == null) { Log?.Invoke("surround: o cabo sumiu depois de reiniciar o driver"); return false; }
                    dt = nt.Id; dc = nc.Id;
                    EsperarLivre(6);
                    ok = DefinirFormato(dt, canais, mascara) && DefinirFormato(dc, canais, mascara);
                }
            }
            DefinirAltoFalantes(dt, mascara);
        }
        else
        {
            DefinirAltoFalantes(dt, mascara);
            ok = DefinirFormato(dt, canais, mascara) & DefinirFormato(dc, canais, mascara);
        }
        Log?.Invoke($"surround {(ligar ? "7.1" : "estéreo")}: formato em {sw.ElapsedMilliseconds} ms");
        return ok;
    }

    /// <summary>After the switch (audio already back): what Windows ended up with -> (ok, message for the screen).</summary>
    public static (bool ok, string msg) Conferir(string tocar, string captar, bool ligar)
    {
        int canais = ligar ? 8 : 2;
        var e = Estado(tocar, captar);
        Log?.Invoke($"surround {(ligar ? "7.1" : "estéreo")}: {e}");
        if (e.Tocar == canais && e.Captar == canais)
            return (true, ligar ? "Cabo em 7.1: jogos e filmes já podem mandar surround" : "Cabo de volta ao estéreo");
        var ocupado = e.Gravando + e.Tocando > 0 ? $" O cabo ainda está em uso ({e.Tocando} tocando, {e.Gravando} gravando): feche o que estiver tocando e tente de novo." : "";
        return (false, $"O Windows ficou com o cabo em {e.Tocar} canais (esperado {canais}).{ocupado}");
    }

    /// <summary>ligar = 7.1, false = stereo, all steps (tests / --cabo71). Caller must have closed its own capture.</summary>
    public static (bool ok, string msg) Ativar(string tocar, string captar, bool ligar)
    {
        var p = Preparar(tocar, captar);
        if (p == null) return (false, "Não achei os dois lados do cabo no Windows");
        if (!Trocar(p, ligar) && ligar && Estado(tocar, captar).Tocar != 8) return (false, "Precisa da permissão de administrador (o Windows pergunta uma vez)");
        return Conferir(tocar, captar, ligar);
    }

    // ------------------------------------------------------------------ admin part (one UAC)
    const string Servico = @"SYSTEM\CurrentControlSet\Services\VirtualAudioCable_83ed7f0e\Parameters", App = @"SOFTWARE\EuMus Design\Virtual Audio Cable\4";

    /// <summary>Runs in the ELEVATED process (--surround71-admin N): registry + driver restart. 0 = ok.</summary>
    public static int ParteAdmin(int maximo)
    {
        try
        {
            using var hk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var b in new[] { Servico, App })
                using (var k = hk.CreateSubKey(b + @"\Cable 1")) k.SetValue("Maximum number of channels", maximo, RegistryValueKind.DWord);
        }
        catch (Exception e) { Log?.Invoke("registro do VAC: " + e.Message); return 2; }
        return CaminhoApi(out _, reiniciar: true) != null ? 0 : 3;
    }

    static bool PedirAdmin(int maximo)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--surround71-admin {maximo}") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            if (p == null) return false;
            p.WaitForExit(60000);
            Log?.Invoke($"parte de administrador terminou: {p.ExitCode}");
            return p.ExitCode == 0;
        }
        catch (Exception e) { Log?.Invoke("UAC recusado: " + e.Message); return false; }
    }
}
