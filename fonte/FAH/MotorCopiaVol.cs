// Own volume of a copy (30/10, rewritten 02/11). Windows process loopback carries the program's session volume and mute, so the copy
// first undoes it (E = 1 / physical session volume) and then applies its own V:
//   follows the original ("segue", default) -> V = the original's logical volume (0 if muted);
//   own -> V = vol / 100 (0 if the copy is muted).
// A live copy with its own audible volume makes its program "independent": Mesa then holds the original's session at Mesa.Piso
// instead of muted/0, so the copy still hears it. State "copiavol" = { "ap|x": {vol, m, segue} }, ap = device name or "mic".
using System.Text.Json;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    const string ChaveCopiaVol = "copiavol";
    readonly Dictionary<(uint pid, string ap), string> xCopia = [];   // clone capture -> program
    readonly Dictionary<uint, string> xInj = [];                      // mic capture -> program

    public sealed record CopiaVol(int Vol, bool M, bool Segue);
    volatile Dictionary<string, CopiaVol> cvs = [];                 // cached state (read per audio packet)
    volatile HashSet<string> vivas = [];                            // live copies "ap|x"
    /// <summary>Mixer session lookup (exe -> physical/logical volume of the original); set by the window.</summary>
    public Func<string, (float Qf, bool Mf, float Ql, bool Ml)?>? Sessao;
    /// <summary>A copy changed (started, stopped, volume): the mixer should hold/release the floor now.</summary>
    public Action? AoMudarCopias;
    bool cvLido;

    Dictionary<string, CopiaVol> CopiaVols()
    {
        if (cvLido) return cvs;
        var r = new Dictionary<string, CopiaVol>();
        lock (tEstado)
            if (estado.TryGetValue(ChaveCopiaVol, out var v) && v.ValueKind == JsonValueKind.Object)
                foreach (var p in v.EnumerateObject())
                    try
                    {
                        var o = p.Value; double N(string k, double d) => o.TryGetProperty(k, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : d;
                        r[p.Name] = new((int)Math.Clamp(N("vol", 100), 0, 100), N("m", 0) != 0, N("segue", 0) != 0);   // old {vol, g}: own, not muted
                    }
                    catch { }
        cvs = r; cvLido = true; return r;
    }

    CopiaVol Cv(string ap, string x) => CopiaVols().TryGetValue(ap + "|" + x, out var c) ? c : new(100, false, true);

    /// <summary>The program has a live copy with its own audible volume (Mesa holds its session at the floor).</summary>
    public bool Independente(string x)
    {
        var cv = CopiaVols();
        foreach (var k in vivas) { int i = k.IndexOf('|'); if (k[(i + 1)..].Equals(x, StringComparison.OrdinalIgnoreCase) && cv.TryGetValue(k, out var c) && !c.Segue && !c.M && c.Vol > 0) return true; }
        return false;
    }

    public bool HaCopiasProprias() { var cv = CopiaVols(); return vivas.Any(k => cv.TryGetValue(k, out var c) && !c.Segue && !c.M && c.Vol > 0); }

    /// <summary>(E, V) for one packet of the copy of x sent to ap.</summary>
    (float E, float V) GanhoDe(string ap, string x)
    {
        var c = Cv(ap, x);
        if (!Independente(x)) return c.Segue ? (1, 1) : (1, 0);    // untouched session: the loopback already follows the original
        var s = Sessao?.Invoke(x);
        float e = s is not { } q || q.Mf || q.Qf <= 0 ? 1 : Math.Min(1 / q.Qf, 1 / Mesa.Piso);
        float v = c.Segue ? (s is not { } q2 ? 1 : q2.Ml ? 0 : q2.Ql) : c.M ? 0 : c.Vol / 100f;
        return (e, v);
    }

    volatile string[] vivasMic = [], vivasDev = [];
    /// <summary>After the mic copies changed (caller holds trava) / the device copies (caller holds tClone).</summary>
    void AtualizarVivas(bool mic)
    {
        if (mic) vivasMic = [.. xInj.Values.Select(x => "mic|" + x)]; else vivasDev = [.. xCopia.Select(p => p.Key.ap + "|" + p.Value)];
        var n = new HashSet<string>(vivasMic.Concat(vivasDev), StringComparer.OrdinalIgnoreCase);
        if (n.SetEquals(vivas)) return;
        vivas = n; AoMudarCopias?.Invoke();
    }

    /// <summary>Partial change from the UI: vol / m / segue (null = keep).</summary>
    public void VolumeCopia(string ap, string x, int? vol, bool? m, bool? segue)
    {
        var k = ap + "|" + x;
        lock (tEstado)
        {
            var cv = new Dictionary<string, CopiaVol>(CopiaVols());
            var c = cv.TryGetValue(k, out var o) ? o : new(100, false, true);
            c = new(vol is { } v ? Math.Clamp(v, 0, 100) : c.Vol, m ?? c.M, segue ?? c.Segue);
            cv[k] = c; cvs = cv;
            estado[ChaveCopiaVol] = JsonSerializer.SerializeToElement(cv.ToDictionary(p => p.Key, p => new { vol = p.Value.Vol, m = p.Value.M ? 1 : 0, segue = p.Value.Segue ? 1 : 0 }));
            Base.GravarEstado(estado);
        }
        AoMudarCopias?.Invoke();
        StatusCopiaVol();
    }

    public void StatusCopiaVol() => Enviar("copiavol", CopiaVols().ToDictionary(p => p.Key, p => new { vol = p.Value.Vol, m = p.Value.M ? 1 : 0, segue = p.Value.Segue ? 1 : 0 }));
}
