// Cable screen (proposal B, chosen 29/10/2026; docs/prototipos/cabo/ESCOLHIDA). Automatic: covers everything below the title bar
// when no cable is found, until the user clicks "Continuar sem cabo" (cfg.caboPassou = never comes up by itself again).
// Manual (04/11, App.cabo.abrir(): Settings, dimmed Som switch): same screen plus what works with this cable; Esc/Voltar closes.
// Engine sends 'cabo' {estado: achou|falta|desativado|servico, marca, nome, desativados[]}; "Verificar de novo" -> 'cabo?'; site/settings -> 'abrir'
// (C# opens only vendor sites + ms-settings:sound). App.cabo.semSom()/motivo() = what screens use to dim what needs a cable.
// Browser tests: App.cabo.ver('falta' | ..., dados) shows a state without the engine.
(() => {
  const wv = window.chrome && chrome.webview, manda = (m) => wv && wv.postMessage(m);
  const CABOS = {
    gratis: [
      { nome: 'VB-CABLE', site: 'https://vb-audio.com/Cable/', diz: 'Um cabo, só estéreo. O mais simples de instalar.', rec: 1 },
      { nome: 'Voicemeeter Banana', site: 'https://vb-audio.com/Voicemeeter/banana.htm', diz: 'Mesa de som com cabos inclusos. Pede doação depois de 30 dias.' },
      { nome: 'Virtual Audio Cable Lite', site: 'https://vac.muzychenko.net/en/download.htm', diz: 'Só estéreo e só para uso em casa.' },
    ],
    pagos: [
      { nome: 'Virtual Audio Cable', site: 'https://vac.muzychenko.net/en/download.htm', diz: 'O que o FAH usa melhor: Som 7.1 completo. Tem teste grátis.', rec: 1 },
      { nome: 'VB-CABLE A+B', site: 'https://vb-audio.com/Cable/', diz: 'Mais dois cabos, para quem já usa o VB-CABLE.' },
      { nome: 'Voicemeeter Potato', site: 'https://vb-audio.com/Voicemeeter/potato.htm', diz: 'Mesa de som maior, com mais cabos.' },
    ],
  };
  const ICO = {
    cabo: '<path d="M9 3v4M15 3v4"/><rect x="6" y="7" width="12" height="6" rx="2"/><path d="M12 13v3a4 4 0 0 1-4 4H6"/>',
    ok: '<path d="m5 12.5 4.5 4.5L19 7.5"/>', site: '<path d="M14 4h6v6M20 4l-9 9"/><path d="M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/>',
    gira: '<path d="M20 12a8 8 0 1 1-2.34-5.66"/><path d="M20 4v4h-4"/>', aviso: '<path d="M12 4 21 20H3z"/><path d="M12 10v4M12 17v.5"/>',
    ajuste: '<path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0-.73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/>',   // gear (Lucide settings, ISC); the old rays read as a sun
    presente: '<path d="M4 11h16v9H4zM3 7h18v4H3zM12 7v13"/>', carteira: '<rect x="3" y="6" width="18" height="13" rx="2"/><path d="M16 12.5h2M3 10h18"/>',
  };
  const ico = (n) => `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${ICO[n]}</svg>`;
  const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  // endpoint "CABLE Input (VB-Audio Virtual Cable)" -> "CABLE Input" (what Windows Settings lists in bold)
  const curto = (n) => esc(String(n).replace(/\s*\([^)]*\)\s*$/, ''));
  const lista = (l) => l.map((n) => `<b>${curto(n)}</b>`).reduce((a, b, i, t) => a + (i === t.length - 1 ? ' e ' : ', ') + b);
  const TEXTO = {
    verificando: () => ['Procurando o cabo virtual…', 'Só um instante.'],
    falta: () => ['Falta um cabo virtual', 'O FAH passa todo o som do PC por um cabo virtual antes de chegar no seu fone. Instale um destes e clique em Verificar de novo.'],
    desativado: (d) => ['O cabo está desligado no Windows', `Achei o ${d.marca || 'cabo virtual'} instalado, mas desativado nas configurações de som. Ative ${(d.desativados || []).map(curto).join(' e ') || 'o cabo'} e clique em Verificar de novo.`],
    servico: () => ['O som do Windows não respondeu', 'O serviço de áudio do Windows está parado ou demorou para responder, então não deu para procurar o cabo. Clique em Verificar de novo. Se continuar assim, reinicie o PC e abra o FAH de novo.'],
    achou: (d, manual) => ['Cabo encontrado', manual ? `O FAH está usando o ${d.marca || 'cabo virtual'}.` : `${d.marca || 'Cabo virtual'} pronto. Abrindo o FAH…`],
  };
  const PASSOS = {
    falta: () => ['<b>Instale</b> um dos cabos ao lado.', 'Se o instalador pedir, <b>reinicie o PC</b>.', 'Volte aqui e clique em <b>Verificar de novo</b>.'],
    desativado: (d) => ['Clique em <b>Abrir configurações de som</b>.', `Ache ${(d.desativados || []).length ? lista(d.desativados) : 'o cabo'} e clique em <b>Permitir</b>.`, 'Volte aqui e clique em <b>Verificar de novo</b>.'],
  };
  const item = (c) => `<div class="cb-it"><b>${c.nome}${c.rec ? '<span class="cb-rec">Recomendado</span>' : ''}</b><small title="${c.diz}">${c.diz}</small><button class="cb-site" data-cb="site" data-url="${c.site}" title="${c.site}">${ico('site')}Site</button></div>`;
  const grupo = (t, i, l) => `<div class="cb-grupo"><h3>${ico(i)}${t}</h3><div class="cb-lista">${l.map(item).join('')}</div></div>`;

  const el = document.createElement('div'); el.id = 'cabo'; el.className = 'cb'; el.hidden = true;
  el.setAttribute('role', 'alertdialog'); el.setAttribute('aria-modal', 'true'); el.setAttribute('aria-labelledby', 'cb-t');
  el.innerHTML = `<div class="cb-esq"><div class="cb-ic"></div><h1 id="cb-t"></h1><p></p><div class="cb-passos"></div><div class="cb-funcs"></div>
    <div class="cb-bts"><button class="cb-bt pri" data-cb="verificar">${ico('gira')}Verificar de novo</button><button class="cb-bt" data-cb="ok"></button></div>
    <div class="cb-dica">O que precisa de cabo fica apagado até você instalar um.</div></div><div class="cb-dir"></div>`;
  document.getElementById('app').append(el);
  const $ = (s) => el.querySelector(s);
  let estado = null, dados = {}, espera = 0, fecha = 0, manual = false, aVolta = null;

  // ---- what needs a cable (reason texts = same for the dim cards, the switch and the 7.1 dock button)
  const S = App.estado;
  const semSom = () => { const c = S.cabo; return !!c && !!c.estado && c.estado !== 'achou' && !S.motor?.ligado; };   // unknown yet = no dim (no flash at start); Som on = the cable is working, whatever a re-check said
  const motivo = () => ({ desativado: 'O cabo está desligado no Windows', servico: 'O som do Windows não respondeu' }[S.cabo?.estado] || 'Falta um cabo virtual');
  const FUNC_SEM = 'Sem cabo virtual, o som vai direto para o fone, sem equalizador e sem efeitos.';
  const funcs = () => { const sem = semSom(), m = S.mic, c = S.cabo71, curtoSem = motivo() === 'Falta um cabo virtual' ? 'Precisa de um cabo' : 'Cabo indisponível';
    const som = sem ? [0, curtoSem, FUNC_SEM] : [1, 'Funciona', S.cabo?.marca ? `O som do PC passa pelo ${S.cabo.marca}.` : 'O som do PC passa pelo cabo virtual.'];
    const micro = !m ? [2, '…', ''] : m.ligado ? [1, 'Funciona', 'Sua voz passa pelo FAH antes de ir para as chamadas.']
      : m.semCabo ? [0, sem ? curtoSem : 'Falta um segundo cabo', m.erro || 'O microfone precisa de um cabo só para ele.']
      : m.erro ? [0, 'Não ligou', m.erro] : [1, 'Desligado', 'Pronto para ligar na aba Microfone do Início.'];
    const j71 = sem ? [0, curtoSem, 'Jogos em 7.1 precisam do Virtual Audio Cable, versão completa.']
      : !c ? [2, '…', ''] : c.suporte === false ? [0, 'Só estéreo', c.motivo || 'Este cabo só passa estéreo.']
      : c.on ? [1, 'Funciona', 'Os jogos podem mandar 7.1 para o cabo.'] : [0, 'Cabo em estéreo', c.erro || 'O cabo não está em 7.1 no Windows.'];
    return [['Som do PC', som], ['Microfone', micro], ['Jogos em 7.1', j71]].map(([n, [ok, st, why]]) =>
      `<div class="cb-fn ${ok === 1 ? 'ok' : ok === 0 ? 'nao' : ''}"${why ? ` data-dica="${esc(n)}|${esc(why)}" data-dica-ja` : ''}>${ok === 2 ? '<i></i>' : ico(ok ? 'ok' : 'aviso')}<b>${n}</b><span>${esc(st)}</span></div>`).join(''); };
  const pintaFuncs = () => { if (!manual) return; const f = funcs(), box = $('.cb-funcs'); if (box.innerHTML !== f) box.innerHTML = f; };

  const ver = (e, d = {}) => {
    clearTimeout(fecha);
    if (el.hidden && !manual && (e === 'achou' || S.cfg?.caboPassou)) { estado = e; dados = d; return; }   // cable there, or user chose to go on without: stays closed
    const vinha = estado; estado = e; dados = d; el.dataset.estado = e; el.classList.toggle('manual', manual);
    if (el.hidden) { el.hidden = false; el.classList.remove('sai'); void el.offsetWidth; el.classList.add('entra-cb'); }
    const [t, p] = TEXTO[e](d, manual); $('h1').textContent = t; $('.cb-esq>p').textContent = p;
    $('.cb-ic').innerHTML = ico({ achou: 'ok', servico: 'aviso', desativado: 'ajuste' }[e] || 'cabo');
    $('[data-cb=ok]').textContent = manual ? 'Voltar' : 'Continuar sem cabo';
    if (e === 'verificando') return;                      // keep steps/list of the previous state while checking
    $('.cb-passos').innerHTML = manual ? '' : (PASSOS[e]?.(d) || []).map((x) => `<div><span>${x}</span></div>`).join('');
    if (manual) pintaFuncs(); else $('.cb-funcs').innerHTML = '';
    $('.cb-bts').style.visibility = $('.cb-dica').style.visibility = !manual && e === 'achou' ? 'hidden' : '';
    $('[data-cb=verificar]').style.display = ''; $('[data-cb=ok]').classList.remove('pri');   // service answer can be a 3 s timeout: always let the user check again
    const dir = $('.cb-dir'), novo = e === 'falta' || (manual && e === 'achou') ? grupo('Grátis', 'presente', CABOS.gratis) + grupo('Pagos', 'carteira', CABOS.pagos)
      : e === 'desativado' ? `<div class="cb-cfg"><div class="cb-fake" aria-hidden="true"><small>Exemplo: como aparece no Windows</small>${(d.desativados?.length ? d.desativados : ['Cabo virtual']).slice(0, 2).map((n) => `<span>${esc(n)}<em>Permitir</em></span>`).join('')}</div>
        <button class="cb-bt" data-cb="config">${ico('ajuste')}Abrir configurações de som</button></div>` : '';
    if (vinha !== e || dir.innerHTML !== novo) { dir.innerHTML = novo; dir.style.visibility = novo ? '' : 'hidden'; dir.classList.remove('troca'); void dir.offsetWidth; dir.classList.add('troca'); }
    if (!manual && e === 'achou') { fecha = setTimeout(fechar, 900); return; }
    if (!el.contains(document.activeElement)) $('[data-cb=verificar]').focus();
  };
  const fechar = () => { clearTimeout(fecha); manual = false; el.classList.add('sai'); const v = aVolta; aVolta = null;
    fecha = setTimeout(() => { el.hidden = true; el.classList.remove('sai', 'entra-cb', 'manual'); if (v?.isConnected) v.focus(); }, 360); };

  // check again: at least 0.8 s of "procurando" (no flash), engine silent for 6 s = service problem (never stuck)
  const verificar = () => {
    if (estado === 'verificando') return;
    const t0 = performance.now(); ver('verificando', dados); clearTimeout(espera);
    espera = setTimeout(() => ver('servico'), 6000);
    const r = (ev) => { removeEventListener('fah:cabo', r); clearTimeout(espera); const d = ev.detail || {};
      setTimeout(() => ver(d.estado, d), Math.max(0, 800 - (performance.now() - t0))); };
    addEventListener('fah:cabo', r);
    // browser tests: App.cabo.simular() = next reply (null = engine silent)
    if (wv) manda({ tipo: 'cabo?' }); else setTimeout(() => { const d = App.cabo.simular ? App.cabo.simular() : { estado: 'falta' }; if (d) dispatchEvent(new CustomEvent('fah:cabo', { detail: d })); }, 300);
  };
  // from Settings or a dimmed Som switch: current state + what works; closes back to where it came from
  const abrir = () => { clearTimeout(fecha); aVolta = document.activeElement; manual = true; el.classList.remove('sai'); manda({ tipo: 'cabo71?' });
    if (estado === null) { verificar(); return; } ver(estado, dados); };

  el.addEventListener('click', (ev) => { const b = ev.target.closest('[data-cb]'); if (!b) return; const a = b.dataset.cb;
    if (a === 'verificar') verificar();
    else if (a === 'ok') { if (!manual) { S.cfg = S.cfg || {}; S.cfg.caboPassou = true; } fechar(); }
    else if (a === 'site') { manda({ tipo: 'abrir', url: b.dataset.url }); b.classList.add('abriu'); }
    else if (a === 'config') { manda({ tipo: 'abrir', url: 'ms-settings:sound' }); b.classList.add('abriu'); } });
  // automatic mode: Esc does nothing (would go back a screen underneath), manual: closes. Tab cycles only inside the screen
  addEventListener('keydown', (e) => { if (el.hidden || el.classList.contains('sai') || (!manual && estado === 'achou')) return;
    if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); if (manual) fechar(); return; }
    if (e.key !== 'Tab') return;
    const f = [...el.querySelectorAll('button')].filter((x) => x.offsetParent && getComputedStyle(x).visibility !== 'hidden'); if (!f.length) return;
    const i = f.indexOf(document.activeElement), n = e.shiftKey ? (i <= 0 ? f.length - 1 : i - 1) : (i + 1) % f.length;
    e.preventDefault(); e.stopImmediatePropagation(); f[n].focus(); }, true);
  addEventListener('fah:cabo', (ev) => { S.cabo = ev.detail || {}; if (estado !== 'verificando') ver(S.cabo.estado, S.cabo); });
  ['mic', 'cabo71'].forEach((t) => addEventListener('fah:' + t, () => { if (!el.hidden) pintaFuncs(); }));
  App.cabo = { ver, verificar, abrir, semSom, motivo, get estado() { return estado; }, get aberto() { return !el.hidden; }, get manual() { return manual; } };
})();
