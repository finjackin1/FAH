// Main window: WebView2 shows the approved screens (ui\index.html). Bridge:
// JS -> C#: chrome.webview.postMessage({tipo, ...});  C# -> JS: {tipo, dados} (ui\base\ponte.js dispatches).
using System.IO;
using System.Runtime.InteropServices;
using FAH.Audio;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FAH;

sealed class Janela : Form
{
    readonly Motor motor;
    readonly Mesa mesa = new();
    Barra? barra;
    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(28, 28, 30) };
    readonly System.Windows.Forms.Timer relogio = new() { Interval = 100 };
    readonly NotifyIcon bandeja = new() { Text = "FAH" };
    bool pronto, sair, micIniciado;
    const int Borda = 5;                              // resize edge around the WebView
    // screens are laid out for 1280x720 CSS px; any window size = same layout zoomed to fit (no scroll ever)
    const float BaseL = 1280, BaseA = 720;
    static readonly Dictionary<string, Size> Tamanhos = new() { ["max"] = new(1440, 810), ["min"] = new(1080, 608) };   // 16:9
    string tam = "max";
    SizeF css;                                        // last normal-window size in CSS px (kept when moving to a monitor with another scale)
    bool dpiMudando;
    Cfg cfg = new();                                  // Settings screen values (estado key "cfg")
    bool escondido;                                   // started by Windows: invisible (Opacity 0) until the page loaded, then hidden
    bool? iconeLig, iconeModo;                        // what the tray icon shows now (switch only on change)
    Icon? iconeCinza;
    readonly List<string> atalhosFalha = [];
    static readonly string[] IdsAtalho = ["ligar", "mic", "preset", "abrir"];   // RegisterHotKey id = index + 1
    bool micAntes;                                    // mic chain was on when the hotkey turned everything off
    string? resultadoAtt;                             // result of the last update (told once, after the page loaded)
    bool procurando, baixando;
    int tentativasAuto;
    static readonly int[] EsperasAuto = [30, 120, 600];
    string? pastaPronta, versaoPronta;
    object estadoAtt = new { estado = "em-dia", versao = (string?)null, pct = 0, erro = (string?)null };

    protected override bool ShowWithoutActivation => escondido;

    public Janela(Motor m, bool oculto = false)
    {
        motor = m;
        cfg = Cfg.De(m.Estado.TryGetValue("cfg", out var c0) ? c0 : null);
        if (oculto && cfg.Minimizado) { escondido = true; Opacity = 0; }
        if (cfg.Inicio) Configuracoes.AplicarInicio(true);       // also rewrites the path if the folder moved
        resultadoAtt = Atualizador.ResultadoDaUltima();
        Text = "FAH";
        if (m.Estado.TryGetValue("tam", out var t) && t.ValueKind == JsonValueKind.String && Tamanhos.ContainsKey(t.GetString()!)) tam = t.GetString()!;
        var s0 = Px(Tamanhos[tam]); ClientSize = new Size(s0.Width + 2 * Borda, s0.Height + 2 * Borda);
        MinimumSize = Px(new Size(800, 450));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 28, 30);
        FormBorderStyle = FormBorderStyle.None;       // own title bar (uiasearra.js)
        Padding = new Padding(Borda);
        var ico = Path.Combine(Base.Ui, "fah.ico");
        Icon = File.Exists(ico) ? new Icon(ico) : SystemIcons.Application;
        Controls.Add(web);
        AtualizarBandeja();
        bandeja.ContextMenuStrip = new ContextMenuStrip();
        bandeja.ContextMenuStrip.Items.Add("Abrir FAH", null, (_, _) => Mostrar());
        bandeja.ContextMenuStrip.Items.Add("Sair", null, (_, _) => Sair());
        bandeja.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Mostrar(); };
        bandeja.Visible = true;
        Resize += (_, _) => { Padding = new Padding(WindowState == FormWindowState.Maximized ? 0 : Borda); Lembrar(); Zoom(); if (pronto) Mandar("janela", new { max = WindowState == FormWindowState.Maximized, tam }); };
        // other monitor with another scale: Windows' suggested size and the old-scale MinimumSize fought and left the window/zoom wrong
        // until "hold maximize". Now: new-scale minimum, then the same CSS size again, zoom refit
        DpiChanged += (_, _) => { dpiMudando = true; MinimumSize = Px(new Size(800, 450)); BeginInvoke(AjustarDpi); };
        Lembrar();
        motor.AoEnviar = (t, d) => { if (pronto && IsHandleCreated) BeginInvoke(() => Mandar(t, d)); };
        mesa.Log = Base.Log; mesa.SaidaFah = () => motor.SaidaAtual; mesa.MicFah = () => motor.MicAtual; mesa.CaboMicFah = () => motor.CaboMicAtual; /* 01/11 user: 7.1 path untouched, Line 1 no longer follows the fader: mesa.CaboSomPar = () => motor.CaboSomEntrada; */ /* 01/11: mic fader moves only the device (outside writer answers when Line 2 leaves 87.5): mesa.CaboMicPar = () => motor.CaboMicEntrada; */ mesa.Icone = IconePng;
        mesa.AoMudar = d => { if (pronto && IsHandleCreated) BeginInvoke(() => Mandar("mixer", d)); };
        mesa.AoNivel = d => { if (pronto && IsHandleCreated) BeginInvoke(() => Mandar("mixniv", d)); };
        mesa.AoFixar = l => motor.Receber("mixfixos", JsonSerializer.SerializeToElement(l));
        if (m.Estado.TryGetValue("mixfixos", out var fx)) try { mesa.Fixos = fx.Deserialize<List<Fixo>>() ?? []; } catch { }
        mesa.AoRegrar = l => motor.Receber("mixregras", JsonSerializer.SerializeToElement(l));
        if (m.Estado.TryGetValue("mixregras", out var rg)) try { mesa.Regras = rg.Deserialize<List<Regra>>() ?? []; } catch { }
        // 02/11 copies with their own volume: Mesa holds the original's session at the floor, the copy undoes it (MotorCopiaVol)
        mesa.Independente = motor.Independente; mesa.HaCopias = motor.HaCopiasProprias;
        motor.Sessao = x => mesa.Sessao.TryGetValue(x, out var s) ? s : null; motor.AoMudarCopias = mesa.Acordar;
        mesa.Vigiar();
        barra = new Barra(mesa);                      // wheel over a taskbar icon = that program's volume (Mixer programs only)
        relogio.Tick += (_, _) => { AtualizarBandeja(); if (pronto && (motor.Ligado || motor.MicLigado)) Mandar("niveis", motor.Niveis()); };
        Load += async (_, _) => await Iniciar();
        // daily backup ~60 s after start, update check ~20 s after start (both off the UI thread / async)
        Task.Run(async () =>
        {
            await Task.Delay(60000);
            while (true)                              // left in the tray for days: next day's backup too (Diario skips if today's exists)
            {
                var c = cfg;
                if (c.BackupDiario) { Configuracoes.Diario(c.BackupQtd); MandarUI("cfginfo", InfoCfg()); }
                await Task.Delay(TimeSpan.FromHours(1));
            }
        });
        Task.Delay(3000).ContinueWith(_ =>
        {
            try { BeginInvoke(() => { if (cfg.AutoAtualizar && Atualizador.EhExeDeVerdade()) _ = Procurar(true); }); } catch { }
        });
    }

    // ---- tray icon follows the sound engine (gray = off)
    void AtualizarBandeja()
    {
        bool lig = motor.Ligado, modo = cfg.IconeEstado;
        if (iconeLig == lig && iconeModo == modo) return;
        iconeLig = lig; iconeModo = modo;
        bandeja.Icon = modo && !lig ? iconeCinza ??= Cinza(Icon ?? SystemIcons.Application) : Icon;
        bandeja.Text = modo ? (lig ? "FAH — som ligado" : "FAH — som desligado") : "FAH";
    }

    static Icon Cinza(Icon ic)
    {
        try
        {
            int t = Math.Max(32, SystemInformation.SmallIconSize.Width);
            using var orig = new Icon(ic, t, t); using var src = orig.ToBitmap();
            using var cinza = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var p = src.GetPixel(x, y); int g = (int)(p.R * .299 + p.G * .587 + p.B * .114);
                    cinza.SetPixel(x, y, Color.FromArgb(p.A, g, g, g));
                }
            return Icon.FromHandle(cinza.GetHicon());       // one icon for the whole run: the handle is never freed
        }
        catch (Exception e) { Base.Log("ícone cinza: " + e.Message); return ic; }
    }

    // ---- global hotkeys
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    void SoltarAtalhos() { for (int i = 1; i <= IdsAtalho.Length; i++) UnregisterHotKey(Handle, i); }

    void RegistrarAtalhos()
    {
        if (!IsHandleCreated) return;
        SoltarAtalhos(); atalhosFalha.Clear();
        for (int i = 0; i < IdsAtalho.Length; i++)
        {
            string id = IdsAtalho[i], s = cfg.Atalhos.GetValueOrDefault(id, "");
            if (s == "") continue;
            var a = Configuracoes.Analisar(s);
            if (a == null || !RegisterHotKey(Handle, i + 1, a.Value.Mods | 0x4000, a.Value.Vk))   // 0x4000 = MOD_NOREPEAT
            {
                atalhosFalha.Add(id); Base.Log($"atalho {id} ({s}): não registrou");
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RegistrarAtalhos(); }
    protected override void OnHandleDestroyed(EventArgs e) { SoltarAtalhos(); base.OnHandleDestroyed(e); }

    void Atalho(int id)
    {
        switch (id >= 1 && id <= IdsAtalho.Length ? IdsAtalho[id - 1] : "")
        {
            case "ligar": AtalhoLigar(); break;
            case "mic": AtalhoMic(); break;
            case "preset": if (pronto) Mandar("atalho", new { acao = "preset" }); break;
            case "abrir": Mostrar(); break;
        }
    }

    /// <summary>Everything off (sound + mic chain, remembering which was on) or back on.</summary>
    void AtalhoLigar()
    {
        bool s = motor.Ligado, mc = motor.MicLigado;
        if (s || mc)
        {
            micAntes = mc;
            Task.Run(() => { try { if (s) motor.Desligar(); if (mc) motor.DesligarMic(); } catch (Exception e) { Base.Log("atalho ligar: " + e.Message); } });
        }
        else
        {
            bool volta = micAntes;
            Task.Run(() => { try { motor.Ligar(); if (volta) motor.LigarMic(); } catch (Exception e) { Base.Log("atalho ligar: " + e.Message); } });
        }
    }

    /// <summary>Windows mute of the physical mic (the one FAH listens to, else the Windows default).</summary>
    void AtalhoMic()
    {
        string nome = motor.MicLigado ? motor.MicAtual ?? "" : "";
        Task.Run(() =>
        {
            try
            {
                var ap = Aparelhos.Achar(false, nome);
                if (ap == null) { MandarUI("aviso", new { tipo = "erro", texto = "Não achei o microfone." }); return; }
                using var d = Aparelhos.Abrir(ap.Id);
                var v = d.AudioEndpointVolume; v.Mute = !v.Mute;
                MandarUI("aviso", new { texto = v.Mute ? "Microfone no mudo" : "Microfone ligado de novo" });
            }
            catch (Exception e) { Base.Log("atalho mic: " + e.Message); MandarUI("aviso", new { tipo = "erro", texto = "Não consegui mudar o microfone." }); }
        });
    }

    // ---- Settings (cfg) and info for the screen
    void AplicarCfg(JsonElement valor)
    {
        cfg = Cfg.De(valor);
        Configuracoes.AplicarInicio(cfg.Inicio);
        RegistrarAtalhos();
        AtualizarBandeja();
        if (pronto) EnviarInfo();
    }

    object InfoCfg() => new
    {
        versao = Atualizador.VersaoTexto, inicio = Configuracoes.InicioAtivo(), atalhosFalha = atalhosFalha.ToArray(),
        podeAtualizar = Atualizador.EhExeDeVerdade(), ultimoBackup = Configuracoes.UltimoBackup(),
    };

    void EnviarInfo() => Mandar("cfginfo", InfoCfg());

    /// <summary>Message to the page from any thread.</summary>
    void MandarUI(string tipo, object dados)
    {
        try { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => Mandar(tipo, dados)); } catch { }
    }

    // ---- backup
    void Backup(string acao)
    {
        if (acao == "exportar")
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Salvar backup do FAH", Filter = "Backup do FAH (*.zip)|*.zip", DefaultExt = "zip", AddExtension = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), FileName = $"FAH-backup-{DateTime.Now:yyyy-MM-dd}.zip",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var destino = dlg.FileName;
            Task.Run(() =>
            {
                try { Configuracoes.Exportar(destino); MandarUI("aviso", new { texto = "Backup salvo." }); }
                catch (Exception e) { Base.Log("backup exportar: " + e.Message); MandarUI("aviso", new { tipo = "erro", texto = "Não consegui salvar o backup." }); }
            });
        }
        else if (acao == "importar")
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Escolher backup do FAH", Filter = "Backup do FAH (*.zip)|*.zip",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!Configuracoes.EhBackup(dlg.FileName)) { Mandar("aviso", new { tipo = "erro", texto = "Esse arquivo não é um backup do FAH." }); return; }
            try
            {
                Configuracoes.PrepararImportacao(dlg.FileName);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--esperar {Environment.ProcessId}") { UseShellExecute = false });
                Base.Log("backup importar: reiniciando");
                Sair();
            }
            catch (Exception e)
            {
                Base.Log("backup importar: " + e.Message); Configuracoes.CancelarImportacao();
                Mandar("aviso", new { tipo = "erro", texto = "Não consegui usar esse backup." });
            }
        }
    }

    // ---- updater
    void Att(string estado, string? versao = null, int pct = 0, string? erro = null)
    {
        estadoAtt = new { estado, versao, pct, erro };
        Mandar("atualizacao", estadoAtt);
    }

    async Task Procurar(bool auto)
    {
        if (procurando) { Mandar("atualizacao", estadoAtt); return; }
        if (pastaPronta != null && Directory.Exists(pastaPronta)) { Att("pronta", versaoPronta); return; }
        procurando = true;
        try
        {
            Att("procurando");
            var (info, erro) = await Task.Run(Atualizador.UltimaAsync);
            if (info == null)
            {
                Att("erro", null, 0, erro);
                if (auto && tentativasAuto < EsperasAuto.Length)   // started with Windows: network often not up yet at +3 s (clean-PC test 06/11)
                {
                    int s = EsperasAuto[tentativasAuto++];
                    Base.Log($"atualizar: tento de novo em {s} s");
                    _ = Task.Delay(s * 1000).ContinueWith(_ => { try { BeginInvoke(() => { if (cfg.AutoAtualizar) _ = Procurar(true); }); } catch { } });
                }
                return;
            }
            if (!Atualizador.EhMaisNova(info.Version)) { Att("em-dia", info.Version); return; }
            if (info.ZipUrl == null) { Att("erro", info.Version, 0, "a versão " + info.Version + " não tem o arquivo para Windows"); return; }
            int ult = -1; baixando = true; Att("baixando", info.Version);
            var (pasta, e2) = await Task.Run(() => Atualizador.BaixarAsync(info, (b, t) =>
            {
                if (t <= 0) return;
                int p = (int)Math.Clamp(b * 100 / t, 0, 100);
                if (p == ult) return;
                ult = p;
                try { BeginInvoke(() => { if (baixando) Att("baixando", info.Version, p); }); } catch { }
            }));
            baixando = false;
            if (pasta == null) { Att("erro", info.Version, 0, e2); return; }
            pastaPronta = pasta; versaoPronta = info.Version;
            Att("pronta", info.Version);
            if (auto)                                     // 05/11 user: at start, update installs on its own (FAH reopens, sound back in a few s)
            {
                string arqTag = Path.Combine(Base.Config, "att-tentada.txt");
                string? tentada = null; try { tentada = File.ReadAllText(arqTag).Trim(); } catch { }
                bool parado = !motor.Ligado && !motor.MicLigado && !motor.InstalandoCabo;   // never cut a VB-CABLE install
                if (!parado || tentada == info.Version)   // sound running, or this version was already tried on its own: user installs by the button
                {
                    Mandar("aviso", new { texto = $"A versão {info.Version} do FAH está pronta. Toque em Instalar quando quiser." });
                    return;
                }
                Mandar("aviso", new { texto = $"Instalando a versão {info.Version} do FAH. Ele reabre sozinho em alguns segundos." });
                await Task.Delay(2500);
                if (motor.Ligado || motor.MicLigado || motor.InstalandoCabo)       // sound came on (or cable install began) during the wait
                {
                    Mandar("aviso", new { texto = $"A versão {info.Version} do FAH está pronta. Toque em Instalar quando quiser." });
                    return;
                }
                try { File.WriteAllText(arqTag, info.Version); } catch { }
                Instalar();
            }
        }
        catch (Exception e) { baixando = false; Base.Log("atualizar: " + e.Message); Att("erro", null, 0, "não consegui procurar atualização"); }
        finally { procurando = false; }
    }

    void Instalar()
    {
        if (!Atualizador.EhExeDeVerdade()) { Att("erro", versaoPronta, 0, "só funciona no FAH instalado"); return; }
        if (pastaPronta == null || !Directory.Exists(pastaPronta)) { Att("erro", null, 0, "não tem atualização pronta"); return; }
        var (ok, erro) = Atualizador.Trocar(pastaPronta);
        if (!ok) { Att("erro", versaoPronta, 0, erro); return; }
        Sair();                                           // normal exit: Windows default sound goes back
    }

    async Task Iniciar()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Base.Pasta("webview"));
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception e)
        {
            Base.Log("webview: " + e);
            bool faltando = e is WebView2RuntimeNotFoundException || e.InnerException is WebView2RuntimeNotFoundException;
            if (faltando)
            {
                var r = MessageBox.Show("O FAH precisa do \"Microsoft Edge WebView2 Runtime\" para mostrar a tela, e ele não está neste PC.\n\nQuer abrir a página da Microsoft para baixar? Depois de instalar, abra o FAH de novo.",
                    "FAH", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true }); }
                    catch (Exception x) { Base.Log("webview: abrir página: " + x.Message); }
            }
            else MessageBox.Show("O FAH não conseguiu montar a tela. O motivo ficou gravado no arquivo fah.log, na pasta usuario\\relatorios.", "FAH", MessageBoxButtons.OK, MessageBoxIcon.Error);
            sair = true; Close();                     // normal exit path: Motor.Dispose gives the Windows default back
            return;
        }
        var cw = web.CoreWebView2;
        cw.Settings.AreDefaultContextMenusEnabled = false;
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.IsZoomControlEnabled = false;     // Ctrl+wheel would break the fit
        cw.Settings.AreDevToolsEnabled = Environment.GetEnvironmentVariable("FAH_DEVTOOLS") == "1";
        cw.SetVirtualHostNameToFolderMapping("fah.local", Base.Ui, CoreWebView2HostResourceAccessKind.Allow);
        // saved state goes in before any screen script runs (modules read App.estado when they register)
        await cw.AddScriptToExecuteOnDocumentCreatedAsync("window.FAH_SALVO=" + JsonSerializer.Serialize(motor.Estado) + ";window.FAH_DISP=" + JsonSerializer.Serialize(Dispositivos()) + ";");
        cw.WebMessageReceived += (_, e) => Recebeu(e.WebMessageAsJson);
        cw.NavigationCompleted += (_, _) => { pronto = true; Zoom(); Mandar("janela", new { max = WindowState == FormWindowState.Maximized, tam }); motor.Status(); motor.CaboStatus(); motor.PaginaPronta(); relogio.Start(); if (!micIniciado) { micIniciado = true; Task.Run(motor.MicInicio); } else motor.StatusMic();
            EnviarInfo();
            if (escondido) { escondido = false; Hide(); Opacity = 1; }       // started by Windows: the page is up, now go to the tray
            if (resultadoAtt != null)
            {
                Mandar("aviso", resultadoAtt == "" ? (object)new { texto = $"FAH atualizado para a versão {Atualizador.VersaoTexto}." } : new { tipo = "erro", texto = "A atualização não deu certo: " + resultadoAtt + "." });
                resultadoAtt = null;
            }
        };
        cw.Navigate("https://fah.local/index.html");
    }

    void Mandar(string tipo, object dados)
    {
        try { web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { tipo, dados })); }
        catch (Exception e) { Base.Log("ponte: " + e.Message); }
    }

    void Recebeu(string json)
    {
        try
        {
            var r = JsonDocument.Parse(json).RootElement;
            switch (r.GetProperty("tipo").GetString())
            {
                case "estado": { string chave = r.GetProperty("chave").GetString()!; var valor = r.GetProperty("valor"); motor.Receber(chave, valor); if (chave == "cfg") AplicarCfg(valor); break; }
                case "backup": Backup(Txt(r, "acao")); break;
                case "atualizar": { string ac = Txt(r, "acao"); if (ac == "procurar") _ = Procurar(false); else if (ac == "instalar") Instalar(); break; }
                case "ligar": Task.Run(motor.Ligar); break;
                case "cabo71": { bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.Cabo71(on)); break; }
                case "cabo71?": Task.Run(() => motor.Cabo71Status()); break;
                case "desligar": Task.Run(motor.Desligar); break;
                case "micligar": Task.Run(motor.LigarMic); break;
                case "micdesligar": Task.Run(motor.DesligarMic); break;
                case "micouvirsai": { bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.OuvirSaida(on)); break; }
                case "micouvir": { bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.Ouvir(on)); break; }
                case "micteste": motor.GravarTeste(); break;
                case "mic?": motor.StatusMic(); break;
                case "clonar": { string x = Txt(r, "x"), ap = Txt(r, "ap"); bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.Clonar(x, ap, on)); break; }
                case "clones?": motor.StatusClones(); motor.StatusCopiaVol(); motor.StatusBypass(); break;
                case "bypass": { string lado = Txt(r, "lado"), id = Txt(r, "id"); bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.Bypass(lado, id, on)); break; }
                case "copiavol": { string x = Txt(r, "x"), ap = Txt(r, "ap"); int? vol = r.TryGetProperty("vol", out var cv) && cv.ValueKind == System.Text.Json.JsonValueKind.Number ? cv.GetInt32() : null;
                    bool? B(string k) => r.TryGetProperty(k, out var e) ? e.ValueKind switch { System.Text.Json.JsonValueKind.True => true, System.Text.Json.JsonValueKind.False => false, System.Text.Json.JsonValueKind.Number => e.GetDouble() != 0, _ => null } : null;
                    bool? cm = B("m"), cs = B("segue"); Task.Run(() => motor.VolumeCopia(ap, x, vol, cm, cs)); break; }
                case "micinjetar": { string x = Txt(r, "x"); bool on = r.GetProperty("on").GetBoolean(); Task.Run(() => motor.Injetar(x, on)); break; }
                case "comparar": motor.Som.Desviar = r.GetProperty("on").GetBoolean(); break;
                case "arrastar": ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); break;    // WM_NCLBUTTONDOWN, HTCAPTION
                case "minimizar": WindowState = FormWindowState.Minimized; break;
                case "maximizar": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                case "tamanho": TrocarTamanho(r.TryGetProperty("tam", out var q) ? q.GetString() : null); break;
                case "esconder": if (cfg.XBandeja) Hide(); else Sair(); break;
                case "sair": Sair(); break;
                case "mixer": mesa.Mostrar(r.GetProperty("on").GetBoolean()); break;
                case "mixvol": mesa.Volume(Txt(r, "id"), r.GetProperty("vol").GetInt32()); break;
                case "mixmudo": mesa.Mudo(Txt(r, "id"), r.GetProperty("m").GetBoolean()); break;
                case "mixapp": mesa.VolumePrograma(Txt(r, "lado"), Txt(r, "x"), r.TryGetProperty("vol", out var av) ? av.GetInt32() : null, r.TryGetProperty("m", out var am) ? am.GetBoolean() : null, r.TryGetProperty("s", out var asv) ? asv.GetBoolean() : null); break;
                case "mixfixar": mesa.Fixar(Txt(r, "lado"), Txt(r, "x"), Txt(r, "n"), r.TryGetProperty("id", out var fi) && fi.ValueKind == JsonValueKind.String ? fi.GetString() : null); break;
                case "mixpadrao": { bool mic = Txt(r, "lado") == "mic"; string id = Txt(r, "id"); Task.Run(() => { motor.TrocarPadrao(mic, id); mesa.Fazer(() => { }); }); break; }
                case "cabo?": Task.Run(motor.RefazerCabo); break;
                case "cabo-instalar": Task.Run(motor.InstalarCabo); break;
                case "cabo-mic-instalar": Task.Run(motor.InstalarCaboMic); break;
                case "abrir": Abrir(Txt(r, "url")); break;
                case "log": Base.Log("ui: " + r.GetProperty("texto").GetString()); break;
            }
        }
        catch (Exception e) { Base.Log("mensagem da tela: " + e.Message); }
    }

    // cable veil: vendor sites and Windows sound settings only (never any URL from the page)
    static readonly string[] Sites = ["https://vb-audio.com/", "https://vac.muzychenko.net/", "ms-settings:sound", "https://github.com/finjackin1/FAH/releases"];

    static void Abrir(string url)
    {
        if (!Sites.Any(url.StartsWith)) { Base.Log("abrir recusado: " + url); return; }
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) { Base.Log("abrir " + url + ": " + e.Message); }
    }

    static string Txt(JsonElement r, string k) => r.GetProperty(k).GetString() ?? "";

    /// <summary>This PC's real devices for the screens (cables and hidden sides left out).</summary>
    static object Dispositivos()
    {
        object Lista(bool saida) => Aparelhos.Listar(saida).Where(a => !a.Virtual).Select((a, i) => new
        {
            id = (saida ? "s" : "m") + i, n = a.Nome, t = saida ? "Saída" : "Microfone", f = 1, p = a.Padrao ? 1 : 0,
            ic = saida ? "fone" : "mic", vol = Volume(a.Id),
        }).ToList();
        try { return new { som = Lista(true), mic = Lista(false) }; }
        catch (Exception e) { Base.Log("aparelhos: " + e.Message); return new { som = Array.Empty<object>(), mic = Array.Empty<object>() }; }
    }

    static int Volume(string id)
    {
        try { using var d = Aparelhos.Abrir(id); return (int)Math.Round(d.AudioEndpointVolume.MasterVolumeLevelScalar * 100); }
        catch { return 100; }
    }

    Size Px(Size css) { float e = DeviceDpi / 96f; return new Size((int)Math.Round(css.Width * e), (int)Math.Round(css.Height * e)); }

    /// <summary>Zoom so the 1280x720 layout fills the window (wider/taller window = more room, never less).</summary>
    void Zoom()
    {
        if (WindowState == FormWindowState.Minimized) return;
        float e = DeviceDpi / 96f, l = (ClientSize.Width - Padding.Horizontal) / e, a = (ClientSize.Height - Padding.Vertical) / e;
        if (l < 50 || a < 50) return;
        var z = Math.Clamp(Math.Min(l / BaseL, a / BaseA), .5, 3);
        if (Math.Abs(web.ZoomFactor - z) > .001) web.ZoomFactor = z;
    }

    /// <summary>Hold maximize (or Settings > Janela): Máx. 1440x810 / Mín. 1080x608, centered; small screen falls back to Mín.</summary>
    void TrocarTamanho(string? novo)
    {
        tam = novo != null && Tamanhos.ContainsKey(novo) ? novo : tam == "max" ? "min" : "max";
        var area = Screen.FromHandle(Handle).WorkingArea; var s = Px(Tamanhos[tam]);
        if (s.Width + 2 * Borda > area.Width || s.Height + 2 * Borda > area.Height) { tam = "min"; s = Px(Tamanhos[tam]); }
        if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
        ClientSize = new Size(s.Width + 2 * Borda, s.Height + 2 * Borda);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        motor.Receber("tam", JsonSerializer.SerializeToElement(tam));
        Mandar("janela", new { max = false, tam });
    }

    void Lembrar()
    {
        if (dpiMudando || WindowState != FormWindowState.Normal) return;
        float e = DeviceDpi / 96f; css = new SizeF((ClientSize.Width - 2 * Borda) / e, (ClientSize.Height - 2 * Borda) / e);
    }

    void AjustarDpi()
    {
        if (WindowState == FormWindowState.Normal && css.Width > 50)
        {
            var area = Screen.FromHandle(Handle).WorkingArea; float e = DeviceDpi / 96f;
            int w = Math.Min((int)Math.Round(css.Width * e) + 2 * Borda, area.Width), h = Math.Min((int)Math.Round(css.Height * e) + 2 * Borda, area.Height);
            var meio = new Point(Left + Width / 2, Top + Height / 2);
            Bounds = new Rectangle(Math.Clamp(meio.X - w / 2, area.Left, area.Right - w), Math.Clamp(meio.Y - h / 2, area.Top, area.Bottom - h), w, h);
        }
        dpiMudando = false; Lembrar();
        web.PerformLayout(); Zoom(); web.Invalidate();
        if (pronto) Mandar("janela", new { max = WindowState == FormWindowState.Maximized, tam });
    }

    void Mostrar() { if (escondido) { escondido = false; Opacity = 1; } Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }

    void Sair() { sair = true; barra?.Parar(); Close(); }

    // borderless: resize from the edge strip, snap/minimize animations kept by WS_THICKFRAME-less hit test
    protected override void WndProc(ref Message m)
    {
        // logoff/shutdown/restart (02/11): Windows may kill FAH before the normal exit runs, so the defaults go back now
        // WM_ENDSESSION with wParam != 0 = the session really ends (QUERYENDSESSION could still be cancelled and left FAH half-closed)
        if (m.Msg == 0x16 && m.WParam != IntPtr.Zero) { try { motor.DevolverPadroes("Windows desligando"); } catch { } try { mesa.SoltarPisos(); } catch { } }
        if (m.Msg == 0x312) { Atalho((int)m.WParam); return; }                     // WM_HOTKEY
        base.WndProc(ref m);
        if (m.Msg == 0x24) { MaxInfo(m.HWnd, m.LParam); return; }                    // WM_GETMINMAXINFO
        if (m.Msg != 0x84 || WindowState == FormWindowState.Maximized) return;    // WM_NCHITTEST
        var p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF)));
        bool l = p.X < Borda, r = p.X >= ClientSize.Width - Borda, t = p.Y < Borda, b = p.Y >= ClientSize.Height - Borda;
        int h = t && l ? 13 : t && r ? 14 : b && l ? 16 : b && r ? 17 : l ? 10 : r ? 11 : t ? 12 : b ? 15 : 0;
        if (h != 0) m.Result = h;
    }

    // maximize = work area of the monitor the window is on now (MaximizedBounds was absolute coords of the monitor at click time,
    // wrong on a secondary monitor); min track size follows the current scale
    void MaxInfo(IntPtr h, IntPtr p)
    {
        var tela = Screen.FromHandle(h); Rectangle b = tela.Bounds, a = tela.WorkingArea; var min = Px(new Size(800, 450));
        Marshal.WriteInt32(p, 8, a.Width); Marshal.WriteInt32(p, 12, a.Height);                    // ptMaxSize
        Marshal.WriteInt32(p, 16, a.Left - b.Left); Marshal.WriteInt32(p, 20, a.Top - b.Top);      // ptMaxPosition (monitor-relative)
        Marshal.WriteInt32(p, 24, min.Width); Marshal.WriteInt32(p, 28, min.Height);               // ptMinTrackSize
    }

    protected override CreateParams CreateParams
    {
        get { var c = base.CreateParams; c.Style |= 0x20000; return c; }             // WS_MINIMIZEBOX: taskbar click minimizes
    }

    /// <summary>Program icon for the Mixer: 48 px from the exe (generic exe icon when it has none), as PNG data URI.</summary>
    static string? IconePng(string exe)
    {
        // a Start menu / desktop shortcut pointing to this exe may carry its own icon (what the user sees): that one first
        Icon? ic = null;
        if (Atalhos().TryGetValue(exe.ToLowerInvariant(), out var at)) try { ic = Icon.ExtractIcon(at.Arq, at.Ind, 48); } catch { }
        ic ??= Icon.ExtractIcon(exe, 0, 48) ?? Icon.ExtractAssociatedIcon(exe);
        using var _ = ic;
        if (ic == null) return null;
        using var bmp = ic.ToBitmap(); using var ms = new MemoryStream(); bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>Shortcuts (Start menu + desktop, user and all users): target exe -> icon file/index. Read once per run.</summary>
    static Dictionary<string, (string Arq, int Ind)>? atalhos;
    static Dictionary<string, (string Arq, int Ind)> Atalhos()
    {
        if (atalhos != null) return atalhos;
        var r = new Dictionary<string, (string, int)>();
        try
        {
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            var pastas = new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory }
                .Select(Environment.GetFolderPath).Where(Directory.Exists);
            foreach (var pa in pastas)
                foreach (var f in Directory.EnumerateFiles(pa, "*.lnk", SearchOption.AllDirectories))
                    try
                    {
                        dynamic l = sh.CreateShortcut(f); string alvo = l.TargetPath, loc = l.IconLocation;
                        if (string.IsNullOrEmpty(alvo) || !alvo.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        int v = loc.LastIndexOf(','); string arq = Environment.ExpandEnvironmentVariables(v > 0 ? loc[..v] : loc).Trim();
                        int ind = v > 0 && int.TryParse(loc[(v + 1)..], out var i) ? i : 0;
                        if (arq == "" || !File.Exists(arq)) continue;                      // no own icon: the exe's is the same
                        r.TryAdd(alvo.ToLowerInvariant(), (arq, ind));
                    }
                    catch { }
        }
        catch { }
        return atalhos = r;
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!sair && e.CloseReason == CloseReason.UserClosing)       // Alt+F4 = same as the X
        {
            if (cfg.XBandeja) { e.Cancel = true; Hide(); return; }
            sair = true; barra?.Parar();
        }
        bandeja.Visible = false;
        relogio.Stop();
        try { mesa.SoltarPisos(); } catch { }                    // originals held at the floor get their real volume back
        base.OnFormClosing(e);
    }
}
