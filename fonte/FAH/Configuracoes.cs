// Settings on the host side: cfg parsing (saved by the UI under estado key "cfg"), start with Windows,
// hotkey strings, backup zip (config/estado.json + dados/...), daily backup, pending import at start.
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Win32;

namespace FAH;

/// <summary>The "cfg" object of the Settings screen; every missing field falls back to its default.</summary>
sealed class Cfg
{
    public bool Inicio, Minimizado = true, XBandeja = true, IconeEstado = true, BackupDiario = true, AutoAtualizar = true;
    public int BackupQtd = 7;
    public Dictionary<string, string> Atalhos = new(Configuracoes.AtalhosPadrao);

    public static Cfg De(JsonElement? e)
    {
        var c = new Cfg();
        if (e is not { ValueKind: JsonValueKind.Object } r) return c;
        bool B(string k, bool def) => r.TryGetProperty(k, out var v) ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => def } : def;
        c.Inicio = B("inicio", false); c.Minimizado = B("minimizado", true); c.XBandeja = B("xBandeja", true);
        c.IconeEstado = B("iconeEstado", true); c.BackupDiario = B("backupDiario", true); c.AutoAtualizar = B("autoAtualizar", true);
        if (r.TryGetProperty("backupQtd", out var q) && q.ValueKind == JsonValueKind.Number && q.TryGetInt32(out var n)) c.BackupQtd = Math.Clamp(n, 1, 365);
        if (r.TryGetProperty("atalhos", out var a) && a.ValueKind == JsonValueKind.Object)
            foreach (var id in Configuracoes.AtalhosPadrao.Keys)
                if (a.TryGetProperty(id, out var s) && s.ValueKind == JsonValueKind.String) c.Atalhos[id] = s.GetString() ?? "";
        return c;
    }
}

static class Configuracoes
{
    public static readonly Dictionary<string, string> AtalhosPadrao = new()
    {
        ["ligar"] = "Ctrl + Alt + F", ["mic"] = "Ctrl + Alt + M", ["preset"] = "Ctrl + Alt + →", ["abrir"] = "Ctrl + Alt + A",
    };

    // ---- start with Windows
    const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static bool avisouBin;

    public static bool InicioAtivo()
    {
        try { using var k = Registry.CurrentUser.OpenSubKey(ChaveRun); return k?.GetValue("FAH") != null; }
        catch { return false; }
    }

    /// <summary>on = write the Run entry (also fixes the path if the folder moved), off = remove it. Only for the real exe.</summary>
    public static void AplicarInicio(bool on)
    {
        try
        {
            if (!Atualizador.EhExeDeVerdade())
            {
                if (!avisouBin) { avisouBin = true; Base.Log("inicio com o Windows: ignorado (não é o FAH instalado)"); }
                return;
            }
            var valor = $"\"{Environment.ProcessPath}\" --bandeja";
            using var k = Registry.CurrentUser.CreateSubKey(ChaveRun);
            var atual = k.GetValue("FAH") as string;
            if (on) { if (atual != valor) { k.SetValue("FAH", valor); Base.Log("inicio com o Windows: ligado"); } }
            else if (atual != null) { k.DeleteValue("FAH", false); Base.Log("inicio com o Windows: desligado"); }
        }
        catch (Exception e) { Base.Log("inicio com o Windows: " + e.Message); }
    }

    // ---- hotkey strings
    static readonly Dictionary<string, uint> Teclas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["→"] = 0x27, ["←"] = 0x25, ["↑"] = 0x26, ["↓"] = 0x28, ["Espaço"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Backspace"] = 0x08,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
    };

    /// <summary>"Ctrl + Alt + F" -> (modifiers, virtual key); null = unknown key or no modifier.</summary>
    public static (uint Mods, uint Vk)? Analisar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var p = texto.Split(" + ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 2) return null;
        uint mods = 0;
        foreach (var m in p[..^1])
        {
            switch (m.ToLowerInvariant())
            {
                case "ctrl": mods |= 2; break;
                case "alt": mods |= 1; break;
                case "shift": mods |= 4; break;
                case "win": mods |= 8; break;
                default: return null;
            }
        }
        if (mods == 0) return null;
        string k = p[^1];
        uint vk;
        if (k.Length == 1 && char.IsAsciiLetterOrDigit(k[0])) vk = char.ToUpperInvariant(k[0]);
        else if (k.Length >= 2 && (k[0] == 'F' || k[0] == 'f') && int.TryParse(k[1..], out var f) && f is >= 1 and <= 24) vk = (uint)(0x70 + f - 1);
        else if (k.StartsWith("Num ", StringComparison.OrdinalIgnoreCase) && k.Length == 5 && char.IsAsciiDigit(k[4])) vk = (uint)(0x60 + (k[4] - '0'));
        else if (!Teclas.TryGetValue(k, out vk)) return null;
        return (mods, vk);
    }

    // ---- backup
    static readonly string[] SoLocal = ["padrao-original.json", "padrao-mic-original.json"];   // restore guard of this PC's Windows defaults: never exported/imported
    static string ArqImportar => Path.Combine(Base.Config, "importar.zip");

    public static void Exportar(string destino)
    {
        var tmp = destino + ".tmp";
        using (var fs = File.Create(tmp))
        using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var estado = Path.Combine(Base.Config, "estado.json");
            if (File.Exists(estado)) Adicionar(z, estado, "config/estado.json");
            else using (var w = new StreamWriter(z.CreateEntry("config/estado.json").Open())) w.Write("{}");
            var dados = Base.Dados;
            foreach (var f in Directory.EnumerateFiles(dados, "*", SearchOption.AllDirectories))
            {
                if (SoLocal.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase) || f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                Adicionar(z, f, "dados/" + Path.GetRelativePath(dados, f).Replace('\\', '/'));
            }
        }
        File.Move(tmp, destino, true);
    }

    static void Adicionar(ZipArchive z, string arq, string nome)
    {
        try
        {
            using var f = new FileStream(arq, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);   // the app may be writing it
            using var o = z.CreateEntry(nome, CompressionLevel.Optimal).Open();
            f.CopyTo(o);
        }
        catch (Exception e) { Base.Log("backup: pulei " + nome + ": " + e.Message); }
    }

    /// <summary>True when the zip is a FAH backup (has config/estado.json).</summary>
    public static bool EhBackup(string zip)
    {
        try { using var z = ZipFile.OpenRead(zip); return z.GetEntry("config/estado.json") != null; }
        catch { return false; }
    }

    /// <summary>Copies the chosen backup where the next start finds it.</summary>
    public static void PrepararImportacao(string zip) => File.Copy(zip, ArqImportar, true);

    public static void CancelarImportacao() { try { File.Delete(ArqImportar); } catch { } }

    /// <summary>At start, before the Motor reads its state: applies a pending importar.zip over config and dados.</summary>
    public static void ImportarPendente()
    {
        var zip = ArqImportar;
        if (!File.Exists(zip)) return;
        try
        {
            using (var z = ZipFile.OpenRead(zip))
            {
                // broken/foreign zip must not wipe the current state: estado.json has to parse, and today's state is kept aside first
                var est = z.GetEntry("config/estado.json");
                if (est == null) { Base.Log("importar: zip sem config/estado.json, nada trocado"); return; }
                using (var r = new StreamReader(est.Open())) JsonDocument.Parse(r.ReadToEnd()).Dispose();
                Exportar(Path.Combine(Base.Pasta("backup"), "antes-de-importar.zip"));
                int n = 0;
                string dados = Path.GetFullPath(Base.Dados) + Path.DirectorySeparatorChar;
                foreach (var e in z.Entries)
                {
                    if (e.FullName.EndsWith('/') || e.Name == "") continue;
                    if (e.FullName == "config/estado.json") { e.ExtractToFile(Path.Combine(Base.Config, "estado.json"), true); n++; continue; }
                    if (!e.FullName.StartsWith("dados/")) continue;
                    if (SoLocal.Contains(e.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    var alvo = Path.GetFullPath(Path.Combine(dados, e.FullName[6..]));
                    if (!alvo.StartsWith(dados, StringComparison.OrdinalIgnoreCase)) { Base.Log("importar: caminho recusado " + e.FullName); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
                    e.ExtractToFile(alvo, true); n++;
                }
                Base.Log($"backup importado: {n} arquivos");
            }
        }
        catch (Exception e) { Base.Log("importar backup: " + e.Message); }
        finally { try { File.Delete(zip); } catch { } }
    }

    /// <summary>Today's backup into backup\ if it is not there yet, then drops the oldest beyond qtd. Errors only logged.</summary>
    public static void Diario(int qtd)
    {
        try
        {
            var pasta = Base.Pasta("backup");
            var hoje = Path.Combine(pasta, $"FAH-{DateTime.Now:yyyy-MM-dd}.zip");
            if (File.Exists(hoje)) return;
            Exportar(hoje);
            Base.Log("backup diário: " + Path.GetFileName(hoje));
            foreach (var velho in Directory.GetFiles(pasta, "FAH-????-??-??.zip").OrderByDescending(Path.GetFileName).Skip(Math.Max(qtd, 1)))
                try { File.Delete(velho); } catch { }
        }
        catch (Exception e) { Base.Log("backup diário: " + e.Message); }
    }

    /// <summary>"yyyy-MM-dd" of the newest daily backup, or null.</summary>
    public static string? UltimoBackup()
    {
        try
        {
            var f = Directory.GetFiles(Base.Pasta("backup"), "FAH-????-??-??.zip").OrderByDescending(Path.GetFileName).FirstOrDefault();
            return f == null ? null : Path.GetFileNameWithoutExtension(f)[4..];
        }
        catch { return null; }
    }
}
