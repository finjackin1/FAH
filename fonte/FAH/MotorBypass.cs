// Device bypass (Mixer, 30/10): everything a device plays (sound side, loopback) or hears (mic side, capture) also comes out of the
// default sound (FAH's real output when on, else the Windows default real output), raw (no FAH effects), at that device's own fader
// and mute (endpoint volume notification). Sound device that IS the default = paused (it already plays there), back when it stops being it.
// Unplugged = paused, back when it returns. State "bypass" = { "som": [device id], "mic": [device id] }; resynced every 1 s.
using System.Text.Json;
using FAH.Audio;
using NAudio.CoreAudioApi;

namespace FAH;

sealed partial class Motor
{
    const string ChaveBypass = "bypass";
    // loopback is taken before the device volume (measured 30/10, FAH.Testes --bypassvol on Line 2: 25 % -> still 1.0), so FAH applies the
    // fader itself. Real mics carry their level in the capture (cables, which ignore it, never show in the Mixer)
    const bool LoopbackAntesDoVolume = true;
    readonly Lock tBypass = new();
    sealed class Desvio { public required Fluxo F; public required Leitor L; public required MMDevice D; public AudioEndpointVolumeNotificationDelegate? Aviso; }
    readonly Dictionary<string, Desvio> desvios = [];                 // "lado|id" -> running bypass
    readonly HashSet<string> desviosRecusados = [];                   // refused bypasses, forgotten every 10 s so they are retried
    DateTime recusadosLimpos = DateTime.UtcNow;
    Destino? alvoBypass; bool alvoProprio; string alvoNome = "";
    System.Threading.Timer? relBypass;

    Dictionary<string, List<string>> Bypasses()
    {
        var r = new Dictionary<string, List<string>> { ["som"] = [], ["mic"] = [] };
        lock (tEstado)
            if (estado.TryGetValue(ChaveBypass, out var v) && v.ValueKind == JsonValueKind.Object)
                foreach (var p in v.EnumerateObject())
                    if (r.ContainsKey(p.Name) && p.Value.ValueKind == JsonValueKind.Array)
                        r[p.Name] = [.. p.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)];
        return r;
    }

    /// <summary>lado "som" | "mic", id = Windows device id.</summary>
    public void Bypass(string lado, string id, bool on)
    {
        if (lado != "som" && lado != "mic") return;
        var b = Bypasses(); var l = b[lado];
        if (on && !l.Contains(id)) l.Add(id); else if (!on) l.Remove(id);
        lock (tEstado) { estado[ChaveBypass] = JsonSerializer.SerializeToElement(b); Base.GravarEstado(estado); }
        lock (tBypass) desviosRecusados.Remove(lado + "|" + id);
        Log((on ? "bypass ligado: " : "bypass desligado: ") + lado + " " + id);
        SincronizarBypass();
        StatusBypass();
    }

    /// <summary>ativos = bypasses really playing now (the rest are paused: device missing, or it is the default sound).</summary>
    public void StatusBypass() => StatusBypass(true);

    string ultimoBypass = "";
    void StatusBypass(bool sempre)
    {
        var b = Bypasses(); string[] at; lock (tBypass) at = [.. desvios.Keys.Order()];
        var m = new { som = b["som"], mic = b["mic"], ativos = at };
        var j = JsonSerializer.Serialize(m);
        if (!sempre && j == ultimoBypass) return;   // the 1 s resync only speaks when something changed
        ultimoBypass = j; Enviar("bypass", m);
    }

    /// <summary>Name of the default sound for the bypass: FAH's own output when on, else the Windows default real device.</summary>
    string NomeAlvo()
    {
        if (destinos.FirstOrDefault() is { } fd) return fd.NomeReal is { Length: > 0 } n ? n : fd.Nome;
        var reais = Aparelhos.Listar(true).Where(a => !a.Virtual).ToList();
        var ultima = estado.TryGetValue("saida", out var us) && us.ValueKind == JsonValueKind.String ? us.GetString() : null;
        return (reais.FirstOrDefault(a => a.Padrao) ?? reais.FirstOrDefault(a => a.Nome == ultima))?.Nome ?? "";
    }

    /// <summary>FAH on = its output (shared); off = an own output on that device. Another device than before = every bypass rebuilt.</summary>
    Destino? AlvoBypass(string nome)
    {
        var fd = destinos.FirstOrDefault();
        if (alvoBypass != null && alvoNome == nome && (fd == null ? alvoProprio : alvoBypass == fd)) return alvoBypass;
        TrocarAlvo();
        if (fd != null) { alvoBypass = fd; alvoNome = nome; return fd; }
        try { var d = new Destino(nome, 40); d.Abrir(); alvoBypass = d; alvoProprio = true; alvoNome = nome; return d; }
        catch (Exception e) { Log("bypass: som padrão não abriu: " + e.Message); return null; }
    }

    /// <summary>Default sound changed: every bypass is rebuilt on the new one.</summary>
    void TrocarAlvo()
    {
        foreach (var k in desvios.Keys.ToList()) PararDesvio(k);
        if (alvoProprio) alvoBypass?.Fechar();
        alvoBypass = null; alvoProprio = false; alvoNome = "";
    }

    void PararDesvio(string k)
    {
        if (!desvios.Remove(k, out var x)) return;
        try { x.F.Fechar(); } catch { }
        try { if (x.Aviso != null) x.D.AudioEndpointVolume.OnVolumeNotification -= x.Aviso; } catch { }
        try { x.D.Dispose(); } catch { }
        alvoBypass?.Remover(x.L);
    }

    void SincronizarBypass()
    {
        lock (tBypass)
        {
            var b = Bypasses();
            if (b["som"].Count + b["mic"].Count == 0) { relBypass?.Dispose(); relBypass = null; TrocarAlvo(); desviosRecusados.Clear(); }
            else
            {
                relBypass ??= new System.Threading.Timer(_ => { try { SincronizarBypass(); } catch { } }, null, 1000, 1000);
                if (DateTime.UtcNow - recusadosLimpos > TimeSpan.FromSeconds(10)) { desviosRecusados.Clear(); recusadosLimpos = DateTime.UtcNow; }
                string nome = NomeAlvo();
                var quer = new Dictionary<string, Aparelho>();
                if (nome.Length > 0)
                    foreach (var lado in new[] { "som", "mic" })
                    {
                        var lst = Aparelhos.Listar(lado == "som");
                        foreach (var id in b[lado])
                            if (lst.FirstOrDefault(a => a.Id == id) is { Virtual: false } a && !(lado == "som" && MesmoAparelho(a.Nome, nome))) quer[lado + "|" + id] = a;
                    }
                // nothing to play (all paused): no output kept open
                var alvo = quer.Count > 0 ? AlvoBypass(nome) : null;
                if (alvo == null) TrocarAlvo();
                foreach (var k in desvios.Keys.Where(k => !quer.ContainsKey(k)).ToList()) PararDesvio(k);
                if (alvo != null) foreach (var (k, a) in quer.Where(q => !desvios.ContainsKey(q.Key) && !desviosRecusados.Contains(q.Key))) Abrir(k, a, alvo);
            }
        }
        StatusBypass(false);
    }

    void Abrir(string k, Aparelho a, Destino alvo)
    {
        bool som = k.StartsWith("som|");
        var lt = alvo.NovoLeitor("bypass-" + a.Nome, 40);
        var f = new Fluxo(a.Nome, new Cadeia(), som);
        MMDevice? dv = null;
        try
        {
            dv = Aparelhos.Abrir(a.Id);
            var ev = dv.AudioEndpointVolume;
            // mic capture already carries the mic level; sound loopback may not carry the device volume (then FAH applies it)
            bool aplica = som && (LoopbackAntesDoVolume || ev.HardwareSupport.HasFlag(EEndpointHardwareSupport.Volume));
            void Poe(float vol, bool mudo) { lt.Volume = aplica ? vol : 1; lt.Mudo = mudo; }
            Poe(ev.MasterVolumeLevelScalar, ev.Mute);
            AudioEndpointVolumeNotificationDelegate aviso = d => Poe(d.MasterVolume, d.Muted);
            ev.OnVolumeNotification += aviso;
            f.Escutas = [lr => lt.Empurrar(lr)];
            f.Abrir(a);
            desvios[k] = new Desvio { F = f, L = lt, D = dv, Aviso = aviso };
        }
        catch (Exception e)
        {
            try { f.Fechar(); } catch { }
            try { dv?.Dispose(); } catch { }
            alvo.Remover(lt); desviosRecusados.Add(k); Log($"bypass {a.Nome}: {e.Message}");
        }
    }

    /// <summary>Same matching as the Mixer's default mark (FAH's output name may carry a suffix).</summary>
    static bool MesmoAparelho(string nome, string alvo) => alvo.Length > 0 && (nome == alvo || alvo.StartsWith(nome) || nome.StartsWith(alvo));

    void FecharBypass()
    {
        lock (tBypass) { relBypass?.Dispose(); relBypass = null; TrocarAlvo(); }
    }
}
