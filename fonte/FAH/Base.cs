// Folders next to the exe (portable), log, saved UI state.
using System.IO;
using System.Text.Json;

namespace FAH;

static class Base
{
    public static readonly string Raiz = AppContext.BaseDirectory;
    /// <summary>User folders (config/dados/webview/relatorios/backup) live together in usuario\ beside the exe; if that folder is
    /// read-only (Program Files), %LOCALAPPDATA%\FAH. "modulos" and "ui" always stay beside the exe (installed with it, never written).</summary>
    public static readonly string[] PastasUsuario = ["config", "dados", "relatorios", "webview", "backup"];
    public static string RaizUsuario => Path.Combine(Raiz, "usuario");
    static readonly Lazy<string> gravavel = new(AcharGravavel);
    static string AcharGravavel()
    {
        var u = RaizUsuario;
        try
        {
            var raiz = MoverPastasAntigas(u) ? u : Raiz;     // config could not move yet (locked): old loose layout for this run, retried next start
            var t = Path.Combine(raiz, "config"); Directory.CreateDirectory(t);
            var prova = Path.Combine(t, ".escrita-" + Environment.ProcessId);
            File.WriteAllText(prova, ""); File.Delete(prova);
            return raiz;
        }
        catch
        {
            if (File.Exists(Path.Combine(u, "config", "estado.json"))) return u;   // saved state is here even if writing fails: never hide it
            var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FAH");
            try { Directory.CreateDirectory(p); } catch { }
            return p;
        }
    }

    /// <summary>Up to 1.1.0 the user folders sat loose beside the exe: move them into usuario\ (same volume, instant). Config goes first;
    /// false = config is still loose (nothing else moved). A folder that half-moved before is merged; the loose copy wins (it is the real data).</summary>
    static bool MoverPastasAntigas(string u)
    {
        foreach (var n in PastasUsuario)
        {
            var velha = Path.Combine(Raiz, n);
            if (!Directory.Exists(velha)) continue;
            try { Directory.CreateDirectory(u); Juntar(velha, Path.Combine(u, n)); } catch { }
            if (n == "config" && Directory.Exists(velha)) return false;
        }
        return true;
    }

    static void Juntar(string velha, string nova)
    {
        if (!Directory.Exists(nova)) { Directory.Move(velha, nova); return; }
        foreach (var f in Directory.GetFiles(velha))
            try { File.Move(f, Path.Combine(nova, Path.GetFileName(f)), true); } catch { }
        foreach (var d in Directory.GetDirectories(velha))
            try { Juntar(d, Path.Combine(nova, Path.GetFileName(d))); } catch { }
        try { Directory.Delete(velha); } catch { }          // only when empty; leftovers retried next start
    }

    /// <summary>Single-file FAH.exe (1.2.0+): files left by the old loose layout (an update copies over it) are not used anymore.
    /// Only runs when the old FAH.deps.json is there, and only deletes the files that deps.json lists (+ known extras).</summary>
    public static void LimparLayoutAntigo()
    {
#pragma warning disable IL3000   // empty Location = running as the single file, which is exactly the test
        if (!string.IsNullOrEmpty(typeof(Base).Assembly.Location) || !Atualizador.EhExeDeVerdade()) return;   // only the published single file
#pragma warning restore IL3000
        var deps = Path.Combine(Raiz, "FAH.deps.json");
        if (!File.Exists(deps)) return;
        var nomes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FAH.dll", "FAH.pdb", "FAH.Audio.pdb", "FAH.Contrato.pdb", "createdump.exe", "fah.ico",
            "Microsoft.Web.WebView2.Core.xml", "Microsoft.Web.WebView2.WinForms.xml", "Microsoft.Web.WebView2.Wpf.xml", "FAH.runtimeconfig.json" };
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(deps));
            void Varre(JsonElement e)
            {
                if (e.ValueKind != JsonValueKind.Object) return;
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name is "runtime" or "native" && p.Value.ValueKind == JsonValueKind.Object)
                        foreach (var a in p.Value.EnumerateObject()) nomes.Add(Path.GetFileName(a.Name));   // keys are package paths (lib/netstandard2.0/x.dll); publish put them flat
                    else Varre(p.Value);
                }
            }
            Varre(doc.RootElement);
        }
        catch (Exception e) { Log("layout antigo: deps.json ilegível: " + e.Message); return; }
        int n = 0;
        void Apaga(string f) { try { if (File.Exists(f)) { File.Delete(f); n++; } } catch { } }
        foreach (var nome in nomes) Apaga(Path.Combine(Raiz, nome));
        var rt = Path.Combine(Raiz, "runtimes");             // WebView2Loader.dll per RID (runtimes\win-*\native\)
        if (Directory.Exists(rt))
            foreach (var f in Directory.GetFiles(rt, "*", SearchOption.AllDirectories)) if (nomes.Contains(Path.GetFileName(f))) Apaga(f);
        foreach (var d in Directory.GetDirectories(Raiz))     // satellite folders (de\, pt-BR\ ...): x.resources.dll of a listed x.dll
        {
            var nome = Path.GetFileName(d);
            if (nome is "modulos" or "ui" or "usuario" or "runtimes" || PastasUsuario.Contains(nome)) continue;
            foreach (var f in Directory.GetFiles(d, "*.resources.dll"))
                if (nomes.Contains(Path.GetFileName(f)[..^".resources.dll".Length] + ".dll")) Apaga(f);
        }
        foreach (var d in Directory.GetDirectories(Raiz))     // language folders / runtimes\ left empty
        {
            var nome = Path.GetFileName(d);
            if (nome is "modulos" or "ui" or "usuario" || PastasUsuario.Contains(nome)) continue;
            try { if (!Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Any()) Directory.Delete(d, true); } catch { }
        }
        try { File.Delete(deps); n++; } catch { }
        if (n > 0) Log($"layout antigo: {n} arquivos soltos removidos");
    }

    public static string Pasta(string nome) { var p = Path.Combine(nome == "modulos" ? Raiz : gravavel.Value, nome); Directory.CreateDirectory(p); return p; }
    public static string Ui => Path.Combine(Raiz, "ui");
    public static string Modulos => Pasta("modulos");
    public static string Dados => Pasta("dados");
    public static string Config => Pasta("config");
    static readonly Lock trava = new();

    public static void Log(string t)
    {
        try { lock (trava) File.AppendAllText(Path.Combine(Pasta("relatorios"), "fah.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {t}{Environment.NewLine}"); }
        catch { }
    }

    static string ArqEstado => Path.Combine(Config, "estado.json");

    /// <summary>UI state keys (eq2, ...) saved between runs.</summary>
    public static Dictionary<string, JsonElement> LerEstado()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(ArqEstado)) ?? []; }
        catch { return []; }
    }

    public static void GravarEstado(Dictionary<string, JsonElement> e)
    {
        try { var tmp = ArqEstado + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(e)); File.Move(tmp, ArqEstado, true); }
        catch (Exception ex) { Log("gravar estado: " + ex.Message); }
    }
}
