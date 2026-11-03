// Program clone (Mixer "Clonar", 29/10): a program keeps playing where it plays and ALSO plays on the devices ticked for it.
// Copy = process-loopback capture (same as "Mandar para o microfone") pushed into an own output per device, one reader per
// (process, device). Works with FAH on or off. Programs restart = new PIDs: resynced every 1.5 s. State "clones" = { exe: [device name] }.
using System.Text.Json;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    const string ChaveClone = "clones";
    readonly Lock tClone = new();
    readonly Dictionary<string, Destino> saidasClone = [];                       // device name -> own output
    readonly Dictionary<(uint pid, string ap), (CapturaPrograma c, Leitor l)> copias = [];
    readonly HashSet<(uint, string)> copiasRecusadas = [];
    readonly HashSet<string> aparelhosRecusados = [];
    readonly HashSet<string> cloneAvisados = [];                                  // program/device already told to the user (once per run)
    System.Threading.Timer? relClone;

    Dictionary<string, List<string>> Clones()
    {
        var r = new Dictionary<string, List<string>>();
        lock (tEstado)
            if (estado.TryGetValue(ChaveClone, out var v) && v.ValueKind == JsonValueKind.Object)
                foreach (var p in v.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Array)
                        r[p.Name] = [.. p.Value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)];
        return r;
    }

    /// <summary>Tick/untick one device for one program (exe name without .exe).</summary>
    public void Clonar(string x, string aparelho, bool on)
    {
        var cl = Clones();
        if (!cl.TryGetValue(x, out var l)) cl[x] = l = [];
        if (on && !l.Contains(aparelho)) l.Add(aparelho); else if (!on) l.Remove(aparelho);
        if (l.Count == 0) cl.Remove(x);
        lock (tEstado) { estado[ChaveClone] = JsonSerializer.SerializeToElement(cl); Base.GravarEstado(estado); }
        Log((on ? "clonando " : "parou de clonar ") + x + (on ? " em " : " de ") + aparelho);
        if (on) lock (tClone) aparelhosRecusados.Remove(aparelho);   // device plugged back in: try again
        SincronizarClones();
        StatusClones();
    }

    public void StatusClones() => Enviar("clones", Clones());

    void SincronizarClones()
    {
        lock (tClone)
        {
            var cl = Clones();
            if (cl.Count > 0) relClone ??= new System.Threading.Timer(_ => { try { SincronizarClones(); } catch { } }, null, 1500, 1500);
            var quer = new Dictionary<(uint, string), string>();
            if (PidsDe != null)
                foreach (var (x, aps) in cl) foreach (var pid in PidsDe(x)) foreach (var ap in aps) quer[(pid, ap)] = x;
            foreach (var k in copias.Keys.Where(k => !quer.ContainsKey(k)).ToList())
            {
                var (c, l) = copias[k]; c.Dispose(); copias.Remove(k); xCopia.Remove(k);
                if (saidasClone.TryGetValue(k.ap, out var d)) d.Remover(l);
            }
            foreach (var (k, x) in quer.Where(q => !copias.ContainsKey(q.Key) && !copiasRecusadas.Contains(q.Key)))
            {
                if (Saida(k.Item2) is not { } d) continue;
                var l = d.NovoLeitor("clone-" + k.Item1, 60);
                var (ap, xx) = (k.Item2, x); var c = new CapturaPrograma(k.Item1, l) { Ganho = () => GanhoDe(ap, xx) };
                try { c.Abrir(); copias[k] = (c, l); xCopia[k] = x; }
                catch (Exception e) { c.Dispose(); d.Remover(l); copiasRecusadas.Add(k); Log($"clonar (processo {k.Item1}) em {k.Item2}: {e.Message}"); if (cloneAvisados.Add(x)) Aviso(TextoCopia($"Não deu para copiar o som de {x}.")); }
            }
            // outputs nobody uses any more close; nothing cloned = timer stops
            foreach (var (n, d) in saidasClone.Where(p => !copias.Keys.Any(k => k.ap == p.Key)).ToList()) { d.Fechar(); saidasClone.Remove(n); }
            AtualizarVivas(false);
            if (cl.Count == 0 && relClone != null) { relClone.Dispose(); relClone = null; copiasRecusadas.Clear(); }
        }
    }

    Destino? Saida(string nome)
    {
        if (saidasClone.TryGetValue(nome, out var d)) return d;
        if (aparelhosRecusados.Contains(nome)) return null;
        try { d = new Destino(nome, 60); d.Abrir(); saidasClone[nome] = d; return d; }
        catch (Exception e) { aparelhosRecusados.Add(nome); Log($"clonar: {nome} não abriu: {e.Message}"); if (cloneAvisados.Add("|" + nome)) Aviso($"O aparelho {nome} não abriu para receber a cópia."); return null; }
    }

    /// <summary>Per-program capture needs Windows 10 2004 (build 19041): older systems get that reason instead.</summary>
    static string TextoCopia(string padrao) => Environment.OSVersion.Version.Build < 19041 ? "Copiar o som de um programa precisa do Windows 10 versão 2004 ou mais novo." : padrao;

    void FecharClones()
    {
        lock (tClone)
        {
            relClone?.Dispose(); relClone = null;
            foreach (var (c, _) in copias.Values) c.Dispose();
            copias.Clear(); xCopia.Clear(); AtualizarVivas(false);
            foreach (var d in saidasClone.Values) d.Fechar();
            saidasClone.Clear();
        }
    }
}
