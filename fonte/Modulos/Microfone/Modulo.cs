// Microfone module (Mic side): noise (UI "ruido"), voice changer ("voz") and the voice tools of the mic EQ screen ("vm").
// The mic EQ itself lives in the Equalizador .dll (key "eqmic"; its LIMIT/teto drive the mic limiter here). Switches: "ligado" {ruido, voz, eqmic}.
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Microfone;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    public readonly Mono Mono = new();
    public readonly Corte Corte = new();
    public readonly Supressor Supressor = new();
    public readonly Portao Portao = new();
    public readonly Voz Voz = new();
    public readonly DeEsser DeEsser = new();
    public readonly Nivelador Nivelador = new();
    public readonly AutoVolume AutoVolume = new();
    public readonly LimitadorMic Limitador = new();
    public readonly Medidor Medidor = new();
    IHost? host;
    Timer? relogio;
    bool onRuido = true, onVoz = true, onEq = true;
    JsonElement? ruido, voz, vm;

    public string Id => "mic";
    public string Nome => "Microfone";
    public IReadOnlyList<IEfeito> Efeitos { get; }

    public Modulo() => Efeitos = [Mono, Corte, new Carrega(this), Portao, Voz, DeEsser, Nivelador, AutoVolume, Limitador, Medidor];

    public void Iniciar(IHost h)
    {
        host = h;
        // voice level for the mic screens, only while the mic path runs
        float ultPico = -1; int tique = 0; string ultEstado = "";
        relogio = new Timer(_ =>
        {
            // live check: one log line each time the noise switches change (voice running)
            if (++tique % 10 == 0 && Medidor.Pico > 0) { var e = $"ruído={Supressor.Ligado} portão={Portao.Ligado} portão-voz={Supressor.Portao} agc={AutoVolume.Ligado}"; if (e != ultEstado) { ultEstado = e; host?.Log($"mic: {e}; supressor {Supressor.Estado}"); } }
            var pk = Medidor.Pico; Medidor.Pico = 0;
            if (pk == 0 && ultPico == 0) return;
            ultPico = pk;
            host?.Enviar("micvoz", new { rms = Math.Round(Medidor.RmsDb, 1), pico = Math.Round(pk, 3), ganho = Math.Round(AutoVolume.GanhoDb, 1), aberto = Supressor.Portao ? Supressor.Aberto : Portao.Aberto,
                ia = Supressor.Erro == null ? null : "O cancelamento com IA não abriu: " + Supressor.Erro });
        }, null, 1000, 100);
    }

    public bool Receber(string chave, JsonElement v)
    {
        switch (chave)
        {
            case "ligado":
                onRuido = Flag(v, "ruido", true); onVoz = Flag(v, "voz", true); onEq = Flag(v, "eqmic", true);
                Aplicar(); return false;                 // shared key
            case "ruido": ruido = v.Clone(); Aplicar(); return true;
            case "voz": voz = v.Clone(); Aplicar(); return true;
            case "vm": vm = v.Clone(); Aplicar(); return true;
            case "eqmic":
                Limitador.TetoDb = Bool(v, "limit", false) && Num(v, "teto", -1) < 0 ? (float)Num(v, "teto", -1) : -1;
                return true;
        }
        return false;
    }

    static bool Flag(JsonElement v, string k, bool pad) =>
        v.ValueKind != JsonValueKind.Object || !v.TryGetProperty(k, out var x) ? pad : x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0);
    static double Num(JsonElement? v, string k, double pad) =>
        v is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : pad;
    static bool Bool(JsonElement? v, string k, bool pad) => v is { ValueKind: JsonValueKind.Object } o ? Flag(o, k, pad) : pad;

    /// <summary>UI state -> effects. Defaults = the screens' defaults.</summary>
    public void Aplicar()
    {
        Supressor.Ligado = onRuido && Bool(ruido, "ia", true);
        Supressor.Forca = (float)(Num(ruido, "forca", 70) / 100);
        // 31/10: with the AI on, the gate opens on speech (model's lsnr), not on level; level gate only without the model
        bool gate = onRuido && Bool(ruido, "gate", true);
        Supressor.Portao = gate && Supressor.Ligado && Supressor.Erro == null;
        Portao.Ligado = gate && !Supressor.Portao;
        Portao.Limite = (float)(Num(ruido, "lim", 35) / 100);
        Supressor.Limiar = 15f * Portao.Limite;
        Portao.SoltarMs = Supressor.SoltarMs = (int)Math.Clamp(Num(ruido, "soltar", 250), 50, 2000);

        Voz.Tom = (float)Math.Clamp(Num(voz, "tom", 0), -12, 12);
        Voz.Timbre = (float)Math.Clamp(Num(voz, "tim", 0), -100, 100);
        Voz.Robo = (float)(Num(voz, "rob", 0) / 100); Voz.Eco = (float)(Num(voz, "eco", 0) / 100); Voz.Radio = (float)(Num(voz, "rad", 0) / 100);
        Voz.Mix = (float)(Num(voz, "mix", 100) / 100);
        Voz.Ligado = onVoz;

        Corte.Ligado = onEq && Bool(vm, "corte", true);
        Nivelador.Forca = onEq ? (float)(Num(vm, "comp", 50) / 100) : 0;
        DeEsser.Forca = onEq ? (float)(Num(vm, "sss", 40) / 100) : 0;
        AutoVolume.Ligado = onEq && Bool(vm, "ganho", true);
        AutoVolume.AlvoDb = (float)(-60 + 0.6 * Num(vm, "alvo", 70));
    }

    public void Parar() { relogio?.Dispose(); Supressor.Dispose(); }

    /// <summary>Suppressor slot: the model loads in the background the first time it is needed (voice passes dry meanwhile).</summary>
    sealed class Carrega(Modulo m) : IEfeito
    {
        int pedido;
        public string Id => m.Supressor.Id;
        public Lado Lado => Lado.Mic;
        public int Posicao => m.Supressor.Posicao;
        public void Preparar(int taxa) => m.Supressor.Preparar(taxa);
        public void Processar(Span<float> lr, int q)
        {
            if (m.Supressor.Ligado && Interlocked.Exchange(ref pedido, 1) == 0)
                Task.Run(() => { m.Supressor.Carregar(); if (m.Supressor.Erro != null) { m.host?.Log("supressor: " + m.Supressor.Erro); m.Aplicar(); } });
            m.Supressor.Processar(lr, q);
        }
    }
}
