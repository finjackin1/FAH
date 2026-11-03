// Mixer engine (28/10, "Mesa de som"): real devices (volume/mute both ways, L/R peaks), programs making sound (sessions),
// per-program volume/mute and pin (= per-app default device, IAudioPolicyConfig, undocumented, EarTrumpet-style).
// Runs on its own thread ONLY while the Mixer screen is visible. Program shows after 0.3 s of sound, leaves after 8 s of silence.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace FAH.Audio;

public sealed record Fixo(string X, string N, string Lado, string Aparelho);   // exe, shown name, som|mic, device name
/// <summary>Program volume set in FAH: follows the program to any device. Salvo = kept for next runs; otherwise ends when the program closes
/// (Fim = closed: Windows would bring the old volume back next time, so the next session gets 100 % / unmuted once).
/// 31/10 A = own volume: V is the level heard (% of full scale), kept when the system volume changes (session = V / system, max 100 %).
/// No A = follows the system volume (session 100 %). Old saved rules (no A) keep V as session %.</summary>
public sealed record Regra(string X, string Lado, int V, bool M, bool Salvo, bool Fim = false, bool A = false);

public sealed class Mesa
{
    const float Limiar = 0.001f;
    /// <summary>02/11: session level used instead of mute/0 while a copy of the program has its own volume (process loopback carries
    /// the session volume; the copy multiplies back by 1/level). Measured exact by FAH.Testes --piso.</summary>
    public const float Piso = 1e-5f;
    const double Entra = 0.3, Sai = 8;

    /// <summary>Snapshot changed (devices, defaults, volumes, programs in the row) -> UI "mixer".</summary>
    public Action<object>? AoMudar;
    /// <summary>Levels every 100 ms -> UI "mixniv".</summary>
    public Action<object>? AoNivel;
    /// <summary>Name of the device FAH plays to while on (Windows default is the cable then); null = FAH off.</summary>
    public Func<string?>? SaidaFah;
    /// <summary>Real mic FAH records while its mic path is on (Windows default = the cable then): shown as the mic default.</summary>
    public Func<string?>? MicFah;
    /// <summary>31/10: endpoint id of the render side of FAH's mic cable (voice + programs sent into the mic): the mic default's meter shows it.</summary>
    public Func<string?>? CaboMicFah;
    /// <summary>01/11: cable endpoints that stand for FAH's devices (programs play into / record from them): the device fader moves them too. Null = that path is off.</summary>
    public Func<string?>? CaboSomPar, CaboMicPar;
    /// <summary>Pins changed (UI saves them).</summary>
    public Action<List<Fixo>>? AoFixar;
    /// <summary>Program volumes changed (UI saves them).</summary>
    public Action<List<Regra>>? AoRegrar;
    public List<Regra> Regras = [];
    public Action<string>? Log;
    public List<Fixo> Fixos = [];
    /// <summary>exe path -> PNG data URI of its icon (done by the window: System.Drawing lives there); null = letter.</summary>
    public Func<string, string?>? Icone;
    readonly Dictionary<string, string> icos = [];          // exe name -> data URI ("" = none); kept for the run so pinned programs keep it after closing

    readonly ConcurrentQueue<Action> fila = new();
    readonly AutoResetEvent acorda = new(false);
    volatile bool visivel;
    Thread? fio;

    sealed class Dev { public required MMDevice D; public required string Id, Nome, Lado; public bool Virtual; }
    sealed class Ses { public required AudioSessionControl S; public required uint Pid; public required string X, Lado, DevId, Id; }
    sealed class Prog { public string N = ""; public double Ini, Ult = -99; public bool Visivel; public float Nivel; }

    List<Dev> devs = [];
    List<Ses> sess = [];
    readonly Dictionary<string, Prog> progs = [];           // "lado:exe"
    readonly HashSet<uint> aplicados = [];                  // pids that already got their pin this run
    readonly HashSet<string> vistas = [];                   // session instances that already got their program volume (later changes elsewhere are not fought)
    readonly Stopwatch rel = Stopwatch.StartNew();
    string ultimo = "";

    public void Mostrar(bool on)
    {
        visivel = on;
        if (on) Fio();
        ultimo = "";
        acorda.Set();
    }

    /// <summary>Program volumes must follow new sessions even with the Mixer closed: light pass every 2 s while any exists.</summary>
    public void Vigiar() { if (Regras.Count > 0 || HaCopias?.Invoke() == true) Fio(); }

    /// <summary>A copy started/stopped or changed its volume: hold or release the floor now.</summary>
    public void Acordar() { Fio(); acorda.Set(); }

    void Fio() { if (fio != null) return; fio = new Thread(Laco) { IsBackground = true, Name = "mesa" }; fio.SetApartmentState(ApartmentState.MTA); fio.Start(); }

    /// <summary>Commands from the UI run on the mixer thread (same COM apartment as the cached objects).</summary>
    public void Fazer(Action a) { fila.Enqueue(a); acorda.Set(); }

    void Laco()
    {
        int volta = 0;
        while (true)
        {
            if (!visivel)
            {
                Soltar();
                int t = 0;
                while (!visivel)
                {
                    // own volumes must catch a system volume change fast (else a 2 s loud blip): 100 ms check, full pass every 2 s
                    // 02/11: a copy with its own volume needs its original's session held at the floor (Pisos) also with the Mixer closed
                    bool copias = HaCopias?.Invoke() == true, proprio = copias || Regras.Any(r => r.A && !r.Fim);
                    acorda.WaitOne(proprio ? 100 : Regras.Count > 0 || presos ? 2000 : Timeout.Infinite); Comandos();
                    if (!visivel && (Regras.Count > 0 || copias || presos))
                        try { if (!proprio || t++ % 20 == 0) { Enumerar(); Aplicar(); } Compensar(); Pisos(); } catch (Exception e) { Log?.Invoke("mixer: " + e.Message); }
                }
                Soltar(); volta = 0;
            }
            Comandos();
            try
            {
                if (volta++ % 10 == 0) { Enumerar(); Despertar(MicFah?.Invoke()); Alinhar(); Aplicar(); Instantaneo(); }
                Niveis();
            }
            catch (Exception e) { Log?.Invoke("mixer: " + e.Message); devs = []; volta = 0; }
            acorda.WaitOne(100);
        }
    }

    void Comandos() { while (fila.TryDequeue(out var a)) { try { a(); } catch (Exception e) { Log?.Invoke("mixer: " + e.Message); } } }

    void Soltar()
    {
        foreach (var s in sess) try { s.S.Dispose(); } catch { }
        foreach (var d in devs) try { d.D.Dispose(); } catch { }
        sess = []; devs = []; progs.Clear();
        foreach (var c in vivos.Values) Fechar(c);
        vivos.Clear(); recusou.Clear();
    }

    // 31/10: a capture endpoint's meter only moves while something records from it: while the Mixer is open, every real mic
    // FAH does not record gets a silent capture (data dropped) so its meter follows the voice like the Windows Recording tab.
    readonly Dictionary<string, (WasapiCapture C, MMDevice D)> vivos = [];
    readonly Dictionary<string, double> recusou = [];       // id -> when it failed (exclusive use etc.): retried after 10 s

    static void Fechar((WasapiCapture C, MMDevice D) c) { try { c.C.StopRecording(); } catch { } try { c.C.Dispose(); } catch { } try { c.D.Dispose(); } catch { } }

    void Despertar(string? fahM)
    {
        var quer = devs.Where(d => d.Lado == "mic" && !d.Virtual && !(fahM != null && (d.Nome == fahM || fahM.StartsWith(d.Nome)))).Select(d => d.Id).ToHashSet();
        foreach (var id in vivos.Keys.Where(k => !quer.Contains(k)).ToList()) { Fechar(vivos[id]); vivos.Remove(id); }
        foreach (var id in quer.Where(k => !vivos.ContainsKey(k) && !(recusou.TryGetValue(k, out var t) && Agora - t < 10)))
        {
            MMDevice? d = null;
            try
            {
                using var en = new MMDeviceEnumerator(); d = en.GetDevice(id);
                var c = new WasapiCapture(d, true, 100);
                c.StartRecording(); vivos[id] = (c, d); recusou.Remove(id);
            }
            catch (Exception e) { try { d?.Dispose(); } catch { } if (!recusou.ContainsKey(id)) Log?.Invoke("mixer: medidor do microfone: " + e.Message); recusou[id] = Agora; }
        }
    }

    void Enumerar()
    {
        foreach (var s in sess) try { s.S.Dispose(); } catch { }
        foreach (var d in devs) try { d.D.Dispose(); } catch { }
        var nd = new List<Dev>(); var ns = new List<Ses>();
        using var en = new MMDeviceEnumerator();
        int eu = Environment.ProcessId;
        bool refez = false;                         // one stale name re-read per pass (no 0.9 s pass every 15 s)
        foreach (var (fluxo, lado) in new[] { (DataFlow.Render, "som"), (DataFlow.Capture, "mic") })
            foreach (var d in en.EnumerateAudioEndPoints(fluxo, DeviceState.Active))
            {
                // each property read costs ~50 ms on some PCs (29/10: 9 endpoints = 0.9 s, meters froze every second): cached 15 s
                string id = d.ID;
                if (!cache.TryGetValue(id, out var cn) || (!refez && Agora - cn.T > 15 && (refez = true))) cache[id] = cn = (Aparelhos.Nome(d), Aparelhos.EhCabo(d), Agora);
                string nome = cn.Nome;
                if (nome.Contains("Hands-Free")) { d.Dispose(); continue; }
                nd.Add(new Dev { D = d, Id = id, Nome = nome, Lado = lado, Virtual = cn.Virtual });
                try
                {
                    var lst = d.AudioSessionManager; lst.RefreshSessions(); var ss = lst.Sessions;
                    for (int i = 0; i < ss.Count; i++)
                    {
                        var s = ss[i]; uint pid = s.GetProcessID;
                        if (pid == 0 || pid == eu || s.IsSystemSoundsSession || s.State == AudioSessionState.AudioSessionStateExpired) { s.Dispose(); continue; }
                        var (x, n, cam) = Programa(pid, s.DisplayName);
                        if (x == "") { s.Dispose(); continue; }
                        if (!icos.ContainsKey(x)) { string ic = ""; try { if (cam != "") ic = Icone?.Invoke(cam) ?? ""; } catch { } icos[x] = ic; }
                        string sid = ""; try { sid = s.GetSessionInstanceIdentifier; } catch { }
                        ns.Add(new Ses { S = s, Pid = pid, X = x, Lado = lado, DevId = d.ID, Id = sid });
                        if (!progs.TryGetValue(lado + ":" + x, out var p)) progs[lado + ":" + x] = p = new Prog();
                        p.N = n;
                    }
                }
                catch { }
            }
        devs = nd; sess = ns;
        foreach (var k in logico.Keys.Where(k => !sess.Any(s => s.Id == k)).ToList()) logico.Remove(k);
        foreach (var s in sess) Reaplicar(s.Pid, s.X, s.Lado);
        foreach (var k in progs.Keys.ToList()) if (!sess.Any(s => s.Lado + ":" + s.X == k)) progs.Remove(k);
    }

    readonly Dictionary<string, (string Nome, bool Virtual, double T)> cache = [];
    static readonly Dictionary<uint, (string, string, string)> nomes = [];
    static (string x, string n, string cam) Programa(uint pid, string disp)
    {
        if (nomes.TryGetValue(pid, out var r)) return r;
        string x = "", n = "", cam = Caminho(pid);
        try { using var p = Process.GetProcessById((int)pid); x = p.ProcessName; } catch { }
        try { if (cam != "") n = FileVersionInfo.GetVersionInfo(cam).FileDescription ?? ""; } catch { }
        if (!string.IsNullOrWhiteSpace(disp) && !disp.StartsWith('@')) n = disp;
        if (string.IsNullOrWhiteSpace(n)) n = x.Length > 0 ? char.ToUpper(x[0]) + x[1..] : x;
        return nomes[pid] = (x, n.Trim(), cam);
    }

    /// <summary>Exe path; limited query works on elevated and 32-bit processes too (MainModule does not).</summary>
    static string Caminho(uint pid)
    {
        var h = OpenProcess(0x1000, false, pid); if (h == IntPtr.Zero) return "";
        try { var b = new System.Text.StringBuilder(1024); int n = b.Capacity; return QueryFullProcessImageNameW(h, 0, b, ref n) ? b.ToString() : ""; }
        finally { CloseHandle(h); }
    }
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint acesso, bool herda, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, int f, System.Text.StringBuilder b, ref int n);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    string Ico(string x) => icos.GetValueOrDefault(x, "");

    /// <summary>Windows keeps pins per program, but a program started before the pin may not have it: give it once per pid.</summary>
    void Reaplicar(uint pid, string x, string lado)
    {
        var f = Fixos.FirstOrDefault(f => f.X == x && f.Lado == lado);
        if (f == null || !aplicados.Add(pid)) return;
        var d = devs.FirstOrDefault(d => d.Lado == lado && d.Nome == f.Aparelho);
        if (d != null) try { PorPrograma.Definir(pid, lado == "mic", d.Id); } catch (Exception e) { Log?.Invoke($"fixar {x} ({pid}): " + e.Message); }
    }

    /// <summary>New sessions of a program with a FAH volume get it (moved to another device / default changed / program restarted).</summary>
    void Aplicar()
    {
        if (Regras.Count == 0) return;
        bool mudou = false; var fins = new HashSet<Regra>();
        foreach (var s in sess)
        {
            var r = Regras.FirstOrDefault(r => r.X == s.X && r.Lado == s.Lado); if (r == null) continue;
            string id; try { id = s.S.GetSessionInstanceIdentifier; } catch { continue; }
            if (!vistas.Add(id)) continue;
            try { Escrever(s, r.Fim ? 1 : r.A ? Alvo(r.V, Mestre(s.DevId)) : r.V / 100f, !r.Fim && r.M); } catch { }
            if (r.Fim) fins.Add(r);
        }
        foreach (var r in fins) { Regras.Remove(r); mudou = true; }
        // a not-saved volume lasts while the program runs
        foreach (var r in Regras.ToList())
            if (!r.Salvo && !r.Fim && PidsDe(r.X).Count() == 0) { Regras.Remove(r); if (r.V != 100 || r.M) Regras.Add(r with { Fim = true }); mudou = true; }
        if (mudou) AoRegrar?.Invoke([.. Regras]);
    }

    int Salvo(string lado, string x) => Regras.Any(r => r.X == x && r.Lado == lado && r.Salvo) ? 1 : 0;

    // ---- 31/10 own volume vs system volume (Regra.A)
    /// <summary>Tests only: pretended system volume and the one program allowed to be touched (the real volumes are never touched by tests).</summary>
    public Func<int>? MestreFalso;
    public string? SoPrograma;
    int Mestre(string devId) { if (MestreFalso != null) return MestreFalso(); var d = Achar(devId); if (d != null) try { return (int)Math.Round(d.D.AudioEndpointVolume.MasterVolumeLevelScalar * 100); } catch { } return 100; }
    /// <summary>Session volume that makes level v heard at system volume m (Windows multiplies them; can't go above the system).</summary>
    static float Alvo(int v, int m) => m <= 0 || v >= m ? 1f : v / (float)m;
    /// <summary>New rule from a level heard: at/above the system volume = follow it again.</summary>
    static Regra Nivel(string x, string lado, int e, int m, bool mudo, bool salvo) => e >= m ? new Regra(x, lado, 100, mudo, salvo) : new Regra(x, lado, Math.Max(0, e), mudo, salvo, A: true);

    readonly Dictionary<string, int> mestres = [];          // device id -> last system volume seen
    /// <summary>Keeps own volumes when the system volume moves; a program moved elsewhere (Windows mixer, Windhawk) gets that level as its own.
    /// A program decides once per pass (first session seen); its other sessions just take the result (several streams never fight).</summary>
    void Compensar()
    {
        bool mudou = false; var feitos = new HashSet<string>();
        var ms = new Dictionary<string, (int M, bool Trocou, int M0)>();
        foreach (var d in devs) { int m = Mestre(d.Id); ms[d.Id] = (m, mestres.TryGetValue(d.Id, out int m0) && m0 != m, m0); mestres[d.Id] = m; }
        // playing sessions decide first (31/10: the taskbar mod moves only the session on the default device; an idle session elsewhere
        // deciding first made FAH put the mod's change back)
        {
            foreach (var s in sess.OrderByDescending(Ativa))
            {
                if (!ms.TryGetValue(s.DevId, out var dm)) continue;
                var (m, trocou, m0) = dm;
                if (SoPrograma != null && s.X != SoPrograma) continue;
                var r = Regras.FirstOrDefault(r => r.X == s.X && r.Lado == s.Lado); if (r is { Fim: true }) continue;
                bool feito = !feitos.Add(s.Lado + ":" + s.X);
                try
                {
                    var (q, mq) = Ler(s);
                    if (r is { A: true })
                    {
                        if (!vistas.Contains(s.S.GetSessionInstanceIdentifier)) continue;   // new session: Aplicar gives it the rule first
                        float quer = Alvo(r.V, m);
                        if (!feito && !trocou && Math.Abs(q - quer) > .015f)
                        {
                            Regras.Remove(r); r = Nivel(r.X, r.Lado, (int)Math.Round(q * m), m, r.M, r.Salvo); Regras.Add(r); mudou = true;
                            foreach (var o in sess.Where(o => o != s && o.X == s.X && o.Lado == s.Lado)) try { Escrever(o, r.A ? Alvo(r.V, Mestre(o.DevId)) : 1, null); } catch { }
                        }
                        else if (Math.Abs(q - quer) > .002f) Escrever(s, quer, null);
                    }
                    else if (trocou && q < .995f && !feito)
                    {
                        // was below the system volume: it was moved, so it keeps the level it had (even if the system now went under it)
                        var n = new Regra(s.X, s.Lado, (int)Math.Round(q * m0), r?.M ?? mq, r?.Salvo ?? false, A: true);
                        if (r != null) Regras.Remove(r);
                        Regras.Add(n); mudou = true;
                        foreach (var o in sess.Where(o => o.X == s.X && o.Lado == s.Lado)) try { Escrever(o, Alvo(n.V, Mestre(o.DevId)), null); vistas.Add(o.S.GetSessionInstanceIdentifier); } catch { }
                    }
                }
                catch { }
            }
        }
        if (mudou) { AoRegrar?.Invoke([.. Regras]); if (visivel) Instantaneo(); }
    }

    // ---- 02/11 floor: a copy with its own volume must keep hearing its program while the original is muted/at 0.
    // Logical volume (what the user set, what the Mixer shows) is kept per session; the physical one is the floor form of it while
    // Independente(x): muted/0 -> Piso unmuted, the copy multiplies back by 1/physical (Sessao). Anything else = changed outside FAH.
    /// <summary>exe -> a live copy of its sound has its own volume (not following, not muted, above 0).</summary>
    public Func<string, bool>? Independente;
    /// <summary>Any copy (device or mic) running: the hidden loop keeps a fast pass.</summary>
    public Func<bool>? HaCopias;
    /// <summary>exe -> (physical volume, physical mute, logical volume, logical mute) of its playing sound session; any thread.</summary>
    public readonly ConcurrentDictionary<string, (float Qf, bool Mf, float Ql, bool Ml)> Sessao = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (float Q, bool M)> logico = [];

    bool Indep(Ses s) => s.Lado == "som" && Independente?.Invoke(s.X) == true;
    static (float Q, bool M) Forma((float Q, bool M) l, bool indep) => indep ? (l.M ? Piso : Math.Max(l.Q, Piso), false) : l;
    static bool Igual((float Q, bool M) a, (float Q, bool M) b) => a.M == b.M && Math.Abs(a.Q - b.Q) <= .002f;

    (float Q, bool M) Ler(Ses s)
    {
        var sv = s.S.SimpleAudioVolume; (float Q, bool M) f = (sv.Volume, sv.Mute);
        if (s.Id != "" && logico.TryGetValue(s.Id, out var l) && (Igual(f, Forma(l, true)) || Igual(f, Forma(l, false)))) return l;
        if (s.Id != "") logico[s.Id] = f;
        return f;
    }

    void Escrever(Ses s, float? q, bool? m)
    {
        var l = Ler(s); l = (q ?? l.Q, m ?? l.M);
        if (s.Id != "") logico[s.Id] = l;
        var f = Forma(l, Indep(s)); var sv = s.S.SimpleAudioVolume;
        if (Math.Abs(sv.Volume - f.Q) > 1e-7f) sv.Volume = f.Q;
        if (sv.Mute != f.M) sv.Mute = f.M;
    }

    /// <summary>Every pass: physical = floor form of logical (copy started/stopped, own volume moved); publishes Sessao.</summary>
    bool presos;                                            // some session is at its floor form now (a pass must release it)
    void Pisos()
    {
        bool pr = false;
        foreach (var s in sess) if (s.Lado == "som")
            try
            {
                var l = Ler(s); var f = Forma(l, Indep(s)); var sv = s.S.SimpleAudioVolume; pr |= f != l;
                if (sv.Mute != f.M || Math.Abs(sv.Volume - f.Q) > (f.Q < .01f ? 1e-7f : .002f)) Escrever(s, null, null);
            }
            catch { }
        presos = pr;
        Publicar();
    }

    void Publicar()
    {
        var feitos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sess.Where(s => s.Lado == "som").OrderByDescending(Ativa))
        {
            if (feitos.Contains(s.X)) continue;
            try { var sv = s.S.SimpleAudioVolume; var l = Ler(s); Sessao[s.X] = (sv.Volume, sv.Mute, l.Q, l.M); feitos.Add(s.X); } catch { }
        }
        foreach (var k in Sessao.Keys) if (!feitos.Contains(k)) Sessao.TryRemove(k, out _);
    }

    /// <summary>FAH closing: sessions held at the floor get their real volume/mute back (Windows remembers it per program).</summary>
    public void SoltarPisos(int ms = 1500)
    {
        Independente = null;
        if (fio == null) return;
        var t = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Fazer(() => { foreach (var s in sess) if (s.Lado == "som") try { Escrever(s, null, null); } catch { } t.TrySetResult(); });
        t.Task.Wait(ms);
    }

    static bool Ativa(Ses s) { try { return s.S.State == AudioSessionState.AudioSessionStateActive; } catch { return false; } }

    double Agora => rel.Elapsed.TotalSeconds;

    void Niveis()
    {
        double t = Agora;
        foreach (var p in progs.Values) p.Nivel = 0;
        foreach (var s in sess)
        {
            float v = 0; try { v = s.S.AudioMeterInformation.MasterPeakValue; } catch { }
            var p = progs[s.Lado + ":" + s.X]; p.Nivel = Math.Max(p.Nivel, v);
        }
        bool mudou = false;
        foreach (var p in progs.Values)
        {
            if (p.Nivel > Limiar) { if (t - p.Ult > 1) p.Ini = t; p.Ult = t; if (!p.Visivel && t - p.Ini >= Entra) { p.Visivel = true; mudou = true; } }
            else if (p.Visivel && t - p.Ult > Sai) { p.Visivel = false; mudou = true; }
        }
        var d = new Dictionary<string, float[]>();
        foreach (var dv in devs)
        {
            try { var pv = dv.D.AudioMeterInformation.PeakValues; float l = pv.Count > 0 ? pv[0] : 0, r = pv.Count > 1 ? pv[1] : l; d[dv.Id] = [MathF.Round(l, 3), MathF.Round(r, 3)]; }
            catch { d[dv.Id] = [0, 0]; }
        }
        // the mic FAH records: show what the call hears (processed voice + programs sent into the mic) = its cable's render side
        string? fahM = MicFah?.Invoke(), cabo = CaboMicFah?.Invoke();
        if (fahM != null && cabo != null && d.TryGetValue(cabo, out var nc))
            foreach (var m in devs.Where(x => x.Lado == "mic" && !x.Virtual && (x.Nome == fahM || fahM.StartsWith(x.Nome)))) d[m.Id] = [nc.Max(), nc.Max()];
        AoNivel?.Invoke(new { d, a = progs.ToDictionary(k => k.Key, k => MathF.Round(k.Value.Nivel, 3)) });
        Compensar(); Pisos();
        if (mudou || VolMudou()) Instantaneo();
    }

    /// <summary>31/10: volumes changed elsewhere (Windows mixer, Windhawk, keys) reach the UI within 100 ms, not on the next 1 s pass.</summary>
    string sigVol = "";
    bool VolMudou()
    {
        var b = new System.Text.StringBuilder();
        foreach (var dv in devs) try { var ev = dv.D.AudioEndpointVolume; b.Append((int)Math.Round(ev.MasterVolumeLevelScalar * 100)).Append(ev.Mute ? 'm' : ','); } catch { b.Append('?'); }
        foreach (var s in sess) try { var sv = s.S.SimpleAudioVolume; b.Append((int)Math.Round(sv.Volume * 100)).Append(sv.Mute ? 'm' : ';'); } catch { b.Append('?'); }
        var n = b.ToString(); if (n == sigVol) return false;
        sigVol = n; return true;
    }

    /// <summary>v = level heard (session x system volume), q = session %, pd = system volume of its device, pr = own volume (else follows).</summary>
    (int v, bool m, int q, int pd, int pr) VolProg(string lado, string x, string? aparelho = null)
    {
        var r = Regras.FirstOrDefault(r => r.X == x && r.Lado == lado && !r.Fim); int pr = r is { A: true } ? 1 : 0;
        foreach (var s in sess.Where(s => s.Lado == lado && s.X == x).OrderByDescending(Ativa)) try { var (q, mq) = Ler(s); int m = Mestre(s.DevId); return ((int)Math.Round(q * m), mq, (int)Math.Round(q * 100), m, pr); } catch { }
        var d = devs.FirstOrDefault(d => d.Lado == lado && d.Nome == aparelho); int pd = d != null ? Mestre(d.Id) : 100;
        return (pr == 1 ? Math.Min(r!.V, pd) : pd, r?.M ?? false, pr == 1 ? (int)Math.Round(Alvo(r!.V, pd) * 100) : 100, pd, pr);
    }

    void Instantaneo()
    {
        string? fah = SaidaFah?.Invoke(), fahM = MicFah?.Invoke();
        string? padS = null, padM = null;
        try { using var en = new MMDeviceEnumerator(); padS = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID; } catch { }
        try { using var en = new MMDeviceEnumerator(); padM = en.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID; } catch { }
        object Lado(string lado) => devs.Where(d => d.Lado == lado && !d.Virtual).Select(d =>
        {
            int vol = 100; bool m = false; string f = "", c = "";
            try { var ev = d.D.AudioEndpointVolume; vol = (int)Math.Round(ev.MasterVolumeLevelScalar * 100); m = ev.Mute; } catch { }
            (f, c) = Formato(d);
            bool p = lado == "som" ? (fah != null ? d.Nome == fah || fah.StartsWith(d.Nome) : d.Id == padS) : fahM != null ? d.Nome == fahM || fahM.StartsWith(d.Nome) : d.Id == padM;
            return new
            {
                id = d.Id, n = d.Nome, t = lado == "som" ? "Saída" : "Microfone", ic = lado == "som" ? "fone" : "mic", p = p ? 1 : 0, f, c, vol, m = m ? 1 : 0,
                fixos = Fixos.Where(x => x.Lado == lado && x.Aparelho == d.Nome).Select(x => { var (v, mm, q, pd, pr) = VolProg(lado, x.X, d.Nome); return new { x = x.X, n = x.N, lado, v, m = mm ? 1 : 0, q, pd, pr, s = Salvo(lado, x.X), ic = Ico(x.X) }; }).ToList(),
            };
        }).ToList();
        var soltos = progs.Where(k => k.Value.Visivel).Select(k => { var (lado, x) = (k.Key[..3], k.Key[4..]); return (lado, x, k.Value.N); })
            .Where(p => !Fixos.Any(f => f.X == p.x && f.Lado == p.lado))
            .Select(p => { var (v, m, q, pd, pr) = VolProg(p.lado, p.x); return new { x = p.x, n = p.N, p.lado, v, m = m ? 1 : 0, q, pd, pr, s = Salvo(p.lado, p.x), ic = Ico(p.x) }; }).ToList();
        var snap = new { som = Lado("som"), mic = Lado("mic"), soltos, fah = fah != null, fahMic = fahM != null };
        var j = System.Text.Json.JsonSerializer.Serialize(snap);
        if (j == ultimo) return;
        ultimo = j; AoMudar?.Invoke(snap);
    }

    // mix format: AudioClient activation is the slow part of a snapshot (now up to 10/s): read again after 5 s
    readonly Dictionary<string, (string F, string C, double T)> fmts = [];
    (string, string) Formato(Dev d)
    {
        if (fmts.TryGetValue(d.Id, out var o) && Agora - o.T < 5) return (o.F, o.C);
        string f = "", c = "";
        try { var ac = d.D.AudioClient; var w = ac.MixFormat; ac.Dispose(); f = $"{w.SampleRate / 1000.0:0.#} kHz".Replace('.', ','); c = w.Channels switch { 1 => "Mono", 2 => "Estéreo", 6 => "5.1", 8 => "7.1", var n => n + " canais" }; } catch { }
        fmts[d.Id] = (f, c, Agora); return (f, c);
    }

    /// <summary>Processes of a program making sound now (any thread: reads the last pass' snapshot).</summary>
    public uint[] Pids(string lado, string x) => [.. sess.Where(s => s.Lado == lado && s.X == x).Select(s => s.Pid).Distinct()];

    // ---- commands (called through Fazer)
    Dev? Achar(string id) => devs.FirstOrDefault(d => d.Id == id);

    public void Volume(string id, int vol) => Fazer(() =>
    {
        var d = Achar(id); if (d == null) return;
        float x = Math.Clamp(vol, 0, 100) / 100f; d.D.AudioEndpointVolume.MasterVolumeLevelScalar = x;
        if (Par(d) is { } c) c.D.AudioEndpointVolume.MasterVolumeLevelScalar = x;
    });

    /// <summary>The cable that goes with this device while FAH routes through it; null = none.</summary>
    Dev? Par(Dev d)
    {
        if (d.Virtual) return null;
        string? fah = d.Lado == "som" ? SaidaFah?.Invoke() : MicFah?.Invoke();
        if (fah == null || !(d.Nome == fah || fah.StartsWith(d.Nome))) return null;
        return (d.Lado == "som" ? CaboSomPar?.Invoke() : CaboMicPar?.Invoke()) is { } id ? Achar(id) : null;
    }

    /// <summary>Device and cable start equal, once per pair: not a continuous copy, which would undo Windows keys and can fight other programs (ping-pong seen 01/11).</summary>
    readonly HashSet<string> alinhados = [];
    void Alinhar()
    {
        foreach (var d in devs) if (Par(d) is { } c && alinhados.Add(d.Id + "|" + c.Id))
            try { float x = d.D.AudioEndpointVolume.MasterVolumeLevelScalar; if (Math.Abs(c.D.AudioEndpointVolume.MasterVolumeLevelScalar - x) > .004f) c.D.AudioEndpointVolume.MasterVolumeLevelScalar = x; } catch { }
    }
    public void Mudo(string id, bool m) => Fazer(() => { var d = Achar(id); if (d != null) d.D.AudioEndpointVolume.Mute = m; Instantaneo(); });

    /// <summary>Volume / mute / "remember" of a program: applied now to all its sessions and kept as a rule (see Regra).</summary>
    public void VolumePrograma(string lado, string x, int? vol, bool? m, bool? salvo = null) => Fazer(() => VolProgAgora(lado, x, vol, m, salvo));

    /// <summary>Taskbar wheel (Barra): sound program volume +- passo %, read and written in one queued step (fast wheel loses no notch).</summary>
    public void Rolar(string x, int passo) { Fio(); Fazer(() =>
    {
        if (!visivel && !sess.Any(s => s.Lado == "som" && s.X == x)) Enumerar();
        if (!sess.Any(s => s.Lado == "som" && s.X == x)) return;
        var (v, _, _, _, _) = VolProg("som", x);
        VolProgAgora("som", x, Math.Clamp(v + passo, 0, 100), null);
    }); }

    /// <summary>Taskbar: sound programs the Mixer shows now (row or pinned, with a session). Mixer closed = sessions playing now.</summary>
    public HashSet<string> Elegiveis(int ms = 1500)
    {
        var t = new TaskCompletionSource<HashSet<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Fio(); Fazer(() =>
        {
            if (!visivel) Enumerar();
            var h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sess.Where(s => s.Lado == "som"))
            {
                bool ok = Fixos.Any(f => f.Lado == "som" && f.X == s.X);
                if (!ok && visivel) ok = progs.TryGetValue("som:" + s.X, out var p) && p.Visivel;
                if (!ok && !visivel) try { ok = s.S.State == AudioSessionState.AudioSessionStateActive; } catch { }
                if (ok) h.Add(s.X);
            }
            t.TrySetResult(h);
        });
        return t.Task.Wait(ms) ? t.Task.Result : [];
    }

    /// <summary>vol = level heard (0..100 of full scale, what the Mixer bar shows). At/above the system volume = follows it again.</summary>
    void VolProgAgora(string lado, string x, int? vol, bool? m, bool? salvo = null)
    {
        if (!visivel && !sess.Any(s => s.Lado == lado && s.X == x)) Enumerar();   // Mixer closed: session list may be empty
        var ss = sess.Where(s => s.Lado == lado && s.X == x).ToList();
        int pd = ss.Count > 0 ? Mestre(ss[0].DevId) : 100;
        int? e = vol is { } v ? Math.Clamp(v, 0, 100) : null;
        foreach (var s in ss)
        {
            try
            {
                Escrever(s, e is { } ev ? ev >= pd ? 1 : Alvo(ev, Mestre(s.DevId)) : null, m);
                vistas.Add(s.S.GetSessionInstanceIdentifier);
            }
            catch { }
        }
        var (v0, m0, q0, _, _) = VolProg(lado, x);
        var r = Regras.FirstOrDefault(r => r.X == x && r.Lado == lado); if (r is { Fim: true }) r = null;
        bool mu = m ?? r?.M ?? m0, sa = salvo ?? r?.Salvo ?? false;
        Regras.RemoveAll(r => r.X == x && r.Lado == lado);
        Regras.Add(e is { } e1 ? Nivel(x, lado, e1, pd, mu, sa)
            : r != null ? r with { M = mu, Salvo = sa }
            : q0 < 100 ? Nivel(x, lado, v0, pd, mu, sa) : new Regra(x, lado, 100, mu, sa));
        AoRegrar?.Invoke([.. Regras]);
        Publicar();
        if (m != null || salvo != null) Instantaneo();
    }

    /// <summary>Pin a program to a device (id) or give it back to the default (id null). Running processes switch now.</summary>
    public void Fixar(string lado, string x, string n, string? id) => Fazer(() =>
    {
        var d = id == null ? null : Achar(id);
        Fixos.RemoveAll(f => f.X == x && f.Lado == lado);
        if (d != null) Fixos.Add(new Fixo(x, n, lado, d.Nome));
        // browsers have many processes; Windows refuses the sandboxed ones (E_INVALIDARG) while the audio one accepts: log only if none took it
        int ok = 0, nao = 0; string? erro = null;
        foreach (var pid in PidsDe(x))
        {
            try { PorPrograma.Definir(pid, lado == "mic", d?.Id); ok++; } catch (Exception e) { nao++; erro = e.Message; }
            aplicados.Add(pid);
        }
        if (ok == 0 && nao > 0) Log?.Invoke($"fixar {x}: nenhum dos {nao} processos aceitou ({erro})");
        if (progs.TryGetValue(lado + ":" + x, out var p) && d == null) { p.Visivel = true; p.Ult = Agora; }   // back in the row at once
        AoFixar?.Invoke(Fixos);
        Instantaneo();
    });

    static IEnumerable<uint> PidsDe(string x)
    {
        Process[] ps; try { ps = Process.GetProcessesByName(x); } catch { yield break; }
        foreach (var p in ps) { yield return (uint)p.Id; p.Dispose(); }
    }
}

/// <summary>Per-app default device (Settings > Sound > Volume mixer > app). Undocumented Windows 10 21H2+/11 interface, same as EarTrumpet.</summary>
public static class PorPrograma
{
    const string Token = @"\\?\SWD#MMDEVAPI#", Saida = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}", Entrada = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";
    static IPolicy? fab;

    static IPolicy Fab()
    {
        if (fab != null) return fab;
        var nome = "Windows.Media.Internal.AudioPolicyConfig";
        Marshal.ThrowExceptionForHR(WindowsCreateString(nome, nome.Length, out var h));
        try
        {
            var iid = new Guid("ab3d4648-e242-459f-b02f-541c70306324");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(h, ref iid, out var p));
            fab = (IPolicy)Marshal.GetObjectForIUnknown(p); Marshal.Release(p);
            return fab;
        }
        finally { WindowsDeleteString(h); }
    }

    /// <summary>id null = back to following the Windows default. All 3 roles: call apps (Discord) play on the communications one (31/10).</summary>
    public static void Definir(uint pid, bool mic, string? id)
    {
        var f = Fab(); IntPtr h = IntPtr.Zero;
        if (id != null) { var s = Token + id + (mic ? Entrada : Saida); Marshal.ThrowExceptionForHR(WindowsCreateString(s, s.Length, out h)); }
        try { foreach (var papel in new[] { 0, 1, 2 }) Marshal.ThrowExceptionForHR(f.SetPersistedDefaultAudioEndpoint(pid, mic ? 1 : 0, papel, h)); }   // eConsole, eMultimedia, eCommunications
        finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
    }

    /// <summary>Pinned device id of a process ("" = follows default).</summary>
    public static string Ler(uint pid, bool mic)
    {
        Marshal.ThrowExceptionForHR(Fab().GetPersistedDefaultAudioEndpoint(pid, mic ? 1 : 0, 1, out var h));
        if (h == IntPtr.Zero) return "";
        try { var s = Marshal.PtrToStringUni(WindowsGetStringRawBuffer(h, out _)) ?? ""; return s.StartsWith(Token) ? s[Token.Length..].Split('#')[0] : s; }
        finally { WindowsDeleteString(h); }
    }

    [DllImport("combase.dll", CharSet = CharSet.Unicode)] static extern int WindowsCreateString(string s, int n, out IntPtr h);
    [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr h);
    [DllImport("combase.dll")] static extern IntPtr WindowsGetStringRawBuffer(IntPtr h, out uint n);
    [DllImport("combase.dll")] static extern int RoGetActivationFactory(IntPtr nome, ref Guid iid, out IntPtr fab);

    [ComImport, Guid("ab3d4648-e242-459f-b02f-541c70306324"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicy
    {
        // IInspectable
        void GetIids(); void GetRuntimeClassName(); void GetTrustLevel();
        // 19 slots we do not use
        void a1(); void a2(); void a3(); void a4(); void a5(); void a6(); void a7(); void a8(); void a9(); void a10();
        void a11(); void a12(); void a13(); void a14(); void a15(); void a16(); void a17(); void a18(); void a19();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint pid, int fluxo, int papel, IntPtr id);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint pid, int fluxo, int papel, out IntPtr id);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }
}
