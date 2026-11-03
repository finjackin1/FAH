// FAH's cables (Som pair + Mic pair) exist in Windows only while FAH runs (user 31/10): disabled on exit, enabled on start.
// Only endpoints FAH itself disabled come back (list on disk); a cable the user turned off stays off. No admin needed (tested 31/10).
// Crash: the cables stay enabled (harmless); next exit disables them.
using System.IO;
using System.Text.Json;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    static string ArqCabos => Path.Combine(Base.Dados, "cabos-desligados.json");

    /// <summary>Start, before any cable lookup.</summary>
    void AtivarCabos()
    {
        if (Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null || !File.Exists(ArqCabos)) return;
        try
        {
            var ids = JsonSerializer.Deserialize<string[]>(File.ReadAllText(ArqCabos)) ?? [];
            foreach (var id in ids) { int hr = PadraoWindows.Visivel(id, true); if (hr != 0) Log($"cabo não ativou: 0x{hr:x8}"); }
            Log($"cabos do FAH ativados ({ids.Length})");
        }
        catch (Exception e) { Log("ativar cabos: " + e.Message); }
        File.Delete(ArqCabos);
    }

    /// <summary>Exit, after the Windows defaults were given back (a disabled default would leave Windows with no sound).</summary>
    void DesativarCabos(Cabo? som, Cabo? mic)
    {
        if (Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) return;
        var ids = new[] { som?.Tocar.Id, som?.Captar.Id, mic?.Tocar.Id, mic?.Captar.Id }.OfType<string>().Distinct().ToArray();
        if (ids.Length == 0) return;
        try
        {
            File.WriteAllText(ArqCabos, JsonSerializer.Serialize(ids));   // first: if we die halfway, next start still turns them on
            foreach (var id in ids) { int hr = PadraoWindows.Visivel(id, false); if (hr != 0) Log($"cabo não desativou: 0x{hr:x8}"); }
            Log($"cabos do FAH desativados ({ids.Length})");
        }
        catch (Exception e) { Log("desativar cabos: " + e.Message); }
    }
}
