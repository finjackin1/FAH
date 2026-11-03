// Crossfeed (bs2b method, MIT): each ear also gets the other side,
// low-passed and lower (the head shadows treble); a shelf on the direct path keeps the balance.
// Levels (cut Hz, cross dB below): suave = Jan Meier 650/9.5; medio = Chu Moy 700/6; forte = bs2b 700/4.5.
// Not with Som 7.1 (its HRTF already crosses): Permitido=false fades out without touching Ligado.
namespace FAH.Efeitos;

public sealed class Crossfeed
{
    static (double fc, double feed) Valores(string n) => n switch { "suave" => (650, 9.5), "forte" => (700, 4.5), _ => (700, 6) };

    int taxa = 48000, rampa = 2400;          // 50 ms: on/off/level change ramps, no thump
    string feito = "";
    double loB, loA, hiB0, hiB1, hiA, mix;
    double loL, loR, hiL, hiR, xL, xR;       // filter state

    public bool Ligado { get; set; }
    public bool Permitido { get; set; } = true;
    public string Nivel { get; set; } = "medio";

    public void Preparar(int t) { taxa = t; rampa = (int)(0.05 * t); feito = ""; }

    void Coeficientes(string n)
    {
        var (fcut, feed) = Valores(n);
        double gbLo = feed * -5 / 6 - 3, gbHi = feed / 6 - 3;
        double gLo = Math.Pow(10, gbLo / 20), gHi = 1 - Math.Pow(10, gbHi / 20);
        double fcHi = fcut * Math.Pow(2, (gbLo - 20 * Math.Log10(gHi)) / 12);
        double x = Math.Exp(-2 * Math.PI * fcut / taxa);
        loB = gLo * (1 - x); loA = x;
        x = Math.Exp(-2 * Math.PI * fcHi / taxa);
        hiB0 = 1 - gHi * (1 - x); hiB1 = -x; hiA = x;
        feito = n;
    }

    public void Processar(Span<float> lr, int quadros)
    {
        double alvo = Ligado && Permitido ? 1 : 0;
        if (alvo == 0 && mix == 0) { loL = loR = hiL = hiR = xL = xR = 0; return; }
        var n = Nivel;
        if (n != feito) Coeficientes(n);
        double passo = 1.0 / rampa;
        for (int i = 0; i < quadros; i++)
        {
            double l = lr[2 * i], r = lr[2 * i + 1];
            loL = loB * l + loA * loL; loR = loB * r + loA * loR;
            hiL = hiB0 * l + hiB1 * xL + hiA * hiL; hiR = hiB0 * r + hiB1 * xR + hiA * hiR;
            xL = l; xR = r;
            if (mix != alvo) mix = alvo > mix ? Math.Min(alvo, mix + passo) : Math.Max(alvo, mix - passo);
            lr[2 * i] = (float)(l + (hiL + loR - l) * mix);
            lr[2 * i + 1] = (float)(r + (hiR + loL - r) * mix);
        }
    }
}
