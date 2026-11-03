// Offline self-test: no device opened, Windows untouched (default switch is simulated in a temp file).
// Run: FAH.Testes.exe [relatorio.txt]. Exit code = failures.
using System.Text;
using System.Text.Json;
using FAH.Audio;
using FAH.Equalizador;
using Fx = FAH.Efeitos;
using Esp = FAH.Espacial;

if (args.Length > 1 && args[0] == "--tocar")     // child for the program-volume test: plays SILENCE on the default device for N ms
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); using var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
    using var o = new NAudio.Wave.WasapiOut(dv, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, 50);
    o.Init(new NAudio.Wave.SilenceProvider(dv.AudioClient.MixFormat)); o.Play(); Thread.Sleep(int.Parse(args[1])); return 0;
}
if (args.Length > 1 && args[0] == "--agudo")     // child for the taskbar test: 19 kHz at -50 dBFS on the default device (inaudible, but the meter sees it)
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); using var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
    using var o = new NAudio.Wave.WasapiOut(dv, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, 50);
    o.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider(new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2) { Frequency = 19000, Gain = .003 })); o.Play(); Thread.Sleep(int.Parse(args[1])); return 0;
}
if (args.Length > 2 && args[0] == "--tom-em")     // child for the copy-volume test: 440 Hz tone on the named (virtual cable) output for N ms
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    var dv = en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active).First(x => x.FriendlyName == args[1]);
    using var o = new NAudio.Wave.WasapiOut(dv, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, 50);
    o.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider(new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2) { Frequency = 440, Gain = .5 })); o.Play(); Thread.Sleep(int.Parse(args[2])); return 0;
}
if (args.Length > 0 && args[0] == "--pontas")     // list every endpoint (state | name | driver interface), nothing else
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    foreach (var f in new[] { NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DataFlow.Capture })
        foreach (var d in en.EnumerateAudioEndPoints(f, NAudio.CoreAudioApi.DeviceState.All))
            try { Console.WriteLine($"{f} {d.State} | {d.FriendlyName} | {d.DeviceFriendlyName}"); } catch { }
    return 0;
}
if (args.Length > 0 && args[0] == "--medidores")  // 31/10: capture meter of each active mic before / while a silent capture is open (Mixer keeps one per mic)
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    foreach (var d in en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active).ToList())
    {
        float Max() { float m = 0; for (int i = 0; i < 15; i++) { m = Math.Max(m, d.AudioMeterInformation.MasterPeakValue); Thread.Sleep(100); } return m; }
        float antes = Max(), com = -1;
        try { using var c = new NAudio.CoreAudioApi.WasapiCapture(d, true, 100); c.StartRecording(); Thread.Sleep(300); com = Max(); c.StopRecording(); }
        catch (Exception e) { Console.WriteLine("  falhou: " + e.Message); }
        Console.WriteLine($"{d.FriendlyName}: antes {antes:0.00000} com captura {com:0.00000}");
    }
    return 0;
}
if (args.Length > 2 && args[0] == "--visivel")    // enable(1)/disable(0) every endpoint whose name starts with args[1] (app CLOSED); prints HRESULT + new state
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    foreach (var f in new[] { NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DataFlow.Capture })
        foreach (var d in en.EnumerateAudioEndPoints(f, NAudio.CoreAudioApi.DeviceState.Active | NAudio.CoreAudioApi.DeviceState.Disabled).ToList())
            if (d.FriendlyName.StartsWith(args[1]))
            {
                int hr = PadraoWindows.Visivel(d.ID, args[2] == "1");
                Thread.Sleep(300);
                Console.WriteLine($"{f} {d.FriendlyName}: hr 0x{hr:x8} -> {en.GetDevice(d.ID).State}");
            }
    return 0;
}
if (args.Length > 0 && args[0] == "--medir71")   // 30/10 measure: fade from behind (BL) through 7.1 (his settings) ± Estéreo 125%
{
    const int R = 48000; int n = 1024 * 282;
    foreach (var (larg, perf) in new[] { (1.0, "Música"), (1.25, "Música"), (1.0, "Jogo") })
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait();
        mm.Receber("s71", JsonDocument.Parse("""{"perfil":"Música","sala":17,"voz":true,"sub":true,"ouvido":"h11"}""").RootElement); mm.Motor.Ligado = true;
        var cc = new Cadeia(); cc.Definir(mm.Efeitos);
        var est = new Fx.Estereo(); est.Preparar(R); est.Largura = larg;
        var rnd = new Random(3); var v8 = new float[n * 8]; var o = new float[n * 2];
        for (int i = 0; i < n; i++) { double t = i / (double)R, db = t < 0.5 ? 0 : t < 4.5 ? -60 * (t - 0.5) / 4 : -200; v8[i * 8 + 4] = (float)(0.2 * Math.Pow(10, db / 20) * (rnd.NextDouble() * 2 - 1)); }
        for (int i = 0; i < n; i += 1024) { cc.Processar8(v8.AsSpan(i * 8, 1024 * 8), o.AsSpan(i * 2, 2048), 1024); est.Processar(o.AsSpan(i * 2, 2048), 1024); }
        var sb = new StringBuilder($"{perf} largura {larg}: ");
        double ref0 = 0;
        for (double t = 0.3; t < 5.2; t += 0.25)
        {
            int a = (int)(t * R), b = a + R / 20; double sl = 0, sr = 0, x = 0;
            for (int i = a; i < b; i++) { sl += o[i * 2] * o[i * 2]; sr += o[i * 2 + 1] * o[i * 2 + 1]; x += o[i * 2] * o[i * 2 + 1]; }
            double db = 10 * Math.Log10((sl + sr) / (2 * (b - a)) + 1e-20); if (t < 0.31) ref0 = db;
            sb.Append($"{t:F2}s {db - ref0:F1} ILD {10 * Math.Log10((sl + 1e-20) / (sr + 1e-20)):F1} corr {x / Math.Sqrt(sl * sr + 1e-30):F2} | ");
        }
        Console.WriteLine(sb);
    }
    return 0;
}

var rel = new StringBuilder();
int falhas = 0;
void Ok(string nome, bool ok, string det) { rel.AppendLine($"{(ok ? "OK  " : "FALHA")} {nome}: {det}"); if (!ok) falhas++; }
if (args.Length > 0 && args[0] == "--volcopia") { TesteVolCopia(); Console.Write(rel); return falhas; }
if (args.Length > 0 && args[0] == "--barra") { TesteBarra(); Console.Write(rel); return falhas; }
if (args.Length > 0 && args[0] == "--piso") { TestePiso(); Console.Write(rel); return falhas; }
if (args.Length > 0 && args[0] == "--sessoes")    // list every session (device | exe | volume | mute), read only
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    foreach (var fl in new[] { NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DataFlow.Capture })
        foreach (var d in en.EnumerateAudioEndPoints(fl, NAudio.CoreAudioApi.DeviceState.Active))
        { var ss = d.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) { var s = ss.Sessions[i]; string x = ""; try { x = System.Diagnostics.Process.GetProcessById((int)s.GetProcessID).ProcessName; } catch { }
            Console.WriteLine($"{d.FriendlyName} | {x} ({s.GetProcessID}) | {s.SimpleAudioVolume.Volume:0.000} | {s.SimpleAudioVolume.Mute} | {s.State}"); } }
    return 0;
}
if (args.Length > 1 && args[0] == "--fixado")     // per-app default (pin) of every process of exe args[1], read only
{
    foreach (var p in System.Diagnostics.Process.GetProcessesByName(args[1]))
    { string r; try { r = FAH.Audio.PorPrograma.Ler((uint)p.Id, false); } catch (Exception e) { r = "erro " + e.Message; } Console.WriteLine($"{p.Id} | {(r == "" ? "(segue o padrão)" : r)}"); }
    return 0;
}
if (args.Length > 0 && args[0] == "--segue"){ TesteSegue(); Console.Write(rel); return falhas; }
if (args.Length > 0 && args[0] == "--bypassvol")
{
    // closed FAH disables its cables (MotorCabos): optional arg = its cabos-desligados.json; those endpoints stay on only during the test
    var desligados = args.Length > 1 && File.Exists(args[1]) ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1])) ?? [] : [];
    foreach (var id in desligados) PadraoWindows.Visivel(id, true);
    if (desligados.Length > 0) Thread.Sleep(2000);
    try { TesteBypassVol(); }
    finally { foreach (var id in desligados) PadraoWindows.Visivel(id, false); }
    Console.Write(rel); return falhas;
}
if (args.Length > 2 && args[0] == "--sessao-vol")  // set volume of every session of exe args[1] (capture + render) to args[2]; optional args[3] = device name filter
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); float alvo = float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    foreach (var fl in new[] { NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.DataFlow.Capture })
        foreach (var d in en.EnumerateAudioEndPoints(fl, NAudio.CoreAudioApi.DeviceState.Active))
        {
            if (args.Length > 3 && !d.FriendlyName.Contains(args[3])) continue;
            var ss = d.AudioSessionManager; ss.RefreshSessions();
            for (int i = 0; i < ss.Sessions.Count; i++) { var s = ss.Sessions[i]; string x = ""; try { x = System.Diagnostics.Process.GetProcessById((int)s.GetProcessID).ProcessName; } catch { }
                if (!x.Equals(args[1], StringComparison.OrdinalIgnoreCase) || Math.Abs(s.SimpleAudioVolume.Volume - alvo) < 0.001) continue;
                Console.WriteLine($"{d.FriendlyName} | {x} | {s.SimpleAudioVolume.Volume:0.000} -> {alvo:0.000}"); s.SimpleAudioVolume.Volume = alvo; }
        }
    return 0;
}
if (args.Length > 3 && args[0] == "--comparar-mic")  // record real mic (args[1]) and FAH's output cable capture (args[2]) at once for N s: floors + delay (read only)
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    var devs = en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active).ToList();
    var ls = new[] { new List<float>(), new List<float>() }; var caps = new List<NAudio.CoreAudioApi.WasapiCapture>();
    for (int k = 0; k < 2; k++)
    {
        var d = devs.First(x => x.FriendlyName.Contains(args[1 + k])); var c = new NAudio.CoreAudioApi.WasapiCapture(d, false, 20); var wf = c.WaveFormat; var l = ls[k];
        if (wf.SampleRate != 48000 || wf.Encoding != NAudio.Wave.WaveFormatEncoding.IeeeFloat && wf.Encoding != NAudio.Wave.WaveFormatEncoding.Extensible) Console.WriteLine($"aviso {d.FriendlyName}: {wf}");
        c.DataAvailable += (_, e) => { var pr = new NAudio.Wave.WaveBuffer(e.Buffer); int ch = wf.Channels, n = e.BytesRecorded / 4 / ch; for (int i = 0; i < n; i++) l.Add(pr.FloatBuffer[i * ch]); };
        caps.Add(c);
    }
    foreach (var c in caps) c.StartRecording();
    Thread.Sleep(int.Parse(args[3]) * 1000);
    foreach (var c in caps) { c.StopRecording(); } Thread.Sleep(200); foreach (var c in caps) c.Dispose();
    double[] Piso(List<float> s) { var l = new List<double>(); for (int p = 0; p + 960 <= s.Count; p += 960) { double e = 0; for (int i = 0; i < 960; i++) e += s[p + i] * s[p + i]; l.Add(10 * Math.Log10(e / 960 + 1e-12)); } l.Sort(); int k = Math.Max(1, l.Count * 15 / 100); return [l.Take(k).Average(), l[l.Count / 2], l.Skip(l.Count - k).Average()]; }
    var (a, b) = (Piso(ls[0]), Piso(ls[1]));
    int n2 = Math.Min(ls[0].Count, ls[1].Count) - 9600, mel = 0; double mc = 0;
    for (int dl = 0; dl < 9600; dl += 8) { double s = 0; for (int i = 0; i < n2; i += 4) s += ls[0][i] * ls[1][i + dl]; if (Math.Abs(s) > mc) { mc = Math.Abs(s); mel = dl; } }
    Console.WriteLine($"entrada: piso {a[0]:F1} meio {a[1]:F1} alto {a[2]:F1} dB | saida: piso {b[0]:F1} meio {b[1]:F1} alto {b[2]:F1} dB | atraso {mel} amostras ({mel / 48.0:F0} ms)");
    if (args.Length > 4) for (int k = 0; k < 2; k++) { using var w = new NAudio.Wave.WaveFileWriter(Path.Combine(args[4], k == 0 ? "entrada.wav" : "saida.wav"), NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)); w.WriteSamples(ls[k].ToArray(), 0, ls[k].Count); }
    return 0;
}
if (args.Length > 2 && args[0] == "--analisar-ruido")  // dir with entrada.wav/saida.wav (from --comparar-mic) + delay: out-in per input level and per band
{
    float[] Ler(string p) { using var w = new NAudio.Wave.WaveFileReader(p); var l = new List<float>(); float[]? f; while ((f = w.ReadNextSampleFrame()) != null) l.Add(f[0]); return [.. l]; }
    var a = Ler(Path.Combine(args[1], "entrada.wav")); var b = Ler(Path.Combine(args[1], "saida.wav")).Skip(int.Parse(args[2])).ToArray();
    int nq = Math.Min(a.Length, b.Length) / 960;
    // bands by one-pole splits: <300, 300-3000, >3000
    float[][] Bandas(float[] x) { var r = new[] { new float[x.Length], new float[x.Length], new float[x.Length] }; float l1 = 0, l2 = 0, c1 = 1 - MathF.Exp(-2 * MathF.PI * 300 / 48000), c2 = 1 - MathF.Exp(-2 * MathF.PI * 3000 / 48000); for (int i = 0; i < x.Length; i++) { l1 += c1 * (x[i] - l1); l2 += c2 * (x[i] - l2); r[0][i] = l1; r[1][i] = l2 - l1; r[2][i] = x[i] - l2; } return r; }
    double Db(float[] x, int q) { double e = 0; for (int i = q * 960; i < q * 960 + 960; i++) e += x[i] * x[i]; return 10 * Math.Log10(e / 960 + 1e-12); }
    var A = Enumerable.Range(0, nq).Select(q => Db(a, q)).ToArray(); var B = Enumerable.Range(0, nq).Select(q => Db(b, q)).ToArray();
    double Med(IEnumerable<double> s) { var l = s.OrderBy(x => x).ToList(); return l.Count == 0 ? double.NaN : l[l.Count / 2]; }
    for (int lo = -75; lo < -5; lo += 5) { var ix = Enumerable.Range(0, nq).Where(q => A[q] >= lo && A[q] < lo + 5).ToList(); if (ix.Count > 0) Console.WriteLine($"in {lo}..{lo + 5} dB: {ix.Count,4} quadros, saida-entrada {Med(ix.Select(q => B[q] - A[q])):F1} dB"); }
    var (ba, bb) = (Bandas(a), Bandas(b)); string[] nm = ["<300 Hz", "300-3k", ">3k"];
    var calmos = Enumerable.Range(0, nq).Where(q => A[q] < Med(A)).ToList();
    for (int k = 0; k < 3; k++) Console.WriteLine($"metade mais baixa, {nm[k]}: entrada {Med(calmos.Select(q => Db(ba[k], q))):F1} saida {Med(calmos.Select(q => Db(bb[k], q))):F1} dB");
    return 0;
}
if (args.Length > 3 && args[0] == "--gravar-mic")  // record N s from the named capture device to a mono 48 kHz wav (read only, nothing else touched)
{
    using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator();
    var d = en.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active).First(x => x.FriendlyName.Contains(args[1]));
    using var cap = new NAudio.CoreAudioApi.WasapiCapture(d, false, 20); var wf = cap.WaveFormat; var l = new List<float>();
    var pico = new float[8];
    cap.DataAvailable += (_, e) => { var pr = new NAudio.Wave.WaveBuffer(e.Buffer); int ch = wf.Channels, n = e.BytesRecorded / 4 / ch;
        for (int i = 0; i < n; i++) { float s = 0; for (int c = 0; c < ch; c++) { s += pr.FloatBuffer[i * ch + c] / ch; pico[c] = Math.Max(pico[c], Math.Abs(pr.FloatBuffer[i * ch + c])); } l.Add(s); } };
    cap.StartRecording(); Thread.Sleep(int.Parse(args[2]) * 1000); cap.StopRecording(); Thread.Sleep(200);
    var x = l.ToArray(); if (wf.SampleRate != 48000) x = [.. Enumerable.Range(0, (int)((long)x.Length * 48000 / wf.SampleRate)).Select(i => x[Math.Min(x.Length - 1, (int)((long)i * wf.SampleRate / 48000))])];
    using var w = new NAudio.Wave.WaveFileWriter(args[3], NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)); w.WriteSamples(x, 0, x.Length);
    Console.WriteLine($"{d.FriendlyName}: {wf.SampleRate} Hz {wf.Channels} ch {wf.Encoding}, {x.Length} amostras, pico por canal {string.Join(" / ", pico.Take(wf.Channels).Select(p => $"{20 * Math.Log10(p + 1e-9):F1} dB"))}"); return 0;
}
if (args.Length > 0 && args[0] == "--ruidos") return Ruidos.Rodar(args);
if (args.Length > 1 && args[0] == "--ruido-dist")  // installed modules like the host loads them (own ALC, saved state), noisy sample through the Mic chain
{
    var pasta = args[1]; var mods = new List<FAH.Contrato.IModulo>();
    foreach (var arq in Directory.GetFiles(Path.Combine(pasta, "modulos"), "FAH.*.dll").Order())
    {
        var ctx = new AlcModulo(Path.GetFileNameWithoutExtension(arq), Path.Combine(pasta, "modulos"));
        using var ms = new MemoryStream(File.ReadAllBytes(arq));
        foreach (var t in ctx.LoadFromStream(ms).GetTypes().Where(t => typeof(FAH.Contrato.IModulo).IsAssignableFrom(t) && !t.IsAbstract)) mods.Add((FAH.Contrato.IModulo)Activator.CreateInstance(t)!);
    }
    var logs = new List<string>(); var host = new HostFalso(logs);
    foreach (var m in mods) m.Iniciar(host);
    var cad = new Cadeia(); cad.Definir(mods.SelectMany(m => m.Efeitos).Where(e => e.Lado == FAH.Contrato.Lado.Mic));
    rel.AppendLine("ordem mic: " + string.Join(" > ", cad.Efeitos.Select(e => e.Id)));
    var est = JsonDocument.Parse(File.ReadAllText(Path.Combine(pasta, "config", "estado.json"))).RootElement;
    foreach (var p in est.EnumerateObject()) foreach (var m in mods) if (m.Receber(p.Name, p.Value)) break;
    var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    using var r0 = new NAudio.Wave.WaveFileReader(args.Length > 2 ? args[2] : Path.Combine(raiz, "amostras", "1_original_com_muito_ruido.wav"));
    var sp = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(r0); var xl = new List<float>(); var bb = new float[sp.WaveFormat.Channels * 4800]; int nn;
    while ((nn = sp.Read(bb, 0, bb.Length)) > 0) for (int i = 0; i < nn; i += sp.WaveFormat.Channels) xl.Add(bb[i]);
    var x = xl.ToArray(); var y = new float[x.Length]; var buf = new float[2 * 480];
    for (int volta = 0; volta < 2; volta++)          // 1st pass: model loads in the background like at startup; 2nd = steady state
        for (int p = 0; p < x.Length; p += 480)
        {
            int q = Math.Min(480, x.Length - p);
            for (int i = 0; i < q; i++) buf[2 * i] = buf[2 * i + 1] = x[p + i];
            cad.Processar(buf.AsSpan(0, q * 2), q);
            for (int i = 0; i < q; i++) y[p + i] = buf[2 * i];
            if (volta == 0) Thread.Sleep(2);
        }
    (double piso, double voz) Niv(float[] s)
    {
        var l = new List<double>();
        for (int p = 4800; p + 960 <= s.Length; p += 960) { double e = 0; for (int i = 0; i < 960; i++) e += s[p + i] * s[p + i]; l.Add(10 * Math.Log10(e / 960 + 1e-12)); }
        l.Sort(); int k = Math.Max(1, l.Count * 15 / 100); return (l.Take(k).Average(), l.Skip(l.Count - k).Average());
    }
    var (a, b) = (Niv(x), Niv(y));
    foreach (var l in logs) rel.AppendLine("log: " + l);
    Ok("ruído (módulos instalados + estado salvo): voz limpa", a.piso - b.piso > 15, $"ruído {a.piso:F1} -> {b.piso:F1} dB, voz {a.voz:F1} -> {b.voz:F1} dB");
    if (args.Length > 3) { using var w = new NAudio.Wave.WaveFileWriter(args[3], NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)); w.WriteSamples(y, 0, y.Length); }
    Console.Write(rel); return falhas;
}
const int T = 48000;

// 1. EQ impulse response == analytic curve (graphic + tone + parametric, both orders)
{
    var aj = new Ajuste
    {
        Preamp = -3, Graves = 4, Agudos = 2,
        Bandas = [.. Modulo.Hz.Select((f, i) => new Banda(Tipo.PassaFaixa, f, Ajuste.QGrafico(10), new[] { 3, 2, 0, -2, 0, 1, 4, 5, 3, 1 }[i])),
            new Banda(Tipo.Pico, 900, 1.4, -4), new Banda(Tipo.PratGrave, 120, 0.7, 3), new Banda(Tipo.PratAguda, 9000, 0.7, -2), new Banda(Tipo.PassaBaixa, 140, 2, 5)],
    };
    var eq = new Equalizador(T); eq.Definir(aj);
    int n = 1 << 16; var x = new float[n * 2]; x[0] = x[1] = 1;
    eq.Processar(x, 1);                         // first block = crossfade from nothing (same set): impulse only
    eq.Processar(x.AsSpan(2), n - 1);
    double pior = 0;
    foreach (var f in new[] { 25.0, 40, 63, 90, 125, 200, 315, 500, 800, 1000, 1600, 2500, 4000, 6300, 8000, 10000, 12500, 16000, 19000 })
    {
        double re = 0, im = 0, w = 2 * Math.PI * f / T;
        for (int i = 0; i < n; i++) { re += x[2 * i] * Math.Cos(w * i); im -= x[2 * i] * Math.Sin(w * i); }
        double med = 20 * Math.Log10(Math.Sqrt(re * re + im * im)), teo = Equalizador.RespostaDb(aj, f, T);
        pior = Math.Max(pior, Math.Abs(med - teo));
    }
    Ok("EQ igual à curva", pior < 0.05, $"maior diferença {pior:F4} dB em 19 frequências");
}

// 2. changing settings every block does not click (1 kHz sine, 50 changes)
{
    var eq = new Equalizador(T); var rnd = new Random(1);
    int bl = 512, nb = 200; var y = new float[bl * nb * 2];
    for (int i = 0; i < bl * nb; i++) y[2 * i] = y[2 * i + 1] = (float)(0.25 * Math.Sin(2 * Math.PI * 1000 * i / T));
    for (int b = 0; b < nb; b++)
    {
        if (b % 4 == 0) eq.Definir(new Ajuste { Preamp = rnd.Next(-6, 3), Bandas = [.. Modulo.Hz.Select(f => new Banda(Tipo.PassaFaixa, f, 1.5, rnd.Next(-6, 7)))] });
        eq.Processar(y.AsSpan(b * bl * 2, bl * 2), bl);
    }
    double passo = 0, pico = 0;
    for (int i = 1; i < bl * nb; i++) { passo = Math.Max(passo, Math.Abs(y[2 * i] - y[2 * i - 2])); pico = Math.Max(pico, Math.Abs(y[2 * i])); }
    double natural = pico * 2 * Math.PI * 1000 / T;   // biggest step a clean sine of this peak has
    Ok("trocar ajuste sem estalo", passo < natural * 1.3, $"maior salto {passo:F4} (seno limpo {natural:F4})");
}

// 3. clock correction: producer 0.1% fast with jitter, 10 simulated minutes
{
    var lt = new Leitor("teste", 30);
    var rnd = new Random(2); var outp = new float[480 * 2]; double fase = 0, prod = 0, lido = 0;
    double maxFila = 0, minFila = 1e9, passo = 0; float ant = 0; long faltasDepois = 0;
    var blocoP = new float[2000 * 2];
    for (int k = 0; k < 60000; k++)                // 10 ms steps
    {
        prod += 480 * 1.001;
        if (k % 3 != 2 || rnd.NextDouble() < 0.3)   // capture arrives in jerks (13-25 ms)
        {
            int m = (int)prod; prod -= m;
            for (int i = 0; i < m; i++) { float v = (float)(0.3 * Math.Sin(fase)); fase += 2 * Math.PI * 440 / T; blocoP[2 * i] = blocoP[2 * i + 1] = v; }
            lt.Empurrar(blocoP.AsSpan(0, m * 2));
        }
        else prod += 0;
        if (lt.Ler(outp, 480))
            for (int i = 0; i < 480; i++) { if (k > 500) passo = Math.Max(passo, Math.Abs(outp[2 * i] - ant)); ant = outp[2 * i]; }
        if (k > 500) { maxFila = Math.Max(maxFila, lt.FilaMs); minFila = Math.Min(minFila, lt.FilaMs); }
        if (k == 500) faltasDepois = -lt.Subcargas;
        lido += 480;
    }
    faltasDepois += lt.Subcargas;
    double natural = 0.3 * 2 * Math.PI * 440 / T;
    Ok("relógio: sem faltas", faltasDepois == 0, $"{faltasDepois} faltas depois de 5 s (alvo final {lt.AlvoMs} ms)");
    Ok("relógio: fila não cresce", maxFila < lt.AlvoMs + 40, $"fila {minFila:F1}..{maxFila:F1} ms, velocidade {lt.RMin:F5}..{lt.RMax:F5}");
    Ok("relógio: som limpo", passo < natural * 1.05, $"maior salto {passo:F5} (seno limpo {natural:F5})");
}

// 4. limiter: never above ceiling, fixed 64-frame delay
{
    var lim = new Limitador(T) { Ligado = true, TetoDb = -1 };
    var rnd = new Random(3); int n = T * 5; var y = new float[n * 2]; var orig = new float[n * 2];
    for (int i = 0; i < n; i++) y[2 * i] = y[2 * i + 1] = orig[2 * i] = (float)((rnd.NextDouble() * 2 - 1) * (i % 9600 < 4800 && i < n - T ? 3 : 0.2));
    for (int b = 0; b < n; b += 441) lim.Processar(y.AsSpan(b * 2, Math.Min(441, n - b) * 2), Math.Min(441, n - b));
    double pk = y.Max(v => Math.Abs(v)), teto = Math.Pow(10, -1 / 20.0);
    // quiet tail (1 s after last loud part) passes untouched, delayed by 64
    int i0 = n - 2000; double dif = 0;
    for (int i = i0; i < i0 + 500; i++) dif = Math.Max(dif, Math.Abs(y[2 * i] - orig[2 * (i - 64)]));
    Ok("limitador: teto", pk <= teto + 1e-4, $"pico {20 * Math.Log10(pk):F2} dB (teto -1)");
    Ok("limitador: atraso fixo e soltura", dif < 1e-3, $"diferença na parte baixa {dif:F5}");
}

// 5. UI state -> engine settings
{
    var js = JsonDocument.Parse("""{"modo":"param","equ":true,"tone":false,"limit":1,"pre":-2,"g":[0,0,0,0,0,0,0,0,0,0],"bass":3,"treble":1,"teto":-2,"bandas":[{"t":"grave","f":90,"g":4,"q":0.7,"on":1},{"t":"pa","f":8000,"g":2,"q":1,"on":0}]}""").RootElement;
    var (aj, limite, teto) = Modulo.Ler(js);
    Ok("tela -> motor", aj.Bandas.Count == 2 && aj.Bandas[0].Tipo == Tipo.PratGrave && aj.Bandas[1].Tipo == Tipo.PassaAlta && !aj.Bandas[1].Ligada && !aj.TomLigado && aj.Preamp == -2 && limite && teto == -2,
        $"{aj.Bandas.Count} bandas, tom {aj.TomLigado}, limite {limite} {teto}");
    var g = JsonDocument.Parse("""{"modo":"grafico","g":[1,2,3,4,5,6,7,8,9,10],"equ":false}""").RootElement;
    var (a2, _, _) = Modulo.Ler(g);
    Ok("tela -> motor (gráfico)", a2.Bandas.Count == 10 && a2.Bandas[9].Freq == 16000 && a2.Bandas[9].Ganho == 10 && !a2.Ligado && Math.Abs(a2.Bandas[0].Q - 1.5) < 0.01, $"Q {a2.Bandas[0].Q:F3}");
    // AutoEQ: own bands + preamp, still active with EQU off; off = flat
    var q = JsonDocument.Parse("""{"modo":"grafico","g":[0,0,0,0,0,0,0,0,0,0],"equ":false,"autoeq":{"nome":"X","on":true,"pre":-6,"b":[["pico",1000,1,6],["pb",50,1,3]]}}""").RootElement;
    var (a3, _, _) = Modulo.Ler(q);
    double r1k = Equalizador.RespostaDb(a3, 1000), r100 = Equalizador.RespostaDb(a3, 100);
    var (a4, _, _) = Modulo.Ler(JsonDocument.Parse("""{"equ":true,"autoeq":{"on":false,"pre":-6,"b":[["pico",1000,1,6]]}}""").RootElement);
    double d1k = Equalizador.RespostaDb(a4, 1000);
    Ok("AutoEQ -> motor", a3.Correcao.Count == 1 && Math.Abs(r1k) < 0.1 && Math.Abs(r100 + 5.7) < 0.5 && Math.Abs(d1k) < 1e-6, $"1 kHz {r1k:F2} dB, 100 Hz {r100:F2} dB (EQU desligado), correção desligada {d1k:F2} dB");
}

// 6. 7.1 cable -> stereo
{
    var s8 = new float[8] { 0.1f, 0.2f, 0.4f, 0f, 0.2f, 0.2f, 0.1f, 0.1f }; var lr = new float[2];
    Fluxo.Descer(s8, lr, 1, 8);
    Ok("7.1 -> estéreo", Math.Abs(lr[0] - (0.1 + 0.707 * 0.4 + 0.1 + 0.05)) < 1e-4 && Math.Abs(lr[1] - (0.2 + 0.707 * 0.4 + 0.1 + 0.05)) < 1e-4, $"{lr[0]:F3} {lr[1]:F3}");
}

// 7. Windows default guard (simulated): switch, then give back
{
    var dir = Path.Combine(Path.GetTempPath(), "fah-teste-" + Environment.ProcessId); Directory.CreateDirectory(dir);
    var sim = Path.Combine(dir, "padrao.txt"); File.WriteAllText(sim, "{placa-real}");
    Environment.SetEnvironmentVariable("FAH_SIMULAR_PADRAO", sim);
    var g = new PadraoWindows(Path.Combine(dir, "guarda.json"));
    g.Trocar("{cabo}"); var durante = PadraoWindows.Atual();
    g.Trocar("{cabo}");                           // second switch must not overwrite the original
    var devolveu = g.Devolver(); var depois = PadraoWindows.Atual();
    Ok("guarda do padrão", durante == "{cabo}" && devolveu && depois == "{placa-real}" && !File.Exists(Path.Combine(dir, "guarda.json")), $"durante {durante}, depois {depois}");
    // 02/11: Windows already on FAH's cable (crash, guard lost) -> exit goes to the real device, never stays on the cable
    File.WriteAllText(sim, "{cabo}");
    g.Trocar("{cabo}", "{placa-real}"); var volta = g.Devolver() ? PadraoWindows.Atual() : null;
    // old guard file (only "saida") still restores; second Devolver = nothing pending
    File.WriteAllText(Path.Combine(dir, "guarda.json"), "{\"saida\":\"{antigo}\"}"); File.WriteAllText(sim, "{cabo}");
    var velho = g.Devolver() ? PadraoWindows.Atual() : null; var deNovo = g.Devolver();
    // user picked another device while FAH ran: exit goes there
    File.WriteAllText(sim, "{placa-real}"); g.Trocar("{cabo}", "{placa-real}"); g.NovoOriginal("{fone}"); g.Devolver(); var novo = PadraoWindows.Atual();
    Ok("guarda do padrão: cabo preso, arquivo antigo, troca no meio", volta == "{placa-real}" && velho == "{antigo}" && !deNovo && novo == "{fone}", $"{volta} {velho} {deNovo} {novo}");
    Environment.SetEnvironmentVariable("FAH_SIMULAR_PADRAO", null);
    Directory.Delete(dir, true);
}

// 8. CPU: EQ with 10 bands + tone + limiter on 60 s of audio
{
    var m = new Modulo(); m.Eq.Definir(new Ajuste { Graves = 3, Agudos = 2, Bandas = [.. Modulo.Hz.Select(f => new Banda(Tipo.PassaFaixa, f, 1.5, 3))] });
    m.Limitador.Ligado = true;
    var c = new Cadeia(); c.Definir(m.Efeitos);
    var y = new float[1024 * 2]; var rnd = new Random(4); for (int i = 0; i < y.Length; i++) y[i] = (float)(rnd.NextDouble() * 0.2 - 0.1);
    var sw = System.Diagnostics.Stopwatch.StartNew(); int blocos = T * 60 / 1024;
    for (int b = 0; b < blocos; b++) c.Processar(y, 1024);
    double pct = 100 * sw.Elapsed.TotalSeconds / 60;
    Ok("custo do EQ", pct < 5, $"{pct:F2}% de 1 núcleo");
}

// 8b. module switch: "ligado".eq=false bypasses EQ + tone + AutoEQ + limiter (sound intact); back on = applies again
{
    var m = new Modulo(); var c = new Cadeia(); c.Definir(m.Efeitos);
    m.Receber("eq2", JsonDocument.Parse("""{"modo":"param","equ":true,"tone":true,"limit":true,"pre":0,"bass":5,"treble":10,"bandas":[{"t":"grave","f":278,"g":5,"q":0.6,"on":1}],"autoeq":{"on":true,"pre":-5.2,"b":[["grave",105,0.7,5]]}}""").RootElement);
    bool Intacto() { var rnd = new Random(6); bool ok = true; for (int k = 0; k < 40; k++) { var x = new float[1024 * 2]; for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() * .2 - .1); var cp = (float[])x.Clone(); c.Processar(x, 1024); if (k > 20) ok &= x.AsSpan().SequenceEqual(cp); } return ok; }
    bool ligado = !Intacto();
    bool dono = m.Receber("ligado", JsonDocument.Parse("""{"eq":false,"efeitos":true}""").RootElement);
    bool desl = Intacto();
    m.Receber("ligado", JsonDocument.Parse("""{"eq":true}""").RootElement);
    bool volta = !Intacto();
    Ok("chave do Equalizador", ligado && desl && volta && !dono && m.Limitador.Ligado, $"ligado mexe {ligado}, desligado intacto {desl}, religado mexe {volta}, 'ligado' livre {!dono}");
}

// 9. Estéreo: neutral = untouched; mono = L equals R; balance fully left = R silent; width change without click
{
    var e = new Fx.Estereo(); e.Preparar(T);
    var x = new float[1024 * 2]; var rnd = new Random(5); for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() - .5);
    var y = (float[])x.Clone(); e.Processar(y, 1024); bool igual = y.SequenceEqual(x);
    e.Mono = true; for (int b = 0; b < 3; b++) { y = (float[])x.Clone(); e.Processar(y, 1024); }
    double difMono = 0; for (int i = 0; i < 1024; i++) difMono = Math.Max(difMono, Math.Abs(y[2 * i] - y[2 * i + 1]));
    e.Mono = false; e.Balanco = -1; for (int b = 0; b < 3; b++) { y = (float[])x.Clone(); e.Processar(y, 1024); }
    double dir = 0; for (int i = 0; i < 1024; i++) dir = Math.Max(dir, Math.Abs(y[2 * i + 1]));
    var e2 = new Fx.Estereo(); e2.Preparar(T); int bl = 512, nb = 200; var s = new float[bl * nb * 2];
    for (int i = 0; i < bl * nb; i++) { s[2 * i] = (float)(.25 * Math.Sin(2 * Math.PI * 1000 * i / T)); s[2 * i + 1] = (float)(.25 * Math.Sin(2 * Math.PI * 1000 * i / T + 1)); }
    double natural = 0; for (int i = 1; i < bl * nb; i++) natural = Math.Max(natural, Math.Abs(s[2 * i] - s[2 * i - 2]));
    for (int b = 0; b < nb; b++) { if (b % 4 == 0) e2.Largura = rnd.NextDouble() * 1.25; e2.Processar(s.AsSpan(b * bl * 2, bl * 2), bl); }
    double passo = 0; for (int i = 1; i < bl * nb; i++) passo = Math.Max(passo, Math.Max(Math.Abs(s[2 * i] - s[2 * i - 2]), Math.Abs(s[2 * i + 1] - s[2 * i - 1])));
    Ok("estéreo", igual && difMono < 1e-6 && dir < 1e-6 && passo < natural * 2.5, $"neutro intacto {igual}, mono L-R {difMono:E1}, direita no balanço todo à esquerda {dir:E1}, salto ao mexer largura {passo:F4} (seno {natural:F4})");
}

// 10. Crossfeed: off = untouched; bass reaches the other ear 'feed' dB lower (bs2b), treble much lower; on/off without click
{
    double Cruzado(string nivel, double f)
    {
        var c = new Fx.Crossfeed { Ligado = true, Nivel = nivel }; c.Preparar(T);
        int n = T; var x = new float[n * 2]; for (int i = 0; i < n; i++) x[2 * i] = (float)(.5 * Math.Sin(2 * Math.PI * f * i / T));
        c.Processar(x, n);
        double pl = 0, pr = 0; for (int i = n / 2; i < n; i++) { pl += x[2 * i] * x[2 * i]; pr += x[2 * i + 1] * x[2 * i + 1]; }
        return 10 * Math.Log10(pl / pr);
    }
    var off = new Fx.Crossfeed(); off.Preparar(T); var z = new float[512 * 2]; z[0] = 1; z[3] = -.5f; var z0 = (float[])z.Clone(); off.Processar(z, 512);
    double sm = Cruzado("suave", 30), md = Cruzado("medio", 30), fo = Cruzado("forte", 30), ag = Cruzado("medio", 8000);
    var t = new Fx.Crossfeed { Nivel = "forte" }; t.Preparar(T); int bl = 480, nb = 200; var s = new float[bl * nb * 2];
    for (int i = 0; i < bl * nb; i++) s[2 * i] = (float)(.25 * Math.Sin(2 * Math.PI * 1000 * i / T));
    for (int b = 0; b < nb; b++) { if (b % 10 == 0) t.Ligado = !t.Ligado; t.Processar(s.AsSpan(b * bl * 2, bl * 2), bl); }
    double passo = 0; for (int i = 1; i < bl * nb; i++) passo = Math.Max(passo, Math.Max(Math.Abs(s[2 * i] - s[2 * i - 2]), Math.Abs(s[2 * i + 1] - s[2 * i - 1])));
    double natural = .25 * 2 * Math.PI * 1000 / T;
    Ok("crossfeed", z.SequenceEqual(z0) && Math.Abs(sm - 9.5) < .5 && Math.Abs(md - 6) < .5 && Math.Abs(fo - 4.5) < .5 && ag > 15 && passo < natural * 1.3,
        $"desligado intacto {z.SequenceEqual(z0)}; grave do outro lado {sm:F1}/{md:F1}/{fo:F1} dB abaixo (esperado 9,5/6/4,5); agudo {ag:F0} dB abaixo; salto ao ligar/desligar {passo:F4} (seno {natural:F4})");
}

// 11. Reverb: every preset makes a tail that dies out, stays bounded on 10 s of noise; off again = untouched
{
    var nomes = new Dictionary<string, double[]>
    {
        ["Reverb leve"] = [.602, .802, .317, .251, .375, .348, .441], ["Sala pequena"] = [0, 1, 1, 0, .32, .623, .529], ["Estúdio"] = [.030, .110, 1, .036, .47, .437, .989],
        ["Palco"] = [.245, .705, .503, .121, .45, .425, .412], ["Auditório"] = [0, .697, .814, 0, .5, 0, .898], ["Grande salão"] = [.519, .709, 0, .946, .44, .526, .259],
        ["Estádio"] = [.993, .621, .828, .445, .376, .739, .840], ["Eco"] = [.727, .905, .272, .541, .37, .583, .363],
    };
    var ruins = new List<string>(); double piorPico = 0;
    foreach (var (nome, p) in nomes)
    {
        var r = new Fx.Reverb(T) { Ligado = true }; r.Definir(p);
        int n = T * 8; var x = new float[n * 2]; x[0] = x[1] = 1;
        for (int i = 0; i < n; i += 1024) r.Processar(x.AsSpan(2 * i, 2 * Math.Min(1024, n - i)), Math.Min(1024, n - i));
        double En(int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[2 * i] * x[2 * i] + x[2 * i + 1] * x[2 * i + 1]; return s; }
        double cedo = En(T / 10, T / 2), meio = En(T * 3, T * 4), tarde = En(T * 7, T * 8);
        var ru = new float[T * 10 * 2]; var rnd = new Random(6); for (int i = 0; i < ru.Length; i++) ru[i] = (float)(rnd.NextDouble() - .5);
        for (int i = 0; i + 1024 <= T * 10; i += 1024) r.Processar(ru.AsSpan(2 * i, 2 * 1024), 1024);
        double pico = ru.Max(v => Math.Abs(v)); piorPico = Math.Max(piorPico, pico);
        if (!(cedo > 1e-4 && meio < cedo * .2 && tarde < meio * .5) || double.IsNaN(pico) || pico > 2) ruins.Add($"{nome} (cedo {cedo:E1}, 3 s {meio:E1}, 7 s {tarde:E1}, pico {pico:F2})");
    }
    var d = new Fx.Reverb(T) { Ligado = true }; var bz = new float[1024 * 2];
    for (int k = 0; k < 20; k++) d.Processar(bz, 1024);
    d.Ligado = false; var q = new float[1024 * 2]; var rq = new Random(7);
    bool intacto = false;
    for (int k = 0; k < 400 && !intacto; k++) { for (int i = 0; i < q.Length; i++) q[i] = (float)(rq.NextDouble() - .5); var q0 = (float[])q.Clone(); d.Processar(q, 1024); intacto = q.SequenceEqual(q0); }
    Ok("reverb", ruins.Count == 0 && intacto, ruins.Count == 0 ? $"8 cenas com cauda que cai até 8 s, pico no ruído {piorPico:F2}; desligado volta intacto {intacto}" : "com problema: " + string.Join("; ", ruins));
}

// 12. UI state -> effects (module switch off turns all three off)
{
    var m = new Fx.Modulo();
    m.Receber("efeitos", JsonDocument.Parse("""{"est":{"on":true,"bal":-40,"larg":125,"mono":false},"cf":{"on":true,"nivel":"forte"},"rv":{"on":true,"preset":"Palco","p":[24,70,50,12,100,42,41]}}""").RootElement);
    bool a = m.Estereo.Balanco == -.4 && m.Estereo.Largura == 1.25 && m.Crossfeed.Ligado && m.Crossfeed.Nivel == "forte" && m.Reverb.Ligado;
    bool dono = m.Receber("ligado", JsonDocument.Parse("""{"efeitos":false,"som71":true}""").RootElement);
    bool b = m.Estereo.Balanco == 0 && m.Estereo.Largura == 1 && !m.Crossfeed.Ligado && !m.Reverb.Ligado;
    // 30/10: width paused over Som 7.1 (it spread the virtual speakers), balance stays; back when 7.1 is off
    m.Receber("ligado", JsonDocument.Parse("""{"efeitos":true,"som71":true}""").RootElement);
    bool p71 = m.Estereo.Largura == 1 && m.Estereo.Balanco == -.4;
    m.Receber("ligado", JsonDocument.Parse("""{"efeitos":true,"som71":false}""").RootElement);
    p71 &= m.Estereo.Largura == 1.25;
    Ok("efeitos: largura pausada com o Som 7.1", p71, $"pausa com 7.1 e volta sem ele: {p71}");
    Ok("estado dos efeitos", a && b && !dono, $"aplica {a}, chave do módulo desliga os 3 {b}, 'ligado' fica livre para os outros {!dono}");
}

// 12b. module switch on real sound: "ligado".efeitos=false = sound intact; back on = applies again
{
    var m = new Fx.Modulo(); var c = new Cadeia(); c.Definir(m.Efeitos);
    m.Receber("efeitos", JsonDocument.Parse("""{"est":{"on":true,"bal":-2,"larg":125,"mono":false},"cf":{"on":true,"nivel":"suave"},"rv":{"on":true,"preset":"Palco","p":[24,70,50,12,100,42,41]}}""").RootElement);
    m.Receber("ligado", JsonDocument.Parse("""{"efeitos":true,"som71":false}""").RootElement);
    bool Intacto() { var rnd = new Random(6); bool ok = true; for (int k = 0; k < 600; k++) { var x = new float[1024 * 2]; for (int i = 0; i < x.Length; i++) x[i] = (float)(rnd.NextDouble() * .2 - .1); var cp = (float[])x.Clone(); c.Processar(x, 1024); if (k > 500) ok &= x.AsSpan().SequenceEqual(cp); } return ok; }
    bool ligado = !Intacto();
    m.Receber("ligado", JsonDocument.Parse("""{"efeitos":false,"som71":false}""").RootElement);
    bool desl = Intacto();
    m.Receber("ligado", JsonDocument.Parse("""{"efeitos":true,"som71":false}""").RootElement);
    bool volta = !Intacto();
    Ok("chave dos Efeitos no som", ligado && desl && volta, $"ligado mexe {ligado}, desligado intacto {desl}, religado mexe {volta}");
    // 29/10: Som 7.1 is its own dock toggle: the Efeitos switch no longer touches it
    var es = new Esp.Modulo();
    es.Receber("ligado", JsonDocument.Parse("""{"efeitos":false,"som71":true}""").RootElement); bool s71Fica = es.Motor.Ligado;
    es.Receber("ligado", JsonDocument.Parse("""{"efeitos":true,"som71":false}""").RootElement); bool s71Desl = !es.Motor.Ligado;
    Ok("Som 7.1 só obedece o próprio botão", s71Fica && s71Desl, $"Efeitos desligado mantém {s71Fica}, botão desliga {s71Desl}");
}

// 13. CPU: stereo + crossfeed + biggest reverb on 60 s of audio
{
    var m = new Fx.Modulo();
    m.Receber("efeitos", JsonDocument.Parse("""{"est":{"on":true,"bal":10,"larg":120},"cf":{"on":true},"rv":{"on":true,"p":[100,70,80,40,100,50,50]}}""").RootElement);
    var c = new Cadeia(); c.Definir(m.Efeitos);
    var y = new float[1024 * 2]; var rnd = new Random(8); for (int i = 0; i < y.Length; i++) y[i] = (float)(rnd.NextDouble() * .2 - .1);
    var sw = System.Diagnostics.Stopwatch.StartNew(); int blocos = T * 60 / 1024;
    for (int b = 0; b < blocos; b++) c.Processar(y, 1024);
    double pct = 100 * sw.Elapsed.TotalSeconds / 60;
    Ok("custo dos efeitos", pct < 5, $"{pct:F2}% de 1 núcleo");
}

// 14-18: Som 7.1 (Modulos\Espacial)
{
    var sw0 = System.Diagnostics.Stopwatch.StartNew();
    var m = new Esp.Modulo(); m.Preparar().Wait();
    m.Receber("s71", JsonDocument.Parse("""{"perfil":"Jogo","sala":50}""").RootElement);
    double prep = sw0.Elapsed.TotalSeconds;
    double Rms(float[] v, int ch, int ini) { double s = 0; int n = 0; for (int i = ini; i < v.Length / 2; i++) { s += v[i * 2 + ch] * v[i * 2 + ch]; n++; } return Math.Sqrt(s / n); }
    float[] Ruido(int n, int ch, Func<int, int, double> g) { var r = new Random(5); var y = new float[n * ch]; for (int i = 0; i < n; i++) { double v = r.NextDouble() * .2 - .1; for (int c = 0; c < ch; c++) y[i * ch + c] = (float)(v * g(i, c)); } return y; }
    var c = new Cadeia(); c.Definir(m.Efeitos);
    int n = 1024 * 141;                     // ~3 s, whole blocks

    // 14. 7.1 cable: only SR (channel 7) = right ear; only BL (4) = left ear; equal volume (energy 1 per speaker)
    m.Motor.Ligado = true;
    var lr = new float[n * 2];
    var s8 = Ruido(n, 8, (i, ch) => ch == 7 ? 1 : 0);
    for (int i = 0; i < n; i += 1024) c.Processar8(s8.AsSpan(i * 8, 1024 * 8), lr.AsSpan(i * 2, 1024 * 2), 1024);
    double sr = 20 * Math.Log10(Rms(lr, 1, T) / Rms(lr, 0, T)), nivel = 20 * Math.Log10(Math.Sqrt(Rms(lr, 0, T) * Rms(lr, 0, T) + Rms(lr, 1, T) * Rms(lr, 1, T)) / 0.0577);
    s8 = Ruido(n, 8, (i, ch) => ch == 4 ? 1 : 0);
    for (int i = 0; i < n; i += 1024) c.Processar8(s8.AsSpan(i * 8, 1024 * 8), lr.AsSpan(i * 2, 1024 * 2), 1024);
    double bl = 20 * Math.Log10(Rms(lr, 0, T) / Rms(lr, 1, T));
    Ok("7.1: cada caixa no seu lado", sr > 6 && bl > 3 && Math.Abs(nivel) < 3, $"SR direita +{sr:F1} dB, BL esquerda +{bl:F1} dB, volume {nivel:+0.0;-0.0} dB (HRTF pronta em {prep:F2} s)");

    // 15. stereo content: hard left stays left, center stays centered, same loudness as dry
    var st = Ruido(n, 2, (i, ch) => ch == 0 ? 1 : 0);
    for (int i = 0; i < n; i += 1024) c.Processar(st.AsSpan(i * 2, 1024 * 2), 1024);
    double esq = 20 * Math.Log10(Rms(st, 0, T) / Rms(st, 1, T));
    var ce = Ruido(n, 2, (i, ch) => 1);
    for (int i = 0; i < n; i += 1024) c.Processar(ce.AsSpan(i * 2, 1024 * 2), 1024);
    double cen = 20 * Math.Log10(Rms(ce, 0, T) / Rms(ce, 1, T)), vol = 20 * Math.Log10(Math.Sqrt((Rms(ce, 0, T) * Rms(ce, 0, T) + Rms(ce, 1, T) * Rms(ce, 1, T)) / 2) / 0.0577);
    Ok("estéreo: esquerda fica à esquerda, centro no meio", esq > 6 && Math.Abs(cen) < 1.5 && Math.Abs(vol) < 3, $"esquerda +{esq:F1} dB, centro {cen:+0.0;-0.0} dB, volume {vol:+0.0;-0.0} dB");

    // 15b. stereo + Música: "Graves no subwoofer" adds low end, "Reforçar voz" changes the mid (both did nothing here before 28/10)
    double Saida(string cfg, Func<int, double> sinal)
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse(cfg).RootElement); mm.Motor.Ligado = true;
        var cc = new Cadeia(); cc.Definir(mm.Efeitos);
        var v = new float[n * 2]; for (int i = 0; i < n; i++) v[i * 2] = v[i * 2 + 1] = (float)sinal(i);
        for (int i = 0; i < n; i += 1024) cc.Processar(v.AsSpan(i * 2, 1024 * 2), 1024);
        return Rms(v, 0, T);
    }
    Func<int, double> grave = (i) => 0.1 * Math.Sin(2 * Math.PI * 50 * i / T);
    Func<double, Func<int, double>> tom = (f) => (i) => 0.1 * Math.Sin(2 * Math.PI * f * i / T);
    double subDb = 20 * Math.Log10(Saida("""{"perfil":"Música","sala":17,"sub":true,"ouvido":"kemar"}""", grave) / Saida("""{"perfil":"Música","sala":17,"sub":false,"ouvido":"kemar"}""", grave));
    // 30/10: averaged over 5 tones in ±1/6 octave: a single pure tone through the room (direct + reflections + tail) sits on
    // whatever comb peak/notch is there and read +3 dB at 1.2 kHz on KEMAR; the band is what the ear hears
    double Voz(double f) { double on = 0, off = 0; foreach (var k in new[] { -2, -1, 0, 1, 2 }) { double fk = f * Math.Pow(2, k / 12.0);
        on += Math.Pow(Saida("""{"perfil":"Música","voz":true,"ouvido":"kemar"}""", tom(fk)), 2); off += Math.Pow(Saida("""{"perfil":"Música","voz":false,"ouvido":"kemar"}""", tom(fk)), 2); }
        return 10 * Math.Log10(on / off); }
    double corpo = Voz(220), forca = Voz(1200), clareza = Voz(3000), fundo = Voz(60);
    double Saida8(string cfg)
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse(cfg).RootElement); mm.Motor.Ligado = true;
        var cc = new Cadeia(); cc.Definir(mm.Efeitos);
        var v8 = new float[n * 8]; var o8 = new float[n * 2];
        for (int i = 0; i < n; i++) { float g = (float)grave(i); v8[i * 8] = v8[i * 8 + 1] = v8[i * 8 + 3] = g; }
        for (int i = 0; i < n; i += 1024) cc.Processar8(v8.AsSpan(i * 8, 1024 * 8), o8.AsSpan(i * 2, 1024 * 2), 1024);
        return Rms(o8, 0, T);
    }
    double sub8Db = 20 * Math.Log10(Saida8("""{"perfil":"Jogo","sala":50,"sub":true,"ouvido":"kemar"}""") / Saida8("""{"perfil":"Jogo","sala":50,"sub":false,"ouvido":"kemar"}"""));
    // 29/10: the approved head "2" (h11, now the default): sub timed for it (Brir.AjusteSub), voice boost adds and never cuts
    double sub11Tom = 20 * Math.Log10(Saida("""{"perfil":"Música","sala":17,"sub":true,"ouvido":"h11"}""", grave) / Saida("""{"perfil":"Música","sala":17,"sub":false,"ouvido":"h11"}""", grave));
    // 01/11: Música got the big room (no more "bolha"): its reflections land 50-150 ms late, so one pure 50 Hz tone sits on a comb
    // peak/notch (same as the 30/10 voice case) → band of 5 tones ±2 semitones around 50 Hz, what the ear hears
    double sub11 = 10 * Math.Log10(new[] { -2, -1, 0, 1, 2 }.Select(k => 50 * Math.Pow(2, k / 12.0)).Sum(f =>
        Math.Pow(Saida("""{"perfil":"Música","sala":17,"sub":true,"ouvido":"h11"}""", i => 0.1 * Math.Sin(2 * Math.PI * f * i / T)), 2))
        / new[] { -2, -1, 0, 1, 2 }.Select(k => 50 * Math.Pow(2, k / 12.0)).Sum(f =>
        Math.Pow(Saida("""{"perfil":"Música","sala":17,"sub":false,"ouvido":"h11"}""", i => 0.1 * Math.Sin(2 * Math.PI * f * i / T)), 2)));
    double sub811 = 20 * Math.Log10(Saida8("""{"perfil":"Jogo","sala":50,"sub":true,"ouvido":"h11"}""") / Saida8("""{"perfil":"Jogo","sala":50,"sub":false,"ouvido":"h11"}"""));
    double Voz11(double f) => 20 * Math.Log10(Saida("""{"perfil":"Música","sala":17,"voz":true,"ouvido":"h11"}""", tom(f)) / Saida("""{"perfil":"Música","sala":17,"voz":false,"ouvido":"h11"}""", tom(f)));
    var voz11 = new[] { Voz11(220), Voz11(1200), Voz11(3000) };
    // 30/10: Música = small room again → less room in the energy norm → sub share ~0.1 dB smaller (still aligned; misaligned ≈ 0 or less)
    // 01/11: big room in Música → h11 band +1.2 dB (was > 1.4); still aligned (misaligned ≈ 0), limit 1.0
    Ok("ouvido 2 (padrão): graves no sub e reforço de voz funcionam", sub11 > 1.0 && sub811 > 0.9 && voz11.All(d => d > 0.1 && d < 2),
        $"50 Hz com sub +{sub11:F1} dB (tom puro +{sub11Tom:F1}) (cabo 7.1 +{sub811:F1} dB), voz 220 Hz/1,2 kHz/3 kHz {string.Join(" / ", voz11.Select(d => d.ToString("+0.0;-0.0")))} dB");
    Ok("estéreo: graves no sub e reforço de voz funcionam", subDb > 1 && sub8Db > 1, $"50 Hz com sub +{subDb:F1} dB (cabo 7.1 +{sub8Db:F1} dB)");
    // 15b2. leveler (29/10): quiet sound rises in Jogo, untouched in Música, loud and near-silence untouched
    double Sobe(string perfil, double amp) => 20 * Math.Log10(Saida($$"""{"perfil":"{{perfil}}","sala":50}""", (i) => amp * Math.Sin(2 * Math.PI * 1000 * i / T)) / amp
        / (Saida($$"""{"perfil":"{{perfil}}","sala":50}""", tom(1000)) / 0.1));
    double nivJ = Sobe("Jogo", 0.003), nivM = Sobe("Música", 0.003), nivS = Sobe("Jogo", 0.0002);
    Ok("nivelador: som baixo sobe em Jogo, Música igual, silêncio não sobe", nivJ > 6 && nivJ < 9.5 && Math.Abs(nivM) < 0.3 && nivS < 1,
        $"Jogo baixinho +{nivJ:F1} dB, Música {nivM:+0.0;-0.0;0.0} dB, quase silêncio {nivS:+0.0;-0.0;0.0} dB");
    // 15b2. every head (29/10): loads, right speaker louder in the right ear, front and back differ; module switches and comes back
    {
        Esp.Brir.Preparar(); var ruins = new List<string>();
        foreach (var id in Esp.Brir.Ouvidos)
        {
            Esp.Brir.Usar(id);
            var (d90, _, _) = Esp.Brir.Caixa(90, true, 1); var (dm90, _, _) = Esp.Brir.Caixa(-90, true, 1);
            double ild = 10 * Math.Log10(Esp.Brir.Energia(d90[1]) / Esp.Brir.Energia(d90[0])), ildm = 10 * Math.Log10(Esp.Brir.Energia(dm90[0]) / Esp.Brir.Energia(dm90[1]));
            var (f, _, _) = Esp.Brir.Caixa(30, true, 1); var (t, _, _) = Esp.Brir.Caixa(150, true, 1);
            double dif = 0; for (int i = 0; i < f[0].Length; i++) dif += Math.Abs(f[0][i] - t[0][i]);
            if (Esp.Brir.Ouvido != id || ild < 6 || ildm < 6 || dif < 1e-3) ruins.Add($"{id} ILD {ild:F1}/{ildm:F1} dif {dif:F3}");
        }
        var mo = new Esp.Modulo(); mo.Preparar().Wait();
        mo.Receber("s71", JsonDocument.Parse("""{"perfil":"Jogo","sala":50,"ouvido":"h20"}""").RootElement); var em = Esp.Brir.Ouvido;
        mo.Receber("s71", JsonDocument.Parse("""{"perfil":"Jogo","sala":50,"ouvido":"xx"}""").RootElement);
        Ok("ouvidos: todos carregam, lado certo, frente ≠ trás; módulo troca", ruins.Count == 0 && em == "h20" && Esp.Brir.Ouvido == "kemar",
            ruins.Count == 0 ? $"{Esp.Brir.Ouvidos.Length} conjuntos ok, troca h20 → desconhecido = kemar" : string.Join("; ", ruins));
    }
    {   // 01/11: Música room got treble (reflections 12 kHz, tail 5.5 kHz; was 6/4.5): measure it at the ear, front-left speaker, head h11
        Esp.Brir.Usar("h11");
        const int NB = 32768;
        var fb = new Esp.Fft(NB); double[] reb = new double[NB], imb = new double[NB];
        var sa = Esp.Brir.Salas[0];
        var (dirb, kb, _) = Esp.Brir.Caixa(-30, true, 0);
        double Banda(double[] rev, bool comDireto, double de, double ate)
        {
            int m = rev.Length / 2; double p = 0;
            for (int e = 0; e < 2; e++)
            {
                var x = new double[NB];
                if (comDireto) for (int i = 0; i < dirb[e].Length; i++) x[i] += dirb[e][i];
                for (int i = 0; i < m; i++) x[i] += kb * rev[e * m + i];
                fb.Real(x, reb, imb);
                for (int b = (int)(de * NB / 48000); b <= (int)(ate * NB / 48000); b++) p += reb[b] * reb[b] + imb[b] * imb[b];
            }
            return p;
        }
        double[] revNova = Esp.Brir.Reverb(sa), revVelha = Esp.Brir.Reverb(sa with { PbEco = 6000, PbCauda = 4500 });
        double sala = 10 * Math.Log10(Banda(revNova, false, 8000, 12000) / Banda(revVelha, false, 8000, 12000));
        double ouvido = 10 * Math.Log10(Banda(revNova, true, 8000, 12000) / Banda(revVelha, true, 8000, 12000));
        Esp.Brir.Usar("kemar");
        Ok("sala do Música tem brilho", sala > 3 && ouvido > 0 && ouvido < 3 && Esp.Brir.Salas[1].PbEco == 6000 && Esp.Brir.Salas[2].PbCauda == 4500,
            $"8-12 kHz: eco da sala {sala:+0.0;-0.0} dB, som final no ouvido {ouvido:+0.0;-0.0} dB; Jogo e Filme sem mudança");
    }
    // 15c. 7.1 switch off with the cable fixed in 8 ch (28/10): stereo music in FL/FR = exactly the stereo-cable path;
    // 7.1 game (C, LFE, back) folds into both sides
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse("""{"perfil":"Música","sala":17}""").RootElement); mm.Motor.Ligado = true;
        var c8 = new Cadeia(); c8.Definir(mm.Efeitos); c8.Estereo = true;
        var mm2 = new Esp.Modulo(); mm2.Preparar().Wait(); mm2.Receber("s71", JsonDocument.Parse("""{"perfil":"Música","sala":17}""").RootElement); mm2.Motor.Ligado = true;
        var c2 = new Cadeia(); c2.Definir(mm2.Efeitos);
        var s2 = Ruido(n, 2, (i, ch) => ch == 0 ? 1 : 0.5); var v8 = new float[n * 8]; var o8 = new float[n * 2];
        for (int i = 0; i < n; i++) { v8[i * 8] = s2[i * 2]; v8[i * 8 + 1] = s2[i * 2 + 1]; }
        for (int i = 0; i < n; i += 1024) { c8.Processar8(v8.AsSpan(i * 8, 1024 * 8), o8.AsSpan(i * 2, 1024 * 2), 1024); c2.Processar(s2.AsSpan(i * 2, 1024 * 2), 1024); }
        double dif = 0; for (int i = 0; i < n * 2; i++) dif = Math.Max(dif, Math.Abs(o8[i] - s2[i]));
        var j8 = new float[n * 8]; var jo = new float[n * 2];
        for (int i = 0; i < n; i++) { j8[i * 8 + 2] = j8[i * 8 + 3] = j8[i * 8 + 4] = (float)grave(i); }
        for (int i = 0; i < n; i += 1024) c8.Processar8(j8.AsSpan(i * 8, 1024 * 8), jo.AsSpan(i * 2, 1024 * 2), 1024);
        double jl = Rms(jo, 0, T), jr = Rms(jo, 1, T);
        Ok("chave 7.1 desligada (cabo fica em 7.1): estéreo igual, jogo 7.1 vira estéreo", dif < 1e-5 && jl > 0.05 && jr > 0.03,
            $"diferença máx {dif:E1}, jogo esquerda {jl:F3} direita {jr:F3}");
    }
    // "Reforçar voz" = enhancer: body + strength + clarity, moderate (not in front of everything), kick/bass untouched
    Ok("realçador de voz: corpo, força e clareza, sem exagero", new[] { corpo, forca, clareza }.All(d => d > 0.4 && d < 2) && Math.Abs(fundo) < 0.6,
        $"220 Hz {corpo:+0.0;-0.0} dB, 1,2 kHz {forca:+0.0;-0.0} dB, 3 kHz {clareza:+0.0;-0.0} dB, 60 Hz {fundo:+0.0;-0.0} dB");

    // 15d. always the same boost, loud or soft: the singing keeps its expression (28/10: a level ride flattened it)
    double Salto(bool voz)
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse(voz ? """{"perfil":"Música","sala":17,"voz":true}""" : """{"perfil":"Música","sala":17,"voz":false}""").RootElement); mm.Motor.Ligado = true;
        var cc = new Cadeia(); cc.Definir(mm.Efeitos);
        int meio = n / 2; var v = new float[n * 2];
        for (int i = 0; i < n; i++) v[i * 2] = v[i * 2 + 1] = (float)((i < meio ? 0.02 : 0.2) * Math.Sin(2 * Math.PI * 3000 * i / T));
        for (int i = 0; i < n; i += 1024) cc.Processar(v.AsSpan(i * 2, 1024 * 2), 1024);
        double s = 0; int a = meio + T / 10, b = meio + T / 2; for (int i = a; i < b; i++) s += v[i * 2] * v[i * 2];
        return Math.Sqrt(s / (b - a));
    }
    double altoDb = 20 * Math.Log10(Salto(true) / Salto(false));
    Ok("realçador de voz: mesmo reforço no forte e no fraco (mantém a expressão)", Math.Abs(altoDb - clareza) < 0.3, $"voz subindo 20 dB: {altoDb:+0.0;-0.0;0.0} dB (parado {clareza:+0.0;-0.0} dB)");

    // 15e. the boost stays out of the room: after the voice stops, the echo is the same with the switch on or off
    // (28/10 44: the room's side-wall reflections got the boost = "two giant plates aiming the voice at me")
    double Eco(bool voz)
    {
        var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse(voz ? """{"perfil":"Música","sala":90,"voz":true}""" : """{"perfil":"Música","sala":90,"voz":false}""").RootElement); mm.Motor.Ligado = true;
        var cc = new Cadeia(); cc.Definir(mm.Efeitos);
        int meio = n / 2; var v = new float[n * 2];
        for (int i = 0; i < meio; i++) v[i * 2] = v[i * 2 + 1] = (float)(0.1 * Math.Sin(2 * Math.PI * 2000 * i / T));
        for (int i = 0; i < n; i += 1024) cc.Processar(v.AsSpan(i * 2, 1024 * 2), 1024);
        double s = 0; int a = meio + T / 10, b = meio + T * 3 / 10; for (int i = a; i < b; i++) s += v[i * 2] * v[i * 2];
        return Math.Sqrt(s / (b - a));
    }
    double ecoDb = 20 * Math.Log10(Eco(true) / Eco(false));
    Ok("realçador de voz: reforça só a voz direta, não o eco da sala", Math.Abs(ecoDb) < 0.3, $"eco da voz a 2 kHz {ecoDb:+0.0;-0.0;0.0} dB");

    // 16. switching type/room mid-song fades (no jump), off again = untouched after the ramp
    var sen = new float[n * 2]; for (int i = 0; i < n; i++) sen[i * 2] = sen[i * 2 + 1] = (float)(0.3 * Math.Sin(2 * Math.PI * 440 * i / T));
    int trocas = m.Motor.Trocas; double salto = 0; float ant = 0;
    for (int i = 0; i < n; i += 512)
    {
        if (i == 512 * 94) m.Receber("s71", JsonDocument.Parse("""{"perfil":"Filme","sala":90}""").RootElement);
        c.Processar(sen.AsSpan(i * 2, 512 * 2), 512);
        for (int k = 0; k < 512; k++) { if (i + k > T / 2) salto = Math.Max(salto, Math.Abs(sen[(i + k) * 2] - ant)); ant = sen[(i + k) * 2]; }
    }
    double passoSeno = 2 * Math.PI * 440 / T * 0.6;
    m.Receber("ligado", JsonDocument.Parse("""{"som71":false}""").RootElement);
    var dry = Ruido(1024 * 47, 2, (i, ch) => 1); var cp = (float[])dry.Clone();
    for (int i = 0; i < 1024 * 47; i += 1024) c.Processar(dry.AsSpan(i * 2, 1024 * 2), 1024);
    bool intacto = dry.AsSpan(T).SequenceEqual(cp.AsSpan(T));
    Ok("troca de sala sem estalo; desligado = intacto", m.Motor.Trocas > trocas && salto < passoSeno * 1.6 && intacto, $"trocas {m.Motor.Trocas - trocas}, maior salto {salto:F4} (seno {passoSeno:F4}), desligado intacto {intacto}");

    // 17. off with a 7.1 cable: host downmix, the module doesn't ask for the 8 channels
    s8 = Ruido(1024, 8, (i, ch) => ch + 1); var a8 = new float[2048]; var b8 = new float[2048];
    c.Processar8(s8, a8, 1024); Fluxo.Descer(s8, b8, 1024, 8);
    Ok("7.1 desligado = mistura do app", a8.AsSpan().SequenceEqual(b8) && !m.Motor.Ativo, "igual ao Descer");

    // 17b. stereo program on the 7.1 cable (01/11): fills sides/backs, fronts and centered sound untouched, real 7.1 wins at once
    {
        double[][] Papeis() => [.. Enumerable.Range(0, 7).Select(_ => new double[512])];
        var rp = new Random(11);
        double Nr() => rp.NextDouble() * .2 - .1;
        // independent L/R noise for 3 s: moved out of the fronts into sides and backs, sides and backs not one phantom
        var pe = new Esp.Preencher(); var lfe0 = new double[512];
        var sl = new List<double>(); var bl2 = new List<double>(); var ent = new List<double>(); double eFrente = 0, eResto = 0;
        for (int b = 0; b < 282; b++)
        {
            var xp = Papeis();
            for (int i = 0; i < 512; i++) { xp[0][i] = Nr(); xp[1][i] = Nr(); }
            if (b >= 94) ent.AddRange(xp[0]);
            pe.Bloco(xp, lfe0);
            if (b >= 94) { sl.AddRange(xp[5]); bl2.AddRange(xp[3]); for (int i = 0; i < 512; i++) { eFrente += xp[0][i] * xp[0][i] + xp[1][i] * xp[1][i]; for (int k = 3; k < 7; k++) eResto += xp[k][i] * xp[k][i]; } }
        }
        double Ener(IList<double> v) => v.Sum(a => a * a);
        // 01/11 night: nothing late: the moved sound lines up with the input (was 6.5-8.9 ms on sides/backs)
        double Lag(IList<double> y)
        {
            double mel = 0; int lag = 0;
            for (int d = -600; d <= 600; d += 1)
            {
                double s = 0; for (int i = 600; i < ent.Count - 600; i += 3) s += ent[i] * y[i + d];
                if (Math.Abs(s) > mel) { mel = Math.Abs(s); lag = d; }
            }
            return lag / 48.0;
        }
        double lagS = Lag(sl), lagT = Lag(bl2), parte = eResto / (eFrente + eResto);
        // 01/11 night: check restored after review (it was dropped with the delays); original limit, independent L/R noise measures .40
        double corr = 0, es = Ener(sl), eb = Ener(bl2);
        for (int d = -1200; d <= 1200; d += 1)
        {
            double s = 0; for (int i = 1200; i < sl.Count - 1200; i += 3) s += sl[i] * bl2[i + d];
            corr = Math.Max(corr, Math.Abs(s) * 3 / Math.Sqrt(es * eb));
        }
        Ok("estéreo no cabo 7.1: o que não é centro sai da frente para lados e trás", parte > 0.5 && pe.Ganho == 1 && corr < 0.65, $"{100 * parte:F0}% fora da frente, ganho {pe.Ganho:F2}, semelhança lado/trás {corr:F2}");
        Ok("estéreo no cabo 7.1: todas as caixas no mesmo tempo, nada atrasado", Math.Abs(lagS) < 0.3 && Math.Abs(lagT) < 0.3, $"lado {lagS:F2} ms, trás {lagT:F2} ms");

        // 01/11 night: voice + instruments on BOTH sides: the voice goes to the center speaker, not to the sides
        {
            var pv = new Esp.Preencher(); int nb = 500; var vo = new double[nb * 512]; var sai = new double[7][]; for (int q = 0; q < 7; q++) sai[q] = new double[nb * 512];
            for (int b = 0; b < nb; b++)
            {
                var xp = Papeis();
                for (int i = 0; i < 512; i++) { double v = Nr(); vo[b * 512 + i] = v; xp[0][i] = v + Nr(); xp[1][i] = v + Nr(); }
                pv.Bloco(xp, lfe0);
                for (int q = 0; q < 7; q++) Array.Copy(xp[q], 0, sai[q], b * 512, 512);
            }
            double Parte(int q)
            {
                double soma = 0, ev = 0; for (int i = 128000; i < vo.Length - 200; i++) ev += vo[i] * vo[i];
                for (int d = 0; d < 96; d++) { double s = 0; for (int i = 128000; i < vo.Length - 200; i++) s += vo[i] * sai[q][i + d]; s /= ev; soma += s * s; }
                return soma;
            }
            var ps = Enumerable.Range(0, 7).Select(Parte).ToArray(); double tot = ps.Sum();
            double noCentro = ps[2] / tot, nosLados = (ps[3] + ps[4] + ps[5] + ps[6]) / tot;
            Ok("estéreo no cabo 7.1: voz com instrumentos dos dois lados vai para a caixa do centro", noCentro > 0.85 && nosLados < 0.05, $"centro {100 * noCentro:F0}%, lados e trás {100 * nosLados:F1}%");
        }

        // real mix (01/11 "um local só"): voice in the middle + guitar on the left. Guitar goes to the LEFT speakers only, voice stays in front; voice with syllables doesn't make it sway
        {
            var pg = new Esp.Preencher(); double sLe = 0, sLd = 0; var janelas = new List<double>(); double jan = 0; double eVoz = 0, eCopia = 0;
            for (int b = 0; b < 800; b++)
            {
                var xp = Papeis(); double silaba = 0.3 + 0.7 * Math.Abs(Math.Sin(2 * Math.PI * 4 * b * 512 / 48000.0));
                var vb = new double[512];
                for (int i = 0; i < 512; i++) { double v = Nr() * silaba, u = Nr() * 0.6; vb[i] = v; xp[0][i] = v + u; xp[1][i] = v; }
                pg.Bloco(xp, lfe0);
                if (b < 200) continue;
                double e = 0, d = 0; for (int i = 0; i < 512; i++) { e += xp[5][i] * xp[5][i] + xp[3][i] * xp[3][i]; d += xp[6][i] * xp[6][i] + xp[4][i] * xp[4][i]; eVoz += vb[i] * vb[i]; }
                sLe += e; sLd += d; jan += e;
                if (b % 10 == 0) { janelas.Add(10 * Math.Log10(jan + 1e-20)); jan = 0; }
            }
            double vazaDir = 10 * Math.Log10((sLd + 1e-20) / sLe), med = janelas.Skip(1).Average(), desv = Math.Sqrt(janelas.Skip(1).Average(v => (v - med) * (v - med)));
            Ok("estéreo no cabo 7.1: guitarra da esquerda só nas caixas da esquerda, sem balançar", vazaDir < -12 && desv < 2, $"lado direito {vazaDir:+0.0;-0.0} dB, variação no tempo {desv:F1} dB");
        }
        // every speaker its own reflections + the centered voice keeps its approved tone (01/11)
        {
            Esp.Brir.Usar("h11");
            var (rv, _) = Esp.Brir.Caixas(Esp.Brir.Ang71, true, 0);
            double Ecos(int a, int b) { double s = 0, ea = 0, eb = 0; for (int e = 0; e < 2; e++) for (int i = 480; i < rv[a][e].Length; i++) { s += rv[a][e][i] * rv[b][e][i]; ea += rv[a][e][i] * rv[a][e][i]; eb += rv[b][e][i] * rv[b][e][i]; } return Math.Abs(s) / Math.Sqrt(ea * eb); }
            double igual = Math.Max(Ecos(0, 5), Math.Max(Ecos(3, 5), Ecos(0, 3)));
            var (rj, _) = Esp.Brir.Caixas(Esp.Brir.Ang71, true, 1);
            Esp.Brir.Usar("kemar");
            // 01/11 night: left/right at the ear stays what the head gives (per-ear contrast made fronts at 35 measure like 85)
            Esp.Brir.Usar("h11");
            var (rf, _) = Esp.Brir.Caixas(Esp.Brir.Ang71, true, 0);
            double[] Db(double[] x, int n) { var fx = new Esp.Fft(4096); var t = new double[4096]; Array.Copy(x, t, Math.Min(n, x.Length)); double[] re = new double[4096], im = new double[4096]; fx.Real(t, re, im); return [.. Enumerable.Range(107, 747).Select(k => re[k] * re[k] + im[k] * im[k] + 1e-20)]; }
            double DifLados(double[][] h, int n) { double[] a = Db(h[0], n), b = Db(h[1], n); double s = 0; for (int f = 0; f + 53 <= a.Length; f += 53) s += 10 * Math.Log10(a.Skip(f).Take(53).Sum() / b.Skip(f).Take(53).Sum()); return s / (a.Length / 53); }
            double difCaixa = DifLados(rf[0], 240), difCru = DifLados(Esp.Brir.Resposta(Esp.Brir.Ang71[0]), 240);
            Esp.Brir.Usar("kemar");
            Ok("frente fica na frente: diferença entre ouvidos é a da cabeça", Math.Abs(difCaixa - difCru) < 1.5, $"caixa {difCaixa:F1} dB, cabeça {difCru:F1} dB");
            Ok("cada caixa tem os próprios ecos", igual < 0.5 && rv[0][0].Length == Esp.Brir.LDCaixa && rj[0][0].Length == Esp.Brir.LDCaixa, $"semelhança dos ecos entre caixas {igual:F2}");
        }

        // centered sound (L = R): nothing goes to the sides/backs
        var pc = new Esp.Preencher(); double vaza = 0, eCen = 0, eFre = 0;
        for (int b = 0; b < 100; b++)
        {
            var xp = Papeis();
            for (int i = 0; i < 512; i++) xp[0][i] = xp[1][i] = Nr();
            pc.Bloco(xp, lfe0);
            for (int k = 3; k < 7; k++) vaza = Math.Max(vaza, xp[k].Max(Math.Abs));
            if (b >= 94) for (int i = 0; i < 512; i++) { eCen += xp[2][i] * xp[2][i]; eFre += xp[0][i] * xp[0][i] + xp[1][i] * xp[1][i]; }
        }
        Ok("estéreo no cabo 7.1: voz no centro sai pela caixa do centro, nada nos lados", vaza == 0 && eCen / (eCen + eFre) > 0.9, $"vazou {vaza:E1}, no centro {100 * eCen / (eCen + eFre):F0}%");

        // real surround: off within one block, stays off 10 s after it stops, then back. One front alone (channel test): never
        var pr = new Esp.Preencher();
        void Blocos(Esp.Preencher p, int nb, int canalExtra, bool direita = true) { for (int b = 0; b < nb; b++) { var xp = Papeis(); for (int i = 0; i < 512; i++) { xp[0][i] = Nr(); if (direita) xp[1][i] = Nr(); if (canalExtra >= 0) xp[canalExtra][i] = Nr(); } p.Bloco(xp, lfe0); } }
        Blocos(pr, 100, -1); double antes = pr.Ganho;
        Blocos(pr, 1, 5); double comSom = pr.Ganho;
        Blocos(pr, Esp.Preencher.Segurar / 512 - 2, -1); double segurou = pr.Ganho;
        Blocos(pr, 100, -1); double voltou = pr.Ganho;
        var pu = new Esp.Preencher(); Blocos(pu, 100, -1, direita: false);
        Ok("estéreo no cabo 7.1: 7.1 de verdade desliga na hora e só volta depois de 10 s", antes == 1 && comSom == 0 && segurou == 0 && voltou > 0.9 && pu.Ganho == 0,
            $"antes {antes:F2}, com lado tocando {comSom:F2}, 10 s depois {segurou:F2}, depois {voltou:F2}, uma frente só {pu.Ganho:F2}");

        // whole engine: the fill doesn't jump in loudness; centered sound keeps its level
        double Nivel(bool iguais)
        {
            var mm = new Esp.Modulo(); mm.Preparar().Wait(); mm.Receber("s71", JsonDocument.Parse("""{"perfil":"Música","sala":17}""").RootElement); mm.Motor.Ligado = true;
            var cc = new Cadeia(); cc.Definir(mm.Efeitos);
            int nn = 1024 * 188; var v = new float[nn * 8]; var o2 = new float[nn * 2]; var rr = new Random(3);
            for (int i = 0; i < nn; i++) { v[i * 8] = (float)(rr.NextDouble() * .2 - .1); v[i * 8 + 1] = iguais ? v[i * 8] : (float)(rr.NextDouble() * .2 - .1); }
            for (int i = 0; i < nn; i += 1024) cc.Processar8(v.AsSpan(i * 8, 1024 * 8), o2.AsSpan(i * 2, 1024 * 2), 1024);
            double E(int a, int z) { double s = 0; for (int i = a * 2; i < z * 2; i++) s += o2[i] * o2[i]; return s / (z - a); }
            return 10 * Math.Log10(E(T * 3, nn) / E(T / 8, T / 4));
        }
        double dLr = Nivel(false), dCen = Nivel(true);
        Ok("estéreo no cabo 7.1: volume quase igual ao encher", Math.Abs(dLr) < 2.5 && Math.Abs(dCen) < 0.5, $"som aberto {dLr:+0.0;-0.0} dB, centro {dCen:+0.0;-0.0} dB");
    }

    // 18. CPU: stereo upmix + 7 speakers + biggest room on 20 s, and 7.1 input
    m.Receber("ligado", JsonDocument.Parse("""{"som71":true}""").RootElement);
    var y = Ruido(1024, 2, (i, ch) => 1); var y8 = Ruido(1024, 8, (i, ch) => 1); var o = new float[2048];
    var swc = System.Diagnostics.Stopwatch.StartNew(); int blocos = T * 20 / 1024;
    for (int b = 0; b < blocos; b++) c.Processar(y, 1024);
    double pE = 100 * swc.Elapsed.TotalSeconds / 20; swc.Restart();
    for (int b = 0; b < blocos; b++) c.Processar8(y8, o, 1024);
    double p8 = 100 * swc.Elapsed.TotalSeconds / 20;
    Ok("custo do Som 7.1", pE < 8 && p8 < 8, $"estéreo {pE:F2}%, 7.1 {p8:F2}% de 1 núcleo");
}

// 19. automatic profile: who decides what, priority, 4 s hysteresis; reading the real sessions doesn't throw (read-only)
{
    var casos = new (string? esperado, string? obtido)[]
    {
        ("Música", Esp.Conteudo.Classificar("Spotify.exe")), ("Filme", Esp.Conteudo.Classificar("vlc.exe")), (null, Esp.Conteudo.Classificar("discord.exe")), (null, Esp.Conteudo.Classificar("FAH.exe")),
        ("Música", Esp.Conteudo.Classificar("chrome.exe", titulos: ["Lofi - YouTube Music - Google Chrome"])), ("Filme", Esp.Conteudo.Classificar("brave.exe", titulos: ["Trailer - YouTube"])),
        (null, Esp.Conteudo.Classificar("chrome.exe", titulos: ["Gmail"])), ("Filme", Esp.Conteudo.Classificar("msedge.exe", titulos: ["x"], telaCheia: true)),
        ("Jogo", Esp.Conteudo.Classificar("cs2.exe", @"D:\Steam\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe")),
        ("Jogo", Esp.Conteudo.Classificar("x.exe", @"C:\x\x.exe", usa3d: () => true)), (null, Esp.Conteudo.Classificar("x.exe", @"C:\x\x.exe", usa3d: () => false)),
        ("Jogo", Esp.Conteudo.Classificar("x.exe", @"C:\x\x.exe", telaCheia: true)),
    };
    int certos = casos.Count(c => c.esperado == c.obtido);
    var d = new Esp.Conteudo.Detector("Jogo");
    bool h = !d.Observar([("Filme", "vlc.exe"), ("Música", "spotify.exe")], 0) && !d.Observar([("Filme", "vlc.exe")], 3.9)
        && d.Observar([("Filme", "vlc.exe")], 4.1) && d.Tipo == "Filme" && d.Fonte == "vlc.exe"
        && !d.Observar([], 10) && !d.Observar([], 20) && d.Tipo == "Filme"                                   // nothing playing: keeps
        && !d.Observar([("Música", "spotify.exe")], 21) && !d.Observar([("Filme", "vlc.exe")], 23)            // back before 4 s: no switch
        && !d.Observar([("Música", "spotify.exe")], 24) && d.Tipo == "Filme";
    bool prio = Esp.Conteudo.Escolher(["Música", "Jogo", "Filme"]) == "Jogo" && Esp.Conteudo.Escolher(["Música", "Filme"]) == "Filme";
    string vivo; try { var l = Esp.Conteudo.Tocando(); vivo = $"{l.Count} tocando agora: {string.Join(", ", l.Select(x => x.tipo + " " + x.exe))}"; } catch (Exception e) { vivo = "ERRO " + e.Message; }
    Ok("perfil automático", certos == casos.Length && h && prio && !vivo.StartsWith("ERRO"), $"{certos}/{casos.Length} casos, espera {h}, prioridade {prio}; {vivo}");
}

// 20. module .dll loaded like the host does (own AssemblyLoadContext from bytes): its NAudio comes from the host
{
    string r;
    try
    {
        var arq = Path.Combine(AppContext.BaseDirectory, "FAH.Espacial.dll");
        var ctx = new System.Runtime.Loader.AssemblyLoadContext("teste-espacial");
        ctx.Resolving += (c, n) => n.Name == "FAH.Contrato" ? typeof(FAH.Contrato.IModulo).Assembly : null;
        var asm = ctx.LoadFromStream(new MemoryStream(File.ReadAllBytes(arq)));
        var l = (System.Collections.ICollection)asm.GetType("FAH.Espacial.Conteudo")!.GetMethod("Tocando")!.Invoke(null, null)!;
        r = $"ok, {l.Count} tocando";
    }
    catch (Exception e) { r = "ERRO " + (e.InnerException ?? e).Message; }
    Ok("módulo 7.1 carrega como no FAH", !r.StartsWith("ERRO"), r);
}

// Mixer: per-program device (undocumented Windows interface) on THIS test process only, undone at once; device list read-only
{
    string r; bool ok;
    try
    {
        uint eu = (uint)Environment.ProcessId;
        var ap = Aparelhos.Listar(true).First();
        string antes = PorPrograma.Ler(eu, false);
        PorPrograma.Definir(eu, false, ap.Id); string fixo = PorPrograma.Ler(eu, false);
        PorPrograma.Definir(eu, false, null); string depois = PorPrograma.Ler(eu, false);
        ok = antes == "" && fixo == ap.Id && depois == "";
        r = $"antes '{antes}', fixado {(fixo == ap.Id ? "certo" : "'" + fixo + "'")}, depois '{depois}'";
    }
    catch (Exception e) { ok = false; r = "ERRO " + e.Message; }
    Ok("fixar programa num aparelho", ok, r);

    var mesa = new Mesa(); object? snap = null; int niv = 0;
    var tn = new List<long>(); var swn = System.Diagnostics.Stopwatch.StartNew();
    mesa.AoMudar = s => snap = s; mesa.AoNivel = _ => { niv++; lock (tn) tn.Add(swn.ElapsedMilliseconds); };
    var swm = System.Diagnostics.Stopwatch.StartNew(); mesa.Mostrar(true); while (snap == null && swm.ElapsedMilliseconds < 3000) Thread.Sleep(10); long t1 = swm.ElapsedMilliseconds; int n0 = niv;
    Thread.Sleep(1500); mesa.Mostrar(false); int n1 = niv; Thread.Sleep(500);
    var j = snap == null ? "" : JsonSerializer.Serialize(snap);
    Ok("mixer lê aparelhos e para quando some", j.Contains("\"som\":[{") && n1 - n0 >= 10 && niv - n1 <= 1, $"1ª leitura em {t1} ms, {n1 - n0} níveis em 1,5 s (maior vão {(tn.Count > 1 ? tn.Zip(tn.Skip(1), (x, y) => y - x).Max() : 0)} ms), parou: {niv - n1 <= 1}, {j.Length} bytes");
}

// Mixer: program volume follows new sessions, ends when the program closes (next start = 100 %), stays when saved.
// Child = copy of this exe named FAHTomTeste playing silence; only ITS session volume is touched and reset at the end.
{
    string r = ""; bool ok = false; var dir = AppContext.BaseDirectory; var exe = Path.Combine(dir, "FAHTomTeste.exe");
    System.Diagnostics.Process? filho = null; var mesa = new Mesa { MestreFalso = () => 100, SoPrograma = "FAHTomTeste" }; List<Regra>? ult = null; mesa.AoRegrar = l => ult = l;
    (float v, bool m)? Sessao() { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); using var d = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
        var ss = d.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) { var x = ss.Sessions[i]; if (filho != null && !filho.HasExited && x.GetProcessID == filho.Id) return (x.SimpleAudioVolume.Volume, x.SimpleAudioVolume.Mute); } return null; }
    void Toca() { filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--tocar 15000") { CreateNoWindow = true, UseShellExecute = false }); }
    bool Espera(Func<bool> c, int ms) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) { if (c()) return true; Thread.Sleep(100); } return c(); }
    try
    {
        File.Copy(Path.Combine(dir, "FAH.Testes.exe"), exe, true);
        Toca(); Espera(() => Sessao() != null, 4000);
        mesa.Mostrar(true); Thread.Sleep(2500);
        mesa.VolumePrograma("som", "FAHTomTeste", 37, true); bool a = Espera(() => Sessao() is { } q && Math.Abs(q.v - .37f) < .01 && q.m, 3000);
        filho!.Kill(); filho.WaitForExit(); mesa.Mostrar(false);   // closed while the Mixer is hidden: background pass marks the end
        bool b = Espera(() => ult?.SingleOrDefault() is { Fim: true }, 5000);
        Toca(); bool c = Espera(() => Sessao() is { } q && q.v > .99f && !q.m && ult?.Count == 0, 6000);
        mesa.Mostrar(true); Thread.Sleep(2000); mesa.VolumePrograma("som", "FAHTomTeste", 52, false, true); Thread.Sleep(500);
        filho!.Kill(); filho.WaitForExit(); mesa.Mostrar(false); Thread.Sleep(3000);
        bool d = ult?.SingleOrDefault() is { Salvo: true, Fim: false, V: 52 };
        Toca(); bool e = Espera(() => Sessao() is { } q && Math.Abs(q.v - .52f) < .01 && !q.m, 6000);
        ok = a && b && c && d && e; r = $"vol+mudo {a}, fechou->fim {b}, reabriu 100% {c}, salvo fica {d}, salvo volta {e}";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally
    {
        mesa.Regras.Clear();
        try { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); using var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
            var ss = dv.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) { var x = ss.Sessions[i]; if (filho != null && x.GetProcessID == filho.Id) { x.SimpleAudioVolume.Volume = 1; x.SimpleAudioVolume.Mute = false; } } } catch { }
        try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { }
        try { File.Delete(exe); } catch { }
    }
    Ok("mixer: volume do programa segue, acaba ao fechar, salvo fica", ok, r);
}

// 21. virtual cable choice by driver (offline lists) + real read-only check of this PC
{
    var A = NAudio.CoreAudioApi.DeviceState.Active; var D = NAudio.CoreAudioApi.DeviceState.Disabled;
    Cabos.Ponta P(string desc, string inter, bool saida, NAudio.CoreAudioApi.DeviceState e) => new(desc + inter + saida, desc, inter, saida, e, false);
    const string VAC = "Virtual Audio Cable", VB = "VB-Audio Virtual Cable", VM = "VB-Audio Voicemeeter VAIO";
    var real = new Cabos.Ponta("r", "Alto-falantes", "Realtek(R) Audio", true, A, true);
    (string, List<Cabos.Ponta>, Func<Achado, bool>)[] casos =
    [
        ("VAC renomeado", [real, P("Saída", VAC, true, A), P("Saída -_NS", VAC, false, A)], a => a.Estado == "achou" && a.Cabo!.Vac && a.Cabo.Captar.Nome == "Saída -_NS (Virtual Audio Cable)"),
        ("VAC de fábrica", [P("Line 1", VAC, true, A), P("Line 1", VAC, false, A)], a => a.Cabo?.Tocar.Nome == "Line 1 (Virtual Audio Cable)"),
        ("VB-CABLE", [real, P("CABLE Input", VB, true, A), P("CABLE Output", VB, false, A)], a => a.Cabo is { Marca: "VB-CABLE", Loopback: false } c && c.Captar.Nome.StartsWith("CABLE Output")),
        ("VB-CABLE A", [P("CABLE-A Input", "VB-Audio Cable A", true, A), P("CABLE-A Output", "VB-Audio Cable A", false, A)], a => a.Cabo?.Captar.Nome.StartsWith("CABLE-A Output") == true),
        ("Voicemeeter = loopback", [P("Voicemeeter Out B1", VM, false, A), P("Voicemeeter Input", VM, true, A)], a => a.Cabo is { Marca: "Voicemeeter", Loopback: true } c && c.Captar.Nome.StartsWith("Voicemeeter Input")),
        ("VAC antes do VB", [P("CABLE Input", VB, true, A), P("CABLE Output", VB, false, A), P("Line 1", VAC, true, A), P("Line 1", VAC, false, A)], a => a.Cabo?.Vac == true),
        ("VB desativado", [real, P("CABLE Input", VB, true, D), P("CABLE Output", VB, false, D)], a => a.Estado == "desativado" && a.Marca == "VB-CABLE" && a.Desativados.Length == 2 && a.Desativados[0].StartsWith("CABLE Input")),
        ("meio desativado", [P("CABLE Input", VB, true, A), P("CABLE Output", VB, false, D)], a => a.Estado == "desativado" && a.Desativados.Single().StartsWith("CABLE Output")),
        ("desativado + outro ativo", [P("CABLE Input", VB, true, D), P("CABLE Output", VB, false, D), P("Voicemeeter Input", VM, true, A)], a => a.Estado == "achou" && a.Cabo!.Marca == "Voicemeeter"),
        ("sem cabo", [real], a => a.Estado == "falta"),
    ];
    var ruins = casos.Where(c => { try { return !c.Item3(Cabos.Escolher(c.Item2)); } catch { return true; } }).Select(c => c.Item1).ToList();
    Ok("cabo achado pelo driver", ruins.Count == 0, ruins.Count == 0 ? $"{casos.Length} casos" : "falhou: " + string.Join(", ", ruins));
    Cabos.Teste = _ => false; var sv = Cabos.Detectar(); Cabos.Teste = null;
    var pc = Cabos.Detectar();
    Ok("cabo: serviço parado avisa, PC real responde", sv.Estado == "servico" && pc.Estado != "servico", $"parado -> {sv.Estado}; este PC -> {pc.Estado} {pc.Cabo?.Tocar.Nome} -> {pc.Cabo?.Captar.Nome}");
}

// 22. Mic chain (B5): DeepFilterNet suppressor on the noisy voice sample, tone/timbre, tools, limiter; listening files in docs\teste-real\amostras-mic
{
    string raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string amostra = Path.Combine(raiz, "amostras", "1_original_com_muito_ruido.wav");
    string saiPasta = Path.Combine(raiz, "docs", "teste-real", "amostras-mic"); Directory.CreateDirectory(saiPasta);
    float[] Ler(string arq) { using var r0 = new NAudio.Wave.WaveFileReader(arq); var r = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(r0); var l = new List<float>(); var b = new float[r.WaveFormat.Channels * 4800]; int n;
        while ((n = r.Read(b, 0, b.Length)) > 0) for (int i = 0; i < n; i += r.WaveFormat.Channels) l.Add(b[i]); return [.. l]; }
    void Gravar(string nome, float[] m) { using var w = new NAudio.Wave.WaveFileWriter(Path.Combine(saiPasta, nome), NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(T, 1)); w.WriteSamples(m, 0, m.Length); }
    // run a mono signal through a chain in 1024 blocks (like the engine), stereo inside
    (float[] y, double ms) Rodar(Cadeia c, float[] x)
    {
        var y = new float[x.Length]; var buf = new float[2048]; var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int p = 0; p < x.Length; p += 1024)
        {
            int q = Math.Min(1024, x.Length - p);
            for (int i = 0; i < q; i++) buf[2 * i] = buf[2 * i + 1] = x[p + i];
            c.Processar(buf.AsSpan(0, q * 2), q);
            for (int i = 0; i < q; i++) y[p + i] = buf[2 * i];
        }
        return (y, sw.Elapsed.TotalMilliseconds);
    }
    // noise floor = level of the quietest 15% of 20 ms frames; voice = loudest 15%
    (double piso, double voz) Niveis(float[] x, int pula)
    {
        var l = new List<double>();
        for (int p = pula; p + 960 <= x.Length; p += 960) { double s = 0; for (int i = 0; i < 960; i++) s += x[p + i] * x[p + i]; l.Add(10 * Math.Log10(s / 960 + 1e-12)); }
        l.Sort(); int k = Math.Max(1, l.Count * 15 / 100);
        return (l.Take(k).Average(), l.Skip(l.Count - k).Average());
    }
    var vazio = JsonDocument.Parse("{}").RootElement;
    var mm = new FAH.Microfone.Modulo(); mm.Receber("ligado", vazio);
    mm.Supressor.Carregar();
    Ok("mic: modelo do supressor abre", mm.Supressor.Erro == null, mm.Supressor.Erro ?? "dfn3.onnx no ONNX Runtime 1.31");
    if (File.Exists(amostra) && mm.Supressor.Erro == null)
    {
        var x = Ler(amostra);
        var c1 = new Cadeia(); c1.Definir([mm.Mono, mm.Supressor]); mm.Supressor.Ligado = true; mm.Supressor.Forca = 1;
        var (y1, ms1) = Rodar(c1, x);
        var a = Niveis(x, 0); var b = Niveis(y1, 4800);
        double queda = a.piso - b.piso, uso = ms1 / (x.Length * 1000.0 / T) * 100;
        Ok("mic: supressor tira o ruído e deixa a voz", queda > 20 && a.voz - b.voz < 6, $"ruído {a.piso:F1} -> {b.piso:F1} dB (-{queda:F1}), voz {a.voz:F1} -> {b.voz:F1} dB, CPU {uso:F1}% de 1 núcleo");
        int melhor = 0; double mc = 0;
        for (int d = 0; d < 4000; d += 4) { double s = 0; for (int i = 48000; i < 96000; i += 2) s += x[i] * y1[i + d]; if (s > mc) { mc = s; melhor = d; } }
        Ok("mic: atraso do supressor", Math.Abs(melhor - 1919) < 60, $"{melhor} amostras = {melhor * 1000.0 / T:F0} ms (esperado ~40 ms)");
        var mm2 = new FAH.Microfone.Modulo(); mm2.Receber("ligado", vazio); mm2.Supressor.Carregar();
        var c2 = new Cadeia(); c2.Definir(mm2.Efeitos.Where(e => e.Id != "ruido").Append(mm2.Supressor));
        var (y2, ms2) = Rodar(c2, x);
        double pk = y2.Max(v => Math.Abs(v)); var n2 = Niveis(y2, 4800);
        Ok("mic: cadeia inteira (padrões das telas)", pk <= 0.892 && y2.All(float.IsFinite) && a.piso - n2.piso > 15,
            $"pico {20 * Math.Log10(pk):F1} dBFS, ruído {n2.piso:F1} dB, voz {n2.voz:F1} dB, CPU {ms2 / (x.Length * 1000.0 / T) * 100:F1}%");
        Gravar("1-original.wav", x); Gravar("2-so-supressor.wav", y1); Gravar("3-microfone-inteiro.wav", y2);
        // 01/11 a program sent into the mic (music, quiet and loud) is leveled to the voice's loudness; ruler = BS.1770 with 400 ms blocks, apart from the code's 100 ms
        double Lufs(float[] m, int de)
        {
            var k = new FiltroK(); var e = new double[(m.Length - de) / 4800]; int w = 0, c = 0; double a = 0;
            for (int i = de; i < m.Length && w < e.Length; i++) { double y = k.Passar(m[i]); a += y * y; if (++c == 4800) { e[w++] = a / 4800; a = 0; c = 0; } }
            var bl = new List<double>(); for (int i = 3; i < w; i++) { double b = (e[i] + e[i - 1] + e[i - 2] + e[i - 3]) / 4; if (-0.691 + 10 * Math.Log10(b + 1e-12) > -70) bl.Add(b); }
            double lim = bl.Average() * 0.1; return -0.691 + 10 * Math.Log10(bl.Where(b => b >= lim).Average());
        }
        float[] Seno(double hz) { var s = new float[T]; for (int i = 0; i < T; i++) s[i] = (float)(0.1 * Math.Sin(2 * Math.PI * hz * i / T)); return s; }
        double l1k = Lufs(Seno(1000), 0), l10k = Lufs(Seno(10000), 0);
        Ok("mic: régua de sonoridade (BS.1770)", Math.Abs(l1k + 23) < 0.2 && Math.Abs(l10k - l1k - 4) < 0.7, $"seno 1 kHz a -20 dBFS = {l1k:F1} (esperado -23,0); 10 kHz {l10k - l1k:+0.0} dB (esperado ~+4)");
        string mus = Path.Combine(raiz, "amostras", "Music_1_original.wav");
        if (File.Exists(mus))
        {
            var nv = new NivelVoz(); var st = new float[2048];
            for (int p = 0; p + 1024 <= y2.Length; p += 1024) { for (int i = 0; i < 1024; i++) st[2 * i] = st[2 * i + 1] = y2[p + i]; nv.Medir(st); }
            double Medidor(float[] m, int de) { var l = new List<double>(); for (int p = de; p + 4800 <= m.Length; p += 4800) { float k = 0; for (int i = 0; i < 4800; i++) k = Math.Max(k, Math.Abs(m[p + i])); if (k > 0.003) l.Add(20 * Math.Log10(k)); }
                l.Sort(); return l[l.Count * 9 / 10]; }
            double mv = Medidor(y2, 4800), lv = Lufs(y2, 4800); var m0 = Ler(mus)[..(10 * T)]; var txt = new List<string>(); bool bom = true; double alvo = NivelPrograma.AlvoVoz;
            // 02/11: fixed target (the live voice reading fell to -47 in silence); -55 = a quiet browser, needed the +40 dB cap
            foreach (double db in new[] { -55.0, -40, -20, -4 })
            {
                double r0 = 0; foreach (var v in m0) r0 += v * v; float gg = (float)Math.Pow(10, (db - 10 * Math.Log10(r0 / m0.Length)) / 20);
                var np = new NivelPrograma(); var ym = new float[m0.Length];
                for (int p = 0; p + 480 <= m0.Length; p += 480) { for (int i = 0; i < 480; i++) st[2 * i] = st[2 * i + 1] = m0[p + i] * gg; np.Processar(st.AsSpan(0, 960), 1); for (int i = 0; i < 480; i++) ym[p + i] = st[2 * i]; }
                double lp = Lufs(ym, 2 * T), mp = Medidor(ym, 2 * T), pkm = ym.Max(v => Math.Abs(v)); bom &= Math.Abs(lp - alvo) < 1.5 && pkm <= 0.892; txt.Add($"música {db} dB -> sonoridade {lp:F1}, medidor {mp:F1}");
            }
            // 02/11 (user: raising the program's volume made the mic jump): 5 s at -44 then +34 dB, and the reverse
            {
                double r0 = 0; foreach (var v in m0) r0 += v * v; double rms = 10 * Math.Log10(r0 / m0.Length); var tj = new List<string>(); bool bj = true;
                foreach (var (da, db2) in new[] { (-44.0, -10.0), (-10.0, -44.0) })
                {
                    var np = new NivelPrograma(); var ym = new float[m0.Length]; int meio = 5 * T;
                    for (int p = 0; p + 480 <= m0.Length; p += 480) { float gg = (float)Math.Pow(10, ((p < meio ? da : db2) - rms) / 20); for (int i = 0; i < 480; i++) st[2 * i] = st[2 * i + 1] = m0[p + i] * gg; np.Processar(st.AsSpan(0, 960), 1); for (int i = 0; i < 480; i++) ym[p + i] = st[2 * i]; }
                    if (db2 > da)
                    {   // louder: every 400 ms block from 0.3 s after the jump stays near the target
                        var k = new FiltroK(); double pior = -100, e = 0; var w = new List<double>();
                        for (int i = 0; i < ym.Length; i++) { double y = k.Passar(ym[i]); e += y * y; if ((i + 1) % 4800 == 0) { w.Add(e / 4800); e = 0; } }
                        for (int i = meio / 4800 + 3 + 3; i < w.Count; i++) pior = Math.Max(pior, -0.691 + 10 * Math.Log10((w[i] + w[i - 1] + w[i - 2] + w[i - 3]) / 4 + 1e-12));
                        bj &= pior <= alvo + 4; tj.Add($"subiu: pior bloco {pior:F1}");
                    }
                    else { double l = Lufs(ym, meio + 3 * T); bj &= Math.Abs(l - alvo) < 3; tj.Add($"desceu: 3 s depois {l:F1}"); }
                }
                Ok("mic: programa no mic não pula quando o volume dele muda", bj, $"alvo {alvo:F0}; " + string.Join(", ", tj));
            }
            bom &= Math.Abs(lv - alvo) < 3;          // the chain's voice really is near the fixed target
            Ok("mic: programa mandado pro mic sai no volume da voz", bom, $"alvo {alvo:F0}; voz: sonoridade {lv:F1} (código {nv.Db:F1}), medidor {mv:F1}; " + string.Join(", ", txt));
        }
        if (Environment.GetEnvironmentVariable("FAH_ETAPAS") == "1")
        {
            var ef = mm2.Efeitos.Where(e => e.Id != "ruido").Append(mm2.Supressor).OrderBy(e => e.Posicao).ToList();
            for (int k = 1; k <= ef.Count; k++) { var m3 = new FAH.Microfone.Modulo(); m3.Receber("ligado", vazio); m3.Supressor.Carregar();
                var l3 = m3.Efeitos.Where(e => e.Id != "ruido").Append(m3.Supressor).OrderBy(e => e.Posicao).Take(k).ToList(); var c3 = new Cadeia(); c3.Definir(l3);
                var n3 = Niveis(Rodar(c3, x).y, 4800); rel.AppendLine($"  etapa {l3[^1].Id}: ruído {n3.piso:F1} voz {n3.voz:F1}"); }
        }
        foreach (var (nome, tom, tim, rob, eco, rad) in new[] { ("grave", -5f, -30f, 0f, 0f, 0f), ("fina", 6f, 40f, 0f, 0f, 0f), ("esquilo", 11f, 70f, 0f, 0f, 0f), ("monstro", -11f, -80f, 0f, .3f, 0f), ("robo", 0f, 0f, .8f, 0f, 0f), ("radio", 0f, 10f, 0f, 0f, .9f), ("caverna", -2f, -10f, 0f, .8f, 0f) })
        {
            var mv = new FAH.Microfone.Modulo(); mv.Voz.Ligado = true;
            mv.Voz.Tom = tom; mv.Voz.Timbre = tim; mv.Voz.Robo = rob; mv.Voz.Eco = eco; mv.Voz.Radio = rad;
            var cv = new Cadeia(); cv.Definir([mv.Voz]); Gravar($"4-voz-{nome}.wav", Rodar(cv, y1).y);
        }
    }
    else if (!File.Exists(amostra)) Ok("mic: amostra de voz com ruído", false, "não achei " + amostra);
    double Pico(float[] y)
    {
        int n = 1 << 14, ini = y.Length - n; double best = 0, fb = 0;
        for (double f = 80; f < 1500; f += 1) { double re = 0, im = 0, w = 2 * Math.PI * f / T; for (int i = 0; i < n; i += 2) { re += y[ini + i] * Math.Cos(w * (ini + i)); im += y[ini + i] * Math.Sin(w * (ini + i)); }
            double m = re * re + im * im; if (m > best) { best = m; fb = f; } }
        return fb;
    }
    var sen = new float[T * 2]; for (int i = 0; i < sen.Length; i++) sen[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 300 * i / T));
    var res = new List<string>(); bool okTom = true;
    foreach (var (tom, tim, quer) in new[] { (12f, 0f, 600.0), (-12f, 0f, 150.0), (5f, 0f, 300 * Math.Pow(2, 5 / 12.0)), (0f, 80f, 300.0) })
    {
        var mv = new FAH.Microfone.Modulo(); mv.Voz.Ligado = true; mv.Voz.Tom = tom; mv.Voz.Timbre = tim;
        var cv = new Cadeia(); cv.Definir([mv.Voz]); var (yv, _) = Rodar(cv, sen); double f = Pico(yv);
        res.Add($"{tom:+0;-0;0}st/{tim:0} -> {f:F0} Hz"); if (Math.Abs(f - quer) > quer * 0.03) okTom = false;
    }
    { var mv = new FAH.Microfone.Modulo(); mv.Voz.Ligado = true; var cv = new Cadeia(); cv.Definir([mv.Voz]); var (yv, _) = Rodar(cv, sen); if (!yv.SequenceEqual(sen)) { okTom = false; res.Add("natural mexeu"); } }
    Ok("mic: tom e timbre", okTom, string.Join(", ", res));
    {
        var mt = new FAH.Microfone.Modulo(); var hum = new float[T]; for (int i = 0; i < T; i++) hum[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 40 * i / T));
        var cc = new Cadeia(); cc.Definir([mt.Corte]); var (yh, _) = Rodar(cc, hum);
        double rIn = Math.Sqrt(hum.Skip(T / 2).Average(v => v * v)), rOut = Math.Sqrt(yh.Skip(T / 2).Average(v => v * v));
        var alto = new float[T]; for (int i = 0; i < T; i++) alto[i] = (float)(1.8 * Math.Sin(2 * Math.PI * 700 * i / T));
        var cl = new Cadeia(); cl.Definir([mt.Limitador]); var (yl, _) = Rodar(cl, alto); double pl = yl.Max(v => Math.Abs(v));
        // quiet "speech": 0.6 s of voice (RMS -34 dB) / 0.4 s pause with -75 dB background, 15 s
        var rn = new Random(5); var baixo = new float[T * 15];
        for (int i = 0; i < baixo.Length; i++) baixo[i] = (float)((i % T < 0.6 * T ? 0.028 * Math.Sin(2 * Math.PI * 220 * i / T) : 0) + 0.00018 * (rn.NextDouble() * 2 - 1) * 1.7);
        mt.AutoVolume.Ligado = true; mt.AutoVolume.AlvoDb = -18; var ca = new Cadeia(); ca.Definir([mt.AutoVolume]); var (ya, _) = Rodar(ca, baixo);
        double fim = 10 * Math.Log10(ya.Skip(ya.Length - T).Take((int)(0.5 * T)).Average(v => v * v));
        // gate: steady -60 dB noise with no voice drops ~30 dB after the release time
        var ruido = new float[T * 2]; for (int i = 0; i < ruido.Length; i++) ruido[i] = (float)(0.0017 * (rn.NextDouble() * 2 - 1));
        mt.Portao.Ligado = true; mt.Portao.Limite = 0.35f; var cg = new Cadeia(); cg.Definir([mt.Portao]); var (yg, _) = Rodar(cg, ruido);
        double gq = 10 * Math.Log10(yg.Skip(T).Average(v => v * v) / ruido.Skip(T).Average(v => v * v));
        Ok("mic: corte de graves, limitador, volume sozinho, portão", 20 * Math.Log10(rOut / rIn) < -20 && pl <= 0.8913 && Math.Abs(fim + 18) < 2.5 && gq < -25,
            $"40 Hz {20 * Math.Log10(rOut / rIn):F1} dB, pico {20 * Math.Log10(pl):F2} dBFS, voz baixa -34 -> {fim:F1} dB (alvo -18), portão {gq:F1} dB");
    }
}

// 23. Program sound into the mic: process-loopback capture of a child playing SILENCE (nothing audible), and the root-process lookup
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null;
    try
    {
        var exe = Environment.ProcessPath!;
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--tocar 4000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(700);
        var lt = new Leitor("teste-programa", 60);
        using (var c = new CapturaPrograma((uint)filho.Id, lt))
        {
            c.Abrir(); Thread.Sleep(1500);
        }
        int chegou = lt.Anel.Count;
        string eu = Path.GetFileNameWithoutExtension(exe);
        var raiz = CapturaPrograma.Raizes(eu);
        bool raizOk = raiz.Contains((uint)Environment.ProcessId) && !raiz.Contains((uint)filho.Id);
        ok = chegou > 0 && raizOk;
        r = $"{chegou / 2} quadros em 1,5 s do processo {filho.Id}; raízes de {eu}: {string.Join(",", raiz)} (eu {Environment.ProcessId}, filho fora: {raizOk})";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } }
    Ok("mic: captura o som de um programa só", ok, r);
}

// 24. Program clone (Mixer "Clonar"): capture of a child playing SILENCE -> own reader of an extra output (real, non-cable,
// non-default when there is one; it only ever receives silence) -> the output pulls it. Nothing audible, default untouched.
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; Destino? d = null;
    try
    {
        var reais = Aparelhos.Listar(true).Where(a => !a.Virtual).ToList();
        var ap = reais.FirstOrDefault(a => !a.Padrao) ?? reais.FirstOrDefault() ?? throw new Exception("sem saída real");
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--tocar 4000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(700);
        d = new Destino(ap.Nome, 60); d.Abrir();
        var lt = d.NovoLeitor("clone-" + filho.Id, 60);
        long b0 = d.Blocos;
        using (var c = new CapturaPrograma((uint)filho.Id, lt)) { c.Abrir(); Thread.Sleep(1500); }
        long lidos = lt.Lidos, blocos = d.Blocos - b0;
        d.Remover(lt);
        bool saiu = !d.Leitores.Contains(lt);
        ok = d.Aberto && lidos > 0 && blocos > 0 && saiu;
        r = $"saída {d.NomeReal}: {blocos} blocos puxados, leitor do clone leu {lidos} quadros, removido {saiu}";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { d?.Fechar(); try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } }
    Ok("clonar: programa toca também em outra saída", ok, r);
}

// 02/11, not in the full run: FAH.Testes.exe --piso. Copy with own volume while the original is muted: FAH holds the session at
// Mesa.Piso instead of muting and the copy multiplies back. Is the process loopback still exact there? Child plays the inaudible 19 kHz
// (-50 dBFS) on the default device; only ITS session is touched. Level ratio vs session 100 % and SNR (sine fit) at 1e-4 and Piso.
void TestePiso()
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null;
    NAudio.CoreAudioApi.SimpleAudioVolume? Sv() { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
        var ss = dv.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) if (filho != null && ss.Sessions[i].GetProcessID == filho.Id) return ss.Sessions[i].SimpleAudioVolume; return null; }
    try
    {
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--agudo 9000") { CreateNoWindow = true, UseShellExecute = false })!;
        var sw = System.Diagnostics.Stopwatch.StartNew(); NAudio.CoreAudioApi.SimpleAudioVolume? sv = null; while (sv == null && sw.ElapsedMilliseconds < 4000) { Thread.Sleep(50); sv = Sv(); }
        if (sv == null) throw new Exception("sessão do filho não achada");
        var lt = new Leitor("piso", 60);
        using var c = new CapturaPrograma((uint)filho.Id, lt); c.Abrir();
        (double amp, double snr, bool silencio) Mede(float vol)
        {
            sv.Volume = vol; Thread.Sleep(400); lt.Anel.Limpar(); Thread.Sleep(1000);
            var b = new float[lt.Anel.Count]; int n = lt.Anel.Ler(b) / 2;
            double sc = 0, cc = 0, ss2 = 0, sx = 0, cx = 0, tot = 0;
            for (int i = 0; i < n; i++) { double w = 2 * Math.PI * 19000 * i / 48000, x = b[2 * i]; sx += x * Math.Sin(w); cx += x * Math.Cos(w); ss2 += Math.Sin(w) * Math.Sin(w); cc += Math.Cos(w) * Math.Cos(w); tot += x * x; }
            double a = sx / Math.Max(ss2, 1e-9), k = cx / Math.Max(cc, 1e-9), res = 0;
            for (int i = 0; i < n; i++) { double w = 2 * Math.PI * 19000 * i / 48000, e = b[2 * i] - a * Math.Sin(w) - k * Math.Cos(w); res += e * e; }
            double amp = Math.Sqrt(a * a + k * k), sig = amp * amp / 2 * n;
            return (amp, 10 * Math.Log10(sig / Math.Max(res, 1e-300)), tot == 0);
        }
        var (a1, s1, _) = Mede(1); var (a2, s2, z2) = Mede(1e-4f); var (a3, s3, z3) = Mede(Mesa.Piso); sv.Volume = 1;
        double r2 = a2 / a1 / 1e-4, r3 = a3 / a1 / Mesa.Piso;
        // delay: packets still at the old level after the session volume drops (CapturaPrograma.Desfazer waits before raising E)
        var lats = new List<double>(); var tb = new float[96000];
        for (int rep = 0; rep < 6; rep++)
        {
            sv.Volume = 1; Thread.Sleep(300); lt.Anel.Limpar();
            var sw2 = System.Diagnostics.Stopwatch.StartNew(); double t0 = -1;
            while (sw2.ElapsedMilliseconds < 1500)
            {
                int n2 = lt.Anel.Ler(tb); float pk = 0; for (int i = 0; i < n2; i++) pk = Math.Max(pk, Math.Abs(tb[i]));
                if (t0 < 0) { if (sw2.ElapsedMilliseconds >= 200 + rep * 7) { sv.Volume = 1e-3f; t0 = sw2.Elapsed.TotalMilliseconds; } }
                else if (n2 > 0 && pk < a1 * 0.1) { lats.Add(sw2.Elapsed.TotalMilliseconds - t0); break; }
                Thread.Yield();
            }
        }
        sv.Volume = 1;
        Console.WriteLine("atraso do volume na cópia (ms): " + string.Join(" ", lats.Select(l => l.ToString("F1"))));
        ok = a1 > 1e-3 && Math.Abs(r3 - 1) < .02 && s3 > s1 - 6;
        r = $"100%: amp {a1:E2} SNR {s1:F0} dB; 1e-4: x{r2:F4} SNR {s2:F0} dB{(z2 ? " SILÊNCIO" : "")}; piso {Mesa.Piso:E0}: x{r3:F4} SNR {s3:F0} dB{(z3 ? " SILÊNCIO" : "")}";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } }
    Ok("cópia: sessão no piso continua exata na cópia", ok, r);
}

// not in the full run (tone on the mic cable): FAH.Testes.exe --volcopia, with FAH closed
void TesteVolCopia()
// 25. Does the program's own volume (Windows mixer session) reach its copy? Child plays a tone on the mic cable's output (Line 2,
// only calls hear it; run with FAH closed); capture it at session volume 100 % then 25 %. Ratio ~1 = pre-volume, ~.25 = post-volume.
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null;
    try
    {
        var cabo = Aparelhos.Listar(true).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem Line 2");
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--tom-em \"{cabo.Nome}\" 6000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(900);
        NAudio.CoreAudioApi.SimpleAudioVolume? Sv() { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); var dv = en.GetDevice(cabo.Id); var ss = dv.AudioSessionManager; ss.RefreshSessions();
            for (int i = 0; i < ss.Sessions.Count; i++) if (ss.Sessions[i].GetProcessID == filho!.Id) return ss.Sessions[i].SimpleAudioVolume; return null; }
        var lt = new Destino(cabo.Nome, 60).NovoLeitor("t", 60);
        using var c = new CapturaPrograma((uint)filho.Id, lt); c.Abrir();
        float Mede() { Thread.Sleep(300); c.Pico = 0; Thread.Sleep(800); return c.Pico; }
        float a = Mede(); var sv = Sv() ?? throw new Exception("sessão do filho não achada"); sv.Volume = .25f; float b = Mede(); sv.Volume = 1;
        ok = a > .1f; r = $"pico 100%: {a:0.000}, pico 25%: {b:0.000}, razão {(a > 0 ? b / a : 0):0.00} ({(a > 0 && b / a < .6 ? "a cópia já sai com o volume do programa" : "a cópia ignora o volume do programa")})";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } }
    Ok("cópia: volume do programa chega nela?", ok, r);
}

// 31/10, not in the full run: FAH.Testes.exe --barra. Volume changed outside reaches the Mixer snapshot fast; taskbar list and wheel step.
// Child = copy named FAHTomTeste playing an inaudible 19 kHz on the default device; only ITS session is touched (back to 100 % at the end).
void TesteBarra()
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; var dir = AppContext.BaseDirectory; var exe = Path.Combine(dir, "FAHTomTeste.exe");
    NAudio.CoreAudioApi.SimpleAudioVolume? Sv() { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
        var ss = dv.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) if (filho != null && ss.Sessions[i].GetProcessID == filho.Id) return ss.Sessions[i].SimpleAudioVolume; return null; }
    bool Espera(Func<bool> c, int ms) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) { if (c()) return true; Thread.Sleep(20); } return c(); }
    var mesa = new Mesa { MestreFalso = () => 100, SoPrograma = "FAHTomTeste" }; string snap = ""; mesa.AoMudar = s => snap = JsonSerializer.Serialize(s);
    try
    {
        File.Copy(Path.Combine(dir, "FAH.Testes.exe"), exe, true);
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--agudo 20000") { CreateNoWindow = true, UseShellExecute = false })!;
        if (!Espera(() => Sv() != null, 4000)) throw new Exception("sessão do filho não achada");
        Sv()!.Volume = 1;
        bool a = mesa.Elegiveis().Contains("FAHTomTeste");                                   // Mixer closed: playing now
        mesa.Mostrar(true);
        bool b = Espera(() => snap.Contains("\"x\":\"FAHTomTeste\""), 4000);               // shows in the row
        bool b2 = mesa.Elegiveis().Contains("FAHTomTeste");                                  // Mixer open: shown in the row
        Thread.Sleep(1200);
        var sw = System.Diagnostics.Stopwatch.StartNew(); Sv()!.Volume = .4f;
        bool c = Espera(() => snap.Contains("\"x\":\"FAHTomTeste\",\"n\"") && snap.Contains("\"v\":40"), 1500); long tc = sw.ElapsedMilliseconds;
        mesa.Rolar("FAHTomTeste", -10); bool d = Espera(() => Sv() is { } q && Math.Abs(q.Volume - .3f) < .01, 1500);
        mesa.Rolar("FAHTomTeste", 2); mesa.Rolar("FAHTomTeste", 2); mesa.Rolar("FAHTomTeste", 2); bool e = Espera(() => Sv() is { } q && Math.Abs(q.Volume - .36f) < .01, 1500);
        ok = a && b && b2 && c && tc < 500 && d && e;
        r = $"lista com Mixer fechado {a}, aparece {b}, lista com Mixer aberto {b2}, mudança de fora na tela {c} em {tc} ms, rodinha -10 {d}, 3 passos rápidos +6 {e}";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { try { if (Sv() is { } q) q.Volume = 1; } catch { } mesa.Mostrar(false); try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } }
    Ok("barra de tarefas + volume de fora", ok, r);
}

// 31/10, not in the full run: FAH.Testes.exe --segue. Program bar follows the system volume until moved; moved = own level kept when the
// system volume changes; back to the system level = follows again. System volume is PRETENDED (MestreFalso): the real one is never touched.
// Child = copy named FAHTomTeste playing silence; only ITS session is touched (back to 100 % at the end).
void TesteSegue()
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; var dir = AppContext.BaseDirectory; var exe = Path.Combine(dir, "FAHTomTeste.exe");
    NAudio.CoreAudioApi.SimpleAudioVolume? Sv() { using var en = new NAudio.CoreAudioApi.MMDeviceEnumerator(); var dv = en.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
        var ss = dv.AudioSessionManager; ss.RefreshSessions(); for (int i = 0; i < ss.Sessions.Count; i++) if (filho != null && ss.Sessions[i].GetProcessID == filho.Id) return ss.Sessions[i].SimpleAudioVolume; return null; }
    bool Espera(Func<bool> c, int ms) { var sw = System.Diagnostics.Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) { if (c()) return true; Thread.Sleep(20); } return c(); }
    bool Q(float v) => Espera(() => Sv() is { } q && Math.Abs(q.Volume - v) < .012f, 2000);
    int sis = 80; var mesa = new Mesa { MestreFalso = () => sis, SoPrograma = "FAHTomTeste" }; List<Regra> ult = []; mesa.AoRegrar = l => ult = l; string snap = ""; mesa.AoMudar = s => snap = JsonSerializer.Serialize(s);
    Regra? Rg() => ult.SingleOrDefault(x => x.X == "FAHTomTeste");
    try
    {
        File.Copy(Path.Combine(dir, "FAH.Testes.exe"), exe, true);
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--agudo 30000") { CreateNoWindow = true, UseShellExecute = false })!;   // must make sound: the Mixer only lists programs that play
        if (!Espera(() => Sv() != null, 4000)) throw new Exception("sessão do filho não achada");
        Sv()!.Volume = 1; mesa.Mostrar(true); Thread.Sleep(1500);
        bool a = Espera(() => snap.Contains("\"v\":80,") && snap.Contains("\"pd\":80,\"pr\":0"), 2000);       // untouched: shows the system volume
        sis = 50; bool b = Q(1) && Espera(() => snap.Contains("\"v\":50,"), 1000);                                  // still follows
        mesa.VolumePrograma("som", "FAHTomTeste", 40, null); bool c = Q(.8f) && Rg() is { A: true, V: 40 };        // moved: own 40
        sis = 80; bool d = Q(.5f);                                                                                    // system up: still hears 40
        sis = 30; bool e = Q(1);                                                                                      // system under 40: capped
        sis = 80; bool f = Q(.5f) && Rg() is { A: true, V: 40 };                                                      // back: 40 again
        Sv()!.Volume = .25f; bool g = Espera(() => Rg() is { A: true, V: 20 }, 1500);                                // moved outside (Windows mixer): own 20
        sis = 40; bool h = Q(.5f);
        mesa.VolumePrograma("som", "FAHTomTeste", 40, null); bool i = Q(1) && Rg() is { A: false };                  // at the system level: follows again
        sis = 70; bool j = Q(1) && Espera(() => snap.Contains("\"pd\":70,\"pr\":0"), 1000);
        mesa.Mostrar(false); mesa.VolumePrograma("som", "FAHTomTeste", 35, null); Thread.Sleep(300); sis = 50; bool k = Q(.7f);   // Mixer closed: still kept
        ok = a && b && c && d && e && f && g && h && i && j && k;
        r = $"segue ao abrir {a}, segue mudança {b}, mexeu=próprio {c}, sistema sobe {d}, sistema abaixo {e}, volta {f}, mexido por fora {g}/{h}, voltou à marca segue {i}/{j}, Mixer fechado {k}" + (ok ? "" : " | snap " + (snap.IndexOf("FAHTomTeste") is int ix and >= 0 ? snap.Substring(ix, Math.Min(160, snap.Length - ix)) : snap[..Math.Min(200, snap.Length)]));
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally { mesa.Regras.Clear(); try { if (Sv() is { } q) q.Volume = 1; } catch { } mesa.Mostrar(false); try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { } try { File.Delete(exe); } catch { } }
    Ok("volume do programa: segue o sistema até mexer", ok, r);
}

// not in the full run (tone on the mic cable): FAH.Testes.exe --bypassvol, with FAH closed
void TesteBypassVol()
// 26. Device bypass: does the device's own volume (endpoint) reach its loopback? Child plays a tone on Line 2; loopback of Line 2
// at endpoint volume 100 % then 25 % (restored after). Ratio ~1 = loopback is pre-volume (FAH must apply the fader), ~.25 = post.
{

{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; NAudio.CoreAudioApi.MMDevice? dv = null; float antes = -1;
    try
    {
        var cabo = Aparelhos.Listar(true).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem Line 2");
        dv = Aparelhos.Abrir(cabo.Id); var ev = dv.AudioEndpointVolume; antes = ev.MasterVolumeLevelScalar; ev.MasterVolumeLevelScalar = 1f;
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--tom-em \"{cabo.Nome}\" 6000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(900);
        using var f = new Fluxo(cabo.Nome, new Cadeia(), true); f.Abrir(cabo);
        float Mede() { Thread.Sleep(300); f.Pico = 0; Thread.Sleep(800); return f.Pico; }
        float a = Mede(); ev.MasterVolumeLevelScalar = .25f; float b = Mede();
        ok = a > .1f; r = $"pico 100%: {a:0.000}, pico 25%: {b:0.000}, razão {(a > 0 ? b / a : 0):0.00} ({(a > 0 && b / a < .6 ? "o som capturado já vem com o volume do aparelho" : "o som capturado ignora o volume do aparelho")}), volume no hardware {ev.HardwareSupport.HasFlag(NAudio.CoreAudioApi.EEndpointHardwareSupport.Volume)}";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally
    {
        try { if (dv != null && antes >= 0) dv.AudioEndpointVolume.MasterVolumeLevelScalar = antes; } catch { }
        try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { }
    }
    Ok("bypass: volume do aparelho chega na captura?", ok, r);
}
// same for the mic side: tone into Line 2's output, captured from Line 2's input at input level 100 % then 25 % (restored after)
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; NAudio.CoreAudioApi.MMDevice? dv = null; float antes = -1;
    try
    {
        var cabo = Aparelhos.Listar(true).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem Line 2");
        var ent = Aparelhos.Listar(false).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem entrada Line 2");
        dv = Aparelhos.Abrir(ent.Id); var ev = dv.AudioEndpointVolume; antes = ev.MasterVolumeLevelScalar; ev.MasterVolumeLevelScalar = 1f;
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--tom-em \"{cabo.Nome}\" 6000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(900);
        using var f = new Fluxo(ent.Nome, new Cadeia(), false); f.Abrir(ent);
        float Mede() { Thread.Sleep(300); f.Pico = 0; Thread.Sleep(800); return f.Pico; }
        float a = Mede(); ev.MasterVolumeLevelScalar = .25f; float b = Mede();
        ok = a > .1f; r = $"pico 100%: {a:0.000}, pico 25%: {b:0.000}, razão {(a > 0 ? b / a : 0):0.00} ({(a > 0 && b / a < .6 ? "a captura já vem com o nível do microfone" : "a captura ignora o nível do microfone")})";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally
    {
        try { if (dv != null && antes >= 0) dv.AudioEndpointVolume.MasterVolumeLevelScalar = antes; } catch { }
        try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { }
    }
    Ok("bypass: nível do microfone chega na captura?", ok, r);
}
// 01/11: does the cable's PLAY side volume reduce what goes through it? Decides whether the fader may move device and cable together (else volume falls twice)
{
    string r; bool ok = false; System.Diagnostics.Process? filho = null; NAudio.CoreAudioApi.MMDevice? dv = null; float antes = -1;
    try
    {
        var cabo = Aparelhos.Listar(true).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem Line 2");
        var ent = Aparelhos.Listar(false).FirstOrDefault(a => a.Virtual && a.Nome.Contains("Line 2")) ?? throw new Exception("sem entrada Line 2");
        dv = Aparelhos.Abrir(cabo.Id); var ev = dv.AudioEndpointVolume; antes = ev.MasterVolumeLevelScalar; ev.MasterVolumeLevelScalar = 1f;
        filho = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--tom-em \"{cabo.Nome}\" 6000") { CreateNoWindow = true, UseShellExecute = false })!;
        Thread.Sleep(900);
        using var f = new Fluxo(ent.Nome, new Cadeia(), false); f.Abrir(ent);
        float Mede() { Thread.Sleep(300); f.Pico = 0; Thread.Sleep(800); return f.Pico; }
        float a = Mede(); ev.MasterVolumeLevelScalar = .25f; float b = Mede();
        ok = a > .1f; r = $"pico 100%: {a:0.000}, pico 25%: {b:0.000}, razão {(a > 0 ? b / a : 0):0.00} ({(a > 0 && b / a < .6 ? "O CABO REDUZ: juntar derruba o volume duas vezes" : "o cabo ignora o próprio volume: pode juntar")})";
    }
    catch (Exception ex) { r = "ERRO " + ex.Message; }
    finally
    {
        try { if (dv != null && antes >= 0) dv.AudioEndpointVolume.MasterVolumeLevelScalar = antes; } catch { }
        try { if (filho is { HasExited: false }) { filho.Kill(); filho.WaitForExit(); } } catch { }
    }
    Ok("cabo: volume de saída reduz o que passa?", ok, r);
}
}

rel.Insert(0, $"FAH testes {DateTime.Now:yyyy-MM-dd HH:mm} — {(falhas == 0 ? "tudo OK" : falhas + " falha(s)")}{Environment.NewLine}");
Console.Write(rel);
if (args.Length > 0 && !args[0].StartsWith('-')) File.WriteAllText(args[0], rel.ToString());   // --flags are not report paths
return falhas;
sealed class AlcModulo(string nome, string pasta) : System.Runtime.Loader.AssemblyLoadContext(nome)
{
    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName n)
    {
        if (n.Name == "FAH.Contrato") return typeof(FAH.Contrato.IModulo).Assembly;
        var arq = Path.Combine(pasta, n.Name + ".dll");
        return n.Name != null && !n.Name.StartsWith("FAH.") && File.Exists(arq) ? LoadFromAssemblyPath(arq) : null;
    }
    protected override IntPtr LoadUnmanagedDll(string nome)
    {
        var arq = Path.Combine(pasta, nome.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? nome : nome + ".dll");
        return File.Exists(arq) ? LoadUnmanagedDllFromPath(arq) : IntPtr.Zero;
    }
}

sealed class HostFalso(List<string> logs) : FAH.Contrato.IHost
{
    public void Enviar(string tipo, object dados) { if (tipo == "micvoz") { var s = JsonSerializer.Serialize(dados); if (s.Contains("\"ia\":\"")) lock (logs) logs.Add(s); } }
    public string PastaDados(string id) => Path.GetTempPath();
    public void Log(string t) { lock (logs) logs.Add(t); }
    public int Taxa => 48000;
}
