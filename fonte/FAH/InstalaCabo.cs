// No virtual cable at all (05/11): FAH downloads VB-CABLE and installs it silently (one admin prompt), once per run on its own,
// again only when the UI asks ({tipo:'cabo-instalar'}). Never in simulation (tests). Never two at once.
// Mic cable (06/11): with a Som cable and no Virtual Audio Cable at all, FAH downloads VAC Lite (free, home use) and opens its setup.
// That setup has no silent mode (RE 06/11; patching it breaks license and signature), so the user accepts license + folder.
// On its own only right after FAH installed VB-CABLE, once ever (flag file); else the mic notice button ({tipo:'cabo-mic-instalar'}).
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    const string UrlVbCable = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip";   // pack 4.5, checked 05/11/2026
    const string UrlVacLite = "https://software.muzychenko.net/freeware/vac471lite.zip";                  // 4.71 Lite, checked 06/11/2026
    const string FalhouVb = "O instalador do VB-CABLE falhou.", FalhouLite = "O instalador do Virtual Audio Cable Lite falhou.";

    int instalaOcupado;
    /// <summary>Cable download/setup running (auto-update must wait).</summary>
    public bool InstalandoCabo => Volatile.Read(ref instalaOcupado) != 0;
    volatile bool instalaTentou, instalaReiniciar;
    volatile string? instalaErro, instalaFase, instalaMicErro;
    int? instalaPct;

    static string FlagLite => Path.Combine(Base.Dados, "vac-lite-auto");

    bool TomarInstala() => Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") == null && Interlocked.CompareExchange(ref instalaOcupado, 1, 0) == 0;
    void SoltarInstala() { instalaFase = null; instalaPct = null; Interlocked.Exchange(ref instalaOcupado, 0); }

    /// <summary>UI "cabo-instalar": try again (clears the last failure).</summary>
    public void InstalarCabo()
    {
        if (!TomarInstala()) return;
        instalaTentou = true;
        ExecutarInstala();
    }

    /// <summary>UI "cabo-mic-instalar": mic notice button.</summary>
    public void InstalarCaboMic()
    {
        if (!PodeInstalarMic) { StatusMic(); return; }
        if (!TomarInstala())
        {
            if (Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) instalaMicErro = "Simulação: o FAH não instala cabos.";
            StatusMic(); return;
        }
        try { ExecutarInstalaMic(); }
        catch (Exception e) { Log("instala cabo do mic: " + e); instalaMicErro ??= FalhouLite; }
        finally { SoltarInstala(); }
        EnviarCabo(); StatusMic();
    }

    // caller already took the slot (instalaOcupado == 1)
    void ExecutarInstala()
    {
        string? erro = null; int saida = -1;
        try
        {
            instalaErro = null; instalaReiniciar = false;
            EnviarCabo("baixando");   // pct null = "Baixando…" until the size is known
            (erro, saida) = BaixarEInstalar(UrlVbCable, "vbcable", "baixando", FalhouVb,
                dir => Cabos.ExeInstalador(RuntimeInformation.ProcessArchitecture, n => File.Exists(Path.Combine(dir, n))),
                Cabos.AssinanteVbAudio, "O instalador baixado não tem a assinatura da VB-Audio. Não instalei.", "-i -h", "Sem internet para baixar o VB-CABLE.");
            if (erro == null)
            {
                EnviarCabo("conferindo");
                Log("instala cabo: conferindo se o Windows já mostra o cabo");
                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(1000);
                    if (Cabos.Detectar().Estado != "falta") break;
                }
            }
        }
        catch (Exception e) { Log("instala cabo: " + e); erro = FalhouVb; }
        if (erro != null) { SoltarInstala(); instalaErro = erro; Log("instala cabo: " + erro); EnviarCabo(); return; }
        try
        {
            RefazerCabo();                                     // found: normal flow (Som comes on with the first cable). Slot kept: screen stays on "conferindo"
            if (Cabo?.Cabo != null && PodeInstalarMic && !File.Exists(FlagLite))
            {
                try { File.WriteAllText(FlagLite, DateTime.Now.ToString("s")); } catch (Exception e) { Log("instala cabo do mic: marca: " + e.Message); }
                ExecutarInstalaMic();
            }
        }
        catch (Exception e) { Log("instala cabo: " + e); }
        finally { SoltarInstala(); }
        if (Cabo?.Estado == "falta")
        {
            instalaReiniciar = saida == 0;                     // setup ok but Windows shows nothing yet: PC restart needed
            if (instalaReiniciar) Log("instala cabo: instalado, falta reiniciar o PC");
            else { instalaErro = FalhouVb; Log($"instala cabo: nada apareceu (código {saida})"); }
        }
        EnviarCabo(); StatusMic();
    }

    // caller holds the slot. VAC Lite for the mic: download, check signer, run its setup (user clicks through it), wait for the line.
    void ExecutarInstalaMic()
    {
        string? erro = null; int saida = -1;
        try
        {
            instalaMicErro = null;
            EnviarCabo("mic-baixando"); StatusMic();
            (erro, saida) = BaixarEInstalar(UrlVacLite, "vaclite", "mic-baixando", FalhouLite, _ => Cabos.ExeLite(RuntimeInformation.ProcessArchitecture),
                Cabos.AssinanteMuzychenko, "O instalador baixado não tem a assinatura do Virtual Audio Cable. Não instalei.", "",
                "Sem internet para baixar o cabo do microfone.", "mic");
            if (erro == null)
            {
                EnviarCabo("mic-conferindo");
                Log("instala cabo do mic: conferindo se o Windows já mostra a linha");
                bool apareceu = false;
                for (int i = 0; i < 20 && !apareceu; i++) { Thread.Sleep(1000); apareceu = Cabos.DetectarMic(Cabo?.Cabo).Cabo != null; }
                if (!apareceu) erro = saida == 0 ? "O Virtual Audio Cable Lite foi instalado, mas o Windows só mostra o cabo depois de reiniciar o PC."
                    : "O Virtual Audio Cable Lite não foi instalado. Se você fechou o instalador, clique em Instalar cabo do microfone de novo.";
            }
        }
        catch (Exception e) { Log("instala cabo do mic: " + e); erro = FalhouLite; }
        if (erro != null) { instalaMicErro = erro; Log($"instala cabo do mic: {erro} (código {saida})"); return; }
        RefazerCabo();                                         // Som stays on its cable (Lite goes last for Som); the mic comes on the Lite line
    }

    /// <summary>Download zip, extract, check the signer, run the setup elevated and wait for it. <paramref name="faseSetup"/> = phase shown
    /// while the setup runs (VB: "instalando"; Lite: "mic", the user must click through it).</summary>
    (string? erro, int saida) BaixarEInstalar(string url, string nome, string faseBaixa, string falhou, Func<string, string> exeDe,
        Func<string?, bool> assinante, string semAssinatura, string args, string semNet, string faseSetup = "instalando")
    {
        string baixados = Base.Pasta("baixados");
        try   // leftovers of earlier runs (best effort)
        {
            foreach (var velho in Directory.GetFileSystemEntries(baixados, nome + "-*"))
                try { if (Directory.Exists(velho)) Directory.Delete(velho, true); else File.Delete(velho); } catch { }
        }
        catch { }
        string dir = Path.Combine(baixados, nome + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        string zip = dir + ".zip";
        bool setupRodando = false, escrevendo = false;   // escrevendo: IOException from the disk is not 'no internet'   // never delete the exe while the setup may still be running
        try
        {
            try
            {
                Log("instala cabo: baixando " + url);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));   // idle cap: renewed on every chunk (slow links still finish)
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
                using var r = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult();
                r.EnsureSuccessStatusCode();
                long total = r.Content.Headers.ContentLength ?? -1, lido = 0; int ult = -1;
                using var ent = r.Content.ReadAsStream(); escrevendo = true; using var sai = File.Create(zip); escrevendo = false;
                var buf = new byte[81920]; int n;
                while ((n = ent.ReadAsync(buf, 0, buf.Length, cts.Token).GetAwaiter().GetResult()) > 0)
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(1)); escrevendo = true; sai.Write(buf, 0, n); escrevendo = false; lido += n;
                    if (total > 0) { int p = (int)(lido * 100 / total); if (p != ult) { ult = p; EnviarCabo(faseBaixa, p); } }
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or System.Net.Sockets.SocketException || (e is IOException && !escrevendo))
            {
                Log("instala cabo: sem internet: " + e.Message);
                return (semNet, -1);
            }
            catch (Exception e) { Log("instala cabo: " + e); return (falhou, -1); }
            try
            {
                Directory.CreateDirectory(dir);
                ZipFile.ExtractToDirectory(zip, dir);
                string exe = exeDe(dir);
                string caminho = Path.Combine(dir, exe);
                if (!File.Exists(caminho)) { Log("instala cabo: instalador não está no pacote: " + exe); return (falhou, -1); }
                if (!Assinado(caminho, assinante)) return (semAssinatura, -1);
                EnviarCabo(faseSetup);
                Log("instala cabo: instalando " + exe);
                setupRodando = true;
                Process? pr0;
                try { pr0 = Process.Start(new ProcessStartInfo(caminho, args) { Verb = "runas", UseShellExecute = true, WorkingDirectory = dir }); }
                catch { setupRodando = false; throw; }                 // never started: its folder can go
                using var pr = pr0;
                if (pr == null) { setupRodando = false; return (falhou, -1); }
                if (!pr.WaitForExit(180000)) { Log("instala cabo: instalador passou de 3 min, esperando ele terminar"); pr.WaitForExit(); }   // keep the slot: never 2 setups at once
                setupRodando = false;
                Log($"instala cabo: instalador terminou, código {pr.ExitCode}");
                return (null, pr.ExitCode);
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1223) { setupRodando = false; Log("instala cabo: administrador recusado"); return ("O Windows não deu permissão para instalar.", -1); }
            catch (Exception e) { Log("instala cabo: " + e); return (falhou, -1); }
        }
        finally
        {
            try { File.Delete(zip); } catch { }
            if (!setupRodando) try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>Authenticode valid (no UI, no revocation lookup) and signed by the expected publisher.</summary>
    bool Assinado(string arquivo, Func<string?, bool> assinante)
    {
        try
        {
            bool valida = Assinatura.Valida(arquivo);
            string? assunto = null;
#pragma warning disable SYSLIB0057   // X509CertificateLoader has no Authenticode reader; this is the only managed way to get the signer
            try { assunto = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(arquivo).Subject; } catch { }
#pragma warning restore SYSLIB0057
            Log($"instala cabo: assinatura {(valida ? "válida" : "inválida")}, assinante {assunto ?? "nenhum"}");
            return valida && assinante(assunto);
        }
        catch (Exception e) { Log("instala cabo: assinatura: " + e.Message); return false; }
    }
}

static class Assinatura
{
    [StructLayout(LayoutKind.Sequential)] struct ArquivoInfo { public uint Tam; public IntPtr Caminho, Arq, Sujeito; }
    [StructLayout(LayoutKind.Sequential)]
    struct DadosConfianca
    {
        public uint Tam; public IntPtr Politica, Sip; public uint UI, Revogacao, Uniao; public IntPtr Arquivo; public uint Acao; public IntPtr Estado, Url; public uint Flags, Contexto; public IntPtr Config;
    }
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] static extern int WinVerifyTrust(IntPtr janela, ref Guid acao, ref DadosConfianca dados);

    /// <summary>WinVerifyTrust WINTRUST_ACTION_GENERIC_VERIFY_V2, no UI, no revocation check.</summary>
    public static bool Valida(string arquivo)
    {
        var acao = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        IntPtr cam = Marshal.StringToHGlobalUni(arquivo), pArq = IntPtr.Zero;
        try
        {
            var fi = new ArquivoInfo { Tam = (uint)Marshal.SizeOf<ArquivoInfo>(), Caminho = cam };
            pArq = Marshal.AllocHGlobal(Marshal.SizeOf<ArquivoInfo>());
            Marshal.StructureToPtr(fi, pArq, false);
            var d = new DadosConfianca { Tam = (uint)Marshal.SizeOf<DadosConfianca>(), UI = 2, Revogacao = 0, Uniao = 1, Arquivo = pArq, Acao = 0, Flags = 0x10 };
            return WinVerifyTrust(new IntPtr(-1), ref acao, ref d) == 0;
        }
        finally { Marshal.FreeHGlobal(cam); if (pArq != IntPtr.Zero) Marshal.FreeHGlobal(pArq); }
    }
}
