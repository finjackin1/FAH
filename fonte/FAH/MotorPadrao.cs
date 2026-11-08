// Windows default watch (31/10, user): while FAH runs, the Windows defaults stay on its cables (sound = Line 1, mic = Line 2).
// The user picks another default in Windows -> that device becomes FAH's real output / real mic and Windows goes back to the cable.
// Another virtual device picked -> Windows goes back to the cable, FAH's device unchanged. FAH path off = left alone.
using FAH.Audio;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace FAH;

sealed partial class Motor
{
    MMDeviceEnumerator? enumPadrao;
    AvisoPadrao? avisoPadrao;
    System.Threading.Timer? relSom, relMicP;
    volatile string? novoSom, novoMic;
    volatile bool encerrando;                      // set first on exit: nothing may switch a default back to the cable after that

    /// <summary>Start watching (after the paths are up; events before that are ignored anyway).</summary>
    void VigiarPadrao()
    {
        try
        {
            relSom = new(_ => Tratar(true), null, Timeout.Infinite, Timeout.Infinite);
            relMicP = new(_ => Tratar(false), null, Timeout.Infinite, Timeout.Infinite);
            enumPadrao = new MMDeviceEnumerator();
            avisoPadrao = new AvisoPadrao(this);
            enumPadrao.RegisterEndpointNotificationCallback(avisoPadrao);
        }
        catch (Exception e) { Log("vigiar padrão: " + e.Message); }
    }

    void PararVigiaPadrao()
    {
        encerrando = true;
        try { if (enumPadrao != null && avisoPadrao != null) enumPadrao.UnregisterEndpointNotificationCallback(avisoPadrao); } catch { }
        enumPadrao?.Dispose(); enumPadrao = null; avisoPadrao = null;
        relSom?.Dispose(); relMicP?.Dispose(); vigiaCabos?.Dispose();
    }

    /// <summary>COM callback thread: only note it; the switch runs 400 ms later (Windows fires once per role).</summary>
    /// <param name="multimidia">false = console/communications role: only pulled back to the cable (Discord follows communications).</param>
    void PadraoMudou(bool som, string id, bool multimidia)
    {
        if (encerrando) return;
        try
        {
            if (som) { if (multimidia) novoSom = id; relSom?.Change(400, Timeout.Infinite); }
            else { if (multimidia) novoMic = id; relMicP?.Change(400, Timeout.Infinite); }
        }
        catch (ObjectDisposedException) { }                    // exit disposed the timers between the check and the call
    }

    void Tratar(bool som)
    {
        try
        {
            var id = som ? novoSom : novoMic;
            if (encerrando) return;
            string? cabo = som ? (fluxo != null ? Cabo?.Cabo?.Tocar.Id : null) : caboMic?.Captar.Id;
            if (cabo == null) return;                                     // path off
            // multimedia moved (and still there) -> old rules; else only the other roles may be off the cable
            var a = id != null && id != cabo && PadraoWindows.Atual(som) == id ? Aparelhos.Listar(som).FirstOrDefault(x => x.Id == id) : null;
            if (a == null)
            {
                if (PadraoWindows.TodosEm(cabo, som)) return;
                lock (trava)
                {
                    string? ainda = som ? (fluxo != null ? Cabo?.Cabo?.Tocar.Id : null) : caboMic?.Captar.Id;
                    if (encerrando || ainda != cabo) return;
                    PadraoWindows.Definir(cabo, som);
                }
                Log($"padrão de comunicação do Windows ({(som ? "som" : "microfone")}) saiu do cabo: voltou para o cabo do FAH");
                return;
            }
            if (a.Virtual)
            {
                lock (trava)
                {
                    // re-check under the lock: Desligar/exit may have given the default back meanwhile
                    string? ainda = som ? (fluxo != null ? Cabo?.Cabo?.Tocar.Id : null) : caboMic?.Captar.Id;
                    if (encerrando || ainda != cabo) return;
                    PadraoWindows.Definir(cabo, som);
                }
                Log($"padrão do Windows ({(som ? "som" : "microfone")}) foi para {a.Nome}: voltou para o cabo do FAH");
                return;
            }
            Log($"padrão do Windows ({(som ? "som" : "microfone")}) trocado para {a.Nome}: vira o aparelho do FAH, o Windows volta para o cabo");
            TrocarPadrao(!som, a.Id);
            if (som) Status(); else StatusMic();
        }
        catch (Exception e) { Log("padrão mudou: " + e.Message); }
    }

    sealed class AvisoPadrao(Motor m) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (!string.IsNullOrEmpty(defaultDeviceId)) m.PadraoMudou(flow == DataFlow.Render, defaultDeviceId, role == Role.Multimedia);
        }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => m.AparelhoMudou();
        public void OnDeviceAdded(string pwstrDeviceId) => m.AparelhoMudou();
        public void OnDeviceRemoved(string deviceId) => m.AparelhoMudou();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
