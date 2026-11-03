// Screen: settings = ONE place (old EQ options + gear + router options merged). Side list of sections, content on the right.
// Add a section = add an entry to SECOES.
(() => {
  const css = `
  .t-cfg{display:grid;grid-template-columns:220px 1fr;gap:24px;align-items:start}
  .t-cfg .menu{display:flex;flex-direction:column;gap:2px;background:var(--bg2);border-radius:var(--r-grupo);padding:8px}
  .t-cfg .menu button{display:flex;align-items:center;gap:10px;border:0;background:none;color:var(--fg);font:500 14px var(--fonte);padding:9px 10px;border-radius:9px;cursor:pointer;text-align:left;transition:background .2s}
  .t-cfg .menu button:hover{background:var(--bg2-hover)}.t-cfg .menu button.on{background:var(--ac);color:#fff}
  .t-cfg .menu .icone{flex:none}
  .t-cfg .cont{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:16px;align-items:start}
  .t-cfg .cont h3{grid-column:1/-1;font-size:var(--t-grupo);font-weight:700;letter-spacing:-.4px;margin:0 0 -2px}
  .t-cfg .atalho{min-width:150px;text-align:center;border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:7px 12px;border-radius:8px;cursor:pointer}
  .t-cfg .atalho.ouve{background:var(--ac);color:#fff;animation:pisca 1s infinite}
  @keyframes pisca{50%{opacity:.6}}
  .t-cfg .bt{border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:8px 14px;border-radius:8px;cursor:pointer}
  .t-cfg .sobre{display:flex;align-items:center;gap:16px}.t-cfg .sobre b{font-size:22px}.t-cfg .sobre small{display:block;color:var(--fg2);font-size:13px;margin-top:4px}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const E = App.estado;
  E.atalhos = E.atalhos || { 'Ligar/desligar tudo': 'Ctrl + Alt + F', 'Mudo do microfone': 'Ctrl + Alt + M', 'Próximo preset do EQ': 'Ctrl + Alt + →', 'Abrir o FAH': 'Ctrl + Alt + A' };
  const P = (t, corpo) => `<div class="painel"><h4>${t}</h4>${corpo}</div>`, L = UI.linha;
  const SECOES = [
    ['geral', 'Geral', 'engr', () => P('Ao ligar o PC', L('Iniciar com o Windows', UI.switch(true)) + L('Começar minimizado na bandeja', UI.switch(true)) + L('Lembrar o último estado de cada módulo', UI.switch(true)))
      + P('Janela', L('Tamanho: Máx. 1440 × 810 ou Mín. 1080 × 608 · também segurando o maximizar', UI.segmento([['max', 'Máx.'], ['min', 'Mín.']], E.tam === 'min' ? 'min' : 'max', { campo: 'tam' })) + L('Fechar no X manda para a bandeja', UI.switch(true)) + L('Ícone da bandeja mostra se está ligado', UI.switch(true)))],
    ['aparencia', 'Aparência', 'efeitos', () => P('Visual', L('Tema', UI.segmento([['escuro', 'Escuro'], ['claro', 'Claro'], ['auto', 'Igual ao Windows']], 'escuro', { campo: 'tema' }), 1)
      + L('Animações', UI.segmento([['todas', 'Todas'], ['poucas', 'Poucas'], ['nenhuma', 'Nenhuma']], 'todas', { campo: 'anim' }), 1) + L('Tamanho do texto', UI.slider(50, { padrao: 50 })))],
    ['audio', 'Áudio', 'fone', () => P('Motor de som', L('Taxa de amostragem', UI.select('48 kHz')) + L('Tamanho do buffer', UI.select('Baixo (10 ms)')) + L('Prioridade alta para o áudio', UI.switch(true)))
      + P('Equalizador no Windows (APO)', L('Instalar e atualizar sozinho', UI.switch(true)) + L('Aplicar em', UI.chips(App.estado.dispositivos.som.filter((d) => d.f).map((d) => [d.n, d.p ? 1 : 0])), 1))
      + P('Quando um dispositivo some', L('Voltar para o padrão do Windows', UI.switch(true)) + L('Avisar na tela', UI.switch(true)))],
    ['atalhos', 'Atalhos', 'tecla', () => P('Teclas de atalho · clique e aperte as teclas', Object.entries(E.atalhos).map(([n, k]) => L(n, `<button class="atalho" data-atalho="${n}">${k}</button>`)).join('')
      + L('Funcionam mesmo com o FAH escondido', UI.switch(true)))],
    ['backup', 'Backup', 'caixa', () => P('Suas configurações', L('Exportar tudo (presets e módulos)', '<button class="bt">Salvar arquivo…</button>') + L('Importar de um arquivo', '<button class="bt">Abrir…</button>')
      + L('Importar preset do Peace / EqualizerAPO (.peq, .txt)', '<button class="bt">Abrir…</button>')) + P('Automático', L('Backup diário', UI.switch(true)) + L('Guardar os últimos', UI.select('7 backups')))],
    ['sobre', 'Sobre', 'info', () => P('FAH', `<div class="sobre">${UI.icone('efeitos', 56, 'linear-gradient(135deg,#ff9f0a,#0a84ff)')}<div><b>FAH</b><small>Versão 1.0 · protótipo</small><small>Equalizador, efeitos e mixer do seu som</small></div></div>`)
      + P('Atualizações', L('Procurar atualização sozinho', UI.switch(true)) + L('Agora', '<button class="bt">Procurar</button>'))],
  ];
  const COR = { geral: '#8e8e93', aparencia: '#bf5af2', audio: '#ff9f0a', atalhos: '#30d158', backup: '#0a84ff', sobre: '#636366' };
  E.cfgSec = E.cfgSec || 'geral';
  // shortcut capture: modifiers + key, Esc cancels
  const nomeTecla = (e) => [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win'].filter(Boolean)
    .concat(['Control', 'Alt', 'Shift', 'Meta'].includes(e.key) ? [] : [{ ArrowRight: '→', ArrowLeft: '←', ArrowUp: '↑', ArrowDown: '↓', ' ': 'Espaço' }[e.key] || (e.key.length === 1 ? e.key.toUpperCase() : e.key)]).join(' + ');

  App.tela({ id: 'config', classe: 't-cfg rolagem',
    montar(el) {
      el.innerHTML = `<div class="menu entra">${SECOES.map(([id, n, ic]) => `<button data-sec="${id}" class="${id === E.cfgSec ? 'on' : ''}">${UI.icone(ic, 26, COR[id])}${n}</button>`).join('')}</div>
        <div class="cont entra" style="animation-delay:.06s"></div>`;
      // section change swaps only the content (no entry animation)
      el._sec = () => { const s = SECOES.find((x) => x[0] === E.cfgSec); el.querySelector('.cont').innerHTML = `<h3>${s[1]}</h3>${s[3]()}`;
        el.querySelectorAll('[data-sec]').forEach((b) => b.classList.toggle('on', b.dataset.sec === E.cfgSec)); };
      el._sec();
      if (el._ok) return; el._ok = 1;
      let ouvindo = null;
      el.addEventListener('c:mudou', (e) => { if (e.detail.dados.campo === 'tam') { E.tam = e.detail.valor; App.motor.enviar({ tipo: 'tamanho', tam: E.tam }); } });
      let tamV = E.tam; addEventListener('fah:janela', (e) => { if (e.detail.tam === tamV) return; tamV = e.detail.tam; if (E.cfgSec === 'geral') el._sec(); });
      el.addEventListener('click', (e) => { const b = e.target.closest('[data-sec]'); if (b) { E.cfgSec = b.dataset.sec; return el._sec(); }
        const a = e.target.closest('[data-atalho]'); if (a) { ouvindo?.classList.remove('ouve'); ouvindo = a; a.classList.add('ouve'); a.textContent = 'Aperte as teclas…'; } });
      addEventListener('keydown', (e) => { if (!ouvindo) return; e.preventDefault(); e.stopImmediatePropagation();
        if (e.key === 'Escape') { ouvindo.textContent = E.atalhos[ouvindo.dataset.atalho]; ouvindo.classList.remove('ouve'); ouvindo = null; return; }
        const t = nomeTecla(e); ouvindo.textContent = t + '…'; if (['Control', 'Alt', 'Shift', 'Meta'].includes(e.key)) return;
        E.atalhos[ouvindo.dataset.atalho] = t; ouvindo.textContent = t; ouvindo.classList.remove('ouve'); ouvindo = null; }, true);
    } });
  App.dock({ id: 'config', icone: 'engr', dica: 'Configurações', tela: 'config', ordem: 9 });
})();
