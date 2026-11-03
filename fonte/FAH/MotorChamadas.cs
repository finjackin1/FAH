// Call programs skip FAH (user 31/10: Discord voice centered and dry, no 7.1 room). While FAH plays, each call program is pinned
// (per-app default device, same as a Mixer pin) to FAH's real output, so its sound never enters the cable. FAH off/exit = unpinned
// (follows the Windows default again). A Mixer pin of the user wins. Crash: the guardian unpins. Checked every 2 s; also right
// before FAH takes the Windows default (a playing stream only moves on a default change, not on a pin).
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FAH.Audio;

namespace FAH;

sealed partial class Motor
{
    static readonly HashSet<string> Chamadas = new(StringComparer.OrdinalIgnoreCase) { "discord", "discordptb", "discordcanary",
        "teamspeak", "ts3client_win64", "slack", "teams", "ms-teams", "zoom", "skype", "whatsapp", "whatsapp.root", "telegram", "signal" };
    static string ArqChamadas => Path.Combine(Base.Dados, "chamadas-presas.json");
    // true = call apps play straight to the real output, outside FAH. User 01/11: wants this back (centered voice, no 7.1 room).
    // false = calls go through FAH like any program (old pins are released).
    const bool ChamadasDiretas = true;

    readonly Dictionary<uint, string> presas = [];               // pid -> device id it was pinned to
    readonly Lock tChamadas = new();
    System.Threading.Timer? vigiaChamadas;
    (string Nome, string Id) saidaId = ("", "");
    int desligada;                                                // passes with FAH off (an output switch closes it for a moment)

    void VigiarChamadas() => vigiaChamadas = new(_ => Chamar(), null, 1000, 2000);

    readonly HashSet<uint> recusadas = [];                        // helper processes Windows refuses (E_INVALIDARG): skipped, no log spam

    void Chamar()
    {
        if (!tChamadas.TryEnter()) return;
        try
        {
            if (!ChamadasDiretas) { Soltar(); return; }
            string? nome = fluxo != null ? destinos.FirstOrDefault()?.Nome : null;
            if (nome == null) { if (++desligada >= 2) Soltar(); return; }
            desligada = 0;
            if (saidaId.Nome != nome) saidaId = (nome, Aparelhos.Listar(true).FirstOrDefault(a => a.Nome == nome)?.Id ?? "");
            Prender(nome, saidaId.Id);
        }
        catch (Exception e) { Log("chamadas: " + e.Message); }
        finally { tChamadas.Exit(); }
    }

    /// <summary>Ligar, right BEFORE the Windows default moves to the cable: open streams (Discord) re-pick their device on that
    /// change, so they land on the pin. A pin alone does not move a stream that is already playing.</summary>
    void PrenderChamadas(Aparelho saida)
    {
        if (!ChamadasDiretas) return;
        lock (tChamadas) try { saidaId = (saida.Nome, saida.Id); desligada = 0; Prender(saida.Nome, saida.Id); } catch (Exception e) { Log("chamadas: " + e.Message); }
    }

    void Prender(string nome, string id)
    {
        if (id == "") return;
        var fixos = FixosSom();
        var vivos = new HashSet<uint>(); var novos = new SortedSet<string>(); int antes = presas.Count;
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                string x; try { x = p.ProcessName; } catch { continue; }
                if (!Chamadas.Contains(x) || fixos.Contains(x)) continue;
                uint pid = (uint)p.Id; vivos.Add(pid);
                if (presas.GetValueOrDefault(pid) == id || recusadas.Contains(pid)) continue;
                try { PorPrograma.Definir(pid, false, id); presas[pid] = id; novos.Add(x); } catch { recusadas.Add(pid); }
            }
        // closed, or pinned in the Mixer meanwhile (the Mixer already set its own device)
        foreach (var pid in presas.Keys.Where(k => !vivos.Contains(k)).ToList()) presas.Remove(pid);
        recusadas.IntersectWith(vivos);
        if (novos.Count > 0) Log($"chamadas direto em {nome} (fora do FAH): {string.Join(", ", novos)}");
        if (novos.Count > 0 || presas.Count != antes) Gravar();
    }

    HashSet<string> FixosSom()
    {
        lock (tEstado)
            try { return estado.TryGetValue("mixfixos", out var v) ? [.. (v.Deserialize<List<Fixo>>() ?? []).Where(f => f.Lado == "som").Select(f => f.X)] : []; }
            catch { return []; }
    }

    /// <summary>Back to following the Windows default (FAH off / exit).</summary>
    void Soltar()
    {
        if (presas.Count == 0) return;
        var fixos = FixosSom();
        foreach (var pid in presas.Keys)
            try { if (Soltavel(pid, fixos)) PorPrograma.Definir(pid, false, null); } catch { }
        Log($"chamadas voltam ao padrão ({presas.Count})");
        presas.Clear(); Gravar();
    }

    /// <summary>Still a call program (pid not reused) and not pinned by the user in the Mixer.</summary>
    static bool Soltavel(uint pid, HashSet<string> fixos) { try { using var p = Process.GetProcessById((int)pid); return Chamadas.Contains(p.ProcessName) && !fixos.Contains(p.ProcessName); } catch { return false; } }

    void Gravar() { try { if (presas.Count == 0) File.Delete(ArqChamadas); else File.WriteAllText(ArqChamadas, JsonSerializer.Serialize(presas.Keys)); } catch { } }

    void PararChamadas()
    {
        vigiaChamadas?.Dispose();
        lock (tChamadas) Soltar();
    }

    /// <summary>Guardian: FAH died with call programs pinned -> unpin the ones still running.</summary>
    static void SoltarChamadasOrfas()
    {
        try
        {
            if (!File.Exists(ArqChamadas)) return;
            var pids = JsonSerializer.Deserialize<uint[]>(File.ReadAllText(ArqChamadas)) ?? [];
            var est = Base.LerEstado();
            var fixos = est.TryGetValue("mixfixos", out var v) ? (v.Deserialize<List<Fixo>>() ?? []).Where(f => f.Lado == "som").Select(f => f.X).ToHashSet() : [];
            foreach (var pid in pids) try { if (Soltavel(pid, fixos)) PorPrograma.Definir(pid, false, null); } catch { }
            File.Delete(ArqChamadas);
            Base.Log($"guardião soltou as chamadas ({pids.Length})");
        }
        catch { }
    }
}
