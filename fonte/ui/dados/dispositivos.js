// Sample data for the plain-browser prototype; FAH.exe injects window.FAH_DISP. Sides + their devices.
App.lado({ id: 'som', nome: 'Som', icone: 'fone', resumo: 'O que sai nos seus fones', ordem: 1 });
App.lado({ id: 'mic', nome: 'Microfone', icone: 'mic', resumo: 'O que os outros ouvem de você', ordem: 2 });

// f = physical (has format/latency), p = Windows default, apps = virtual channel apps, destinos = where it goes
App.estado.dispositivos = {
  som: [
    { id: 'fone', n: 'Fone de ouvido', t: 'Fone', f: 1, p: 1, ic: 'fone', vol: 72 },
    { id: 'caixa', n: 'Caixas de som', t: 'Saída', f: 1, ic: 'fone', vol: 60 },
    { id: 'jogo', n: 'Jogo', t: 'Canal virtual', ic: 'mix', vol: 85, apps: ['Valorant', 'Steam'] },
    { id: 'chat', n: 'Chat', t: 'Canal virtual', ic: 'mix', vol: 70, apps: ['Discord'] },
    { id: 'musica', n: 'Música', t: 'Canal virtual', ic: 'mix', vol: 55, apps: ['Spotify', 'Chrome'] },
  ],
  mic: [
    { id: 'micfone', n: 'Mic do fone', t: 'Microfone', f: 1, p: 1, ic: 'mic', vol: 80 },
    { id: 'webcam', n: 'Webcam', t: 'Microfone', f: 1, ic: 'mic', vol: 65 },
    { id: 'tratado', n: 'Mic tratado', t: 'Saída virtual (com efeitos)', ic: 'mix', vol: 90 },
    { id: 'live', n: 'Para a live', t: 'Saída virtual', ic: 'mix', vol: 75 },
  ],
};
App.estado.destinos = { som: ['Fone de ouvido', 'Caixas de som'], mic: ['Discord', 'OBS', 'Gravação'] };
// inside FAH.exe: this PC's real devices replace the samples (virtual channels/programs come with the Router)
if (window.FAH_DISP) { App.estado.dispositivos = window.FAH_DISP;
  App.estado.destinos = { som: FAH_DISP.som.map((d) => d.n), mic: [] }; }
