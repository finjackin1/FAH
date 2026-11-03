// Module: noise cancel (Mic). detalhe = own screen. Engine: FAH.Microfone.dll (DeepFilterNet3 + gate), state "ruido".
// 31/10: with the AI on, the gate is a VOICE gate (opens on the model's speech score, lim = how sure); AI off = old level gate.
App.modulo({ id: 'ruido', lado: 'mic', ordem: 1, nome: 'Cancelar ruído', icone: 'ruido', desc: 'Tira ventilador, teclado e barulho de fundo.',
  previa: () => `<svg viewBox="0 0 300 100" preserveAspectRatio="none"><path d="${Array.from({ length: 61 }, (_, i) => `${i ? 'L' : 'M'}${i * 5} ${50 + (Math.random() - .5) * (i > 30 ? 10 : 60)}`).join(' ')}" fill="none" stroke="var(--ac)" stroke-width="2"/></svg>` });

(() => {
  const css = `
  .rd{display:flex;flex-direction:column;gap:14px}
  .rd>.painel:first-child{flex:1;min-height:0;display:flex;flex-direction:column}.rd>.painel:first-child>.dois{flex:1;min-height:0}
  .rd>.painel:first-child>.dois>div{display:flex;flex-direction:column}
  .rd .onda2{flex:1;min-height:56px;border-radius:var(--r-med);background:rgba(0,0,0,.25);overflow:hidden;display:flex;align-items:center;gap:3px;padding:0 10px}
  .rd .onda2 i{flex:1;background:var(--ac);border-radius:2px;transition:height .12s}.rd .onda2.antes i{background:var(--fg3)}
  .rd .rot{display:flex;justify-content:space-between;font-size:12px;color:var(--fg2);margin:0 0 8px}
  .rd .dois{display:grid;grid-template-columns:1fr 1fr;gap:16px}
  .rd .alvo{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:8px}
  .rd .alvo>div{display:flex;align-items:center;gap:8px;background:var(--controle);border-radius:var(--r-med);padding:10px 12px;font-size:13px;white-space:nowrap;min-width:0}
  .rd .alvo>div i{width:8px;height:8px;border-radius:50%;background:var(--ac);flex:none}.rd .alvo>div.meio i{background:none;box-shadow:inset 0 0 0 1.5px var(--ac)}
  .rd .alvo+small{display:block;margin-top:10px;font-size:12px;color:var(--fg2)}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const S = (App.estado.ruido = Object.assign({ forca: 70, ia: true, gate: true, lim: 35, soltar: 250 }, App.estado.ruido));
  // AI model failed to load on this PC (engine "micvoz".ia): one notice per run, AI switch greyed; the simple filter keeps working
  addEventListener('fah:micvoz', (e) => { if (!e.detail || !e.detail.ia || App.iaFalhou) return; App.iaFalhou = true;
    App.toast?.('A limpeza com IA não carregou neste PC. O microfone segue com o filtro simples.');
    document.querySelectorAll('.rd [data-rd=ia]').forEach((x) => x.closest('.c-linha').style.opacity = .45); });
  const barras = Array.from({ length: 40 }, () => '<i></i>').join('');
  const MODOS = [['Leve', 35], ['Normal', 70], ['Forte', 100]];
  const modo = () => (MODOS.find(([, f]) => f === S.forca) || [''])[0];
  // what it removes: DeepFilterNet takes out steady and sudden noise; room echo only partly
  const CORTA = [['Ventilador e chiado', 1], ['Teclado e mouse', 1], ['Palmas e batidas', 1], ['Moto e trânsito', 1], ['Música (em parte)', 0], ['Eco da sala (em parte)', 0]];

  // with the AI the gate opens only on speech: higher = needs clearer speech (more noise stays out, a weak voice can be cut)
  const DICA_LIM = () => S.ia ? 'Exigência|Mais alto: só abre com voz clara e corta mais barulho. Mais baixo: abre até com voz fraca, mas deixa passar mais.' : 'Limite|Volume que abre o microfone. Mais alto: precisa falar mais forte.';
  App.detalhe({ id: 'ruido', montar(el) {
    const real = App.motor.conectado;
    el.innerHTML = `<div class="rd">
      <div class="painel entra"><h4>Antes e depois (ao vivo)</h4><div class="dois">
        <div><div class="rot"><span>Entrada do microfone</span><span data-rt="antes">com ruído</span></div><div class="onda2 antes" data-w="antes">${barras}</div></div>
        <div><div class="rot"><span>O que os outros ouvem</span><span data-rt="depois">limpo</span></div><div class="onda2" data-w="depois">${barras}</div></div></div></div>
      <div class="dois">
        <div class="painel entra" style="animation-delay:.06s"><h4>Intensidade</h4>${UI.linha('Força', UI.slider(S.forca, { forca: 1, padrao: 70 }))}
          ${UI.linha('Modo', UI.chips(MODOS.map(([n]) => [n, n === modo()]), { modo: 1 }), 1)}${UI.linha('Limpar com IA (só a voz passa)', UI.switch(S.ia, { rd: 'ia' }))}</div>
        <div class="painel entra" style="animation-delay:.12s"><h4 data-pt>${S.ia ? 'Portão de voz' : 'Portão de ruído'}</h4>${UI.linha('Abafar quando você para de falar', UI.switch(S.gate, { rd: 'gate' }))}
          ${UI.linha(S.ia ? 'Exigência' : 'Limite', UI.slider(S.lim, { rd: 'lim', padrao: 35 })).replace('<div class="c-linha', `<div data-dica-ja data-dica="${DICA_LIM()}" data-lim class="c-linha`)}${UI.linha('Soltar após', UI.segmento([[100, '100 ms'], [250, '250 ms'], [500, '500 ms']], S.soltar, { rd: 'soltar' }))}</div></div>
      <div class="painel entra" style="animation-delay:.18s" data-dica="O que ele corta|A IA tira tudo o que não é voz. Quando você para de falar, o portão de voz fecha o microfone, então nem barulho alto passa. Enquanto você fala, música e vídeo diminuem mas não somem, e gente falando no vídeo conta como voz."><h4>O que ele corta</h4><div class="alvo">
        ${CORTA.map(([n, t]) => `<div class="${t ? '' : 'meio'}"><i></i>${n}</div>`).join('')}</div></div></div>`;
    const a = el.querySelectorAll('[data-w=antes] i'), d = el.querySelectorAll('[data-w=depois] i');
    // real: history of mic peaks in and out (engine "niveis"); browser: simulated
    const ha = Array(40).fill(0), hd = Array(40).fill(0);
    const alt = (p) => Math.max(4, Math.min(100, (20 * Math.log10(p + 1e-6) + 60) / 60 * 100));
    const pinta = () => a.forEach((b, i) => { b.style.height = alt(ha[i]) + '%'; d[i].style.height = alt(hd[i]) + '%'; });
    const nv = (e) => { if (!el.isConnected) return removeEventListener('fah:niveis', nv); if (App.estado.tela !== 'modulo') return;
      ha.shift(); ha.push(e.detail.mic || 0); hd.shift(); hd.push(e.detail.micSai || 0); pinta(); };
    if (real) { addEventListener('fah:niveis', nv); pinta(); }
    else { const t = setInterval(() => { if (!el.isConnected) return clearInterval(t); if (App.estado.tela !== 'modulo') return;
      const f = S.ia ? S.forca / 100 : 0, agora = Date.now();
      a.forEach((b, i) => { const voz = Math.sin(i / 3 + agora / 300) * .5 + .5, r = Math.random();
        b.style.height = 8 + voz * 40 + r * 45 + '%'; d[i].style.height = 8 + voz * 45 + r * 45 * (1 - f) + '%'; }); }, 120); }
    const desliga = () => el.querySelectorAll('[data-forca],[data-modo]').forEach((x) => x.closest('.c-linha').style.opacity = S.ia ? '' : .45);
    const portao = () => el.querySelectorAll('[data-rd=lim],[data-rd=soltar]').forEach((x) => x.closest('.c-linha').style.opacity = S.gate ? '' : .45);
    desliga(); portao();
    if (App.iaFalhou) el.querySelectorAll('[data-rd=ia]').forEach((x) => x.closest('.c-linha').style.opacity = .45);
    el.addEventListener('c:mudou', (e) => { const x = e.detail;
      if (x.dados.forca) { S.forca = Math.round(x.valor); el.querySelectorAll('[data-modo]').forEach((c) => c.classList.toggle('on', c.textContent === modo())); }
      if (x.dados.modo) { S.forca = MODOS.find(([n]) => n === e.target.textContent)[1];
        el.querySelectorAll('[data-modo]').forEach((c) => c.classList.toggle('on', c === e.target)); UI.poe(el.querySelector('[data-forca]'), S.forca); }
      const k = x.dados.rd;
      if (k === 'ia' || k === 'gate') { S[k] = !!x.valor; desliga(); portao();
        const h = el.querySelector('[data-pt]'), l = el.querySelector('[data-lim]'); h.textContent = S.ia ? 'Portão de voz' : 'Portão de ruído'; l.dataset.dica = DICA_LIM(); l.querySelector(':scope>span').textContent = S.ia ? 'Exigência' : 'Limite'; }
      if (k === 'lim') S.lim = Math.round(x.valor);
      if (k === 'soltar') S.soltar = +x.valor; });
  } });
})();
