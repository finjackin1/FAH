// Screen: settings = ONE place. Side list of sections, content on the right. Everything here is real (03/11):
// App.estado.cfg goes to the host through ponte.js (VIGIA 'cfg') and Janela.AplicarCfg applies it (Run key, hotkeys, tray, backup, updates).
// Host -> here: 'cfginfo' {versao, inicio, atalhosFalha, podeAtualizar, ultimoBackup}, 'atualizacao' {estado, versao, pct, erro}.
(() => {
  const css = `
  .t-cfg{display:grid;grid-template-columns:220px 1fr;gap:24px;align-items:start;padding-bottom:96px!important}   /* last line clears the dock */
  .t-cfg .menu{display:flex;flex-direction:column;gap:2px;background:var(--bg2);border-radius:var(--r-grupo);padding:8px}
  .t-cfg .menu button{display:flex;align-items:center;gap:10px;border:0;background:none;color:var(--fg);font:500 14px var(--fonte);padding:9px 10px;border-radius:9px;cursor:pointer;text-align:left;transition:background .2s}
  .t-cfg .menu button:hover{background:var(--bg2-hover)}.t-cfg .menu button.on{background:var(--ac);color:#fff}
  .t-cfg .menu .icone{flex:none}
  .t-cfg .cont{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:16px;align-items:start}
  .t-cfg .cont h3{grid-column:1/-1;font-size:var(--t-grupo);font-weight:700;letter-spacing:-.4px;margin:0 0 -2px}
  .t-cfg .atalho{min-width:150px;text-align:center;border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:7px 12px;border-radius:8px;cursor:pointer;transition:background .2s,color .2s}
  .t-cfg .atalho.vazio{color:var(--fg2)}.t-cfg .atalho.falhou{background:#3a1d1d;color:#ffb4a8}
  .t-cfg .atalho.ouve{background:var(--ac);color:#fff;animation:pisca 1s infinite}
  @keyframes pisca{50%{opacity:.6}}
  .t-cfg .dica{font-size:12px;color:var(--fg2);margin:8px 0 0;line-height:1.45}
  .t-cfg .bt{border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:8px 14px;border-radius:8px;cursor:pointer;white-space:nowrap;transition:background .2s,color .2s}
  .t-cfg .bt:hover{background:var(--bg3)}.t-cfg .bt.ac{background:var(--ac);color:#fff}.t-cfg .bt:disabled{opacity:.5;cursor:default}
  .t-cfg .sobre{display:flex;align-items:center;gap:16px}.t-cfg .sobre b{font-size:22px}.t-cfg .sobre small{display:block;color:var(--fg2);font-size:13px;margin-top:4px}
  .t-cfg .att{color:var(--fg2);font-size:13px}.t-cfg .att.erro{color:#ffb4a8}
  body.sem-anim *,body.sem-anim *::before,body.sem-anim *::after{animation-duration:.001s!important;animation-delay:0s!important;transition-duration:.001s!important;transition-delay:0s!important}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const E = App.estado;
  const PADRAO = { minimizado: true, xBandeja: true, iconeEstado: true, anim: true, backupDiario: true, backupQtd: 7, autoAtualizar: true,
    atalhos: { ligar: 'Ctrl + Alt + F', mic: 'Ctrl + Alt + M', preset: 'Ctrl + Alt + →', abrir: 'Ctrl + Alt + A' } };
  E.cfg = { ...PADRAO, ...(E.cfg || {}), atalhos: { ...PADRAO.atalhos, ...(E.cfg?.atalhos || {}) } };
  delete E.atalhos;                                  // old prototype key
  const C = E.cfg, info = () => E.cfginfo || {};
  const NOMES = { ligar: 'Ligar/desligar o som e o microfone', mic: 'Microfone no mudo', preset: 'Próximo preset do EQ', abrir: 'Abrir o FAH' };

  // animations off: CSS ones via body class, script ones (Element.animate) run instantly
  const animar = Element.prototype.animate;
  Element.prototype.animate = function (k, o) { if (!C.anim) o = typeof o === 'number' ? 1 : { ...(o || {}), duration: 1, delay: 0 }; return animar.call(this, k, o); };
  const poeAnim = () => document.body.classList.toggle('sem-anim', !C.anim);
  if (document.body) poeAnim(); else addEventListener('DOMContentLoaded', poeAnim);

  const P = (t, corpo) => `<div class="painel"><h4>${t}</h4>${corpo}</div>`, L = UI.linha, S = (k) => UI.switch(!!C[k], { campo: k });
  const inicio = () => C.inicio ?? !!info().inicio;
  const textoAtt = () => { const a = E.atualizacao; if (!a) return ['', 'Procurar agora', 0];
    const v = a.versao ? ` ${a.versao}` : '';
    return { procurando: ['Procurando…', 'Procurar agora', 0, 1], 'em-dia': ['Você já tem a versão mais nova.', 'Procurar agora', 0],
      baixando: [`Baixando a versão${v}… ${a.pct ?? 0}%`, 'Procurar agora', 0, 1], pronta: [`A versão${v} está pronta. O FAH fecha, troca os arquivos e abre de novo.`, 'Instalar agora', 1],
      erro: [`Não deu: ${a.erro || 'erro desconhecido'}.`, 'Tentar de novo', 0, 0, 1] }[a.estado] || ['', 'Procurar agora', 0]; };
  const SECOES = [
    ['geral', 'Geral', 'engr', () => P('Ao ligar o PC', L('Iniciar com o Windows', UI.switch(inicio(), { campo: 'inicio' })) + L('Começar escondido na bandeja', S('minimizado')))
      + P('Janela', L('Tamanho: Máx. 1440 × 810 ou Mín. 1080 × 608 · também segurando o maximizar', UI.segmento([['max', 'Máx.'], ['min', 'Mín.']], E.tam === 'min' ? 'min' : 'max', { campo: 'tam' }))
        + L('Fechar no X manda para a bandeja', S('xBandeja')) + L('Ícone da bandeja fica cinza com o som desligado', S('iconeEstado')) + L('Animações', S('anim')))
      + P('Cabo virtual', L('Procurar o cabo de novo e ver o que funciona com ele', '<button class="bt" data-cabo-tela="1">Abrir tela do cabo</button>'))],
    ['atalhos', 'Atalhos', 'tecla', () => P('Teclas de atalho', Object.keys(NOMES).map((id) => L(NOMES[id], botaoAtalho(id))).join('')
      + '<p class="dica">Clique e aperte as teclas, com Ctrl, Alt, Shift ou Win junto. Esc cancela, Delete tira o atalho. Funcionam com o FAH escondido e com outro programa na frente.</p>')],
    ['backup', 'Backup', 'caixa', () => P('Suas configurações', L('Exportar tudo (presets e módulos)', '<button class="bt" data-bk="exportar">Salvar arquivo…</button>')
        + L('Importar de um arquivo', '<button class="bt" data-bk="importar">Abrir…</button>')
        + L('Importar preset do Peace / EqualizerAPO', '<button class="bt" data-peq="1">Abrir…</button>')
        + '<p class="dica">Importar um backup troca as configurações atuais e reinicia o FAH. O preset do Peace entra no Equalizador do som, no modo paramétrico, e fica salvo na lista de presets.</p>')
      + P('Automático', L('Backup diário', S('backupDiario')) + L('Guardar os últimos', UI.segmento([[3, '3'], [7, '7'], [14, '14'], [30, '30']], C.backupQtd, { campo: 'backupQtd' }))
        + `<p class="dica" data-ultimo>${ultimo()}</p>`)],
    ['sobre', 'Sobre', 'info', () => P('FAH', `<div class="sobre">${UI.icone('efeitos', 56, 'linear-gradient(135deg,#ff9f0a,#0a84ff)')}<div><b>FAH</b><small data-versao>${versao()}</small><small>Equalizador, efeitos e mixer do seu som</small></div></div>`)
      + P('Atualizações', L('Procurar atualização sozinho', S('autoAtualizar')) + L(`<span class="att" data-att></span>`, '<button class="bt" data-att-bt></button>'))],
  ];
  const ultimo = () => info().ultimoBackup ? `Último backup: ${info().ultimoBackup.split('-').reverse().join('/')}. Ficam na pasta backup, ao lado do FAH.` : 'Ficam na pasta backup, ao lado do FAH.';
  const versao = () => info().versao ? `Versão ${info().versao}` : 'Versão —';
  const botaoAtalho = (id) => { const k = C.atalhos[id], f = (info().atalhosFalha || []).includes(id);
    return `<button class="atalho ${k ? '' : 'vazio'} ${f ? 'falhou' : ''}" data-atalho="${id}" title="${f ? 'Outro programa já usa essas teclas. Escolha outras.' : ''}">${k || 'Nenhum'}${f ? ' · em uso' : ''}</button>`; };
  const COR = { geral: '#8e8e93', atalhos: '#30d158', backup: '#0a84ff', sobre: '#636366' };
  if (!SECOES.some((s) => s[0] === E.cfgSec)) E.cfgSec = 'geral';
  const mudou = () => (E.cfg = C);                  // ponte.js watches App.estado.cfg (JSON diff) and sends it
  const trocaBt = (b) => { b.insertAdjacentHTML('afterend', botaoAtalho(b.dataset.atalho)); b.remove(); };
  // key name for the host's RegisterHotKey parser (Janela.cs): modifiers + one key from e.code (layout-proof)
  const tecla = (c) => /^Key[A-Z]$/.test(c) ? c[3] : /^Digit\d$/.test(c) ? c[5] : /^F\d{1,2}$/.test(c) ? c : /^Numpad\d$/.test(c) ? 'Num ' + c[6]
    : { ArrowRight: '→', ArrowLeft: '←', ArrowUp: '↑', ArrowDown: '↓', Space: 'Espaço', Enter: 'Enter', Tab: 'Tab', Insert: 'Insert', Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown' }[c] || null;
  const mods = (e) => [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win'].filter(Boolean);

  // .peq / EqualizerAPO text -> Equalizer (som), parametric bands
  const abrirPeq = () => { const i = document.createElement('input'); i.type = 'file'; i.accept = '.peq,.txt';
    i.onchange = async () => { const f = i.files[0]; if (!f) return; const r = App.eqImportar ? App.eqImportar(await f.text(), f.name.replace(/\.[^.]+$/, '')) : 'O Equalizador não está carregado.';
      App.toast?.(r, 5000); }; i.click(); };

  App.tela({ id: 'config', classe: 't-cfg rolagem',
    montar(el) {
      el.innerHTML = `<div class="menu entra">${SECOES.map(([id, n, ic]) => `<button data-sec="${id}" class="${id === E.cfgSec ? 'on' : ''}">${UI.icone(ic, 26, COR[id])}${n}</button>`).join('')}</div>
        <div class="cont entra" style="animation-delay:.06s"></div>`;
      const att = () => { const s = el.querySelector('[data-att]'), b = el.querySelector('[data-att-bt]'); if (!s) return;
        const [t, bt, instalar, ocupado, erro] = textoAtt(); s.textContent = t; s.classList.toggle('erro', !!erro);
        b.textContent = bt; b.classList.toggle('ac', !!instalar); b.disabled = !!ocupado || (instalar && !info().podeAtualizar); b.dataset.acao = instalar ? 'instalar' : 'procurar'; };
      // section change swaps only the content (no entry animation)
      el._sec = () => { const s = SECOES.find((x) => x[0] === E.cfgSec); el.querySelector('.cont').innerHTML = `<h3>${s[1]}</h3>${s[3]()}`;
        el.querySelectorAll('[data-sec]').forEach((b) => b.classList.toggle('on', b.dataset.sec === E.cfgSec)); att(); };
      el._sec();
      if (el._ok) return; el._ok = 1;
      let ouvindo = null, confirma = 0;
      el.addEventListener('c:mudou', (e) => { const k = e.detail.dados.campo, v = e.detail.valor;
        if (k === 'tam') { E.tam = v; return App.motor.enviar({ tipo: 'tamanho', tam: E.tam }); }
        C[k] = k === 'backupQtd' ? +v : v; mudou(); if (k === 'anim') poeAnim(); });
      let tamV = E.tam; addEventListener('fah:janela', (e) => { if (e.detail.tam === tamV) return; tamV = e.detail.tam;
        const b = el.querySelector(`[data-campo=tam][data-v="${tamV}"]`); b?.parentNode.querySelectorAll('button').forEach((x) => x.classList.toggle('on', x === b)); });
      // host info: patch only what changed on the open section
      addEventListener('fah:cfginfo', () => { el.querySelector('[data-versao]')?.replaceChildren(versao()); const u = el.querySelector('[data-ultimo]'); if (u) u.textContent = ultimo();
        el.querySelectorAll('[data-atalho]').forEach((b) => b !== ouvindo && trocaBt(b));
        const sw = el.querySelector('[data-campo=inicio]'); if (sw && C.inicio === undefined) sw.classList.toggle('on', inicio()); att(); });
      addEventListener('fah:atualizacao', att);
      el.addEventListener('click', (e) => { const b = e.target.closest('[data-sec]'); if (b) { E.cfgSec = b.dataset.sec; return el._sec(); }
        const a = e.target.closest('[data-atalho]'); if (a) { if (ouvindo && ouvindo !== a) parar(); ouvindo = a; a.classList.add('ouve'); a.classList.remove('falhou', 'vazio'); a.textContent = 'Aperte as teclas…'; return; }
        const bk = e.target.closest('[data-bk]'); if (bk) { if (bk.dataset.bk === 'importar' && Date.now() - confirma > 4000) { confirma = Date.now(); bk.textContent = 'Clique de novo para trocar';
          return setTimeout(() => { if (bk.isConnected) bk.textContent = 'Abrir…'; }, 4000); }
          bk.textContent = bk.dataset.bk === 'importar' ? 'Abrir…' : bk.textContent; return App.motor.enviar({ tipo: 'backup', acao: bk.dataset.bk }); }
        if (e.target.closest('[data-peq]')) return abrirPeq();
        if (e.target.closest('[data-cabo-tela]')) return App.cabo.abrir();
        const at = e.target.closest('[data-att-bt]'); if (at) App.motor.enviar({ tipo: 'atualizar', acao: at.dataset.acao }); });
      const parar = () => { if (!ouvindo) return; const b = ouvindo; ouvindo = null; trocaBt(b); };
      addEventListener('keydown', (e) => { if (!ouvindo) return; e.preventDefault(); e.stopImmediatePropagation();
        if (e.key === 'Escape') return parar();
        if ((e.key === 'Delete' || e.key === 'Backspace') && !mods(e).length) { C.atalhos[ouvindo.dataset.atalho] = ''; mudou(); return parar(); }
        const m = mods(e), k = tecla(e.code);
        if (!k) { ouvindo.textContent = (m.length ? m.join(' + ') + ' + ' : '') + '…'; return; }
        if (!m.length) { ouvindo.textContent = 'Use Ctrl, Alt, Shift ou Win junto'; return; }
        const t = [...m, k].join(' + '), id = ouvindo.dataset.atalho;
        Object.keys(C.atalhos).forEach((o) => { if (o !== id && C.atalhos[o] === t) C.atalhos[o] = ''; });   // one combo, one action
        C.atalhos[id] = t; mudou(); parar(); el.querySelectorAll('[data-atalho]').forEach(trocaBt); }, true);
      addEventListener('pointerdown', (e) => { if (ouvindo && !e.target.closest('[data-atalho]')) parar(); }, true);
    } });
  App.dock({ id: 'config', icone: 'engr', dica: 'Configurações', tela: 'config', ordem: 9 });
})();
