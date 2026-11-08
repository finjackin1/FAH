// Dev screen (08/11): every engine knob, only in dev mode (flag file usuario\config\dev -> window.FAH_DEV). The user UI stays fixed.
// App.estado.dev = flat {key: value}; missing key = shipped value. ponte.js sends 'dev' (dev mode only); Efeitos + Espacial Modulo.Dev() read it.
// 7.1 speakers (angle / volume / mute / test) edit App.estado.s71 directly; outside dev mode efeitos71.js resets them to the approved layout.
(() => {
  if (!window.FAH_DEV) return;
  document.head.insertAdjacentHTML('beforeend', `<style>
  .t-dev{--ac:var(--som)}
  .t-dev .pag{display:flex;flex-direction:column;gap:24px;padding-bottom:64px}
  .t-dev .topo{display:flex;align-items:center;gap:16px}
  .t-dev h1{margin:0;font-size:26px;font-weight:700;letter-spacing:-.5px;flex:1}
  .t-dev .cols{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px;align-items:start}
  .t-dev .col{display:flex;flex-direction:column;gap:16px;min-width:0}
  .t-dev .grupo{background:var(--bg2);border-radius:12px;overflow:hidden}
  .t-dev .grupo>h4{margin:0;padding:12px 16px 4px;font-size:12px;font-weight:600;letter-spacing:.4px;text-transform:uppercase;color:var(--fg2)}
  .t-dev .linha{min-height:44px;display:flex;align-items:center;gap:8px;padding:6px 16px;position:relative}
  .t-dev .linha+.linha::before{content:"";position:absolute;left:16px;right:0;top:0;height:.5px;background:var(--linha)}
  .t-dev .nome{width:128px;flex:none;font-size:13px;color:var(--fg)}
  .t-dev .linha.sw .nome{flex:1;width:auto}
  .t-dev .vl{width:72px;flex:none;text-align:right;font-size:12px;color:var(--fg2);font-variant-numeric:tabular-nums}
  .t-dev .c-segmento{flex:none}.t-dev .c-segmento button{padding:0 14px;height:28px}
  .t-dev .bt{border:0;height:32px;padding:0 14px;border-radius:8px;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);cursor:pointer;white-space:nowrap;flex:none;transition:background .2s,transform .1s}
  .t-dev .bt:hover{background:var(--controle-forte)}.t-dev .bt:active{transform:scale(.97)}
  .t-dev .cx{display:grid;grid-template-columns:110px 1fr 64px 1fr 56px auto auto;align-items:center;gap:8px}
  .t-dev .cx .nome{width:auto}
  .t-dev .cx .rot{font-size:11px;color:var(--fg3);text-transform:uppercase;letter-spacing:.4px}
  .t-dev .dica{margin:0;padding:2px 16px 12px;font-size:12px;line-height:16px;color:var(--fg2)}
  .t-dev .sobe{animation:entra .5s var(--curva) backwards}</style>`);

  const E = App.estado;
  if (!E.dev || typeof E.dev !== 'object') E.dev = {};
  const D = () => E.dev;
  // key: [min, max, shipped, unit]
  const N = {
    eInt: [0, 100, 100, '%'], s71Int: [0, 100, 100, '%'],
    eGanhoVoz: [0.5, 2, 1.24, '×'], eLado: [0, 1.5, 1, '×'], eCaixa: [0, 1.5, 0.5, '×'], eGanhoSaidaDb: [-6, 6, 0, ' dB'],
    eCorteCaixaHz: [0, 16000, 0, 'hz'], ePicoHz: [200, 8000, 1600, 'hz'], ePicoQ: [0.1, 4, 0.7, ''], ePicoDb: [-15, 15, -8, ' dB'],
    eEspalhamento: [10, 360, 360, '°'], eHaloDb: [0, 30, 10, ' dB'],
    nAlvo: [-40, -5, -20, ' dB'], nRazao: [1, 10, 3, ':1'], nGanhoMax: [0, 20, 9, ' dB'], nGuardaDb: [0, 20, 10, ' dB'], nIntensidade: [0, 1, 0.5, ''],
    mHaloDb: [0, 30, 10, ' dB'], mEspalhamento: [10, 360, 360, '°'], mGanhoCentroBase: [0, 2, 1, '×'], mForcaDifuso: [0, 1, 0.5, ''],
    mForcaFrente: [0, 1.5, 1, '×'], mPalcoAbre: [0.2, 1, 0.6, ''],
    mMistura: [0, 1, 0.35, ''], mSubAgudo: [0, 1, 0.25, ''], mSubEstereo: [0, 2, 0.7, '×'] };
  const B = { eV2: true, eVozCentroPlana: true, mBaseEstereo: true, mVozPlana: true, mVozHalo: true, mPalcoAberto: true };
  Object.entries(N).forEach(([k, [a, b]]) => (UI.escalas['dev_' + k] = { de: (p) => a + (b - a) * p / 100, para: (v) => (v - a) / (b - a) * 100 }));
  UI.escalas.dev_ang = { de: (p) => -180 + 3.6 * p, para: (v) => (v + 180) / 3.6 };
  const val = (k) => (D()[k] ?? N[k]?.[2] ?? B[k]);
  const txt = (k, v) => { const u = N[k][3]; return u === 'hz' ? (v < 1 ? 'desligado' : UI.hz(v)) : UI.num(v, 2) + u; };
  const pos = (k, v) => UI.escalas['dev_' + k].para(v);

  const sl = (k, nome) => `<div class="linha"><span class="nome">${nome}</span>${UI.slider(pos(k, val(k)), { k, escala: 'dev_' + k, padrao: pos(k, N[k][2]) })}<span class="vl" data-vl="${k}">${txt(k, val(k))}</span></div>`;
  const sw = (k, nome) => `<div class="linha sw"><span class="nome">${nome}</span>${UI.switch(!!val(k), { k })}</div>`;
  const G = (nome, linhas) => `<section class="grupo sobe"><h4>${nome}</h4>${linhas}</section>`;

  const NOMES = { FL: 'Frente esq.', C: 'Centro', FR: 'Frente dir.', SL: 'Lado esq.', SR: 'Lado dir.', RL: 'Trás esq.', RR: 'Trás dir.', LFE: 'Subwoofer' };
  const S = () => S71.S;
  const cx = (id) => { const s = S(), a = s.ang[id], v = s.vol[id] ?? 100, temAng = S71.ANG[id] != null;
    return `<div class="linha cx"><span class="nome">${NOMES[id]}</span>
      ${temAng ? UI.slider((a + 180) / 3.6, { cxang: id, escala: 'dev_ang', padrao: (S71.ANG[id] + 180) / 3.6 }) : '<span></span>'}
      <span class="vl" data-cxa="${id}">${temAng ? UI.num(a, 1) + '°' : ''}</span>
      ${UI.slider(v, { cxvol: id, padrao: 100 })}<span class="vl" data-cxv="${id}">${UI.num(v, 1)}%</span>
      ${UI.mudo(!!s.mudo[id], { cxmudo: id })}<button class="bt" data-testar="${id}">Testar</button></div>`; };

  const pagina = () => `<div class="pag">
    <div class="topo sobe"><h1>Dev</h1><button class="bt" data-padrao-tudo="1">Voltar ao padrão</button></div>
    <div class="cols">
      <div class="col">${G('Sessão', `<div class="linha sw"><span class="nome">Iniciar do zero</span>${UI.switch(!!window.FAH_ZERO, { zero: 1 })}</div>`
          + '<p class="dica">Ligado: toda vez que o FAH abrir, começa tudo no padrão (EQ plano, módulos e efeitos desligados) e nada desta sessão fica salvo. Só esta opção é lembrada. Vale a partir da próxima abertura.</p>')}
        ${G('Intensidade', sl('eInt', 'Estéreo+') + sl('s71Int', '7.1'))}
        ${G('Nivelador', sl('nAlvo', 'Alvo') + sl('nRazao', 'Razão') + sl('nGanhoMax', 'Ganho máx.') + sl('nGuardaDb', 'Guarda') + sl('nIntensidade', 'Intensidade'))}</div>
      <div class="col">${G('Estéreo+',
        `<div class="linha sw"><span class="nome">Versão</span>${UI.segmento([[3, 'v3'], [4, 'v4'], [5, 'v5']], +val('eVersao') || 5, { k: 'eVersao' })}</div>`
        + sw('eV2', 'V2') + sw('eVozCentroPlana', 'Voz do centro plana')
        + sl('eGanhoVoz', 'Ganho da voz') + sl('eLado', 'Lado') + sl('eCaixa', 'Caixa') + sl('eGanhoSaidaDb', 'Ganho de saída')
        + sl('eCorteCaixaHz', 'Corte da caixa') + sl('ePicoHz', 'Pico: freq.') + sl('ePicoQ', 'Pico: Q') + sl('ePicoDb', 'Pico: ganho')
        + sl('eEspalhamento', 'Espalhamento') + sl('eHaloDb', 'Halo'))}</div>
      <div class="col">${G('Motor 7.1',
        sw('mBaseEstereo', 'Base estéreo') + sw('mVozPlana', 'Voz plana') + sw('mVozHalo', 'Halo na voz') + sw('mPalcoAberto', 'Palco aberto')
        + sl('mHaloDb', 'Halo') + sl('mEspalhamento', 'Espalhamento') + sl('mGanhoCentroBase', 'Ganho centro') + sl('mForcaDifuso', 'Força difuso')
        + sl('mForcaFrente', 'Força frente') + sl('mPalcoAbre', 'Palco: abre')
        + sl('mMistura', 'Caixas: todas freq.') + sl('mSubAgudo', 'Sub: agudos') + sl('mSubEstereo', 'Sub no estéreo')
        + '<p class="dica">Caixas: 0 = cada caixa só na faixa dela (antigo); mais alto = toda caixa toca tudo, com foco na faixa dela. Sub: agudos 0 = só graves; Sub no estéreo 0 = sub mudo em música estéreo.</p>')}</div>
    </div>
    ${G('Caixas do 7.1 (ângulo · volume · mudo)', Object.keys(NOMES).map(cx).join(''))}</div>`;

  App.tela({ id: 'dev', classe: 't-dev rolagem',
    montar(el) {
      el.innerHTML = pagina();
      el.querySelectorAll('.sobe').forEach((s, i) => (s.style.animationDelay = i * 40 + 'ms'));
      if (el._ok) return; el._ok = 1;
      el.addEventListener('c:mudou', (e) => { const { valor, dados } = e.detail, k = dados.k, s = S();
        if (dados.cxang) { const a = Math.round(UI.escalas.dev_ang.de(valor) * 10) / 10; s.ang[dados.cxang] = a; el.querySelector(`[data-cxa="${dados.cxang}"]`).textContent = UI.num(a, 1) + '°'; return; }
        if (dados.cxvol) { s.vol[dados.cxvol] = valor; el.querySelector(`[data-cxv="${dados.cxvol}"]`).textContent = UI.num(valor, 1) + '%'; return; }
        if (dados.cxmudo) { if (valor) s.mudo[dados.cxmudo] = true; else delete s.mudo[dados.cxmudo]; return; }
        if (dados.zero) { window.FAH_ZERO = !!valor; App.motor.enviar({ tipo: 'zero', on: !!valor }); App.toast?.(valor ? 'Na próxima abertura o FAH começa do zero' : 'Na próxima abertura o FAH volta a lembrar os ajustes', 4000); return; }
        if (!k) return;
        if (k === 'eVersao') D()[k] = +valor;
        else if (k in B) D()[k] = !!valor;
        else if (k in N) { const v = +UI.escalas['dev_' + k].de(valor).toFixed(4); D()[k] = v; el.querySelector(`[data-vl="${k}"]`).textContent = txt(k, v); } });
      el.addEventListener('click', (e) => {
        const t = e.target.closest('[data-testar]'); if (t) { S().teste = t.dataset.testar + ':' + Date.now(); return; }
        if (e.target.closest('[data-padrao-tudo]')) { E.dev = {}; const s = S();
          s.ang = { ...S71.ANG }; s.vol = Object.fromEntries(Object.keys(S71.ANG).map((k) => [k, 100])); s.mudo = {};
          el.innerHTML = pagina(); App.toast?.('Tudo de volta ao padrão', 3000); } });
    } });
  UI.icones.dev = '<path d="M8 6 2 12l6 6"/><path d="M16 6l6 6-6 6"/><path d="M14 4l-4 16"/>';
  App.dock({ id: 'dev', icone: 'dev', dica: 'Dev', tela: 'dev', ordem: 10 });
})();
