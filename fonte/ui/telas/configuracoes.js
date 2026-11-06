// Screen: settings = ONE page (proposal B, chosen 05/11/2026; docs/prototipos/configuracoes/ESCOLHIDA). Update card on top, then 3 columns.
// App.estado.cfg goes to the host through ponte.js (VIGIA 'cfg') and Janela.AplicarCfg applies it (Run key, hotkeys, tray, backup, updates).
// Host -> here: 'cfginfo' {versao, inicio, atalhosFalha, podeAtualizar, ultimoBackup}, 'atualizacao' {estado, versao, pct, erro}.
// App.estado.attUlt = time of the last finished update check (saved by the host like any state key).
(() => {
  const css = `
  .t-cfg .pag{display:flex;flex-direction:column;gap:24px;padding-bottom:64px}   /* last line clears the dock */
  .t-cfg h1{margin:0;font-size:26px;font-weight:700;letter-spacing:-.5px}
  .t-cfg .cols{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px;align-items:start}
  .t-cfg .col{display:flex;flex-direction:column;gap:16px;min-width:0}
  .t-cfg .grupo{background:var(--bg2);border-radius:12px;overflow:hidden}
  .t-cfg .grupo>h4{display:flex;align-items:center;gap:8px;margin:0;padding:12px 16px 4px;font-size:12px;font-weight:600;letter-spacing:.4px;text-transform:uppercase;color:var(--fg2)}
  .t-cfg .grupo>h4 .ico{width:14px;height:14px}
  .t-cfg .linha{min-height:48px;display:flex;align-items:center;gap:12px;padding:8px 16px;position:relative}
  .t-cfg .linha+.linha::before{content:"";position:absolute;left:16px;right:0;top:0;height:.5px;background:var(--linha)}
  .t-cfg .linha .nome{flex:1;min-width:0;font-size:14px;color:var(--fg)}
  .t-cfg .linha .nome small{display:block;font-size:12px;color:var(--fg2);margin-top:2px;line-height:16px}
  .t-cfg .dica{margin:0;padding:4px 16px 12px;font-size:12px;line-height:16px;color:var(--fg2)}
  .t-cfg .c-segmento{flex:none}.t-cfg .c-segmento button{padding:0 12px;height:28px}
  .t-cfg .bt{border:0;height:32px;padding:0 14px;border-radius:8px;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);cursor:pointer;white-space:nowrap;flex:none;transition:background .2s,color .2s,transform .1s}
  .t-cfg .bt:hover{background:var(--controle-forte)}.t-cfg .bt:active{transform:scale(.97)}
  .t-cfg .bt.ac{background:var(--mic);color:#fff}.t-cfg .bt.ac:hover{background:#2a94ff}
  .t-cfg .bt.perigo{background:#3a1d1d;color:#ffb4a8}.t-cfg .bt:disabled{opacity:.5;cursor:default;transform:none}
  /* shortcut chip: keys as kbd */
  .t-cfg .atalho{border:0;background:none;display:flex;align-items:center;justify-content:flex-end;gap:4px;min-width:150px;padding:4px 0;cursor:pointer;font:500 13px var(--fonte);color:var(--fg2);flex:none}
  .t-cfg .atalho kbd{font:500 12px var(--fonte);color:var(--fg);background:var(--controle);border-radius:6px;display:inline-block;height:22px;line-height:22px;padding:0 8px;min-width:24px;text-align:center;box-shadow:inset 0 -1px 0 rgba(0,0,0,.35);transition:background .2s}
  .t-cfg .atalho:hover kbd{background:var(--controle-forte)}.t-cfg .atalho.vazio{color:var(--fg3)}
  .t-cfg .atalho.falhou kbd{background:#3a1d1d;color:#ffb4a8}.t-cfg .atalho em{font-style:normal;font-size:11px;font-weight:600;color:#ffb4a8;margin-left:4px}
  .t-cfg .atalho.ouve{color:#fff;background:var(--mic);border-radius:8px;padding:6px 12px;animation:cfg-pulsa 1s infinite}
  @keyframes cfg-pulsa{50%{background:#0861c4}}
  /* update card */
  .t-cfg .att{position:relative;display:flex;align-items:center;gap:16px;background:var(--bg2);border-radius:16px;padding:16px 20px;border:1px solid transparent;transition:border-color .3s,background .3s;overflow:hidden}
  .t-cfg .att.nova{border-color:rgba(10,132,255,.6);background:linear-gradient(90deg,#0d2440,var(--bg2) 60%)}
  .t-cfg .att .icone{box-shadow:var(--sombra-icone);border-radius:14px}
  .t-cfg .att .txt{flex:1;min-width:0}
  .t-cfg .att .txt b{display:flex;align-items:center;gap:8px;font-size:17px;font-weight:600}
  .t-cfg .att .txt b .v{font-size:13px;font-weight:500;color:var(--fg2)}.t-cfg .att .txt b .ico{width:18px;height:18px;color:var(--ok)}
  .t-cfg .att .txt span{display:block;font-size:13px;color:var(--fg2);margin-top:4px;transition:color .2s}
  .t-cfg .att.erro .txt span{color:#ffb4a8}
  .t-cfg .att .auto{display:flex;align-items:center;gap:8px;font-size:13px;color:var(--fg2);padding-right:16px;margin-right:4px;border-right:.5px solid var(--linha);height:32px;white-space:nowrap}
  .t-cfg .att .bt{height:36px;padding:0 18px;font-size:14px;min-width:188px;display:flex;align-items:center;justify-content:center;gap:8px}
  .t-cfg .att .bt .ico{width:16px;height:16px}
  .t-cfg .att .notas{border:0;background:none;color:var(--mic);font:500 13px var(--fonte);cursor:pointer;padding:0;margin-left:4px}
  .t-cfg .barra-att{position:absolute;left:0;right:0;bottom:0;height:3px;background:var(--controle);opacity:0;transition:opacity .2s}
  .t-cfg .att.ocupado .barra-att{opacity:1}
  .t-cfg .barra-att i{display:block;height:100%;background:var(--mic);transition:width .14s linear}
  .t-cfg .gira{animation:cfg-gira 1s linear infinite}@keyframes cfg-gira{to{transform:rotate(360deg)}}
  .t-cfg .sobe{animation:entra .5s var(--curva) backwards}
  body.sem-anim *,body.sem-anim *::before,body.sem-anim *::after{animation-duration:.001s!important;animation-delay:0s!important;transition-duration:.001s!important;transition-delay:0s!important}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const E = App.estado;
  const PADRAO = { minimizado: true, xBandeja: true, iconeEstado: true, anim: true, backupDiario: true, backupQtd: 7, autoAtualizar: true,
    atalhos: { ligar: 'Ctrl + Alt + F', mic: 'Ctrl + Alt + M', preset: 'Ctrl + Alt + →', abrir: 'Ctrl + Alt + A' } };
  E.cfg = { ...PADRAO, ...(E.cfg || {}), atalhos: { ...PADRAO.atalhos, ...(E.cfg?.atalhos || {}) } };
  delete E.atalhos; delete E.cfgSec;                 // old keys (prototype / side-menu version)
  const C = E.cfg, info = () => E.cfginfo || {};
  const NOMES = { ligar: 'Ligar/desligar o som e o microfone', mic: 'Microfone no mudo', preset: 'Próximo preset do EQ', abrir: 'Abrir o FAH' };
  const NOTAS = 'https://github.com/finjackin1/FAH/releases/latest';   // allowed in Janela.Sites

  // animations off: CSS ones via body class, script ones (Element.animate) run instantly
  const animar = Element.prototype.animate;
  Element.prototype.animate = function (k, o) { if (!C.anim) o = typeof o === 'number' ? 1 : { ...(o || {}), duration: 1, delay: 0 }; return animar.call(this, k, o); };
  const poeAnim = () => document.body.classList.toggle('sem-anim', !C.anim);
  if (document.body) poeAnim(); else addEventListener('DOMContentLoaded', poeAnim);

  // last finished check, kept even while this screen was never opened
  addEventListener('fah:atualizacao', (e) => { if (['em-dia', 'pronta', 'erro'].includes(e.detail?.estado)) E.attUlt = Date.now(); });

  const SV = {
    power: '<path d="M12 3v8"/><path d="M6.3 6.8a8 8 0 1 0 11.4 0"/>', janela: '<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><path d="M3 9h18"/>',
    tecla: '<rect x="2.5" y="6" width="19" height="12" rx="2.5"/><path d="M6.5 10h1M10.5 10h1M14.5 10h1M8 14h8"/>',
    caixa: '<path d="M3.5 8 12 3.5 20.5 8v8L12 20.5 3.5 16z"/><path d="M3.5 8 12 12.5 20.5 8M12 12.5v8"/>',
    cabo: '<path d="M4 12h4M16 12h4"/><rect x="8" y="8" width="8" height="8" rx="2"/>', nuvem: '<path d="M12 4v11"/><path d="m7 10 5 5 5-5"/><path d="M4 19.5h16"/>',
    ok: '<path d="m5 12.5 4.5 4.5L19 7.5"/>', gira: '<path d="M20 12a8 8 0 1 1-2.4-5.7"/><path d="M20 4v4.5h-4.5"/>', roda: '<path d="M20 12a8 8 0 1 1-8-8"/>' };
  const ico = (n, w = 2, cl = '') => `<svg class="ico ${cl}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round">${SV[n]}</svg>`;
  const GRUPOS = { pc: ['Ao ligar o PC', 'power', '#30d158'], janela: ['Janela', 'janela', '#8e8e93'], atalhos: ['Atalhos', 'tecla', '#ff9f0a'],
    backup: ['Backup', 'caixa', '#0a84ff'], cabo: ['Cabo virtual', 'cabo', '#bf5af2'] };
  const inicio = () => C.inicio ?? !!info().inicio;
  const S = (k, v = C[k]) => UI.switch(!!v, { campo: k });
  const L = (nome, controle, sub = '') => `<div class="linha"><span class="nome">${nome}${sub ? `<small>${sub}</small>` : ''}</span>${controle}</div>`;
  const ultimo = () => (info().ultimoBackup ? `Último backup: ${info().ultimoBackup.split('-').reverse().join('/')}. ` : '') + 'Ficam na pasta backup, ao lado do FAH. Importar troca tudo e reinicia o FAH.';
  const dicaAtalhos = () => 'Clique e aperte as teclas, com Ctrl, Alt, Shift ou Win junto. Esc cancela, Delete tira. Funcionam com o FAH escondido.'
    + ((info().atalhosFalha || []).length ? ' "Em uso" quer dizer que outro programa já tem essas teclas: escolha outras.' : '');
  const botaoAtalho = (id) => { const k = C.atalhos[id], f = (info().atalhosFalha || []).includes(id);
    return `<button class="atalho ${k ? '' : 'vazio'} ${f ? 'falhou' : ''}" data-atalho="${id}">${k ? k.split(' + ').map((x) => `<kbd>${x}</kbd>`).join('') : 'Nenhum'}${f ? '<em>em uso</em>' : ''}</button>`; };
  const G = (g, linhas, dica = '', d = '') => { const [n, ic, cor] = GRUPOS[g];
    return `<section class="grupo sobe"><h4><span style="color:${cor};display:flex">${ico(ic)}</span>${n}</h4>${linhas}${dica ? `<p class="dica" ${d}>${dica}</p>` : ''}</section>`; };
  const pagina = () => `<div class="pag">
    <h1 class="sobe">Configurações</h1>
    <section class="att sobe" data-att></section>
    <div class="cols">
      <div class="col">${G('pc', L('Iniciar com o Windows', S('inicio', inicio())) + L('Começar escondido na bandeja', S('minimizado')))}
        ${G('janela', L('Tamanho da janela', UI.segmento([['max', 'Máx.'], ['min', 'Mín.']], E.tam === 'min' ? 'min' : 'max', { campo: 'tam' }), '1440×810 ou 1080×608 · também no maximizar')
          + L('Fechar no X manda para a bandeja', S('xBandeja')) + L('Ícone da bandeja cinza com o som desligado', S('iconeEstado')) + L('Animações', S('anim')))}</div>
      <div class="col">${G('atalhos', Object.keys(NOMES).map((id) => L(NOMES[id], botaoAtalho(id))).join(''), dicaAtalhos(), 'data-dica-at')}
        ${G('cabo', L('Procurar o cabo de novo e ver o que funciona', '<button class="bt" data-cabo-tela="1">Abrir tela do cabo</button>'))}</div>
      <div class="col">${G('backup', L('Exportar tudo (presets e módulos)', '<button class="bt" data-bk="exportar">Salvar arquivo…</button>')
          + L('Importar de um arquivo', '<button class="bt" data-bk="importar">Abrir…</button>')
          + L('Importar preset do Peace / EqualizerAPO', '<button class="bt" data-peq="1">Abrir…</button>', 'Entra no Equalizador do som, modo paramétrico')
          + L('Backup diário', S('backupDiario')) + L('Guardar os últimos', UI.segmento([[3, '3'], [7, '7'], [14, '14'], [30, '30']], C.backupQtd, { campo: 'backupQtd' })),
        ultimo(), 'data-ultimo')}</div>
    </div></div>`;

  // update card: text per host state (Atualizador: procurando, em-dia, baixando, pronta, erro; none = not checked this session)
  const quando = (t) => { if (!t) return ''; const d = new Date(t), h = d.toTimeString().slice(0, 5), hoje = new Date().toDateString();
    return d.toDateString() === hoje ? `hoje, ${h}` : new Date(Date.now() - 864e5).toDateString() === d.toDateString() ? `ontem, ${h}` : d.toLocaleDateString('pt-BR'); };
  const estadoAtt = () => { const a = E.atualizacao || {}, v = info().versao || '—', nv = a.versao || '';
    return ({
      procurando: { tit: 'Procurando…', sub: 'Olhando o GitHub do FAH', bt: 'Procurando…', tom: 'ocupado', bar: 30 },
      'em-dia': { tit: `${ico('ok', 2.4)}Você já tem a mais nova`, sub: `Versão ${v} · procurado ${quando(E.attUlt) || 'agora'}`, bt: 'Procurar de novo', tom: 'ok', ic: 'nuvem' },
      baixando: { tit: `Baixando a versão ${nv}`, sub: `${a.pct ?? 0}%`, bt: 'Baixando…', tom: 'ocupado', bar: a.pct ?? 0 },
      pronta: { tit: `Versão ${nv} pronta<button class="notas" data-notas="1">O que mudou</button>`, bt: 'Instalar e reiniciar', tom: 'nova', ic: 'gira', instalar: 1,
        sub: info().podeAtualizar ? 'O FAH fecha, troca os arquivos e abre de novo. Seus ajustes ficam.' : 'Esta cópia do FAH não se troca sozinha. Baixe a versão nova no GitHub.' },
      erro: { tit: 'Não deu para procurar', sub: `Motivo: ${a.erro || 'erro desconhecido'}. O FAH procura de novo quando abrir.`, bt: 'Tentar de novo', tom: 'erro', ic: 'nuvem' },
    })[a.estado] || { tit: `FAH <span class="v">versão ${v}</span>`, sub: E.attUlt ? `Última procura: ${quando(E.attUlt)}` : 'Ainda não procurou atualização', bt: 'Procurar atualizações', tom: '', ic: 'nuvem' }; };

  // .peq / EqualizerAPO text -> Equalizer (som), parametric bands
  const abrirPeq = () => { const i = document.createElement('input'); i.type = 'file'; i.accept = '.peq,.txt';
    i.onchange = async () => { const f = i.files[0]; if (!f) return; const r = App.eqImportar ? App.eqImportar(await f.text(), f.name.replace(/\.[^.]+$/, '')) : 'O Equalizador não está carregado.';
      App.toast?.(r, 5000); }; i.click(); };
  const mudou = () => (E.cfg = C);                  // ponte.js watches App.estado.cfg (JSON diff) and sends it
  const trocaBt = (b) => { b.insertAdjacentHTML('afterend', botaoAtalho(b.dataset.atalho)); b.remove(); };
  // key name for the host's RegisterHotKey parser (Janela.cs): modifiers + one key from e.code (layout-proof)
  const tecla = (c) => /^Key[A-Z]$/.test(c) ? c[3] : /^Digit\d$/.test(c) ? c[5] : /^F\d{1,2}$/.test(c) ? c : /^Numpad\d$/.test(c) ? 'Num ' + c[6]
    : { ArrowRight: '→', ArrowLeft: '←', ArrowUp: '↑', ArrowDown: '↓', Space: 'Espaço', Enter: 'Enter', Tab: 'Tab', Insert: 'Insert', Home: 'Home', End: 'End', PageUp: 'PageUp', PageDown: 'PageDown' }[c] || null;
  const mods = (e) => [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win'].filter(Boolean);

  App.tela({ id: 'config', classe: 't-cfg rolagem',
    montar(el) {
      el.innerHTML = pagina();
      el.querySelectorAll('.sobe').forEach((s, i) => (s.style.animationDelay = i * 40 + 'ms'));
      let pintado = '';
      // full card paint only when the state changes; download ticks patch the bar and the % (no redraw)
      const att = () => { const c = el.querySelector('[data-att]'); if (!c) return; const s = estadoAtt(), est = (E.atualizacao || {}).estado || '';
        if (est === 'baixando' && pintado === 'baixando') { c.querySelector('.barra-att i').style.width = s.bar + '%'; c.querySelector('.txt span').textContent = s.sub; return; }
        pintado = est; c.className = `att ${s.tom}`;
        c.innerHTML = `${UI.icone('efeitos', 56, 'linear-gradient(135deg,#ff9f0a,#0a84ff)')}
          <div class="txt"><b>${s.tit}</b><span>${s.sub}</span></div>
          <div class="auto">${S('autoAtualizar')}Atualizar sozinho</div>
          <button class="bt ${s.tom === 'nova' ? 'ac' : ''}" data-att-bt data-acao="${s.instalar ? 'instalar' : 'procurar'}" ${s.tom === 'ocupado' || (s.instalar && !info().podeAtualizar) ? 'disabled' : ''}>${s.tom === 'ocupado' ? ico('roda', 2.2, 'gira') : ico(s.ic)}${s.bt}</button>
          <div class="barra-att"><i style="width:${s.bar || 0}%"></i></div>`; };
      att();
      if (el._ok) return; el._ok = 1;
      let ouvindo = null, confirma = 0;
      el.addEventListener('c:mudou', (e) => { const k = e.detail.dados.campo, v = e.detail.valor;
        if (k === 'tam') { E.tam = v; return App.motor.enviar({ tipo: 'tamanho', tam: E.tam }); }
        C[k] = k === 'backupQtd' ? +v : v; mudou(); if (k === 'anim') poeAnim(); });
      let tamV = E.tam; addEventListener('fah:janela', (e) => { if (e.detail.tam === tamV) return; tamV = e.detail.tam;
        const b = el.querySelector(`[data-campo=tam][data-v="${tamV}"]`); b?.parentNode.querySelectorAll('button').forEach((x) => x.classList.toggle('on', x === b)); });
      // host info: patch only what changed
      addEventListener('fah:cfginfo', () => { const u = el.querySelector('[data-ultimo]'); if (u) u.textContent = ultimo();
        const d = el.querySelector('[data-dica-at]'); if (d) d.textContent = dicaAtalhos();
        el.querySelectorAll('[data-atalho]').forEach((b) => b !== ouvindo && trocaBt(b));
        const sw = el.querySelector('[data-campo=inicio]'); if (sw && C.inicio === undefined) sw.classList.toggle('on', inicio());
        pintado = ''; att(); });
      addEventListener('fah:atualizacao', att);
      el.addEventListener('click', (e) => {
        const a = e.target.closest('[data-atalho]'); if (a) { if (ouvindo && ouvindo !== a) parar(); ouvindo = a; a.className = 'atalho ouve'; a.textContent = 'Aperte as teclas…'; return; }
        const bk = e.target.closest('[data-bk]'); if (bk) { if (bk.dataset.bk === 'importar' && Date.now() - confirma > 4000) { confirma = Date.now(); bk.textContent = 'Clique de novo para trocar'; bk.classList.add('perigo');
          return setTimeout(() => { if (bk.isConnected) { bk.textContent = 'Abrir…'; bk.classList.remove('perigo'); } }, 4000); }
          if (bk.dataset.bk === 'importar') { bk.textContent = 'Abrir…'; bk.classList.remove('perigo'); confirma = 0; }
          return App.motor.enviar({ tipo: 'backup', acao: bk.dataset.bk }); }
        if (e.target.closest('[data-peq]')) return abrirPeq();
        if (e.target.closest('[data-cabo-tela]')) return App.cabo.abrir();
        if (e.target.closest('[data-notas]')) return App.motor.enviar({ tipo: 'abrir', url: NOTAS });
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
