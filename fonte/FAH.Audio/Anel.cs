// Float ring (stereo by default; 8 for a 7.1 cable) between the thread that produces and the one that consumes (port of sistema/anel.py).
// Full = drops the oldest (latency never grows).
namespace FAH.Audio;

public sealed class Anel(int capacidade, int canais = 2)
{
    readonly float[] buf = new float[capacidade * canais];
    public int Canais => canais;
    readonly int cap = capacidade;
    int ini, n;
    readonly Lock trava = new();

    public long Transbordos { get; private set; }
    public int Count { get { lock (trava) return n; } }

    public void Escrever(ReadOnlySpan<float> lr)
    {
        int m = lr.Length / canais;
        if (m == 0) return;
        lock (trava)
        {
            if (m >= cap) { lr = lr[^(cap * canais)..]; Transbordos += m - cap; m = cap; }
            int sobra = n + m - cap;
            if (sobra > 0) { ini = (ini + sobra) % cap; n -= sobra; Transbordos += sobra; }
            int fim = (ini + n) % cap, p = Math.Min(m, cap - fim);
            lr[..(p * canais)].CopyTo(buf.AsSpan(fim * canais));
            if (p < m) lr[(p * canais)..(m * canais)].CopyTo(buf);
            n += m;
        }
    }

    /// <summary>Up to dest.Length/canais frames; returns frames read.</summary>
    public int Ler(Span<float> dest)
    {
        lock (trava)
        {
            int m = Math.Min(dest.Length / canais, n), p = Math.Min(m, cap - ini);
            buf.AsSpan(ini * canais, p * canais).CopyTo(dest);
            if (p < m) buf.AsSpan(0, (m - p) * canais).CopyTo(dest[(p * canais)..]);
            ini = (ini + m) % cap; n -= m;
            return m;
        }
    }

    public void Descartar(int m) { lock (trava) { m = Math.Min(m, n); ini = (ini + m) % cap; n -= m; } }

    public void Limpar() { lock (trava) ini = n = 0; }
}
