// Som 7.1 (29/10): no card any more. User approved Música + head "2" (h11) + Reforçar voz + Graves no sub as the fixed setup;
// the only control left is the "7.1" dock toggle (ligado.som71). Engine key stays App.estado.s71 (fonte\Modulos\Espacial).
// Angles, volumes and room = approved "Meu padrão" (backup/padrao-7.1-2026-10-28). Efeitos screen = 3 cards again. Loads after efeitos.js.
const S71 = (() => {
  const ANG = { FL: -30, C: 0, FR: 30, SR: 100, RR: 145, RL: -145, SL: -100, LFE: null };   // 01/11 night: too lateral (was ±35/±90/±145; cinema ±30/±100/±140, 29/10)
  const FIXO = { perfil: 'Música', sala: 17, auto: false, voz: true, sub: true, ouvido: 'h11' };
  const S = (App.estado.s71 = App.estado.s71 || { mudo: {}, ang: { ...ANG }, vol: Object.fromEntries(Object.keys(ANG).map((k) => [k, 100])) });
  if (S.angV !== 4) { S.ang = { ...ANG }; S.angV = 4; }   // saved older layout -> this one, once
  Object.assign(S, FIXO); delete S.teste;
  // 08/11 user UI is fixed: speaker angles/volumes/mutes only change on the Dev screen (dev mode); outside it, always the approved layout
  if (!window.FAH_DEV) { S.ang = { ...ANG }; S.vol = Object.fromEntries(Object.keys(ANG).map((k) => [k, 100])); S.mudo = {}; }
  App.estado.ligado = App.estado.ligado || {};
  const on = () => App.estado.ligado.som71 !== false;

  // ---- dock toggle "7.1": lit = on. Switching runs a glow through the letters (left to right) both ways ----
  document.head.insertAdjacentHTML('beforeend', `<style>
  .dock .t71 span{grid-area:1/1;font:700 17px/1 var(--fonte);letter-spacing:.3px;pointer-events:none}
  .dock .t71 .b{color:var(--fg3)}
  .dock .t71.apagada{opacity:.4;cursor:default}
  .dock .t71 .l{color:#ffb340;filter:drop-shadow(0 0 3px rgba(255,159,10,.95)) drop-shadow(0 0 9px rgba(255,159,10,.55));clip-path:inset(-40px calc(100% + 40px) -40px -40px)}
  .dock .t71 .f{color:#fff6e0;filter:drop-shadow(0 0 4px #fff) drop-shadow(0 0 10px #ffb340);clip-path:inset(-40px calc(100% + 40px) -40px -40px);opacity:0}
  .dock .t71.lig .l{clip-path:inset(-40px)}
  .dock .t71.acende .l{animation:l71on .75s cubic-bezier(.45,0,.25,1) both}
  .dock .t71.apaga .l{animation:l71off .75s cubic-bezier(.45,0,.25,1) both}
  .dock .t71.acende .f,.dock .t71.apaga .f{animation:f71 .75s cubic-bezier(.45,0,.25,1) both}
  @keyframes l71on{from{clip-path:inset(-40px calc(100% + 40px) -40px -40px)}to{clip-path:inset(-40px -40px -40px -40px)}}
  @keyframes l71off{from{clip-path:inset(-40px -40px -40px -40px)}to{clip-path:inset(-40px -40px -40px calc(100% + 40px))}}
  @keyframes f71{0%{clip-path:inset(-40px calc(100% + 40px) -40px -30%);opacity:1}85%{opacity:1}100%{clip-path:inset(-40px -30% -40px calc(100% + 40px));opacity:0}}
  @media (prefers-reduced-motion:reduce){.dock .t71 .l,.dock .t71 .f{animation-duration:.01s!important}}
  </style>`);
  const c71 = () => App.estado.cabo71 || {};   // engine: {suporte:false, motivo} = this cable can't take 7.1 (Lite etc.)
  const sem = () => App.cabo.semSom();   // no usable cable at all: dimmed and blocked (a stereo cable keeps 7.1: engine upmixes)
  const dica = () => (sem() ? App.cabo.motivo() + ': o Som 7.1 precisa dele' : (on() ? 'Som 7.1 ligado' : 'Som 7.1 desligado') + (c71().suporte === false ? ' · seu cabo só passa estéreo' : ''));   // dock tip = CSS ::after, one line
  let ultMotivo = '';   // full reason as a toast, once per reason, only while 7.1 is on
  const avisa = () => { const m = !sem() && c71().suporte === false && on() ? String(c71().motivo || '') : ''; if (m && m !== ultMotivo && App.toast) App.toast(m, 6000); ultMotivo = m; };
  const pintaDock = () => { const b = document.querySelector('[data-dock=s71]'); if (b) { b.dataset.dica = dica(); b.classList.toggle('apagada', sem()); } };
  addEventListener('fah:cabo71', () => { pintaDock(); avisa(); });
  addEventListener('fah:cabo', pintaDock);
  App.dock({ id: 's71', dica: 'Som 7.1', ordem: 5, classe: () => 't71' + (on() ? ' lig' : '') + (sem() ? ' apagada' : ''), dicaViva: dica,
    html: () => '<span class="b">7.1</span><span class="l">7.1</span><span class="f">7.1</span>',
    acao: () => { if (sem()) { App.toast?.(App.cabo.motivo() + ': o Som 7.1 precisa dele', 4000); return; }
      const v = !on(); App.estado.ligado.som71 = v;
      const b = document.querySelector('[data-dock=s71]');
      if (b) { b.classList.remove('acende', 'apaga', 'lig'); void b.offsetWidth; b.classList.add(v ? 'acende' : 'apaga'); if (v) b.classList.add('lig'); b.dataset.dica = dica(); }
      dispatchEvent(new CustomEvent('fah:s71liga', { detail: v })); avisa(); } });

  // Estéreo/Crossfeed/Reverb controls: same pieces and handlers as the approved screen (efeitos.js)
  const E = Efx.E;
  const lin = (rot, v, d, txt) => `<div class="ln"><span>${rot}</span>${UI.slider(v, d)}<span class="vl">${txt}</span></div>`;
  const ctlEst = () => `${lin('Balanço', Efx.paraBal(E.est.bal), { bal: 1, escala: 'bal', padrao: 50 }, Efx.txtBal(E.est.bal))}${lin('Largura', Efx.paraLarg(E.est.larg), { larg: 1, escala: 'larg', padrao: Efx.paraLarg(100) }, Efx.txtLarg(E.est.larg, E.est.mono))}${UI.linha('Mono', UI.switch(E.est.mono, { mono: 1 }))}`;
  const ctlCf = () => `${UI.segmento(Efx.NIVEIS, E.cf.nivel, { nivel: 1 })}<div class="dica">Cada ouvido escuta um pouco do outro lado, atrasado e sem agudo, como acontece com caixas de som.</div>`;
  const ctlRv = () => `${UI.chips(Object.keys(Efx.PRESETS).map((k) => [k, k === E.rv.preset]), { preset: 1 })}<div class="duas">${Efx.PARAMS.map((n, i) => lin(n, E.rv.p[i], { rp: i, padrao: Efx.deP(E.rv.preset)[i] }, UI.num(E.rv.p[i]) + '%')).join('')}</div>`;
  const fxMudou = (el, e, desenha) => { const { tipo, valor, dados } = e.detail, alvo = e.target, vl = alvo.parentNode.querySelector('.vl');
    if (dados.bal) { E.est.bal = Efx.deBal(valor); vl.textContent = Efx.txtBal(E.est.bal); desenha('est'); }
    if (dados.larg) { E.est.larg = Efx.deLarg(valor); vl.textContent = Efx.txtLarg(E.est.larg, E.est.mono); desenha('est'); }
    if (dados.mono) { E.est.mono = valor; el.querySelector('[data-larg]').parentNode.querySelector('.vl').textContent = Efx.txtLarg(E.est.larg, valor); desenha('est'); }
    if (dados.nivel) { E.cf.nivel = valor; desenha('cf'); }
    if (dados.rp !== undefined && tipo === 'slider') { E.rv.p[+dados.rp] = valor; vl.textContent = UI.num(valor) + '%'; desenha('rv'); }
    if (dados.preset) { E.rv.preset = alvo.textContent; E.rv.p = Efx.deP(E.rv.preset);
      el.querySelectorAll('[data-preset]').forEach((c) => c.classList.toggle('on', c === alvo));
      el.querySelectorAll('[data-rp]').forEach((s) => { const i = +s.dataset.rp; UI.poe(s, E.rv.p[i]); s.dataset.padrao = E.rv.p[i]; s.parentNode.querySelector('.vl').textContent = UI.num(E.rv.p[i]) + '%'; });
      desenha('rv'); } };
  // crossfeed pauses while 7.1 is on (engine: Modulos\Efeitos, same rule)
  const pausaCf = () => on() && !!E.cf.on;
  const marcaPausa = (el) => el.querySelectorAll('[data-p=cf]').forEach((c) => c.classList.toggle('pausa', pausaCf()));
  const NOMES = { est: 'Estéreo', cf: 'Crossfeed', rv: 'Reverb' };
  const SUBS = { est: 'Abrir, fechar ou puxar para um lado', cf: 'Som fora de dentro da cabeça', rv: 'Ambiente em volta do som' };
  const AVISO_CF = '<div class="aviso">Pausado: o Som 7.1 já faz isso.</div>';
  const CTL = { est: ctlEst, cf: ctlCf, rv: ctlRv };
  // one effect card (same look as the approved Efeitos cards)
  const card = (id, d = 0) => `<div class="card entra ${E[id].on ? '' : 'off'} ${id === 'cf' && pausaCf() ? 'pausa' : ''}" data-p="${id}" style="animation-delay:${d}s">
    <header><div><b>${NOMES[id]}</b><small>${SUBS[id]}</small></div>${UI.switch(!!E[id].on, { liga: id })}</header>
    <div class="vista" data-v="${id}"><svg></svg>${id === 'cf' ? AVISO_CF : ''}</div><div class="ctls">${CTL[id]()}</div></div>`;
  const desenhador = (el) => (id) => { const v = el.querySelector(`[data-v=${id}]`); if (!v) return; const s = v.querySelector('svg'), w = v.clientWidth, hh = v.clientHeight;
    if (w < 40 || hh < 40) return;
    // scenes use fixed offsets: drawn as if the box were about 250x170, then grown (up to 1.8x) or shrunk to fit
    const z = Math.min(1.8, w / 250, hh / 170), W = w / z, H = hh / z;
    s.setAttribute('viewBox', `0 0 ${W} ${H}`); s.innerHTML = Efx.VISTAS[id](W, H); };
  const montar = (el) => { el.innerHTML = `<div class="fxb">${['est', 'cf', 'rv'].map((id, i) => card(id, i * .05)).join('')}</div>`;
    const desenha = desenhador(el), tudo = () => ['est', 'cf', 'rv'].forEach(desenha);
    requestAnimationFrame(tudo); new ResizeObserver(() => requestAnimationFrame(tudo)).observe(el.firstElementChild);
    if (el.dataset.fx71) return; el.dataset.fx71 = 1;
    el.addEventListener('c:mudou', (e) => { const { valor, dados } = e.detail;
      if (dados.liga) { E[dados.liga].on = valor; el.querySelectorAll(`[data-p=${dados.liga}]`).forEach((c) => c.classList.toggle('off', !valor)); }
      fxMudou(el, e, desenha); marcaPausa(el); });
    addEventListener('fah:s71liga', () => { if (el.isConnected) marcaPausa(el); }); };

  return { S, ANG, FIXO, on, pausaCf, montar };
})();

// Efeitos screen: Estéreo, Crossfeed, Reverb (7.1 moved to the dock)
document.head.insertAdjacentHTML('beforeend', `<style>
.fxb .card.pausa .vista svg,.fxb .card.pausa .ctls{opacity:.3;filter:grayscale(1);pointer-events:none}
.fxb .card:not(.pausa) .aviso{display:none}</style>`);
App.detalhe({ id: 'efeitos', montar: (el) => S71.montar(el) });
