// FAH's cables (Som pair + Mic pair) exist in Windows only while FAH runs (user 31/10): disabled on exit, enabled on start.
// Only endpoints FAH itself disabled come back (list on disk); a cable the user turned off stays off. No admin needed (tested 31/10).
// Exception (user 04/11): a pair with exactly one side on is useless, so the other side is turned on too (LigarOutroLado). Sound pair only.
// The list lives in usuario\dados: a new install in another folder can't see it, so its cables stay off until turned on by hand.
// Crash: the cables stay enabled (harmless); next exit disables them.
using System.IO;
using System.Text.Json;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    static string ArqCabos => Path.Combine(Base.Dados, "cabos-desligados.json");

    readonly HashSet<string> ladoRecusado = [];   // side Windows would not turn on: not retried this run (each try costs up to 1.5 s)

    /// <summary>Half-on Som pair: turns on the missing side and waits for Windows to show it. Both sides off stays off (the veil asks).
    /// Any failure = the check's original answer, so the veil names the side to turn on by hand.</summary>
    Achado LigarOutroLado(Achado a)
    {
        if (a.Ligar is not { } id || Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) return a;
        lock (ladoRecusado) if (ladoRecusado.Contains(id)) return a;
        int hr; try { hr = PadraoWindows.Visivel(id, true); } catch (Exception e) { hr = e.HResult; }
        if (hr == 0)
        {
            Cabos.Log = null;
            for (int i = 0; i < 6; i++)
            {
                Thread.Sleep(250);
                var b = Cabos.Detectar();
                if (b.Cabo != null) { Log($"cabo: ligou o lado que faltava: {b.Cabo.Tocar.Nome} -> {b.Cabo.Captar.Nome}"); return b; }
            }
        }
        lock (ladoRecusado) ladoRecusado.Add(id);
        Log($"cabo: o lado que faltava não ligou ({string.Join(" + ", a.Desativados)}): 0x{hr:x8}");
        return a;
    }

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
