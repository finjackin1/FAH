// Virtual cable detection by DRIVER (endpoint interface name), not by endpoint name (user can rename "Line 1" to anything).
// Accepted (user 29/10): VAC (only one with 7.1 switch), VB-CABLE (+A/B), Voicemeeter (VAIO/AUX/VAIO3: loopback of its input,
// so it works without the Voicemeeter app routing). Never blocks the caller silently: 3 s worker, any failure = "servico".
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace FAH.Audio;

/// <summary>Tocar = what Windows default points to. Captar = endpoint FAH records (Loopback: Tocar itself, loopback mode).</summary>
public sealed record Cabo(string Marca, Aparelho Tocar, Aparelho Captar, bool Loopback)
{
    public bool Vac => Marca == "Virtual Audio Cable";
}

/// <summary>Estado: achou | falta | desativado | servico. Desativados = endpoint names to turn on (estado desativado).
/// Ligar = id of the only side missing from a half-on pair (FAH turns it on itself: Motor.LigarOutroLado).</summary>
public sealed record Achado(string Estado, Cabo? Cabo, string? Marca, string[] Desativados, string Detalhe, string? Ligar = null);

public static class Cabos
{
    // driver family by interface name (DeviceFriendlyName). Order = preference when several are installed.
    static readonly (string Marca, Func<string, bool> E)[] Familias =
    [
        ("Virtual Audio Cable", n => n.Contains("Virtual Audio Cable", StringComparison.OrdinalIgnoreCase)),
        ("VB-CABLE", n => n.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) && n.Contains("Cable", StringComparison.OrdinalIgnoreCase)),
        ("Voicemeeter", n => n.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase)),
    ];
    // FAH's renamed VAC first (this PC: "Saída" -> "Saída -_NS"), then factory names
    static readonly (string Tocar, string Captar)[] Preferidos = [("Saída", "Saída -_NS"), ("Line 1", "Line 1")];
    // FAH mic = a SECOND pair of the same driver (old FAH convention: FAH plays "Entrada -_NS", apps record "Entrada"; VAC factory "Line 2")
    static readonly (string Tocar, string Captar)[] PreferidosMic = [("Entrada -_NS", "Entrada"), ("Line 2", "Line 2"), ("CABLE-A Input", "CABLE-A Output")];

    public static Func<string, bool>? Teste;   // tests: replaces the Windows audio service check
    public static Action<string>? Log;

    public static string? Familia(string interfaceNome) => Familias.FirstOrDefault(f => f.E(interfaceNome)).Marca;

    /// <summary>Endpoint as seen by the detector (tests build these by hand).</summary>
    public sealed record Ponta(string Id, string Desc, string Interface, bool Saida, DeviceState Estado, bool Padrao)
    {
        public string Nome => $"{Desc} ({Interface})";
    }

    public static Achado Detectar(int limiteMs = 3000)
    {
        Achado? r = null; Exception? erro = null;
        var th = new Thread(() => { try { r = Escolher(Listar()); } catch (Exception e) { erro = e; } }) { IsBackground = true, Name = "cabos" };
        th.SetApartmentState(ApartmentState.MTA);
        th.Start();
        if (!th.Join(limiteMs)) r = new("servico", null, null, [], $"o som do Windows não respondeu em {limiteMs / 1000} s");
        else if (erro != null) r = new("servico", null, null, [], erro.GetType().Name + ": " + erro.Message);
        else if (!ServicoRodando()) r = new("servico", null, null, [], "serviço Audiosrv parado");
        Log?.Invoke($"cabo: {r!.Estado} {r.Cabo?.Marca ?? r.Marca} {r.Cabo?.Tocar.Nome} -> {r.Cabo?.Captar.Nome}{(r.Cabo?.Loopback == true ? " (loopback)" : "")} {string.Join(" + ", r.Desativados)} {r.Detalhe}".TrimEnd());
        return r;
    }

    /// <summary>FAH mic cable: another pair than the Som one (<paramref name="som"/>). Tocar = where FAH plays the clean voice,
    /// Captar = what Windows/Discord record. Voicemeeter has no second pair FAH can use.</summary>
    /// <summary>What a cable re-check must do. TrocarSom = reopen Som on the new cable; LigarSom = first cable ever seen, Som comes on
    /// (Som the user turned off with a cable present stays off); RefazerMic = mic is running on the pair Som now takes.</summary>
    public readonly record struct PlanoCabo(bool TrocarSom, bool LigarSom, bool RefazerMic, bool LigarMic);

    public static PlanoCabo Replanejar(bool tinhaCabo, string? caboNovo, bool somLigado, string? caboDoSom, string? caboDoMic, bool faltaMic)
    {
        if (caboNovo == null) return default;
        bool refazMic = caboDoMic == caboNovo;
        return new(somLigado && caboDoSom != null && caboDoSom != caboNovo, !tinhaCabo && !somLigado, refazMic, faltaMic || refazMic);
    }

    public static Achado DetectarMic(Cabo? som, int limiteMs = 3000)
    {
        Achado? r = null;
        var th = new Thread(() => { try { r = EscolherMic(Listar(), som); } catch (Exception e) { r = new("servico", null, null, [], e.Message); } }) { IsBackground = true, Name = "cabos-mic" };
        th.SetApartmentState(ApartmentState.MTA); th.Start();
        if (!th.Join(limiteMs)) r = new("servico", null, null, [], $"o som do Windows não respondeu em {limiteMs / 1000} s");
        return r!;
    }

    public static Achado EscolherMic(List<Ponta> pontas, Cabo? som)
    {
        var fora = som == null ? [] : new HashSet<string> { som.Tocar.Id, som.Captar.Id };
        foreach (var (marca, e) in Familias)
        {
            if (marca == "Voicemeeter") continue;
            var ativos = pontas.Where(p => e(p.Interface) && p.Estado == DeviceState.Active && !fora.Contains(p.Id)).ToList();
            if (ativos.Count == 0) continue;
            var c = Par(marca, ativos, PreferidosMic);
            if (c != null) return new("achou", c, marca, [], "");
        }
        return new("falta", null, null, [], "nenhum segundo cabo virtual ligado (o microfone do FAH usa a Line 2 do Virtual Audio Cable)");
    }

    static List<Ponta> Listar()
    {
        if (!ServicoRodando()) throw new InvalidOperationException("serviço Audiosrv parado");
        using var en = new MMDeviceEnumerator();
        var l = new List<Ponta>();
        foreach (var (fluxo, saida) in new[] { (DataFlow.Render, true), (DataFlow.Capture, false) })
        {
            string? pad = null;
            try { pad = en.GetDefaultAudioEndpoint(fluxo, Role.Multimedia).ID; } catch { }
            foreach (var d in en.EnumerateAudioEndPoints(fluxo, DeviceState.Active | DeviceState.Disabled | DeviceState.Unplugged))
            {
                try
                {
                    string inter = Aparelhos.Interface(d), nome = Aparelhos.Nome(d);
                    string desc = inter.Length > 0 && nome.EndsWith($"({inter})") ? nome[..^(inter.Length + 2)].Trim() : nome;
                    l.Add(new(d.ID, desc, inter, saida, d.State, d.ID == pad));
                }
                catch { }
                finally { d.Dispose(); }
            }
        }
        return l;
    }

    /// <summary>Pure choice over a device list (no Windows calls): tested offline.</summary>
    public static Achado Escolher(List<Ponta> pontas)
    {
        string? marcaDesligada = null, ligar = null; var desligados = new List<string>();
        foreach (var (marca, e) in Familias)
        {
            var fam = pontas.Where(p => e(p.Interface)).ToList();
            if (fam.Count == 0) continue;
            var ativos = fam.Where(p => p.Estado == DeviceState.Active).ToList();
            var c = Par(marca, ativos, Preferidos);
            if (c != null) return new("achou", c, marca, [], "");
            if (marcaDesligada == null)
            {
                // installed but not usable: name only what is missing from ONE pair, with its side (both sides of a VAC line have the same name)
                var off = fam.Where(p => p.Estado != DeviceState.Active).ToList();
                if (off.Count > 0)
                {
                    var par = Par(marca, fam.Where(p => p.Estado is DeviceState.Active or DeviceState.Disabled).ToList(), Preferidos);
                    var falta = (par == null ? off.Where(p => p.Estado == DeviceState.Disabled) : off.Where(p => p.Id == par.Tocar.Id || p.Id == par.Captar.Id)).ToList();
                    marcaDesligada = marca; desligados.AddRange(Ordem(marca, falta).Select(p => $"{p.Desc} na seção {(p.Saida ? "Saída" : "Entrada")}").Distinct().Take(2));
                    if (par != null && par.Tocar.Id != par.Captar.Id && falta.Count == 1) ligar = falta[0].Id;   // Voicemeeter's loopback has one endpoint: no "other side"
                }
            }
        }
        return marcaDesligada != null ? new("desativado", null, marcaDesligada, [.. desligados], "", ligar)
            : new("falta", null, null, [], "nenhum cabo virtual instalado");
    }

    static IEnumerable<Ponta> Ordem(string marca, List<Ponta> l) =>
        l.OrderBy(p => p.Saida ? 0 : 1).ThenBy(p => marca == "Voicemeeter" && !p.Desc.Contains("Input", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

    static Aparelho Ap(Ponta p) => new(p.Id, p.Nome, p.Saida, p.Padrao, true);

    static Cabo? Par(string marca, List<Ponta> ativos, (string Tocar, string Captar)[] preferidos)
    {
        var toc = ativos.Where(p => p.Saida).ToList(); var cap = ativos.Where(p => !p.Saida).ToList();
        if (toc.Count == 0) return null;
        if (marca == "Voicemeeter")
        {
            // apps play into "Voicemeeter Input"; FAH records it as loopback (no need for the Voicemeeter app to route)
            var t = toc.FirstOrDefault(p => p.Desc.StartsWith("Voicemeeter Input", StringComparison.OrdinalIgnoreCase))
                ?? toc.FirstOrDefault(p => p.Desc.Contains("Input", StringComparison.OrdinalIgnoreCase)) ?? toc[0];
            return new(marca, Ap(t), Ap(t), true);
        }
        if (cap.Count == 0) return null;
        // same interface (= same cable/line) on both sides
        foreach (var (pt, pc) in preferidos)
        {
            var t = toc.FirstOrDefault(p => p.Desc == pt); var c = t == null ? null : cap.FirstOrDefault(p => p.Desc == pc && p.Interface == t.Interface);
            if (t != null && c != null) return new(marca, Ap(t), Ap(c), false);
        }
        foreach (var t in toc)
        {
            // VB: "CABLE Input" <-> "CABLE Output", "CABLE-A Input" <-> "CABLE-A Output"; VAC: "Line 2" <-> "Line 2"
            string alvo = t.Desc.Replace("Input", "Output", StringComparison.OrdinalIgnoreCase);
            var c = cap.FirstOrDefault(p => p.Interface == t.Interface && p.Desc.Equals(alvo, StringComparison.OrdinalIgnoreCase))
                ?? cap.FirstOrDefault(p => p.Interface == t.Interface && p.Desc.StartsWith(t.Desc, StringComparison.OrdinalIgnoreCase));
            if (c != null) return new(marca, Ap(t), Ap(c), false);
        }
        // renamed line (both sides of one interface, single pair)
        foreach (var g in toc.GroupBy(p => p.Interface))
        {
            var cs = cap.Where(p => p.Interface == g.Key).ToList();
            if (g.Count() == 1 && cs.Count == 1) return new(marca, Ap(g.First()), Ap(cs[0]), false);
        }
        return null;
    }

    // ---- Windows audio service (Audiosrv) running?
    [StructLayout(LayoutKind.Sequential)] struct SvcStatus { public int Tipo, Estado, Aceita, Saida, SaidaSvc, Ponto, Espera; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr OpenSCManagerW(string? m, string? b, int a);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr OpenServiceW(IntPtr h, string n, int a);
    [DllImport("advapi32.dll")] static extern bool QueryServiceStatus(IntPtr h, out SvcStatus s);
    [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr h);

    public static bool ServicoRodando()
    {
        if (Teste != null) return Teste("audiosrv");
        IntPtr m = OpenSCManagerW(null, null, 0x0001), s = IntPtr.Zero;   // SC_MANAGER_CONNECT
        if (m == IntPtr.Zero) return true;                                 // can't ask: let the device list decide
        try
        {
            s = OpenServiceW(m, "Audiosrv", 0x0004);                       // SERVICE_QUERY_STATUS
            return s == IntPtr.Zero || !QueryServiceStatus(s, out var st) || st.Estado == 4;   // SERVICE_RUNNING
        }
        finally { if (s != IntPtr.Zero) CloseServiceHandle(s); CloseServiceHandle(m); }
    }
}
