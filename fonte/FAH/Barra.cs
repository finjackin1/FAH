// Taskbar wheel (31/10): the volume change itself is the Windhawk mod "Taskbar Volume Control Per-App" (user's choice; it changes the
// program's sessions on the Windows default device, FAH's Mixer follows them like any outside change). FAH only gates it: wheel over the
// icon of a program the Mixer does NOT show is swallowed (the mod never sees it); over a Mixer program it passes untouched.
// Low-level mouse hook on its own thread (a busy UI thread would lag the whole mouse). Icon rects are read by UI Automation
// ("Appid: ..." buttons, ~20 ms) on a worker when the mouse is over the taskbar, at most once a second.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using FAH.Audio;

namespace FAH;

sealed class Barra
{
    readonly Mesa mesa;
    readonly HookProc proc;
    volatile (int L, int T, int R, int B, bool Ok)[] alvos = [];   // Ok = the Mixer shows this program
    long lido = -99999; int lendo;
    uint fio;

    public Barra(Mesa m)
    {
        mesa = m; proc = Gancho;
        var t = new Thread(() =>
        {
            fio = GetCurrentThreadId();
            var h = SetWindowsHookEx(14, proc, GetModuleHandle(null), 0);   // WH_MOUSE_LL
            if (h == IntPtr.Zero) { Base.Log("barra: gancho falhou " + Marshal.GetLastWin32Error()); return; }
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { }
            UnhookWindowsHookEx(h);
        }) { IsBackground = true, Name = "barra" };
        t.Start();
    }

    public void Parar() { if (fio != 0) PostThreadMessage(fio, 0x12, IntPtr.Zero, IntPtr.Zero); }   // WM_QUIT

    IntPtr Gancho(int code, IntPtr wp, IntPtr lp)
    {
        if (code >= 0)
        {
            int msg = (int)wp;
            if (msg == 0x200 && Environment.TickCount64 - lido > 1000)          // WM_MOUSEMOVE: refresh icons while over the taskbar
            {
                var h = Marshal.PtrToStructure<MSLL>(lp);
                if (NaBarra(h.X, h.Y)) Ler();
            }
            else if (msg == 0x20A)                                              // WM_MOUSEWHEEL
            {
                var h = Marshal.PtrToStructure<MSLL>(lp);
                foreach (var a in alvos)
                    if (h.X >= a.L && h.X < a.R && h.Y >= a.T && h.Y < a.B && NaBarra(h.X, h.Y))
                    {
                        if (Environment.TickCount64 - lido > 1000) Ler();
                        if (!a.Ok) return 1;                                           // not in the Mixer: the mod must not touch it
                        break;
                    }
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wp, lp);
    }

    static bool NaBarra(int x, int y)
    {
        var w = GetAncestor(WindowFromPoint(new PT { X = x, Y = y }), 2);   // GA_ROOT
        if (w == IntPtr.Zero) return false;
        var b = new System.Text.StringBuilder(64); GetClassName(w, b, b.Capacity);
        var c = b.ToString(); return c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
    }

    void Ler()
    {
        lido = Environment.TickCount64;
        if (Interlocked.Exchange(ref lendo, 1) == 1) return;
        Task.Run(() =>
        {
            try { alvos = Montar(); }
            catch (Exception e) { Base.Log("barra: " + e.Message); }
            finally { lido = Environment.TickCount64; lendo = 0; }
        });
    }

    (int, int, int, int, bool)[] Montar()
    {
        var ok = mesa.Elegiveis();
        var r = new List<(int, int, int, int, bool)>();
        var cond = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");
        Dictionary<string, HashSet<string>>? janelas = null;
        foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            for (IntPtr w = IntPtr.Zero; (w = FindWindowEx(IntPtr.Zero, w, cls, null)) != IntPtr.Zero;)
                foreach (AutomationElement b in AutomationElement.FromHandle(w).FindAll(TreeScope.Descendants, cond))
                {
                    string id = b.Current.AutomationId; if (!id.StartsWith("Appid: ")) continue; id = id[7..];
                    var q = b.Current.BoundingRectangle; if (q.IsEmpty) continue;
                    r.Add(((int)q.Left, (int)q.Top, (int)q.Right, (int)q.Bottom, ok.Count > 0 && Exe(id, ok, ref janelas) != null));
                }
        return [.. r];
    }

    /// <summary>Taskbar AppID -> exe name among the allowed ones: exe path; AppID set on a window (Chrome profiles, Electron);
    /// packaged app id; last, an AppID part equal to the exe name ("Brave" -> brave, "com.squirrel.Discord.Discord" -> discord).</summary>
    static string? Exe(string id, HashSet<string> ok, ref Dictionary<string, HashSet<string>>? janelas)
    {
        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { var n = Path.GetFileNameWithoutExtension(id); return ok.Contains(n) ? n : null; }
        janelas ??= Janelas(ok);
        if (janelas.TryGetValue(id, out var xs)) foreach (var x in xs) if (ok.Contains(x)) return x;
        foreach (var p in id.Split('.', '\\', '_', '-', ' ', '!')) if (ok.Contains(p)) return ok.First(o => o.Equals(p, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    /// <summary>AppID -> exe names, from visible windows (window AppID) and allowed processes (packaged app id).</summary>
    static Dictionary<string, HashSet<string>> Janelas(HashSet<string> ok)
    {
        var d = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void Poe(string id, string x) { if (id == "") return; if (!d.TryGetValue(id, out var s)) d[id] = s = new(StringComparer.OrdinalIgnoreCase); s.Add(x); }
        var nomes = new Dictionary<uint, string>();
        string Nome(uint pid) { if (nomes.TryGetValue(pid, out var n)) return n; try { using var p = Process.GetProcessById((int)pid); n = p.ProcessName; } catch { n = ""; } return nomes[pid] = n; }
        EnumWindows((w, _) =>
        {
            if (!IsWindowVisible(w)) return true;
            GetWindowThreadProcessId(w, out var pid); var x = Nome(pid); if (!ok.Contains(x)) return true;
            Poe(AppIdJanela(w), x); return true;
        }, IntPtr.Zero);
        foreach (var x in ok)
            foreach (var p in Process.GetProcessesByName(x))
                using (p)
                {
                    var h = OpenProcess(0x1000, false, (uint)p.Id); if (h == IntPtr.Zero) continue;
                    try { int n = 260; var b = new System.Text.StringBuilder(n); if (GetApplicationUserModelId(h, ref n, b) == 0) Poe(b.ToString(), x); }
                    finally { CloseHandle(h); }
                }
        return d;
    }

    static string AppIdJanela(IntPtr w)
    {
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(w, ref iid, out var ps) != 0 || ps == null) return "";
        try
        {
            var k = new PKEY { G = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), P = 5 };   // PKEY_AppUserModel_ID
            var v = new PROPVARIANT(); if (ps.GetValue(ref k, ref v) != 0) return "";
            try { return v.Tipo == 31 ? Marshal.PtrToStringUni(v.Ptr) ?? "" : ""; } finally { PropVariantClear(ref v); }   // VT_LPWSTR
        }
        finally { Marshal.ReleaseComObject(ps); }
    }

    // ---- Win32
    delegate IntPtr HookProc(int code, IntPtr wp, IntPtr lp);
    delegate bool EnumProc(IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct PT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MSLL { public int X, Y; public uint Dados, Flags, Tempo; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr H; public uint M; public IntPtr W, L; public uint T; public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct PKEY { public Guid G; public uint P; }
    [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort Tipo, r1, r2, r3; public IntPtr Ptr, Ptr2; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint n);
        [PreserveSig] int GetAt(uint i, out PKEY k);
        [PreserveSig] int GetValue(ref PKEY k, ref PROPVARIANT v);
    }
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc f, IntPtr mod, uint fio);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint fio, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(PT p);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr w, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr w, System.Text.StringBuilder b, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string cls, string? t);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr w);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr w, out uint pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? n);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint acesso, bool herda, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr h, ref int n, System.Text.StringBuilder b);
    [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr w, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ps);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);
}
