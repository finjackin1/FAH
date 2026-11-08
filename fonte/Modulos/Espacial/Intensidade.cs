// 08/11 user: Estéreo+ and 7.1 "quase não mostram diferença pra off ... deixar isso mais amostra sem ferrar o audio" -> "Intensidade" slider.
// One knob t (0..1) for the side part S = (L-R)/2 of the fronts: Plano·S stays plain at the ears, Caixa·S goes to the virtual speakers.
// The voice (L+R)/2 is never touched, so a centered voice sounds the same at any setting. 0.5 = the approved sound (plain 1, speakers 0.5);
// 0 = no speakers (E+ off for the sides); 1 = plain 0.7, speakers 1.4 (plain side kept high: lowering it pulls hard pans to the middle).
namespace FAH.Espacial;

public static class Intensidade
{
    public const double Padrao = 1.0;   // 08/11 user: default 100 for both (E+ and 7.1)
    public static (double Plano, double Caixa) Mapa(double t)
    {
        t = Math.Clamp(t, 0, 1);
        if (t <= 0.5) return (1, t);
        double u = (t - 0.5) / 0.5;
        return (1 - 0.3 * u, 0.5 + 0.9 * u);
    }
}
