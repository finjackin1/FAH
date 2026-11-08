// Cable watcher (user 08/11): Windows device events (added/removed/state change) + a 30 s timer re-check every endpoint (on, off,
// unplugged). A usable cable that shows up (installed, plugged, turned on, half-on pair) is turned on and used, and what depends on it
// comes back (Som on its first cable, a mic waiting for a 2nd cable, a path that lost its cable). Acts only when the detection differs
// from what FAH holds, so a steady PC costs one endpoint listing per 30 s and logs nothing. Never installs a cable on its own (that
// stays at start and on the UI button): a driver update briefly removing endpoints must not start a download.
// Parking (user 08/11, "liga ao usar, desliga ao parar"): Som/mic turned off by the user -> its cable pair is turned off in Windows
// (after the default was given back); turned on again right before the path opens. Listed in cabos-desligados.json first, so a crash
// still gets them back on next start (AtivarCabos). Voicemeeter (loopback of the user's own mixer) is never parked.
using System.IO;
using System.Text.Json;
using FAH.Audio;
using NAudio.CoreAudioApi;

namespace FAH;

sealed partial class Motor
{
    System.Threading.Timer? vigiaCabos;
    int vigiando;                                                  // 1 = a check is running (events come in bursts)
    readonly Dictionary<string, string[]> estacionados = [];      // "som"/"mic" -> endpoint ids FAH turned off on user stop
    volatile bool caboCaiu, micCaiu;                               // path closed because its cable stopped: one automatic retry

    void VigiarCabos()
    {
        try { vigiaCabos = new(_ => VigiaCabos(), null, 30000, 30000); }
        catch (Exception e) { Log("vigia de cabos: " + e.Message); }
    }

    /// <summary>Device event (COM thread): only reschedule; Windows fires several events per change.</summary>
    void AparelhoMudou()
    {
        if (encerrando) return;
        try { vigiaCabos?.Change(1500, 30000); } catch (ObjectDisposedException) { }
    }

    void VigiaCabos()
    {
        if (encerrando || Cabo is not { } c || InstalandoCabo) return;
        if (Interlocked.Exchange(ref vigiando, 1) == 1) return;
        try
        {
            string? motivo = null;
            bool somParado; lock (estacionados) somParado = estacionados.ContainsKey("som");
            if (!somParado)
            {
                var a = Detectar();
                // cable gone: confirm once more (endpoints blink during driver updates)
                if (a.Cabo == null && c.Cabo != null && a.Estado != "servico") { Thread.Sleep(3000); if (encerrando) return; a = Detectar(); }
                if (a.Estado != "servico" && (a.Estado != c.Estado || a.Cabo?.Tocar.Id != c.Cabo?.Tocar.Id || a.Cabo?.Captar.Id != c.Cabo?.Captar.Id))
                    motivo = $"{c.Estado} {c.Cabo?.Tocar.Nome ?? c.Marca} -> {a.Estado} {a.Cabo?.Tocar.Nome ?? a.Marca}".Replace("  ", " ");
                else if (caboCaiu && !Ligado && a.Cabo != null)
                {
                    caboCaiu = false;
                    Log("vigia de cabos: o cabo do som voltou, ligando o som de novo");
                    Ligar();
                }
            }
            if (motivo == null && (SemCaboMic || (micCaiu && !MicLigado)) && Cabos.DetectarMic(Cabo?.Cabo).Cabo is { } m)
            {
                if (SemCaboMic) motivo = "cabo para o microfone apareceu: " + m.Tocar.Nome;
                else { micCaiu = false; Log("vigia de cabos: o cabo do microfone voltou, ligando o microfone de novo"); LigarMic(); }
            }
            if (motivo == null) return;
            Log("vigia de cabos: " + motivo);
            instalaTentou = true;                                  // RefazerCabo must not start an install from here
            RefazerCabo();
        }
        catch (Exception e) { Log("vigia de cabos: " + e.Message); }
        finally { Volatile.Write(ref vigiando, 0); }
    }

    static Achado Detectar() { var l = Cabos.Log; Cabos.Log = null; try { return Cabos.Detectar(); } finally { Cabos.Log = l; } }

    /// <summary>User stop: the pair goes off in Windows. Caller already gave the Windows default back.</summary>
    void Estacionar(string lado, Cabo? c)
    {
        if (c == null || c.Loopback || encerrando || Environment.GetEnvironmentVariable("FAH_SIMULAR_PADRAO") != null) return;
        string[] ids = [.. new[] { c.Tocar.Id, c.Captar.Id }.Distinct()];
        lock (estacionados) { estacionados[lado] = ids; GravarEstacionados(); }
        int hr = 0;
        foreach (var id in ids) { int h; try { h = PadraoWindows.Visivel(id, false); } catch (Exception e) { h = e.HResult; } if (h != 0 && hr == 0) hr = h; }
        Log(hr == 0 ? $"cabo do {lado} desligado no Windows (parado): {c.Tocar.Nome}" : $"cabo do {lado} não desligou: 0x{hr:x8}");
    }

    /// <summary>Before a path opens: its parked pair back on, waiting (bounded) until Windows lists it active.</summary>
    void Desestacionar(string lado)
    {
        string[]? ids;
        lock (estacionados) { if (!estacionados.Remove(lado, out ids)) return; GravarEstacionados(); }
        int hr = 0;
        foreach (var id in ids) { int h; try { h = PadraoWindows.Visivel(id, true); } catch (Exception e) { h = e.HResult; } if (h != 0 && hr == 0) hr = h; }
        bool ok = false;
        for (int i = 0; i < 12 && !(ok = Ativos(ids)); i++) Thread.Sleep(250);
        Log(ok ? $"cabo do {lado} ligado de novo no Windows" : $"cabo do {lado} não voltou a tempo: 0x{hr:x8}");
    }

    static bool Ativos(string[] ids)
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            return ids.All(id => { using var d = en.GetDevice(id); return d.State == DeviceState.Active; });
        }
        catch { return false; }
    }

    string[] IdsEstacionados() { lock (estacionados) return [.. estacionados.Values.SelectMany(x => x).Distinct()]; }

    // caller holds the lock on estacionados
    void GravarEstacionados()
    {
        try
        {
            var ids = estacionados.Values.SelectMany(x => x).Distinct().ToArray();
            if (ids.Length == 0) File.Delete(ArqCabos); else File.WriteAllText(ArqCabos, JsonSerializer.Serialize(ids));
        }
        catch (Exception e) { Log("cabos parados: " + e.Message); }
    }
}
