// WASAPI devices found by NAME (ids change when plugging things), Windows default switch (IPolicyConfig) + guard
// (stores the original default on disk; restored on exit and on next start if we crashed).
using System.Runtime.InteropServices;
using System.Text.Json;
using NAudio.CoreAudioApi;

namespace FAH.Audio;

public sealed record Aparelho(string Id, string Nome, bool Saida, bool Padrao, bool Virtual);

public static class Aparelhos
{
    public static List<Aparelho> Listar(bool saida)
    {
        using var en = new MMDeviceEnumerator();
        var fluxo = saida ? DataFlow.Render : DataFlow.Capture;
        string? pad = null;
        try { pad = en.GetDefaultAudioEndpoint(fluxo, Role.Multimedia).ID; } catch { }
        var lst = new List<Aparelho>();
        foreach (var d in en.EnumerateAudioEndPoints(fluxo, DeviceState.Active))
        {
            using var _ = d;
            string nome = Nome(d);
            if (nome.Contains("Hands-Free")) continue;
            lst.Add(new(d.ID, nome, saida, d.ID == pad, EhCabo(d)));
        }
        return lst;
    }

    /// <summary>Exact name; else start of name (Windows adds "2- " or changes the suffix); "" = default.</summary>
    public static Aparelho? Achar(bool saida, string nome)
    {
        var lst = Listar(saida);
        if (string.IsNullOrEmpty(nome)) return lst.FirstOrDefault(d => d.Padrao);
        var ex = lst.FirstOrDefault(d => d.Nome == nome);
        if (ex != null) return ex;
        var b = nome.Split('(')[0].Trim().ToLowerInvariant();
        return lst.FirstOrDefault(d => d.Nome.ToLowerInvariant().StartsWith(b) || d.Nome.ToLowerInvariant().Contains(b));
    }

    /// <summary>Any accepted virtual cable (VAC, VB-CABLE, Voicemeeter), by driver interface name.</summary>
    public static bool EhCabo(MMDevice d) => Cabos.Familia(Interface(d)) != null;

    /// <summary>"Line 1 (Virtual Audio Cable)": endpoint name as Windows shows it.</summary>
    public static string Nome(MMDevice d) => Prop(d, PropertyKeys.PKEY_Device_FriendlyName).Replace('\r', ' ').Replace('\n', ' ').Trim();
    /// <summary>"Virtual Audio Cable": driver/interface name (same for every endpoint of one cable).</summary>
    public static string Interface(MMDevice d) => Prop(d, PropertyKeys.PKEY_DeviceInterface_FriendlyName).Trim();

    // NAudio's PropertyStore[key] walks every property (~20 ms per read on this PC, 29/10); IPropertyStore.GetValue is direct
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IProps { [PreserveSig] int GetCount(out int c); [PreserveSig] int GetAt(int i, out PropertyKey k); [PreserveSig] int GetValue(ref PropertyKey k, out NAudio.CoreAudioApi.Interfaces.PropVariant v); }
    static readonly System.Reflection.FieldInfo? Loja = typeof(PropertyStore).GetField("storeInterface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    [DllImport("ole32.dll")] static extern int PropVariantClear(ref NAudio.CoreAudioApi.Interfaces.PropVariant v);

    static string Prop(MMDevice d, PropertyKey k)
    {
        try
        {
            if (Loja?.GetValue(d.Properties) is IProps p)
            {
                if (p.GetValue(ref k, out var v) != 0) return "";
                try { return v.Value as string ?? ""; } finally { PropVariantClear(ref v); }
            }
        }
        catch { }
        try { var ps = d.Properties; return ps.Contains(k) ? ps[k].Value as string ?? "" : ""; } catch { return ""; }
    }

    public static MMDevice Abrir(string id) { using var en = new MMDeviceEnumerator(); return en.GetDevice(id); }
}

/// <summary>Windows default output (or input: <paramref name="saida"/> false = the mic guard). FAH_SIMULAR_PADRAO=&lt;file&gt; = tests:
/// reads/writes that file (mic: file + ".mic"), Windows untouched.</summary>
public sealed class PadraoWindows(string arquivoGuarda, bool saida = true)
{
    static string? Simulado => Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO");
    static string? SimuladoDe(bool saida) => Simulado is { } f ? (saida ? f : f + ".mic") : null;

    public static string? Atual(bool saida = true)
    {
        if (SimuladoDe(saida) is { } f) return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
        try { using var en = new MMDeviceEnumerator(); return en.GetDefaultAudioEndpoint(saida ? DataFlow.Render : DataFlow.Capture, Role.Multimedia).ID; }
        catch { return null; }
    }

    /// <summary>All 3 roles (console, multimedia, communications) on <paramref name="id"/>? Discord etc. follow communications.</summary>
    public static bool TodosEm(string id, bool saida = true)
    {
        if (SimuladoDe(saida) != null) return Atual(saida) == id;
        try
        {
            using var en = new MMDeviceEnumerator();
            var f = saida ? DataFlow.Render : DataFlow.Capture;
            return new[] { Role.Console, Role.Multimedia, Role.Communications }.All(r => en.GetDefaultAudioEndpoint(f, r).ID == id);
        }
        catch { return true; }   // can't read: don't fight Windows
    }

    public static void Definir(string id, bool saida = true)
    {
        if (SimuladoDe(saida) is { } f) { File.WriteAllText(f, id); return; }
        var pc = (IPolicyConfig)new PolicyConfigObj();
        for (int papel = 0; papel < 3; papel++) Marshal.ThrowExceptionForHR(pc.SetDefaultEndpoint(id, papel));
    }

    /// <summary>Default per role: 0 console, 1 multimedia, 2 communications (Discord follows communications). Simulation: one id for all.</summary>
    static string? AtualPapel(bool saida, int papel)
    {
        if (SimuladoDe(saida) != null) return Atual(saida);
        try { using var en = new MMDeviceEnumerator(); return en.GetDefaultAudioEndpoint(saida ? DataFlow.Render : DataFlow.Capture, (Role)papel).ID; }
        catch { return null; }
    }

    static void DefinirPapel(string id, bool saida, int papel)
    {
        if (SimuladoDe(saida) is { } f) { if (papel == 1) File.WriteAllText(f, id); return; }
        Marshal.ThrowExceptionForHR(((IPolicyConfig)new PolicyConfigObj()).SetDefaultEndpoint(id, papel));
    }

    /// <summary>Restore steps go here (FAH log). Set once by the app.</summary>
    public static Action<string>? Log;

    /// <summary>Enable/disable an endpoint (same as Sound panel "Disable"). HRESULT, 0 = ok.</summary>
    public static int Visivel(string id, bool v) => ((IPolicyConfig)new PolicyConfigObj()).SetEndpointVisibility(id, v);

    readonly object trava = new();

    /// <summary>Switch the default to <paramref name="id"/> (FAH's cable), storing the original of each role once (never overwrites a
    /// pending guard). A role already on FAH's cable (left there by a crash whose guard was lost) stores <paramref name="real"/>
    /// instead (the real device FAH plays to / records), so exit never leaves Windows on a cable that FAH then disables.</summary>
    public void Trocar(string id, string? real = null)
    {
        lock (trava)
        {
            var papeis = new string?[3];
            for (int p = 0; p < 3; p++) { var o = AtualPapel(saida, p); papeis[p] = o == null || o == id ? real : o; }
            if (!File.Exists(arquivoGuarda) && papeis.Any(x => x != null))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(arquivoGuarda)!);
                File.WriteAllText(arquivoGuarda, JsonSerializer.Serialize(new { saida = papeis[1] ?? papeis.FirstOrDefault(x => x != null), papeis }));
            }
            if (Atual(saida) != id || !TodosEm(id, saida)) Definir(id, saida);
        }
    }

    /// <summary>User picked another default while FAH holds the cable (28/10): on exit every role goes to this one instead.</summary>
    public void NovoOriginal(string id)
    {
        lock (trava)
            if (File.Exists(arquivoGuarda)) File.WriteAllText(arquivoGuarda, JsonSerializer.Serialize(new { saida = id, papeis = new[] { id, id, id } }));
    }

    /// <summary>Give the original defaults back, role by role (on exit, or at start if the last run died). Old guard files
    /// (only "saida") restore all roles to it. A device that is gone is skipped (Windows picks one); the others still go back.</summary>
    public bool Devolver()
    {
        lock (trava)
        {
            if (!File.Exists(arquivoGuarda)) return false;
            try
            {
                var r = JsonDocument.Parse(File.ReadAllText(arquivoGuarda)).RootElement;
                var um = r.TryGetProperty("saida", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                var papeis = r.TryGetProperty("papeis", out var pp) && pp.ValueKind == JsonValueKind.Array && pp.GetArrayLength() == 3
                    ? pp.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToArray() : [um, um, um];
                // multimedia last: it is the one most programs follow, so it ends on the right device even if another role fails
                int tentou = 0, voltou = 0;
                foreach (int p in new[] { 0, 2, 1 })
                {
                    var id = papeis[p];
                    if (string.IsNullOrEmpty(id)) continue;
                    tentou++;
                    try { DefinirPapel(id, saida, p); voltou++; }
                    catch (Exception e) { Log?.Invoke($"padrão {(saida ? "som" : "microfone")} papel {p} não voltou: {e.Message}"); }
                }
                // nothing went back (audio service down, shutdown): keep the guard so the guardian / next start tries again
                if (tentou > 0 && voltou == 0) return false;
            }
            catch (Exception e) { Log?.Invoke("guarda do padrão ilegível: " + e.Message); }
            try { File.Delete(arquivoGuarda); } catch { }
            return true;
        }
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class PolicyConfigObj { }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr f);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d, bool pad, IntPtr f);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr a, IntPtr b);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string d, bool pad, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr a);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr m);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string d, IntPtr m);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string d, bool fx, IntPtr k, IntPtr v);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string d, bool fx, IntPtr k, IntPtr v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string d, int papel);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string d, bool v);
    }
}
