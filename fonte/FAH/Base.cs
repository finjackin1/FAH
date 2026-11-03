// Folders next to the exe (portable), log, saved UI state.
using System.IO;
using System.Text.Json;

namespace FAH;

static class Base
{
    public static readonly string Raiz = AppContext.BaseDirectory;
    /// <summary>Where config/dados/webview/relatorios live: beside the exe as always; if that folder is read-only (Program Files),
    /// %LOCALAPPDATA%\FAH. "modulos" always stays beside the exe (it is installed with it, never written).</summary>
    static readonly Lazy<string> gravavel = new(AcharGravavel);
    static string AcharGravavel()
    {
        try
        {
            var t = Path.Combine(Raiz, "config"); Directory.CreateDirectory(t);
            var prova = Path.Combine(t, ".escrita-" + Environment.ProcessId);
            File.WriteAllText(prova, ""); File.Delete(prova);
            return Raiz;
        }
        catch
        {
            var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FAH");
            try { Directory.CreateDirectory(p); } catch { }
            return p;
        }
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
