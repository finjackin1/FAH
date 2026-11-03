// Which KIND of sound is playing (Música / Filme / Jogo), so the 7.1 picks its profile alone (port of sistema/conteudo.py).
// By the PROGRAM making sound now (Windows audio sessions with peak > 0), not by the sound itself (spectrum guesses badly):
// known music player = Música; video player = Filme; browser by window title; game = .exe in a games folder, fullscreen
// window that is not browser/player, or Direct3D/Vulkan loaded. Several playing: Jogo > Filme > Música. Nothing known: keep last.
// Switches only after the same answer for TrocarS seconds.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.CoreAudioApi;

namespace FAH.Espacial;

public static class Conteudo
{
    public const double TrocarS = 4;
    static readonly string[] Prioridade = ["Jogo", "Filme", "Música"];

    static readonly HashSet<string> Musica = ["spotify.exe", "foobar2000.exe", "musicbee.exe", "aimp.exe", "itunes.exe", "tidal.exe", "deezer.exe",
        "winamp.exe", "amazon music.exe", "amazonmusic.exe", "music.ui.exe", "applemusic.exe", "qobuz.exe", "dopamine.exe", "clementine.exe",
        "strawberry.exe", "youtube music.exe", "soundcloud.exe", "audacious.exe", "mediamonkey.exe", "plexamp.exe"];
    static readonly HashSet<string> Filme = ["vlc.exe", "mpc-hc.exe", "mpc-hc64.exe", "mpc-be.exe", "mpc-be64.exe", "potplayer.exe", "potplayermini64.exe",
        "potplayermini.exe", "mpv.exe", "kmplayer.exe", "video.ui.exe", "netflix.exe", "disneyplus.exe", "stremio.exe", "plex.exe", "kodi.exe",
        "microsoft.media.player.exe", "wmplayer.exe", "smplayer.exe", "gom.exe", "primevideo.exe", "jellyfin.exe", "mpvnet.exe"];
    static readonly HashSet<string> Navegadores = ["chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "opera_gx.exe", "brave.exe", "vivaldi.exe",
        "arc.exe", "zen.exe", "librewolf.exe", "waterfox.exe", "floorp.exe"];
    // decide nothing (voice, system, launchers, ourselves)
    static readonly HashSet<string> Ignorar = ["discord.exe", "teamspeak.exe", "ts3client_win64.exe", "slack.exe", "teams.exe", "ms-teams.exe",
        "zoom.exe", "skype.exe", "whatsapp.exe", "telegram.exe", "steam.exe", "steamwebhelper.exe", "epicgameslauncher.exe", "explorer.exe",
        "fah.exe", "powerampc.exe", "python.exe", "pythonw.exe", "shellexperiencehost.exe", "systemsettings.exe", "audiodg.exe", "obs64.exe",
        "nvcontainer.exe", "radeonsoftware.exe", "battle.net.exe", "riotclientservices.exe", "eadesktop.exe", "upc.exe", "galaxyclient.exe",
        "xboxpcapp.exe", "gamebar.exe", "steelseriesgg.exe", "steelseriessonar.exe", "wallpaper32.exe", "wallpaper64.exe", "lively.exe",
        "msedgewebview2.exe"];
    static readonly string[] Dll3d = ["d3d9.dll", "d3d11.dll", "d3d12.dll", "vulkan-1.dll"];
    static readonly string[] TituloMusica = ["youtube music", "spotify", "deezer", "soundcloud", "tidal", "apple music", "amazon music",
        "bandcamp", "last.fm", "qobuz", "rádio", "radio"];
    static readonly string[] TituloFilme = ["youtube", "netflix", "prime video", "disney+", "hbo max", "twitch", "crunchyroll", "globoplay",
        "star+", "paramount+", "apple tv", "vimeo", "pluto tv", "mubi", "filme", "série"];
    static readonly string[] PastasJogo = [@"\steamapps\common\", @"\epic games\", @"\gog galaxy\games\", @"\gog games\", @"\xboxgames\",
        @"\riot games\", @"\battle.net\", @"\ea games\", @"\electronic arts\", @"\ubisoft game launcher\games\", @"\ubisoft\",
        @"\rockstar games\", @"\games", @"\jogos"];

    /// <summary>"Música" | "Filme" | "Jogo" | null (unknown / doesn't count). Pure. usa3d is called last (reading modules costs).</summary>
    public static string? Classificar(string exe, string caminho = "", IEnumerable<string>? titulos = null, bool telaCheia = false, Func<bool>? usa3d = null)
    {
        var e = (exe ?? "").ToLowerInvariant();
        if (e.Length == 0 || Ignorar.Contains(e) || e.StartsWith("scrcpy")) return null;
        if (Musica.Contains(e)) return "Música";
        if (Filme.Contains(e)) return "Filme";
        if (Navegadores.Contains(e))
        {
            var t = string.Join(' ', titulos ?? []).ToLowerInvariant();
            if (TituloMusica.Any(t.Contains)) return "Música";           // "YouTube Music" before "YouTube"
            return TituloFilme.Any(t.Contains) || telaCheia ? "Filme" : null;
        }
        var c = (caminho ?? "").ToLowerInvariant();
        if (PastasJogo.Any(c.Contains) || telaCheia) return "Jogo";
        return usa3d != null && usa3d() ? "Jogo" : null;
    }

    public static string? Escolher(IEnumerable<string> tipos) { var l = tipos.ToList(); return Prioridade.FirstOrDefault(l.Contains); }

    /// <summary>Decided type with hysteresis. Observar() every ~2 s with Tocando().</summary>
    public sealed class Detector(string inicial)
    {
        public string Tipo { get; private set; } = inicial;
        public string Fonte { get; private set; } = "";
        string? candidato; double desde;

        /// <summary>True when the type changed now. agora = seconds (monotonic).</summary>
        public bool Decidir(string? tipo, string fonte, double agora)
        {
            if (tipo == null || tipo == Tipo) { candidato = null; if (tipo == Tipo && fonte != "") Fonte = fonte; return false; }
            if (tipo != candidato) { candidato = tipo; desde = agora; return false; }
            if (agora - desde < TrocarS) return false;
            Tipo = tipo; Fonte = fonte; candidato = null;
            return true;
        }

        public bool Observar(IReadOnlyList<(string tipo, string exe)> achados, double agora)
        {
            var tipo = Escolher(achados.Select(a => a.tipo));
            return Decidir(tipo, achados.FirstOrDefault(a => a.tipo == tipo).exe ?? "", agora);
        }

        public void Forcar(string tipo) { Tipo = tipo; candidato = null; }
    }

    // ------------------------------------------------------------------ Windows
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int l, t, r, b; }
    [StructLayout(LayoutKind.Sequential)] struct MonInfo { public int cb; public Rect mon, trab; public int flags; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowTextLengthW(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, int f);
    [DllImport("user32.dll")] static extern bool GetMonitorInfoW(IntPtr m, ref MonInfo i);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(int a, bool h, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, int f, StringBuilder s, ref int n);
    [DllImport("psapi.dll")] static extern bool EnumProcessModulesEx(IntPtr h, [Out] IntPtr[] m, int cb, out int prec, int f);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)] static extern int GetModuleBaseNameW(IntPtr h, IntPtr m, StringBuilder s, int n);

    /// <summary>pid -> (titles of visible windows, has the foreground window covering its whole monitor).</summary>
    static Dictionary<int, (List<string> titulos, bool cheia)> Janelas()
    {
        var fg = GetForegroundWindow();
        var saida = new Dictionary<int, (List<string>, bool)>();
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            int n = GetWindowTextLengthW(h); var sb = new StringBuilder(n + 1); GetWindowTextW(h, sb, n + 1);
            GetWindowThreadProcessId(h, out int pid);
            bool cheia = false;
            if (h == fg && GetWindowRect(h, out var r))
            {
                var mi = new MonInfo { cb = Marshal.SizeOf<MonInfo>() };
                cheia = GetMonitorInfoW(MonitorFromWindow(h, 2), ref mi) && r.l <= mi.mon.l && r.t <= mi.mon.t && r.r >= mi.mon.r && r.b >= mi.mon.b;
            }
            if (!saida.TryGetValue(pid, out var v)) saida[pid] = v = ([], false);
            v.Item1.Add(sb.ToString());
            if (cheia) saida[pid] = (v.Item1, true);
            return true;
        }, IntPtr.Zero);
        return saida;
    }

    static string Caminho(int pid)
    {
        var h = OpenProcess(0x1000, false, pid);                 // QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return "";
        try { var sb = new StringBuilder(1024); int n = sb.Capacity; return QueryFullProcessImageNameW(h, 0, sb, ref n) ? sb.ToString() : ""; }
        finally { CloseHandle(h); }
    }

    static readonly Dictionary<(int, string), bool> cache3d = [];

    /// <summary>Direct3D/Vulkan loaded (once per process). No access (anti-cheat): false.</summary>
    static bool Usa3d(int pid, string caminho)
    {
        if (cache3d.TryGetValue((pid, caminho), out var s)) return s;
        if (cache3d.Count > 200) cache3d.Clear();
        bool r = false;
        var h = OpenProcess(0x0410, false, pid);                 // QUERY_INFORMATION | VM_READ
        if (h != IntPtr.Zero)
            try
            {
                var m = new IntPtr[1024];
                if (EnumProcessModulesEx(h, m, m.Length * IntPtr.Size, out int prec, 3))
                {
                    var sb = new StringBuilder(260);
                    for (int i = 0; i < Math.Min(m.Length, prec / IntPtr.Size) && !r; i++)
                        if (GetModuleBaseNameW(h, m[i], sb, 260) > 0) r = Dll3d.Contains(sb.ToString().ToLowerInvariant());
                }
            }
            finally { CloseHandle(h); }
        return cache3d[(pid, caminho)] = r;
    }

    /// <summary>[(type, exe)] of the programs with sound GOING OUT now, on every active output. Call from one thread only.</summary>
    public static List<(string tipo, string exe)> Tocando()
    {
        var saida = new List<(string, string)>();
        Dictionary<int, (List<string> titulos, bool cheia)>? jan = null;
        int eu = Environment.ProcessId;
        using var en = new MMDeviceEnumerator();
        foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            using (d)
            {
                var ss = d.AudioSessionManager.Sessions;
                for (int i = 0; i < ss.Count; i++)
                {
                    var s = ss[i];
                    try
                    {
                        if (s.IsSystemSoundsSession || s.AudioMeterInformation.MasterPeakValue < 1e-4f) continue;
                        int pid = (int)s.GetProcessID;
                        if (pid == 0 || pid == eu) continue;
                        var caminho = Caminho(pid);
                        var exe = caminho.Length > 0 ? Path.GetFileName(caminho) : SafeNome(pid);
                        jan ??= Janelas();
                        jan.TryGetValue(pid, out var j);
                        if (j.titulos == null && Navegadores.Contains(exe.ToLowerInvariant()))   // browser sound comes from a windowless child process
                            j = Irmas(jan, Path.GetFileNameWithoutExtension(exe));
                        var t = Classificar(exe, caminho, j.titulos, j.cheia, () => Usa3d(pid, caminho));
                        if (t != null) saida.Add((t, exe));
                    }
                    catch { }
                    finally { s.Dispose(); }
                }
            }
        return saida;
    }

    /// <summary>Windows of every process with that name (titles + fullscreen).</summary>
    static (List<string> titulos, bool cheia) Irmas(Dictionary<int, (List<string> titulos, bool cheia)> jan, string nome)
    {
        var r = (titulos: new List<string>(), cheia: false);
        foreach (var p in Process.GetProcessesByName(nome))
            using (p)
                if (jan.TryGetValue(p.Id, out var j)) { r.titulos.AddRange(j.titulos); r.cheia |= j.cheia; }
        return r;
    }

    static string SafeNome(int pid) { try { using var p = Process.GetProcessById(pid); return p.ProcessName + ".exe"; } catch { return ""; } }
}
