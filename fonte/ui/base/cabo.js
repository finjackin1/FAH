// Cable screen (proposal B of docs/prototipos/cabo2, chosen 05/11/2026; see ESCOLHIDA.md). Automatic: covers everything below the title bar
// while there is no usable cable (FAH downloads/installs VB-CABLE by itself); no way to skip it. Closes by itself once a cable works.
// Manual (App.cabo.abrir(): Settings, dimmed Som switch): same screen with what works with this cable; Esc/Voltar closes.
// Engine sends 'cabo' {estado: achou|falta|desativado|servico|instalando|reiniciar, marca, desativados[], erro, fase: baixando|instalando|conferindo|mic-baixando|mic|mic-conferindo, pct}.
// mic-* = VAC Lite for the mic (06/11): its setup is not silent, so phase 'mic' tells the user what to click.
// Buttons: Tentar de novo -> 'cabo-instalar' (service state: 'cabo?'), Verificar de novo -> 'cabo?', site/settings -> 'abrir'.
// App.cabo.semSom()/motivo() = what screens use to dim what needs a cable. Browser tests: App.cabo.ver('falta', {erro}) shows a state without the engine.
(() => {
  const wv = window.chrome && chrome.webview, manda = (m) => wv && wv.postMessage(m);
  const CABOS = [
    { nome: 'VB-CABLE', site: 'https://vb-audio.com/Cable/', diz: 'Grátis. É o que o FAH instala sozinho.', rec: 1 },
    { nome: 'Virtual Audio Cable', site: 'https://vac.muzychenko.net/en/download.htm', diz: 'Pago. O único com Som 7.1 completo.' },
    { nome: 'Voicemeeter Banana', site: 'https://vb-audio.com/Voicemeeter/banana.htm', diz: 'Grátis. Mesa de som com cabos inclusos.' },
  ];
  const ICO = {
    cabo: '<path d="M9 3v4M15 3v4"/><rect x="6" y="7" width="12" height="6" rx="2"/><path d="M12 13v3a4 4 0 0 1-4 4H6"/>',
    ok: '<path d="m5 12.5 4.5 4.5L19 7.5"/>', site: '<path d="M14 4h6v6M20 4l-9 9"/><path d="M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/>',
    gira: '<path d="M20 12a8 8 0 1 1-2.34-5.66"/><path d="M20 4v4h-4"/>', aviso: '<path d="M12 4 21 20H3z"/><path d="M12 10v4M12 17v.5"/>',
    baixa: '<path d="M12 4v11M7 10l5 5 5-5M5 20h14"/>', escudo: '<path d="M12 3 5 6v6c0 4 3 7.5 7 9 4-1.5 7-5 7-9V6z"/><path d="m9 12 2 2 4-4"/>',
    reinicia: '<path d="M12 3v6"/><path d="M6.3 7.3a8 8 0 1 0 11.4 0"/>', semnet: '<path d="M2 8.5a15 15 0 0 1 20 0M5 12a10 10 0 0 1 14 0M8.5 15.5a5 5 0 0 1 7 0"/><path d="M3 3l18 18"/>',
    mic: '<rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3"/>',
    sete: '<circle cx="12" cy="12" r="3"/><path d="M5 5l2 2M19 5l-2 2M5 19l2-2M19 19l-2-2"/>',
    nuvem: '<path d="M7 18a4 4 0 0 1-.5-8A6 6 0 0 1 18 9a4.5 4.5 0 0 1-.5 9z"/>',
    ajuste: '<path d="M4 6h10M18 6h2M4 12h4M12 12h8M4 18h12M20 18h0"/><circle cx="16" cy="6" r="2"/><circle cx="10" cy="12" r="2"/><circle cx="18" cy="18" r="2"/>',
    seta: '<path d="m9 6 6 6-6 6"/>',
  };
  const ico = (n) => `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${ICO[n]}</svg>`;
  const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  // endpoint "CABLE Input (VB-Audio Virtual Cable)" -> "CABLE Input" (what Windows Settings lists in bold)
  const curto = (n) => String(n).replace(/\s*\([^)]*\)\s*$/, '');
  const S = App.estado;

  // ---- scenes (texts = prototype). Row status: g = working, ok, nao = not available (does not block), x = blocks, '' = waiting
  const ESPERA = ['', 'Na fila'], TRACO = ['', 'Depois do cabo'], NEUTRO = ['', '—'];
  const GENERICO = 'O instalador do VB-CABLE falhou.';
  const attReal = (procurando) => { const a = S.atualizacao;
    if (!a || !a.estado) return procurando ? ['g', 'Procurando…'] : NEUTRO;
    return ({ procurando: ['g', 'Procurando…'], 'em-dia': ['ok', 'Em dia'], baixando: ['g', `Baixando ${a.pct ?? 0}%`], pronta: ['ok', 'Nova versão pronta'], erro: ['nao', 'Não consegui'] })[a.estado] || NEUTRO; };
  const micReal = () => { const m = S.mic; return !m ? NEUTRO : m.semCabo ? ['nao', 'Falta outro cabo'] : m.erro && !m.ligado ? ['nao', 'Não ligou'] : ['ok', m.ligado ? 'Ligado' : 'Pronto']; };
  const s71Real = () => { const c = S.cabo71; return !c ? NEUTRO : c.suporte === false ? ['nao', 'Só estéreo'] : c.on ? ['ok', 'Ligado'] : ['nao', 'Cabo em estéreo']; };
  const L = (cabo, mic, s71, att) => ({ cabo, mic, s71, att });
  const CENAS = {
    procurando: () => ({ ic: 'cabo', t: 'Preparando o FAH', p: 'Procurando cabos, microfone, 7.1 e atualização. Não precisa fazer nada.', l: L(['g', 'Procurando…'], ESPERA, ESPERA, attReal(1)) }),
    baixando: (d) => ({ ic: 'baixa', t: 'Instalando o cabo virtual', p: 'Não achei nenhum cabo neste PC, então o FAH está baixando o VB-CABLE, que é grátis.', pct: d.pct ?? 0, l: L(['g', d.pct != null ? `Baixando ${d.pct}%` : 'Baixando…'], ESPERA, ESPERA, attReal()) }),
    instalando: () => ({ ic: 'escudo', t: 'Aceite o pedido do Windows', p: 'O Windows vai perguntar se pode instalar o VB-CABLE. Clique em Sim. Se aparecer “Instalar software de dispositivo”, clique em Instalar.', pct: 100, l: L(['g', 'Instalando…'], ESPERA, ESPERA, attReal()) }),
    'mic-baixando': (d) => ({ ic: 'baixa', t: 'Instalando o cabo do microfone', p: 'O som do PC já tem cabo. Agora o FAH está baixando o Virtual Audio Cable Lite, que é grátis, para o microfone.', pct: d.pct ?? 0, l: L(['ok', d.marca || 'Cabo virtual'], ['g', d.pct != null ? `Baixando ${d.pct}%` : 'Baixando…'], ESPERA, attReal()) }),
    mic: (d) => ({ ic: 'escudo', t: 'Instale o cabo do microfone', p: 'Clique em Sim no pedido do Windows. Na janela do Virtual Audio Cable, aceite a licença, deixe a pasta como está e confirme. O FAH continua sozinho quando terminar.', pct: 100, l: L(['ok', d.marca || 'Cabo virtual'], ['g', 'Instalando…'], ESPERA, attReal()) }),
    'mic-conferindo': (d) => ({ ic: 'mic', t: 'Quase lá', p: 'Cabo do microfone instalado. Esperando o Windows mostrar o cabo.', l: L(['ok', d.marca || 'Cabo virtual'], ['g', 'Conferindo…'], ESPERA, attReal()) }),
    conferindo: () => ({ ic: 'cabo', t: 'Quase lá', p: 'VB-CABLE instalado. Esperando o Windows mostrar o cabo.', l: L(['g', 'Conferindo…'], ESPERA, ESPERA, attReal()) }),
    reiniciar: () => ({ ic: 'reinicia', t: 'Reinicie o PC', p: 'O VB-CABLE foi instalado, mas o Windows só mostra o cabo depois de reiniciar. O FAH abre sozinho na volta.', l: L(['x', 'Falta reiniciar'], TRACO, TRACO, attReal()), bts: ['verificar'] }),   // no engine message to restart the PC: only re-check
    'sem-internet': () => ({ ic: 'semnet', t: 'Sem internet para baixar o cabo', p: 'O FAH precisa de um cabo virtual e tentou baixar o VB-CABLE, mas não conseguiu. Ligue a internet e clique em Tentar de novo.', l: L(['x', 'Sem internet'], TRACO, TRACO, S.atualizacao?.estado ? attReal() : ['nao', 'Sem internet']), bts: ['tentar'], manual: 1 }),
    recusado: () => ({ ic: 'escudo', t: 'O Windows não deixou instalar', p: 'Para instalar o cabo virtual o Windows pede permissão. Clique em Tentar de novo e responda Sim.', l: L(['x', 'Sem permissão'], TRACO, TRACO, attReal()), bts: ['tentar'], manual: 1 }),
    falhou: (d) => ({ ic: 'aviso', t: 'O instalador do cabo falhou', p: (d.erro && d.erro !== GENERICO ? d.erro + ' ' : '') + 'Tente de novo. Se não der, instale um destes cabos pelo site e o FAH acha sozinho.', l: L(['x', 'Falhou'], TRACO, TRACO, attReal()), bts: ['tentar'], manual: 1 }),
    desativado: (d) => { const n = (d.desativados || []).map(curto).filter(Boolean);
      return { ic: 'ajuste', t: 'O cabo está bloqueado no Windows', p: `Achei o ${d.marca || 'cabo virtual'}, mas o Windows não deixou o FAH ligar. Abra as configurações de som, ache ${n.length ? n.map((x) => `“${x}”`).join(' e ') : '“CABLE Input” e “CABLE Output”'} e clique em Permitir.`, l: L(['x', 'Desativado'], TRACO, TRACO, attReal()), bts: ['config', 'verificar'] }; },
    servico: () => ({ ic: 'aviso', t: 'O som do Windows não respondeu', p: 'O serviço de áudio do Windows está parado ou travado. Clique em Tentar de novo. Se continuar, reinicie o PC.', l: L(['x', 'Sem resposta'], TRACO, TRACO, attReal()), bts: ['tentar'] }),
    achou: (d) => ({ ic: 'ok', t: 'Tudo pronto', p: 'Abrindo o FAH…', l: L(['ok', d.marca || 'Cabo virtual'], micReal(), s71Real(), attReal()), fecha: 1 }),
    umcabo: (d) => ({ ic: 'ok', t: 'Pronto, com um cabo', p: 'O som do PC já passa pelo FAH. O microfone precisa de um segundo cabo e fica desligado.', l: L(['ok', d.marca || 'Cabo virtual'], ['nao', 'Falta outro cabo'], s71Real(), attReal()), fecha: 1 }),
    manual: (d) => ({ ic: 'cabo', t: 'Cabos do FAH', p: S.mic?.podeInstalar ? 'O som do PC já passa pelo FAH. O microfone precisa de um segundo cabo, e o FAH instala um grátis.' : 'O que o FAH achou neste PC e o que funciona com isso.', l: L(['ok', d.marca || 'Cabo virtual'], micReal(), s71Real(), attReal()), bts: S.mic?.podeInstalar ? ['micinst', 'verificar', 'voltar'] : ['verificar', 'voltar'], manual: 1 }),
  };
  const LINHAS = [['cabo', 'cabo', 'Cabo virtual'], ['mic', 'mic', 'Microfone'], ['s71', 'sete', 'Som 7.1'], ['att', 'nuvem', 'Atualização']];
  const BTS = { tentar: ['gira', 'Tentar de novo', 1], verificar: ['gira', 'Verificar de novo'], config: ['ajuste', 'Abrir configurações de som', 1], voltar: ['', 'Voltar'], micinst: ['baixa', 'Instalar cabo do microfone', 1] };
  const bt = (k, pri) => { const [i, t, p] = BTS[k]; return `<button class="cb-bt${(pri ?? p) ? ' pri' : ''}" data-cb="${k}">${i ? ico(i) : ''}${t}</button>`; };
  const stIco = (s) => s === 'g' ? `<i class="cb-gi">${ico('gira')}</i>` : s === 'ok' ? ico('ok') : s === 'x' || s === 'nao' ? ico('aviso') : '<i class="cb-pt"></i>';
  const site = (c) => `<button class="cb-site" data-cb="site" data-url="${c.site}" title="${c.site}">${ico('site')}Site</button>`;

  // engine state -> scene key
  const cenaDe = (e, d) => {
    if (e === 'verificando') return 'procurando';
    if (e === 'instalando') return ['instalando', 'conferindo', 'mic-baixando', 'mic', 'mic-conferindo'].includes(d.fase) ? d.fase : 'baixando';
    if (e === 'falta') return !d.erro ? 'procurando' : /internet/i.test(d.erro) ? 'sem-internet' : /permiss/i.test(d.erro) ? 'recusado' : 'falhou';
    if (e === 'achou') return manual ? 'manual' : S.mic?.semCabo ? 'umcabo' : 'achou';
    return CENAS[e] ? e : 'falhou';
  };

  const el = document.createElement('div'); el.id = 'cabo'; el.className = 'cb'; el.hidden = true; el.tabIndex = -1; el.style.outline = 'none';
  el.setAttribute('role', 'alertdialog'); el.setAttribute('aria-modal', 'true'); el.setAttribute('aria-labelledby', 'cb-t');
  el.innerHTML = `<div class="cb-col"><div class="cb-anel"><svg class="cb-r" viewBox="0 0 96 96"><circle class="f" cx="48" cy="48" r="46"/><circle class="p" cx="48" cy="48" r="46"/></svg><div class="cb-ic"></div></div>
    <h1 id="cb-t"></h1><p></p><div class="cb-lista"></div><div class="cb-bts"></div><div class="cb-extra"></div></div>`;
  document.getElementById('app').append(el);
  const $ = (s) => el.querySelector(s);
  let estado = null, dados = {}, espera = 0, fecha = 0, manual = false, aVolta = null, extraOn = null, ouve = null;
  const set = (box, html) => { if (box.innerHTML !== html) box.innerHTML = html; };

  // ---- what needs a cable (reason texts = same for the dim cards, the switch and the 7.1 dock button)
  const semSom = () => { const c = S.cabo; return !!c && !!c.estado && c.estado !== 'achou' && !S.motor?.ligado; };   // unknown yet = no dim (no flash at start); Som on = the cable is working, whatever a re-check said
  const motivo = () => ({ desativado: 'O cabo está desligado no Windows', servico: 'O som do Windows não respondeu', instalando: 'O FAH está instalando o cabo virtual', reiniciar: 'Falta reiniciar o PC para o cabo aparecer' }[S.cabo?.estado] || 'Falta um cabo virtual');

  const pinta = () => {
    const k = cenaDe(estado, dados), c = CENAS[k](dados), bts = [...(c.bts || [])];
    if (manual && k !== 'manual' && !bts.includes('voltar')) bts.push('voltar');
    const t = $('h1'), p = $('.cb-col>p');
    if (t.textContent !== c.t) t.textContent = c.t;
    if (p.textContent !== c.p) p.textContent = c.p;
    set($('.cb-ic'), ico(c.ic));
    el.dataset.estado = k; el.classList.toggle('vf', estado === 'verificando'); el.classList.toggle('x', c.l.cabo[0] === 'x');
    $('.cb-anel .p').style.strokeDashoffset = c.pct != null ? 289 * (1 - c.pct / 100) : '';
    set($('.cb-lista'), LINHAS.map(([n, i, nome]) => { const [s, tx] = c.l[n]; return `<div class="cb-ln ${s}">${ico(i)}<b>${nome}</b><span>${stIco(s)}${esc(tx)}</span></div>`; }).join(''));
    set($('.cb-bts'), bts.map((b) => bt(b, bts.length === 1 && b === 'verificar' ? 1 : undefined)).join(''));
    const quer = !!c.manual, ex = $('.cb-extra');
    if (extraOn !== quer) { extraOn = quer;
      ex.innerHTML = quer ? `<button class="cb-mais" data-cb="mais" aria-expanded="false">${ico('seta')}Ou instale outro cabo pelo site</button>
        <div class="cb-sites"><div><div class="cb-lista">${CABOS.map((x) => `<div class="cb-it"><b>${x.nome}${x.rec ? '<span class="cb-rec">Recomendado</span>' : ''}</b><small>${x.diz}</small>${site(x)}</div>`).join('')}</div></div></div>` : ''; }
    return c;
  };
  const foco = () => { if (el.contains(document.activeElement) && document.activeElement !== el) return; (el.querySelector('.cb-bt.pri') || el.querySelector('.cb-bt') || el).focus(); };

  const ver = (e, d = {}) => {
    const saindo = el.classList.contains('sai');
    if ((el.hidden || saindo) && !manual && e === 'achou') { estado = e; dados = d; return; }   // cable there: stays closed (or keeps closing)
    clearTimeout(fecha); estado = e; dados = d;
    if (el.hidden || saindo) { el.hidden = false; el.classList.remove('sai', 'entra-cb'); void el.offsetWidth; el.classList.add('entra-cb'); }   // new problem mid fade-out: back in
    const c = pinta();
    if (e === 'achou') manda({ tipo: 'cabo71?' });
    if (!manual && c.fecha) { fecha = setTimeout(fechar, 1600); return; }
    foco();
  };
  const fechar = () => { clearTimeout(fecha); clearTimeout(espera); if (ouve) { removeEventListener('fah:cabo', ouve); ouve = null; }
    if (estado === 'verificando') { estado = S.cabo?.estado ?? null; dados = S.cabo || {}; }   // closed mid re-check: no late "service" reopen
    manual = false; el.classList.add('sai'); const v = aVolta; aVolta = null;
    fecha = setTimeout(() => { el.hidden = true; el.classList.remove('sai', 'entra-cb', 'x'); extraOn = null; if (v?.isConnected) v.focus(); }, 360); };

  // check again: at least 0.8 s of "procurando" (no flash), engine silent for 6 s = service problem (never stuck)
  const verificar = () => {
    if (estado === 'verificando') return;
    const t0 = performance.now(); ver('verificando', dados); clearTimeout(espera);
    espera = setTimeout(() => ver('servico'), 6000);
    if (ouve) removeEventListener('fah:cabo', ouve);
    const r = ouve = (ev) => { removeEventListener('fah:cabo', r); ouve = null; clearTimeout(espera); const d = ev.detail || {};
      setTimeout(() => ver(d.estado, d), Math.max(0, 800 - (performance.now() - t0))); };
    addEventListener('fah:cabo', r);
    // browser tests: App.cabo.simular() = next reply (null = engine silent)
    if (wv) manda({ tipo: 'cabo?' }); else setTimeout(() => { const d = App.cabo.simular ? App.cabo.simular() : { estado: 'falta', erro: 'Sem internet para baixar o VB-CABLE.' }; if (d) dispatchEvent(new CustomEvent('fah:cabo', { detail: d })); }, 300);
  };
  // from Settings or a dimmed Som switch: current state + what works; closes back to where it came from
  const abrir = () => { if (!el.hidden && !el.classList.contains('sai')) return;   // already open (locked stays locked)
    clearTimeout(fecha); aVolta = document.activeElement; manual = true; el.classList.remove('sai'); manda({ tipo: 'cabo71?' });
    if (estado === null) { verificar(); return; } ver(estado, dados); };

  el.addEventListener('click', (ev) => { const b = ev.target.closest('[data-cb]'); if (!b) return; const a = b.dataset.cb;
    if (a === 'verificar') verificar();
    else if (a === 'tentar') { if (estado === 'servico') verificar(); else manda({ tipo: 'cabo-instalar' }); }
    else if (a === 'voltar') { if (manual) fechar(); }
    else if (a === 'micinst') manda({ tipo: 'cabo-mic-instalar' });
    else if (a === 'mais') { const on = b.getAttribute('aria-expanded') !== 'true'; b.setAttribute('aria-expanded', on); $('.cb-sites').classList.toggle('on', on); }
    else if (a === 'site') { manda({ tipo: 'abrir', url: b.dataset.url }); b.classList.add('abriu'); }
    else if (a === 'config') { manda({ tipo: 'abrir', url: 'ms-settings:sound' }); b.classList.add('abriu'); } });
  // locked: Esc does nothing (would go back a screen underneath); manual: closes. Tab cycles only inside the screen
  addEventListener('keydown', (e) => { if (el.hidden || el.classList.contains('sai')) return;
    if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); if (manual) fechar(); return; }
    if (e.key !== 'Tab') return;
    const f = [...el.querySelectorAll('button')].filter((x) => x.offsetParent && getComputedStyle(x).visibility !== 'hidden'); e.preventDefault(); e.stopImmediatePropagation();
    if (!f.length) { el.focus(); return; }
    const i = f.indexOf(document.activeElement), n = e.shiftKey ? (i <= 0 ? f.length - 1 : i - 1) : (i + 1) % f.length; f[n].focus(); }, true);
  addEventListener('fah:cabo', (ev) => { S.cabo = ev.detail || {}; if (estado !== 'verificando') ver(S.cabo.estado, S.cabo); });
  ['mic', 'cabo71', 'atualizacao'].forEach((t) => addEventListener('fah:' + t, () => { if (!el.hidden && estado) { pinta(); foco(); } }));
  App.cabo = { ver, verificar, abrir, semSom, motivo, get estado() { return estado; }, get aberto() { return !el.hidden; }, get manual() { return manual; } };
})();
