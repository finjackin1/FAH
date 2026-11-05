// GitHub updater (port of DSE's UpdateChecker + Atualizador): asks the latest release, downloads the zip silently,
// checks size + SHA-256, extracts to a work folder. Swapping files needs admin: an elevated hidden .ps1 waits for this
// process to exit and robocopies over the install (user data folders excluded); a second non-elevated .ps1 reopens FAH.
// FAH_TESTE_API = file:///...json instead of GitHub; FAH_TESTE_SEM_ADMIN=1 = no UAC (bench only).
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FAH;

/// <summary>Latest published version and its Windows zip.</summary>
sealed class UpdateInfo
{
    public required string Version { get; init; }      // "1.2.0" (no "v")
    public required string Url { get; init; }          // release page
    public string? ZipUrl { get; init; }               // null = release without the FAH zip
    public string? ZipName { get; init; }
    public long ZipSize { get; init; }
    public string? Sha256 { get; init; }               // the "digest" the API publishes
}

static class Atualizador
{
    const string ApiUrl = "https://api.github.com/repos/finjackin1/FAH/releases/latest";
    const string PaginaReleases = "https://github.com/finjackin1/FAH/releases/latest";
    static readonly Regex PadraoZip = new(@"^FAH(-[\d.]+)?-win-x64\.zip$", RegexOptions.IgnoreCase);

    public static string Trabalho => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FAH", "atualizacao");
    static string ArqResultado => Path.Combine(Trabalho, "resultado.txt");

    static readonly Dictionary<string, string> Motivos = new()
    {
        ["nao-fechou"] = "o FAH demorou a fechar",
        ["copiar"] = "não consegui copiar todos os arquivos novos, então a versão anterior foi mantida",
        ["guardar"] = "não consegui guardar a versão atual antes de trocar, então nada foi mudado",
        ["restaurar"] = "a cópia falhou no meio e não consegui voltar a versão anterior. Baixe o FAH de novo no GitHub",
        ["bloqueado"] = "o Windows ou o antivírus não deixou a troca rodar",
    };

    public static Version VersaoAtual
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    public static string VersaoTexto => VersaoAtual.ToString(3);

    /// <summary>"1.2" == "1.2.0"; invalid text = 0.0.0.</summary>
    public static Version Numeros(string v)
    {
        var n = Regex.Matches(v ?? "", @"\d+").Take(3).Select(m => int.TryParse(m.Value, out var x) ? x : 0).ToList();
        while (n.Count < 3) n.Add(0);
        return new Version(n[0], n[1], n[2]);
    }

    public static bool EhMaisNova(string versao) => Numeros(versao) > VersaoAtual;

    /// <summary>Running from the build output (bin\Debug or bin\Release): never swaps anything. A user folder named "bin" is fine.</summary>
    public static bool EhExeDeVerdade()
    {
        var exe = Environment.ProcessPath ?? "";
        return exe.EndsWith("FAH.exe", StringComparison.OrdinalIgnoreCase)
            && !exe.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) && !exe.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);
    }

    static HttpClient NovoHttp(int segundos)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(segundos) };
        http.DefaultRequestHeaders.Add("User-Agent", "FAH-Atualizador");   // the API refuses without
        return http;
    }

    static bool EhArquivo(string url) => url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    /// <summary>(info, erro). info null = could not ask.</summary>
    public static async Task<(UpdateInfo? Info, string Erro)> UltimaAsync()
    {
        try
        {
            var url = Environment.GetEnvironmentVariable("FAH_TESTE_API") ?? ApiUrl;
            string json;
            if (EhArquivo(url)) json = await File.ReadAllTextAsync(new Uri(url).LocalPath);
            else
            {
                using var http = NovoHttp(20);
                http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                json = await http.GetStringAsync(url);
            }
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            var tag = raiz.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            if (tag.Length == 0) return (null, "a resposta do GitHub veio sem versão");
            var pagina = raiz.TryGetProperty("html_url", out var h) ? h.GetString() ?? PaginaReleases : PaginaReleases;
            string? zipUrl = null, zipNome = null, sha = null;
            long tam = 0;
            if (raiz.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var nome = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!PadraoZip.IsMatch(nome)) continue;
                    zipNome = nome;
                    zipUrl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    tam = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : 0;
                    var dg = a.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                    sha = dg.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? dg[7..] : null;
                    break;
                }
            var info = new UpdateInfo { Version = tag.TrimStart('v', 'V'), Url = pagina, ZipUrl = zipUrl, ZipName = zipNome, ZipSize = tam, Sha256 = sha };
            Base.Log($"atualizar: publicada {info.Version} (atual {VersaoAtual}), zip: {zipNome ?? "nenhum"}");
            return (info, "");
        }
        catch (Exception ex)
        {
            Base.Log("atualizar: procura falhou: " + ex.Message);
            return (null, "o GitHub não respondeu");
        }
    }

    /// <summary>Downloads, checks and extracts. Returns (folder holding the new FAH.exe, erro). progresso(baixado, total) runs off the UI thread.</summary>
    public static async Task<(string? Pasta, string Erro)> BaixarAsync(UpdateInfo info, Action<long, long>? progresso = null)
    {
        if (info.ZipUrl == null || info.ZipName == null) return (null, "a versão nova não tem o arquivo para Windows");
        var trabalho = Trabalho;
        var destino = Path.Combine(trabalho, info.ZipName);
        var parcial = destino + ".parcial";
        try
        {
            Directory.CreateDirectory(trabalho);
            long baixado = 0;
            using (var sha = SHA256.Create())
            {
                await using (var saida = File.Create(parcial))
                {
                    Stream entrada;
                    HttpClient? http = null;
                    HttpResponseMessage? resp = null;
                    if (EhArquivo(info.ZipUrl)) entrada = File.OpenRead(new Uri(info.ZipUrl).LocalPath);
                    else
                    {
                        http = NovoHttp(60);
                        resp = await http.GetAsync(info.ZipUrl, HttpCompletionOption.ResponseHeadersRead);
                        resp.EnsureSuccessStatusCode();
                        entrada = await resp.Content.ReadAsStreamAsync();
                    }
                    try
                    {
                        var buf = new byte[256 * 1024];
                        int lidos;
                        var ultimo = DateTime.MinValue;
                        while ((lidos = await entrada.ReadAsync(buf)) > 0)
                        {
                            await saida.WriteAsync(buf.AsMemory(0, lidos));
                            sha.TransformBlock(buf, 0, lidos, null, 0);
                            baixado += lidos;
                            if (progresso != null && (DateTime.Now - ultimo).TotalMilliseconds > 200) { ultimo = DateTime.Now; progresso(baixado, info.ZipSize); }
                        }
                        sha.TransformFinalBlock([], 0, 0);
                    }
                    finally { entrada.Dispose(); resp?.Dispose(); http?.Dispose(); }
                }
                if (info.ZipSize > 0 && baixado != info.ZipSize) { Apagar(parcial); return (null, "o arquivo chegou incompleto"); }
                var soma = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
                if (string.IsNullOrEmpty(info.Sha256) && !EhArquivo(info.ZipUrl)) { Apagar(parcial); return (null, "o GitHub não informou a assinatura do arquivo"); }
                if (!string.IsNullOrEmpty(info.Sha256) && soma != info.Sha256.ToLowerInvariant()) { Apagar(parcial); return (null, "o arquivo chegou diferente do publicado"); }
            }
            Apagar(destino);
            File.Move(parcial, destino);
            // extract now (no admin): the elevated part only copies
            var novo = Path.Combine(trabalho, "novo");
            if (Directory.Exists(novo)) Directory.Delete(novo, true);
            ZipFile.ExtractToDirectory(destino, novo);
            Apagar(destino);
            var pasta = File.Exists(Path.Combine(novo, "FAH.exe")) ? novo
                : Directory.GetDirectories(novo).FirstOrDefault(d => File.Exists(Path.Combine(d, "FAH.exe")));
            if (pasta == null) return (null, "o zip não tinha o FAH dentro");
            Base.Log($"atualizar: {info.ZipName} baixado e conferido ({baixado} bytes)");
            return (pasta, "");
        }
        catch (Exception ex)
        {
            Apagar(parcial);
            Base.Log("atualizar: download falhou: " + ex.Message);
            return (null, "o download falhou");
        }
    }

    static void Apagar(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }

    static string Q(string s) => "'" + s.Replace("'", "''").Replace("‘", "‘‘").Replace("’", "’’") + "'";   // PowerShell also quotes with ‘ ’

    /// <summary>Arms the swap (UAC prompt). true = FAH must close now; false = cancelled/failed (short text).</summary>
    public static (bool Ok, string Erro) Trocar(string pastaNova)
    {
        try
        {
            var exe = Environment.ProcessPath!;
            var prog = Path.GetDirectoryName(exe)!;
            var log = Path.Combine(Trabalho, "atualizar.txt");
            int pid = Environment.ProcessId;
            File.Delete(ArqResultado); File.Delete(ArqResultado + ".inicio");

            var trocar = Path.Combine(Trabalho, "trocar.ps1");
            File.WriteAllText(trocar, string.Join("\r\n",
                "# Generated by FAH: swaps the program for the new version. Safe to delete.",
                $"$res = {Q(ArqResultado)}; $log = {Q(log)}",
                $"$novo = {Q(pastaNova)}; $prog = {Q(prog)}; $exe = {Q(exe)}",
                "Add-Content $log \"=== $(Get-Date -Format s) atualizar FAH ===\"",
                "Set-Content \"$res.inicio\" '1' -Encoding ascii",          // reabrir.ps1: the elevated script did start
                "$n = 0",
                $"while (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{",
                "    $n++; if ($n -ge 60) { Set-Content $res 'erro nao-fechou' -Encoding ascii; exit }",
                "    Start-Sleep -Milliseconds 500",
                "}",
                // the restore guard (same exe, --guardiao) outlives us for a moment: wait until the exe is free
                "$n = 0",
                "while (@(Get-Process -Name FAH -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count -gt 0 -and $n -lt 40) { $n++; Start-Sleep -Milliseconds 500 }",
                // top-level user folders only (full paths): ui\dados and modulos\*\dados ship with the release and must update
                "$pastas = 'usuario','config','dados','relatorios','webview','backup','atualizacao'",
                "$xd = $pastas | ForEach-Object { Join-Path $novo $_ }",
                "$xdp = $pastas | ForEach-Object { Join-Path $prog $_ }",
                // copy of the current program first: a copy that fails halfway (antivirus lock) is undone instead of leaving old + new mixed
                $"$ant = {Q(Path.Combine(Trabalho, "anterior"))}",
                "Remove-Item -Recurse -Force $ant -ErrorAction SilentlyContinue",
                "robocopy $prog $ant /E /R:3 /W:1 /NP /NFL /NDL /XD @xdp | Out-File -Append -Encoding utf8 $log",
                "if ($LASTEXITCODE -ge 8) { Set-Content $res 'erro guardar' -Encoding ascii; exit }",
                "robocopy $novo $prog /E /R:3 /W:1 /NP /NFL /NDL /XD @xd | Out-File -Append -Encoding utf8 $log",
                "if ($LASTEXITCODE -ge 8) {",
                "    Add-Content $log 'copia falhou: voltando a versao anterior'",
                "    robocopy $ant $prog /E /R:5 /W:2 /NP /NFL /NDL | Out-File -Append -Encoding utf8 $log",
                "    if ($LASTEXITCODE -ge 8) { Set-Content $res 'erro restaurar' -Encoding ascii } else { Set-Content $res 'erro copiar' -Encoding ascii }",
                "    exit",
                "}",
                "Remove-Item -Recurse -Force $ant -ErrorAction SilentlyContinue",
                $"Remove-Item -Recurse -Force {Q(Path.Combine(Trabalho, "novo"))} -ErrorAction SilentlyContinue",
                "Set-Content $res 'ok' -Encoding ascii",
                ""), new UTF8Encoding(true));

            var reabrir = Path.Combine(Trabalho, "reabrir.ps1");
            File.WriteAllText(reabrir, string.Join("\r\n",
                "# Generated by FAH: reopens the program after the update. Safe to delete.",
                $"$res = {Q(ArqResultado)}",
                // 3 min without the elevated script starting = blocked (policy/antivirus): say so; once started, wait up to 15 min
                "$n = 0",
                "while (-not (Test-Path $res)) {",
                "    $n++; Start-Sleep -Milliseconds 500",
                "    if ($n -ge 360 -and -not (Test-Path \"$res.inicio\")) { Set-Content $res 'erro bloqueado' -Encoding ascii; break }",
                "    if ($n -ge 1800) { break }",
                "}",
                "Remove-Item \"$res.inicio\" -ErrorAction SilentlyContinue",
                "Start-Sleep -Seconds 1",
                $"Start-Process -FilePath {Q(exe)}",
                ""), new UTF8Encoding(true));

            bool semAdmin = Environment.GetEnvironmentVariable("FAH_TESTE_SEM_ADMIN") == "1";   // bench only
            var elevado = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{trocar}\"")
            {
                UseShellExecute = true,
                Verb = semAdmin ? "" : "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            try { Process.Start(elevado); }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Base.Log("atualizar: administrador negado");
                return (false, "a permissão de administrador foi negada");
            }
            Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{reabrir}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Base.Log("atualizar: troca armada; fechando o FAH");
            return (true, "");
        }
        catch (Exception ex)
        {
            Base.Log("atualizar: não consegui armar a troca: " + ex.Message);
            return (false, "não consegui preparar a atualização");
        }
    }

    /// <summary>At start: null = no update happened; "" = it worked; text = why it failed. Deletes the file (told once).</summary>
    public static string? ResultadoDaUltima()
    {
        try
        {
            if (!File.Exists(ArqResultado)) return null;
            var t = File.ReadAllText(ArqResultado).Trim();
            File.Delete(ArqResultado);
            if (t == "ok") return "";
            var motivo = t.StartsWith("erro") ? t[4..].Trim() : t;
            return Motivos.TryGetValue(motivo, out var frase) ? frase : "motivo desconhecido";
        }
        catch { return null; }
    }
}
