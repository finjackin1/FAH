// Module: mic equalizer (Mic). Same EQ screen as Som (App.criaEq) + voice presets + voice tools panel.
App.modulo({ id: 'eqmic', lado: 'mic', ordem: 3, nome: 'Equalizador', icone: 'eq', desc: 'Deixa a voz mais clara ou mais grave.', previa: () => App.prev.curva() });

(() => {
  const css = `
  .pq:has(>.vm){grid-template-columns:minmax(0,1fr) 290px}.pq>.painel,.pq>.baixo{grid-column:1}
  .vm{grid-column:2;grid-row:1/3;display:flex;flex-direction:column;gap:14px;min-height:0}.vm>.painel{flex:1}
  .vm .nivel{height:14px;border-radius:7px;background:#111;position:relative;overflow:hidden;margin:4px 0 10px}
  .vm .nivel i{position:absolute;left:0;top:0;bottom:0;background:linear-gradient(90deg,#30d158 60%,#ffd60a 80%,#ff453a);transition:width .1s}
  .vm .nivel b{position:absolute;top:0;bottom:0;width:2px;background:#fff;transition:left .2s}
  .vm .rot{display:flex;justify-content:space-between;font-size:12px;color:var(--fg2)}
  .vm .c-linha.lg .c-slider{flex:none;align-self:stretch;margin:4px 11px 8px}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const PRESETS = { Plano: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0], 'Voz clara': [-6, -4, -2, 0, 1, 2, 4, 4, 2, 0], 'Voz de rádio': [-8, -2, 3, 2, 0, 1, 3, 2, -2, -6],
    'Mais grave': [2, 4, 4, 2, 0, -1, 0, 0, 0, -1], 'Tirar abafado': [-4, -2, -3, -4, -2, 1, 3, 3, 2, 1], 'Microfone barato': [-10, -6, -1, 1, 2, 3, 2, 1, -1, -4] };
  const S = (App.estado.vm = App.estado.vm || { corte: 1, ganho: 1, comp: 50, sss: 40, alvo: 70 });

  App.criaEq({ id: 'eqmic', chave: 'eqmic', presets: PRESETS, padrao: 'Voz clara',
    extra: (anima) => `<div class="vm">
      <div class="painel ${anima ? 'entra' : ''}" style="animation-delay:.12s"><h4>Ferramentas de voz</h4>
        ${UI.linha('Cortar graves (zumbido, batida na mesa)', UI.switch(S.corte, { vm: 'corte' }))}${UI.linha('Nivelar a voz (fala baixa sobe, grito desce)', UI.slider(S.comp, { vm: 'comp', padrao: 50 }), 1)}
        ${UI.linha('Suavizar o "S" chiado', UI.slider(S.sss, { vm: 'sss', padrao: 40 }), 1)}</div>
      <div class="painel ${anima ? 'entra' : ''}" style="animation-delay:.18s"><h4>Volume da sua voz</h4>
        <div class="rot"><span>Fale normalmente</span><span data-vmtxt="1"></span></div><div class="nivel"><i></i><b></b></div>
        ${UI.linha('Ajustar volume sozinho', UI.switch(S.ganho, { vm: 'ganho' }))}${UI.linha('Volume alvo', UI.slider(S.alvo, { vm: 'alvo', padrao: 70 }))}</div></div>`,
    liga(el) {
      el.addEventListener('c:mudou', (e) => { const { dados, valor } = e.detail; if (dados.vm) S[dados.vm] = valor; });
      // voice level bar: real = engine "micvoz" (dBFS RMS -> % = (dB + 60) / 0.6, same scale as "Volume alvo"); browser = simulated
      const mostra = (v) => { const i = el.querySelector('.vm .nivel i'), b = el.querySelector('.vm .nivel b'), tx = el.querySelector('[data-vmtxt]'); if (!i) return false;
        i.style.width = Math.max(3, Math.min(100, v)) + '%'; b.style.left = S.alvo + '%';
        tx.textContent = v > S.alvo + 18 ? 'alto demais' : v < S.alvo - 25 ? 'baixo' : 'bom'; return true; };
      if (App.motor.conectado) {
        const nv = (e) => { if (!el.isConnected) return removeEventListener('fah:micvoz', nv); if (App.estado.tela !== 'modulo') return; mostra((e.detail.rms + 60) / 0.6); };
        addEventListener('fah:micvoz', nv); mostra(0);
        if (!App.estado.mic?.ligado) el.querySelector('[data-vmtxt]').textContent = 'microfone desligado';
        return; }
      const t = setInterval(() => { if (!el.isConnected) return clearInterval(t); if (App.estado.tela !== 'modulo') return;
        const bruto = 25 + Math.random() * 65; mostra(S.ganho ? S.alvo + (bruto - 57) * (1 - S.comp / 100) * .6 : bruto); }, 140);
    } });
})();
