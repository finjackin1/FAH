// Som 7.1 module: UI state "s71" (ui\modulos\efeitos71.js, fixed: Música + head h11 + voz + sub) + "ligado.som71" (dock toggle "7.1") -> Motor, chain position 10.
// Stereo content is spread over virtual speakers (upmix); a cable in 7.1 (8 channels) feeds them directly.
// s71.auto = profile chosen by the program playing (Conteudo.cs).
using System.Text.Json;
using FAH.Contrato;

namespace FAH.Espacial;

[ModuloFAH(Versao.Contrato)]
public sealed class Modulo : IModulo
{
    // UI speaker ids -> engine roles (FL FR C BL BR SL SR)
    static readonly Dictionary<string, int> Papel = new() { ["FL"] = 0, ["FR"] = 1, ["C"] = 2, ["RL"] = 3, ["RR"] = 4, ["SL"] = 5, ["SR"] = 6 };
    static readonly Dictionary<string, int> Perfil = new() { ["Música"] = 0, ["Filme"] = 1, ["Jogo"] = 2 };

    readonly Motor motor = new();
    readonly Lock trava = new();
    readonly double[] ang = (double[])Brir.Ang71.Clone();
    int tipo = 0, sala = 0; bool on = true;                 // 29/10 default: Música
    string ouvido = "h11";                                 // HRIR set (Brir.Ouvidos), s71.ouvido; user picked "2" = h11 (29/10)
    string ultimoTeste = "";
    Task? preparo;
    // automatic profile (Conteudo): poll every 2 s while "auto" and the switch are on; UI gets "s71auto" {perfil, fonte}
    static readonly Dictionary<string, int> SalaDoPerfil = new() { ["Música"] = 0, ["Jogo"] = 1, ["Filme"] = 2 };    // ui PERFIS 17/50/83
    bool auto; IHost? host; Timer? relogio; Conteudo.Detector? detector; int lendo;

    public string Id => "som71";
    public string Nome => "Som 7.1";
    public IReadOnlyList<IEfeito> Efeitos { get; }
    public Motor Motor => motor;

    public Modulo() => Efeitos = [new Efeito(motor)];

    public void Iniciar(IHost h)
    {
        host = h;
        relogio = new Timer(_ => Observar(), null, 2000, 2000);
        Preparar();
    }

    public void Parar() => relogio?.Dispose();

    void Observar()
    {
        if (!auto || !on || Interlocked.Exchange(ref lendo, 1) == 1) return;
        try
        {
            var achados = Conteudo.Tocando();
            Conteudo.Detector d;
            lock (trava) d = detector ??= new(Perfil.First(p => p.Value == tipo).Key);
            if (!d.Observar(achados, Environment.TickCount64 / 1000.0)) return;
            host?.Log($"som71 automático: {d.Tipo} ({d.Fonte})");
            lock (trava) { tipo = Perfil[d.Tipo]; sala = SalaDoPerfil[d.Tipo]; }
            if (Brir.Pronto) Reconstruir();
            host?.Enviar("s71auto", new { perfil = d.Tipo, fonte = d.Fonte });
        }
        catch (Exception e) { host?.Log("som71 automático: " + e.Message); }
        finally { lendo = 0; }
    }

    /// <summary>Loads the HRTF set in the background, then builds the filters (the engine passes sound through until then).</summary>
    public Task Preparar() => preparo ??= Task.Run(() => { Brir.Preparar(); Reconstruir(); });

    public bool Receber(string chave, JsonElement v)
    {
        if (chave == "ligado")
        {
            on = Bool(v, "som71", true);          // 29/10: 7.1 is a dock toggle, no longer tied to the Efeitos switch
            motor.Ligado = on;
            return false;                       // shared key
        }
        if (chave != "s71") return false;
        lock (trava)
        {
            bool eraAuto = auto;
            auto = Bool(v, "auto", false);
            if (v.TryGetProperty("perfil", out var p) && p.ValueKind == JsonValueKind.String && Perfil.TryGetValue(p.GetString()!, out var t)) tipo = t;      // same order as Brir.Tipos
            if (auto && !eraAuto) detector = null;           // starts from the profile on screen
            else if (auto && detector != null) tipo = Perfil[detector.Tipo];
            if (v.TryGetProperty("ouvido", out var ou) && ou.ValueKind == JsonValueKind.String) ouvido = ou.GetString()!;
            double s = Num(v, "sala", 50);
            sala = s < 34 ? 0 : s < 67 ? 1 : 2;
            if (v.TryGetProperty("ang", out var a) && a.ValueKind == JsonValueKind.Object)
                foreach (var (id, r) in Papel) if (a.TryGetProperty(id, out var x) && x.ValueKind == JsonValueKind.Number) ang[r] = x.GetDouble();
            v.TryGetProperty("vol", out var vol); v.TryGetProperty("mudo", out var mudo);
            foreach (var (id, r) in Papel) motor.Ganho[r] = GanhoDe(vol, mudo, id);
            motor.GanhoLfe = GanhoDe(vol, mudo, "LFE");
            motor.Voz = Bool(v, "voz", true); motor.Sub = Bool(v, "sub", true);
        }
        if (ultimoTeste == "" && !v.TryGetProperty("teste", out _)) ultimoTeste = "-";   // nothing saved: the 1st test click must play
        if (v.TryGetProperty("teste", out var te) && te.ValueKind == JsonValueKind.String && te.GetString() is { } ts && ts != ultimoTeste)
        {
            bool primeiro = ultimoTeste == ""; ultimoTeste = ts;
            if (!primeiro && Papel.TryGetValue(ts.Split(':')[0], out var cx)) motor.Testar(cx);
        }
        if (Brir.Pronto) Reconstruir();
        return true;
    }

    static double GanhoDe(JsonElement vol, JsonElement mudo, string id)
    {
        if (mudo.ValueKind == JsonValueKind.Object && mudo.TryGetProperty(id, out var m) && (m.ValueKind == JsonValueKind.True)) return 0;
        return vol.ValueKind == JsonValueKind.Object && vol.TryGetProperty(id, out var x) && x.ValueKind == JsonValueKind.Number ? Math.Clamp(x.GetDouble(), 0, 100) / 100 : 1;
    }

    (int, int, string, string) feito = (-1, -1, "", "");

    void Reconstruir()
    {
        lock (trava)
        {
            var chave = (tipo, sala, string.Join(',', ang), ouvido);
            if (chave == feito) return;
            bool soAngulos = feito.Item1 == tipo && feito.Item2 == sala && feito.Item4 == ouvido;
            feito = chave;
            Brir.Usar(ouvido);
            if (!soAngulos || motor.PedidoEst == null) motor.PedidoEst = Ajustes.Construir(false, tipo, sala, ang);   // stereo layout ignores the angles
            motor.Pedido71 = Ajustes.Construir(true, tipo, sala, ang);
        }
    }

    static bool Bool(JsonElement o, string k, bool pad) => o.TryGetProperty(k, out var x)
        ? x.ValueKind == JsonValueKind.True || (x.ValueKind == JsonValueKind.Number && x.GetDouble() != 0) : pad;
    static double Num(JsonElement o, string k, double pad) => o.TryGetProperty(k, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetDouble() : pad;

    sealed class Efeito(Motor m) : IEfeitoMulticanal
    {
        public string Id => "som71";
        public Lado Lado => Lado.Som;
        public int Posicao => 10;
        public bool QuerMulticanal => m.Ativo;
        public void Preparar(int taxa) { }
        public void Processar(Span<float> lr, int quadros) => m.Processar(lr, 2, lr, quadros);
        public void ProcessarMulticanal(ReadOnlySpan<float> src, Span<float> lr, int quadros) => m.Processar(src, 8, lr, quadros);
    }
}
