// Module: Efeitos (Som) = approved proposal B "Palco ao vivo" (27/10): 3 cards (Estéreo, Crossfeed, Reverb), each with a
// live drawing on top and its controls below. Engine: fonte\Modulos\Efeitos (state key "efeitos", see ponte.js VIGIA).
const Efx = (() => {
  // Reverb controls: Size, Filter, Fade, Pre-Delay, Mix, Pre-Delay Mix, Damp
  const PRESETS = {
    'Reverb leve': [.602, .802, .317, .251, .375, .348, .441], 'Sala pequena': [0, 1, 1, 0, .32, .623, .529],
    'Estúdio': [.030, .110, 1, .036, .47, .437, .989], 'Palco': [.245, .705, .503, .121, .45, .425, .412],
    'Auditório': [0, .697, .814, 0, .5, 0, .898], 'Grande salão': [.519, .709, 0, .946, .44, .526, .259],
    'Estádio': [.993, .621, .828, .445, .376, .739, .840], 'Eco': [.727, .905, .272, .541, .37, .583, .363],
  };
  const PARAMS = ['Tamanho', 'Filtro', 'Cauda', 'Pré-atraso', 'Mix', 'Mix do pré-atraso', 'Abafar'];
  const NIVEIS = [['suave', 'Suave'], ['medio', 'Médio'], ['forte', 'Forte']];
  // Som 7.1 has no engine yet: its switch must not block crossfeed until it does (same flag in Modulos\Efeitos\Modulo.cs)
  const SOM71_EXISTE = true;
  // preset -> slider %; Mix: 100% = engine max (0.3), the loudest preset (0.5) sits at 100%
  const deP = (nome) => PRESETS[nome].map((v, i) => Math.min(100, Math.round(i === 4 ? v / .5 * 100 : v * 100)));
  const E = (App.estado.efeitos = App.estado.efeitos || { est: { on: true, modo: 'auto' }, cf: { on: true, nivel: 'medio' }, rv: { on: false, preset: 'Reverb leve', p: deP('Reverb leve') } });
  App.estado.ligado = App.estado.ligado || {};
  const s71 = () => SOM71_EXISTE && App.estado.ligado.som71 !== false;
  App.modulo({ id: 'efeitos', lado: 'som', ordem: 3, nome: 'Estéreo+', icone: 'efeitos', desc: 'Abre o som no fone.',
    previa: () => `<svg viewBox="0 0 300 100"><path d="M20 50 Q75 10 150 50 T280 50" fill="none" stroke="var(--ac)" stroke-width="3"><animate attributeName="d" dur="2.4s" repeatCount="indefinite"
      values="M20 50 Q75 10 150 50 T280 50;M20 50 Q75 90 150 50 T280 50;M20 50 Q75 10 150 50 T280 50"/></path></svg>` });

  // labels shown next to sliders
  const txtBal = (b) => (Math.abs(b) < 3 ? 'Centro' : b < 0 ? `Esq. ${UI.num(-b)}%` : `Dir. ${UI.num(b)}%`);
  const txtLarg = (l, mono) => (mono ? 'Mono' : `${UI.num(l)}%`);
  // 0..100 slider <-> -100..100 balance / 0..125 width (right-click box types in these units)
  const deBal = (v) => (v - 50) * 2, paraBal = (b) => b / 2 + 50;
  const deLarg = (v) => v * 1.25, paraLarg = (l) => l / 1.25;
  UI.escalas.bal = { de: deBal, para: paraBal }; UI.escalas.larg = { de: deLarg, para: paraLarg };
  return { PRESETS, PARAMS, NIVEIS, E, s71, deP, txtBal, txtLarg, deBal, paraBal, deLarg, paraLarg };
})();

(() => {
  const { E, PRESETS, PARAMS, NIVEIS } = Efx;
  const css = `
  .fxb{display:grid;grid-template-columns:1fr 1fr 1.35fr;grid-template-rows:minmax(0,1fr);gap:16px}
  .fxb .card{background:var(--bg2);border-radius:var(--r-card);padding:16px;display:flex;flex-direction:column;min-height:0;transition:transform .25s var(--curva)}
  .fxb .card:hover{transform:translateY(-2px)}
  .fxb .card>header{display:flex;align-items:center;gap:10px;margin-bottom:12px}
  .fxb .card>header b{font-size:15px;font-weight:600}
  .fxb .card>header small{display:block;font-size:12px;color:var(--fg2);margin-top:2px}
  .fxb .card>header .c-switch{margin-left:auto}
  .fxb .vista{position:relative;flex:1 1 auto;min-height:148px;max-height:236px;border-radius:10px;background:radial-gradient(circle at 50% 60%,rgba(255,159,10,.10),#151517 72%);overflow:hidden;margin-bottom:12px}
  .fxb .vista svg{position:absolute;inset:0;width:100%;height:100%}
  .fxb .card.off .vista,.fxb .card.off .ctls{opacity:.3;filter:grayscale(1);pointer-events:none}
  .fxb .vista,.fxb .ctls{transition:opacity .3s,filter .3s}
  .fxb .ctls{display:flex;flex-direction:column;min-height:0}
  .fxb .c-linha{padding:7px 0}
  .fxb .ln{display:grid;grid-template-columns:72px 1fr 52px;align-items:center;font-size:13px;padding:9px 0;box-shadow:inset 0 -.5px 0 var(--linha)}
  .fxb .ln .c-slider{margin:0 11px}
  .fxb .vl{text-align:right;color:var(--fg2);font-size:12px;font-variant-numeric:tabular-nums}
  .fxb .duas .ln{grid-template-columns:1fr auto;row-gap:10px;padding:6px 0 12px;box-shadow:none}
  .fxb .duas .ln .c-slider{grid-column:1/-1;grid-row:2;margin:0 11px}.fxb .duas .ln .vl{grid-row:1;grid-column:2}
  .fxb .c-segmento{margin-top:4px}.fxb .c-segmento button{flex:1;padding:7px 0}
  .fxb .dica{font-size:12px;color:var(--fg3);line-height:1.45;margin-top:12px}
  .fxb .aviso{position:absolute;inset:auto 10px 10px;font-size:12px;color:var(--som);background:rgba(20,20,22,.85);border-radius:var(--r-peq);padding:7px 10px;text-align:center}
  .fxb .c-chips{margin-bottom:6px}.fxb .c-chips span{padding:4px 10px}
  .fxb .duas{display:grid;grid-template-columns:1fr 1fr;column-gap:18px}
  @media (max-height:660px){.fxb .vista{min-height:120px}.fxb .duas .ln{padding:4px 0 9px;row-gap:8px}.fxb .card>header{margin-bottom:10px}}
  /* drawings */
  .fxb .fio{fill:none;stroke:var(--som);stroke-linecap:round;transition:opacity .3s,stroke-width .3s}
  .fxb .fio.anda{stroke-dasharray:4 8;animation:fio 1s linear infinite}
  @keyframes fio{to{stroke-dashoffset:-12}}
  .fxb .cx{transition:transform .35s var(--curva)}
  .fxb .onda{fill:none;stroke:var(--som);transform-origin:center;transform-box:fill-box}
  .fxb .sala{fill:none;stroke:#3a3a3c;stroke-width:1.5;transition:all .35s var(--curva)}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);

  const lin = (rot, v, d, txt) => `<div class="ln"><span>${rot}</span>${UI.slider(v, d)}<span class="vl">${txt}</span></div>`;
  const cabeca = (x, y) => `<circle cx="${x}" cy="${y}" r="15" fill="#3a3a3c"/><path d="M${x - 5} ${y - 15} L${x} ${y - 22} L${x + 5} ${y - 15}" fill="#3a3a3c"/>`;

  // Estéreo: two speakers on an arc in front of the head; width spreads them, balance tilts toward a side, mono = one in the middle
  const vEst = (w, h) => { const cx = w / 2, cy = h - 34, larg = E.est.mono ? 0 : E.est.larg / 125, a = 10 + larg * 62, r = 92;
    const p = (s) => { const t = (s * a + E.est.bal * .25) * Math.PI / 180; return [cx + Math.sin(t) * r, cy - Math.cos(t) * r]; };
    const [l, d] = [p(-1), p(1)], gl = Math.min(1, 1 - E.est.bal / 100), gd = Math.min(1, 1 + E.est.bal / 100);
    return `<path d="M${cx - r} ${cy} A${r} ${r} 0 0 1 ${cx + r} ${cy}" fill="none" stroke="#2c2c2e" stroke-dasharray="3 5"/>
      <path d="M${l[0]} ${l[1]} Q${cx} ${cy - r * 1.25} ${d[0]} ${d[1]}" fill="rgba(255,159,10,.10)" stroke="none"/>
      ${[[l, gl, 'E'], [d, gd, 'D']].map(([q, g, t]) => `<g class="cx"><circle cx="${q[0]}" cy="${q[1]}" r="${8 + g * 6}" fill="var(--som)" opacity="${.35 + g * .65}"/>
      <text x="${q[0]}" y="${q[1] + 4}" text-anchor="middle" font-size="10" font-weight="700" fill="#000">${E.est.mono ? 'M' : t}</text></g>`).join('')}${cabeca(cx, cy)}`; };

  // Crossfeed: each speaker reaches its own ear (solid) and the other ear (dashed, thicker = stronger)
  const vCf = (w, h) => { const cx = w / 2, cy = h / 2 + 18, lv = { suave: 1.2, medio: 2.2, forte: 3.2 }[E.cf.nivel], ex = 17;
    const L = [cx - 92, 34], R = [cx + 92, 34];
    return `<path class="fio" d="M${L[0]} ${L[1]} Q${cx - 60} ${cy} ${cx - ex} ${cy}" stroke-width="2.5"/><path class="fio" d="M${R[0]} ${R[1]} Q${cx + 60} ${cy} ${cx + ex} ${cy}" stroke-width="2.5"/>
      <path class="fio anda" d="M${L[0]} ${L[1]} Q${cx + 30} ${cy - 50} ${cx + ex} ${cy}" stroke-width="${lv}" opacity=".7"/><path class="fio anda" d="M${R[0]} ${R[1]} Q${cx - 30} ${cy - 50} ${cx - ex} ${cy}" stroke-width="${lv}" opacity=".7"/>
      ${[L, R].map((q, i) => `<rect x="${q[0] - 12}" y="${q[1] - 12}" width="24" height="24" rx="7" fill="#3a3a3c"/><text x="${q[0]}" y="${q[1] + 4}" text-anchor="middle" font-size="10" font-weight="700" fill="#f5f5f7">${i ? 'D' : 'E'}</text>`).join('')}
      ${cabeca(cx, cy)}`; };

  // Reverb: room outline (size), echo rings leaving the listener (fade = how long, mix = how strong)
  const vRv = (w, h) => { const cx = w / 2, cy = h / 2, p = E.rv.p, sw = Math.min(w / 2 - 14, 70 + p[0] * 1.9), sh = Math.min(h / 2 - 14, 34 + p[0] * .5), dur = 1.2 + p[2] / 40, op = .25 + p[4] / 140;
    return `<rect class="sala" x="${cx - sw}" y="${cy - sh}" width="${sw * 2}" height="${sh * 2}" rx="8"/>
      ${[0, 1, 2].map((i) => `<circle class="onda" cx="${cx}" cy="${cy}" r="14" stroke-width="2" opacity="0"><animate attributeName="r" from="14" to="${Math.max(sw, sh) + 10}" dur="${dur}s" begin="${i * dur / 3}s" repeatCount="indefinite"/>
        <animate attributeName="opacity" from="${op}" to="0" dur="${dur}s" begin="${i * dur / 3}s" repeatCount="indefinite"/></circle>`).join('')}
      <circle cx="${cx}" cy="${cy}" r="10" fill="var(--som)"/>`; };

  const VISTAS = { est: vEst, cf: vCf, rv: vRv };
  Efx.VISTAS = VISTAS;                              // Início card draws the same scenes, small

  App.detalhe({ id: 'efeitos', montar(el) {
    const bloq = Efx.s71();
    const card = (id, t, sub, on, corpo, d = 0) => `<div class="card entra ${on ? '' : 'off'}" data-p="${id}" style="animation-delay:${d}s"><header><div><b>${t}</b><small>${sub}</small></div>${UI.switch(on, { liga: id })}</header>
      <div class="vista" data-v="${id}"><svg></svg>${id === 'cf' && bloq ? '<div class="aviso">Desligado: o Som 7.1 já faz isso.</div>' : ''}</div><div class="ctls">${corpo}</div></div>`;
    el.innerHTML = `<div class="fxb">
      ${card('est', 'Estéreo', 'Abrir, fechar ou puxar para um lado', E.est.on, `${lin('Balanço', Efx.paraBal(E.est.bal), { bal: 1, escala: 'bal', padrao: 50 }, Efx.txtBal(E.est.bal))}
        ${lin('Largura', Efx.paraLarg(E.est.larg), { larg: 1, escala: 'larg', padrao: Efx.paraLarg(100) }, Efx.txtLarg(E.est.larg, E.est.mono))}${UI.linha('Mono', UI.switch(E.est.mono, { mono: 1 }))}`)}
      ${card('cf', 'Crossfeed', 'Som fora de dentro da cabeça', E.cf.on && !bloq, `${UI.segmento(NIVEIS, E.cf.nivel, { nivel: 1 })}
        <div class="dica">Cada ouvido escuta um pouco do outro lado, atrasado e sem agudo, como acontece com caixas de som.</div>`, .06)}
      ${card('rv', 'Reverb', 'Ambiente em volta do som', E.rv.on, `${UI.chips(Object.keys(PRESETS).map((k) => [k, k === E.rv.preset]), { preset: 1 })}
        <div class="duas">${PARAMS.map((n, i) => lin(n, E.rv.p[i], { rp: i, padrao: Efx.deP(E.rv.preset)[i] }, UI.num(E.rv.p[i]) + '%')).join('')}</div>`, .12)}</div>`;
    const desenha = (id) => { const v = el.querySelector(`[data-v=${id}]`), s = v.querySelector('svg'), w = v.clientWidth, h = v.clientHeight;
      if (w < 40 || h < 40) return;                  // not laid out yet (screen opened and left in the same instant)
      s.setAttribute('viewBox', `0 0 ${w} ${h}`); s.innerHTML = VISTAS[id](w, h); };
    const tudo = () => Object.keys(VISTAS).forEach(desenha);
    requestAnimationFrame(tudo);
    new ResizeObserver(() => requestAnimationFrame(tudo)).observe(el.querySelector('.fxb'));   // next frame: no 'ResizeObserver loop' error
    el.addEventListener('c:mudou', (e) => { const { tipo, valor, dados } = e.detail, alvo = e.target, vl = alvo.parentNode.querySelector('.vl');
      if (dados.liga) { E[dados.liga].on = valor; el.querySelector(`[data-p=${dados.liga}]`).classList.toggle('off', !valor); }
      if (dados.bal) { E.est.bal = Efx.deBal(valor); vl.textContent = Efx.txtBal(E.est.bal); desenha('est'); }
      if (dados.larg) { E.est.larg = Efx.deLarg(valor); vl.textContent = Efx.txtLarg(E.est.larg, E.est.mono); desenha('est'); }
      if (dados.mono) { E.est.mono = valor; el.querySelector('[data-larg]').parentNode.querySelector('.vl').textContent = Efx.txtLarg(E.est.larg, valor); desenha('est'); }
      if (dados.nivel) { E.cf.nivel = valor; desenha('cf'); }
      if (dados.rp !== undefined && tipo === 'slider') { E.rv.p[+dados.rp] = valor; vl.textContent = UI.num(valor) + '%'; desenha('rv'); }
      if (dados.preset) { E.rv.preset = alvo.textContent; E.rv.p = Efx.deP(E.rv.preset);
        el.querySelectorAll('[data-preset]').forEach((c) => c.classList.toggle('on', c === alvo));
        el.querySelectorAll('[data-rp]').forEach((s) => { const i = +s.dataset.rp; UI.poe(s, E.rv.p[i]); s.dataset.padrao = E.rv.p[i]; s.parentNode.querySelector('.vl').textContent = UI.num(E.rv.p[i]) + '%'; });
        desenha('rv'); } });
  } });
})();
