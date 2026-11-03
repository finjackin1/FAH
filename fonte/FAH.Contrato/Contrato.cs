// Contract shared by FAH.exe and every module .dll. Change = bump Versao.Contrato (old .dll refused).
using System.Text.Json;

namespace FAH.Contrato;

public static class Versao { public const int Contrato = 1; }

/// <summary>Marks the module class inside a .dll (the loader looks for it).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuloFAH(int contrato) : Attribute { public int Contrato { get; } = contrato; }

/// <summary>Which signal path an effect lives on.</summary>
public enum Lado { Som, Mic }

/// <summary>One effect in a chain. Runs on the processing thread: no allocation in steady state, no exceptions.</summary>
public interface IEfeito
{
    string Id { get; }
    Lado Lado { get; }
    /// <summary>Fixed place in the chain (lower runs first). Som: 10 spatial, 20 crossfeed, 30 suppressor, 40 EQ, 50 tone, 60 smart volume, 70 stereo, 80 reverb, 90 boost, 95 limiter.</summary>
    int Posicao { get; }
    void Preparar(int taxa);
    /// <summary>In place on interleaved stereo float (L R L R ...), <paramref name="quadros"/> frames.</summary>
    void Processar(Span<float> lr, int quadros);
}

/// <summary>Optional: effect that takes the raw 7.1 input (cable in 8 channels) instead of the host's stereo downmix.
/// Added without a contract bump: old modules simply don't implement it.</summary>
public interface IEfeitoMulticanal : IEfeito
{
    /// <summary>True while it wants the 8 channels (switch on or still fading out).</summary>
    bool QuerMulticanal { get; }
    /// <summary>src = interleaved 8 channels (FL FR FC LFE BL BR SL SR) -> lr stereo, same frame count. Replaces Processar for that block.</summary>
    void ProcessarMulticanal(ReadOnlySpan<float> src, Span<float> lr, int quadros);
}

/// <summary>What the host offers to modules.</summary>
public interface IHost
{
    /// <summary>Message to the UI (JS receives { tipo, dados }).</summary>
    void Enviar(string tipo, object dados);
    /// <summary>Module data folder (created on demand).</summary>
    string PastaDados(string id);
    void Log(string texto);
    int Taxa { get; }
}

/// <summary>A feature module (= 1 .dll). Its UI screen lives in ui\modulos\&lt;id&gt;.js.</summary>
public interface IModulo
{
    string Id { get; }
    string Nome { get; }
    IReadOnlyList<IEfeito> Efeitos { get; }
    void Iniciar(IHost host);
    /// <summary>UI changed a state key (e.g. "eq2"). Return true if this module owns it.</summary>
    bool Receber(string chave, JsonElement valor);
    void Parar() { }
}
