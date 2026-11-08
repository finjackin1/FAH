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
    /// <summary>08/11 dev mode: flag file usuario\config\dev (never in the release zip). Off = no Dev screen and the "dev" key is never applied.</summary>
    public static bool Dev => File.Exists(Path.Combine(Config, "dev"));
    /// <summary>08/11 Dev "Iniciar do zero": flag file usuario\config\zero (dev mode only). On = every start ignores estado.json and nothing is saved;
    /// the flag is the only thing kept between runs. Decided once per run (first LerEstado), so toggling mid-session counts from the next start.</summary>
    public static bool Zero => Dev && File.Exists(Path.Combine(Config, "zero"));
    static bool zeroSessao;
    public static void PorZero(bool on)
    {
        try { var f = Path.Combine(Config, "zero"); if (on) File.WriteAllText(f, ""); else File.Delete(f); Log("iniciar do zero: " + (on ? "ligado" : "desligado") + " (vale ao abrir de novo)"); }
        catch (Exception ex) { Log("iniciar do zero: " + ex.Message); }
    }
    // zero state = flat / off: every module switch off, both EQs on Plano with tone 0, Estéreo+/Crossfeed/Reverb off. Missing keys = factory defaults.
    const string BandasZero = "[{\"t\":\"grave\",\"f\":90,\"g\":0,\"q\":0.7,\"on\":1},{\"t\":\"pico\",\"f\":900,\"g\":0,\"q\":1.4,\"on\":1},{\"t\":\"pico\",\"f\":3500,\"g\":0,\"q\":1,\"on\":1},{\"t\":\"agudo\",\"f\":10000,\"g\":0,\"q\":0.7,\"on\":1}]";
    const string EqZero = "{\"modo\":\"grafico\",\"equ\":true,\"tone\":true,\"limit\":false,\"pre\":0,\"g\":[0,0,0,0,0,0,0,0,0,0],\"bass\":0,\"treble\":0,\"teto\":-1,\"preset\":\"Plano\",\"sel\":0,\"bandas\":" + BandasZero + ",\"meus\":{}}";
    const string EstadoZero = "{\"ligado\":{\"eq\":false,\"efeitos\":false,\"som71\":false,\"eqmic\":false,\"ruido\":false,\"voz\":false},\"eq2\":" + EqZero + ",\"eqmic\":" + EqZero
        + ",\"efeitos\":{\"est\":{\"on\":true,\"modo\":\"auto\",\"bal\":0,\"larg\":100,\"mono\":false},\"cf\":{\"on\":false,\"nivel\":\"medio\"},\"rv\":{\"on\":false,\"preset\":\"Reverb leve\",\"p\":[60,80,32,25,75,35,44]}}}";
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
        zeroSessao = Zero;
        if (zeroSessao) { Log("iniciar do zero: estado salvo ignorado nesta sessão"); return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(EstadoZero)!; }
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(ArqEstado)) ?? []; }
        catch { return []; }
    }

    public static void GravarEstado(Dictionary<string, JsonElement> e)
    {
        if (zeroSessao) return;                         // zero session: estado.json keeps the last normal session, untouched
        try { var tmp = ArqEstado + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(e)); File.Move(tmp, ArqEstado, true); }
        catch (Exception ex) { Log("gravar estado: " + ex.Message); }
    }
}
