// Entry: single instance; --guardiao PID = restore guard. Offline tests: fonte\Testes.
namespace FAH;

static class Programa
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--guardiao") { Motor.Guardiao(int.Parse(args[1])); return 0; }
        if (args.Length >= 2 && args[0] == "--surround71-admin") { Audio.Surround71.Log = Base.Log; return Audio.Surround71.ParteAdmin(int.Parse(args[1])); }
        if (args.Length >= 1 && args[0] == "--cabos") { Audio.Cabos.Log = Base.Log; Audio.Cabos.Detectar(); return 0; }   // tests: read-only cable check -> log
        if (args.Length >= 1 && args[0] == "--cabo71")       // tests: --cabo71 [on|off] with the app closed; result in the log
        {
            Audio.Surround71.Log = Base.Log;
            var (cb, c) = Motor.AcharCabo(); var t = cb?.Nome ?? "";
            if (args.Length >= 2) Base.Log("cabo71 " + Audio.Surround71.Ativar(t, c, args[1] == "on"));
            Base.Log("cabo71 estado " + Audio.Surround71.Estado(t, c));
            return 0;
        }
        if (args.Length >= 1 && args[0] == "--teste-mic") return TesteMic();   // tests: mic modules as loaded by the app (no device) -> log
        if (args.Length >= 1 && args[0] == "--arrumar") { Base.LimparLayoutAntigo(); return 0; }   // tests: old loose layout cleanup only -> log
        if (args.Length >= 1 && args[0] == "--teste-tela") return TesteTela();                     // tests: WebView2 + ui\ off screen, no Motor -> log
        // --esperar PID (restart after a backup import): wait for the old process to leave before taking the mutex
        int ie = Array.IndexOf(args, "--esperar");
        if (ie >= 0 && ie + 1 < args.Length)
        {
            if (int.TryParse(args[ie + 1], out var velho)) { try { using var p = System.Diagnostics.Process.GetProcessById(velho); p.WaitForExit(30000); } catch { } }
            args = args.Where((_, i) => i != ie && i != ie + 1).ToArray();
        }
        bool bandeja = args.Contains("--bandeja");          // started by Windows: hidden in the tray (unless cfg.minimizado is off)
        using var unica = new Mutex(true, "FAH-instancia-unica", out bool primeira);
        if (!primeira) return 0;
        try
        {
            Base.LimparLayoutAntigo();                     // 1.2.0 single file: drop the loose dlls an update left behind
            Configuracoes.ImportarPendente();               // a backup chosen before the restart goes in before anything reads the state
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;   // no long blocking GC over the audio threads
            ApplicationConfiguration.Initialize();
            Application.ThreadException += (_, e) => Base.Log("erro: " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Base.Log("erro fatal: " + e.ExceptionObject);
            using var motor = new Motor();
            Base.Log($"aberto; módulos: {string.Join(", ", motor.Modulos.Select(m => m.Id))}");
            Task.Run(motor.Ligar);
            Application.Run(new Janela(motor, bandeja));
            return 0;
        }
        catch (Exception e)
        {
            Base.Log("não abriu: " + e);
            try { MessageBox.Show("O FAH não conseguiu abrir. O motivo ficou gravado no arquivo fah.log, na pasta usuario\\relatorios.", "FAH", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            return 1;
        }
    }

    /// <summary>Single-file check without touching sound: off-screen form, WebView2 from usuario\webview, opens ui\index.html, logs the title. 0 = page loaded.</summary>
    static int TesteTela()
    {
        ApplicationConfiguration.Initialize();
        int r = 1;
        var f = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Size = new Size(800, 600), ShowInTaskbar = false };
        var w = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
        f.Controls.Add(w);
        var limite = new System.Windows.Forms.Timer { Interval = 20000 }; limite.Tick += (_, _) => { Base.Log("teste-tela: tempo esgotado"); f.Close(); }; limite.Start();
        f.Shown += async (_, _) =>
        {
            try
            {
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, Base.Pasta("webview"));
                await w.EnsureCoreWebView2Async(env);
                w.CoreWebView2.SetVirtualHostNameToFolderMapping("fah.local", Base.Ui, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                w.CoreWebView2.NavigationCompleted += (_, e) => { Base.Log($"teste-tela: ok={e.IsSuccess} titulo={w.CoreWebView2.DocumentTitle} ico={File.Exists(Path.Combine(Base.Ui, "fah.ico"))}"); r = e.IsSuccess ? 0 : 1; f.Close(); };
                w.CoreWebView2.Navigate("https://fah.local/index.html");
            }
            catch (Exception e) { Base.Log("teste-tela: " + e); f.Close(); }
        };
        Application.Run(f);
        return r;
    }

    /// <summary>Loads the modules like the app (own load contexts, onnxruntime from modulos\), runs the mic chain on noise for 4 s of
    /// audio (the suppressor model loads in the background meanwhile) and logs in/out level. 0 = model ran and lowered the noise.</summary>
    static int TesteMic()
    {
        var erros = new List<string>();
        var mods = Carregador.Carregar(Base.Modulos, erros);
        foreach (var e in erros) Base.Log("teste-mic: módulo recusado: " + e);
        var host = new HostTeste();
        foreach (var m in mods) { try { m.Iniciar(host); } catch (Exception e) { Base.Log("teste-mic: " + m.Id + ": " + e.Message); } }
        var vazio = System.Text.Json.JsonDocument.Parse("{}").RootElement;
        foreach (var m in mods) m.Receber("ligado", vazio);
        var c = new Audio.Cadeia(); c.Definir(mods.SelectMany(m => m.Efeitos).Where(e => e.Lado == Contrato.Lado.Mic));
        var rnd = new Random(1); var buf = new float[2048]; double ein = 0, eout = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int b = 0; b < 400; b++)                         // 400 x 1024 = 8.5 s of audio, paced so the model can load
        {
            for (int i = 0; i < 1024; i++) buf[2 * i] = buf[2 * i + 1] = (float)(0.03 * (rnd.NextDouble() * 2 - 1));
            double si = 0; for (int i = 0; i < 2048; i++) si += buf[i] * buf[i];
            c.Processar(buf, 1024);
            double so = 0; for (int i = 0; i < 2048; i++) so += buf[i] * buf[i];
            if (b >= 300) { ein += si; eout += so; }
            if (b < 100) Thread.Sleep(20);
        }
        double queda = 10 * Math.Log10(ein / Math.Max(eout, 1e-20));
        Base.Log($"teste-mic: {mods.Count} módulos, cadeia mic: {string.Join(" > ", c.Efeitos.Select(e => e.Id))}; ruído branco -{queda:F1} dB no fim, {sw.ElapsedMilliseconds} ms");
        foreach (var m in mods) try { m.Parar(); } catch { }
        return queda > 10 ? 0 : 1;
    }

    sealed class HostTeste : Contrato.IHost
    {
        public void Enviar(string tipo, object dados) { }
        public string PastaDados(string id) => Path.Combine(Path.GetTempPath(), "fah-teste-" + id);
        public void Log(string texto) => Base.Log("teste-mic: " + texto);
        public int Taxa => 48000;
    }
}
