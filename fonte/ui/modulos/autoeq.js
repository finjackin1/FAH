// AutoEQ on the Som Equalizer: proposal A (sheet: list + detail), chosen 27/Oct/2026.
// Source of truth: docs/prototipos/autoeq/ESCOLHIDA (motor.js + proposta-A.html); styles in modulos/autoeq.css.
// Applied correction lives in App.estado.eq2.autoeq = {nome, tipo, fonte, pre, b, on} -> engine (Modulos\Equalizador\Modulo.cs).
(() => {
  document.head.insertAdjacentHTML('beforeend', '<link rel="stylesheet" href="modulos/autoeq.css">');
  const AQ = (App.aq = {
    populares: ['Sony WH-1000XM4', 'Apple AirPods Pro', 'HyperX Cloud II', 'Samsung Galaxy Buds2 Pro', 'Sennheiser HD 600', 'Razer BlackShark V2', 'JBL Tune 510BT'],
  });
  const E = () => App.estado.eq2 || {};
  Object.defineProperty(AQ, 'atual', { get: () => E().autoeq || null, set: (v) => { if (App.estado.eq2) App.estado.eq2.autoeq = v; } });

  // ---- data: [name, "Source type", preamp, bands] -> grouped by headphone, best source first ----
  AQ.norm = (t) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
  const PRIOR = ['oratory1990', 'crinacle', 'rtings', 'innerfidelity', 'super review', 'hypethesonics'];
  const peso = (o) => { const i = PRIOR.findIndex((p) => AQ.norm(o).startsWith(p)); return i < 0 ? 99 : i; };
  AQ.tipo = (m) => (/in-ear/i.test(m) ? 'in' : /earbud/i.test(m) ? 'bud' : 'over');
  AQ.TIPO = { over: 'Headphone', in: 'In-ear', bud: 'Earbud' };
  AQ.origem = (m) => m.replace(/\s+(over-ear|in-ear|earbud)$/i, '').replace(/^Oratory1990/, 'oratory1990');
  let IDX = null;
  const indice = () => IDX || (IDX = (() => { const g = new Map();
    for (const [n, m, , b] of App.AUTOEQ || []) { if (!g.has(n)) g.set(n, { nome: n, k: AQ.norm(n), fontes: [] }); g.get(n).fontes.push({ origem: AQ.origem(m), tipo: AQ.tipo(m), b }); }
    for (const h of g.values()) { h.fontes.sort((a, b) => peso(a.origem) - peso(b.origem)); h.tipo = h.fontes[0].tipo; }
    return [...g.values()]; })());
  AQ.achar = (nome) => indice().find((h) => h.nome === nome);
  AQ.buscar = (t, max = 80) => { const q = AQ.norm(t).trim(), p = q.split(/\s+/).filter(Boolean); if (!p.length) return [];
    return indice().filter((h) => p.every((x) => h.k.includes(x))).sort((a, b) => (b.k.startsWith(q) - a.k.startsWith(q)) || a.k.length - b.k.length || a.k.localeCompare(b.k)).slice(0, max); };
  AQ.realce = (nome, t) => { let h = nome.replace(/[&<>]/g, (c) => `&#${c.charCodeAt(0)};`); const p = AQ.norm(t || '').split(/\s+/).filter(Boolean); if (!p.length) return h;
    const k = AQ.norm(nome), marca = new Array(nome.length).fill(0); p.forEach((x) => { const i = k.indexOf(x); if (i >= 0) marca.fill(1, i, i + x.length); });
    return [...nome].map((c, i) => (marca[i] && !marca[i - 1] ? '<b>' : '') + c + (marca[i] && !marca[i + 1] ? '</b>' : '')).join(''); };
  // headphone connected now: Windows default output name vs data (all model words, brand optional; longest match wins)
  AQ.detectado = () => { const d = (App.estado.dispositivos?.som || []).find((x) => x.p && x.f); if (!d) return null; const n = ' ' + AQ.norm(d.n).replace(/[()\-_,]/g, ' ') + ' ';
    let m = null; for (const h of indice()) { const w = h.k.replace(/[()\-_,]/g, ' ').split(/\s+/).filter(Boolean), mod = w.slice(1);
      if (mod.length && mod.every((x) => n.includes(' ' + x + ' ')) && (!m || h.k.length > m.k.length)) m = h; } return m; };

  // ---- response of the correction (same biquads as the engine) ----
  const TX = 48000;
  const coef = (t, f, q, db) => { const w = 2 * Math.PI * f / TX, c = Math.cos(w), sn = Math.sin(w), al = sn / (2 * q), A = Math.pow(10, db / 40), r = 2 * Math.sqrt(A) * al; let b, a;
    if (t === 'pico') { b = [1 + al * A, -2 * c, 1 - al * A]; a = [1 + al / A, -2 * c, 1 - al / A]; }
    else if (t === 'grave') { b = [A * ((A + 1) - (A - 1) * c + r), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - r)]; a = [(A + 1) + (A - 1) * c + r, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - r]; }
    else { b = [A * ((A + 1) + (A - 1) * c + r), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - r)]; a = [(A + 1) - (A - 1) * c + r, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - r]; }
    return [b[0] / a[0], b[1] / a[0], b[2] / a[0], a[1] / a[0], a[2] / a[0]]; };
  AQ.db = (bs, f) => { const w = 2 * Math.PI * f / TX; let m = 0;
    for (const [t, fr, q, g] of bs) { if (t === 'pa' || t === 'pb') continue; const k = coef(t, fr, q, g);
      const nr = k[0] + k[1] * Math.cos(w) + k[2] * Math.cos(2 * w), ni = -k[1] * Math.sin(w) - k[2] * Math.sin(2 * w), dr = 1 + k[3] * Math.cos(w) + k[4] * Math.cos(2 * w), di = -k[3] * Math.sin(w) - k[4] * Math.sin(2 * w);
      m += 10 * Math.log10((nr * nr + ni * ni) / (dr * dr + di * di)); } return m; };
  AQ.pre = (bs) => { let m = 0; for (let i = 0; i <= 60; i++) m = Math.max(m, AQ.db(bs, 20 * Math.pow(1000, i / 60))); return -Math.round(m * 10) / 10; };
  AQ.num = (v, u = ' dB') => (v > 0 ? '+' : v < 0 ? '−' : '') + String(Math.abs(v)).replace('.', ',') + u;
  AQ.path = (bs, W, H, esc = 15, mg = 18) => { let p = ''; for (let x = 0; x <= W; x += 3) { const y = H / 2 - AQ.db(bs, 20 * Math.pow(1000, x / W)) / esc * (H / 2 - mg); p += (p ? 'L' : 'M') + x + ' ' + y.toFixed(1); } return p; };
  AQ.grafico = (bs, W, H) => { const fx = (f) => Math.log(f / 20) / Math.log(1000) * W, fy = (g) => H / 2 - g / 15 * (H / 2 - 18), hz = (f) => (f >= 1000 ? f / 1000 + 'k' : f);
    const grade = [50, 100, 200, 500, 1000, 2000, 5000, 10000].map((f) => `<line x1="${fx(f)}" x2="${fx(f)}" y1="0" y2="${H}" stroke="#262626"/><text x="${fx(f) + 4}" y="${H - 6}">${hz(f)}</text>`).join('')
      + [-12, -6, 0, 6, 12].map((g) => `<line x1="0" x2="${W}" y1="${fy(g)}" y2="${fy(g)}" stroke="${g ? '#262626' : '#444'}"/><text x="6" y="${fy(g) - 4}">${g > 0 ? '+' : ''}${g}</text>`).join('');
    const p = bs ? AQ.path(bs, W, H) : `M0 ${H / 2}L${W} ${H / 2}`;
    return `<svg viewBox="0 0 ${W} ${H}" width="${W}" height="${H}">${grade}<defs><linearGradient id="aqg" x1="0" x2="1"><stop offset="0" stop-color="#ffb340"/><stop offset="1" stop-color="#ff6a3d"/></linearGradient></defs>
      <path class="aq-a" d="${p}L${W} ${H}L0 ${H}Z" fill="url(#aqg)" opacity="${bs ? .12 : 0}"/><path class="aq-p" d="${p}" fill="none" stroke="${bs ? 'url(#aqg)' : '#3a3a3c'}" stroke-width="2.5"/></svg>`; };
  AQ.pintar = (el, bs) => { const W = el.clientWidth, H = el.clientHeight, s = el.querySelector('svg');
    if (s && bs && el._bs && +s.getAttribute('width') === W && +s.getAttribute('height') === H) { const p = AQ.path(bs, W, H);
      s.querySelector('.aq-p').style.d = `path("${p}")`; s.querySelector('.aq-a').style.d = `path("${p}L${W} ${H}L0 ${H}Z")`; }
    else el.innerHTML = AQ.grafico(bs, W, H);
    el._bs = bs; };

  // ---- icons (one stroke, like base/componentes.js) ----
  const IC = { busca: '<circle cx="11" cy="11" r="6.5"/><path d="m16 16 4.5 4.5"/>', x: '<path d="M7 7l10 10M17 7 7 17"/>',
    over: '<path d="M4 15v-3a8 8 0 0 1 16 0v3"/><rect x="3" y="14" width="4.5" height="6.5" rx="2"/><rect x="16.5" y="14" width="4.5" height="6.5" rx="2"/>',
    in: '<path d="M9.5 3.5a5 5 0 0 1 5 5V20a1.5 1.5 0 0 1-3 0v-7.5h-2a4.5 4.5 0 0 1 0-9z"/>', bud: '<path d="M9.5 3.5a5 5 0 0 1 5 5V20a1.5 1.5 0 0 1-3 0v-7.5h-2a4.5 4.5 0 0 1 0-9z"/>',
    ok: '<path d="m5 12.5 4.5 4.5L19 7.5"/>', bt: '<path d="m7 7 10 10-5 5V2l5 5L7 17"/>' };
  AQ.ico = (n) => `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${IC[n]}</svg>`;
  AQ.campo = (ph, valor = '') => `<label class="aq-busca">${AQ.ico('busca')}<input placeholder="${ph}" value="${valor.replace(/[&"<]/g, (c) => `&#${c.charCodeAt(0)};`)}" spellcheck="false" autocomplete="off"><button class="aq-limpa" tabindex="-1" title="Limpar">${AQ.ico('x')}</button></label>`;
  AQ.kbd = (t) => `<kbd>${t}</kbd>`;

  // ---- Equalizer (Som) screen hooks: only the screen whose EQ was built with autoeq (.pq.com-aq) ----
  AQ.tela = () => [...document.querySelectorAll('.tela.t-mod:not(.fora)')].find((t) => t.querySelector('.pq.com-aq'));
  const pilula = () => { const a = AQ.atual; return a ? `<button class="aq-pil ${a.on ? 'on' : ''}" data-aq="abrir" title="AutoEQ: ${a.nome} (${a.fonte}). Clique para trocar">${AQ.ico(a.tipo)}<span>${a.nome}</span><i class="aq-mini ${a.on ? 'on' : ''}" data-aq="liga" title="Liga/desliga a correção"></i></button>`
    : `<button class="aq-bt" data-aq="abrir" title="Correção medida em laboratório para o seu fone">${AQ.ico('over')}<span>AutoEQ</span></button>`; };
  const repor = () => { const s = AQ.tela()?.querySelector('.aq-topo'); if (s) s.innerHTML = pilula(); };
  const pôr = () => { const t = AQ.tela(), pr = t && t.querySelector('.pq .preset'); if (!pr || t.querySelector('.aq-topo')) return;
    pr.insertAdjacentHTML('afterend', `<span class="aq-topo">${pilula()}</span>`); desenhar(); };
  // overlay on the big curve: applied correction (dashed). Survives curve redraws.
  const desenhar = () => { const t = AQ.tela(), cv = t && t.querySelector('.pq .curva'); if (!cv) return;
    if (!cv._obs) { cv._obs = new ResizeObserver(() => requestAnimationFrame(desenhar)); cv._obs.observe(cv); }
    let ov = cv.parentNode.querySelector('.aq-ov'); if (!ov) { ov = document.createElement('div'); ov.className = 'aq-ov'; cv.parentNode.append(ov); }
    Object.assign(ov.style, { left: cv.offsetLeft + 'px', top: cv.offsetTop + 'px', width: cv.offsetWidth + 'px', height: cv.offsetHeight + 'px' });
    const W = cv.offsetWidth, H = cv.offsetHeight, a = AQ.atual && AQ.atual.on ? AQ.atual : null;
    ov.innerHTML = a ? `<svg viewBox="0 0 ${W} ${H}"><path class="aq-fixo" d="${AQ.path(a.b, W, H)}"/></svg><div class="aq-leg"><span><i class="l-eq"></i>Seu equalizador</span><span><i class="l-fone"></i>Correção do fone</span></div>` : ''; };
  addEventListener('resize', desenhar);
  AQ.aplicar = (h, f) => { AQ.atual = { nome: h.nome, tipo: h.tipo, fonte: f.origem, pre: AQ.pre(f.b), b: f.b, on: true }; repor(); desenhar(); };
  AQ.aviso = (txt) => { let a = document.querySelector('.aq-aviso'); if (!a) { a = document.createElement('div'); a.className = 'aq-aviso'; document.getElementById('app').append(a); }
    a.innerHTML = `${AQ.ico('ok')}<span>${txt}</span>`; a.classList.remove('on'); void a.offsetWidth; a.classList.add('on'); clearTimeout(a._t); a._t = setTimeout(() => a.classList.remove('on'), 3200); };

  // ---- proposal A: sheet (list on the left, chosen headphone on the right) ----
  // search text survives closing the sheet; cleared only when going back to the main screen (user, 29/Oct)
  let aberta = null, filtro = 'todos', itens = [], sel = -1, fonte = 0, det = null, busca = '';
  const ir = App.ir; App.ir = (id, p) => { if (id === 'inicio') busca = ''; return ir.call(App, id, p); };
  const lista = () => aberta.querySelector('.a-lista'), campo = () => aberta.querySelector('input');
  const linha = (h, i, t, extra = '') => `<div class="a-it ${extra}" data-i="${i}">${AQ.ico(h.tipo)}<div><span class="n">${AQ.realce(h.nome, t)}</span>
    <small>${extra === 'sug' && h === det ? `${AQ.ico('bt')}Conectado agora · ` : ''}${AQ.TIPO[h.tipo]} · ${h.fontes.length} ${h.fontes.length > 1 ? 'medições' : 'medição'}</small></div></div>`;

  // wheel list (user, 27/Oct): whole base, only VIS rows exist; the middle row is always the chosen one and the list moves under it
  const VIS = 7, MEIO = 3, ALT = 48;
  let ordem = null; const todos = () => ordem || (ordem = [...indice()].sort((a, b) => a.k.localeCompare(b.k)));
  // topo (filter click, user 29/Oct): always back to the first row, with or without a search
  const montarLista = (topo) => { const ant = sel; busca = campo().value; const t = busca.trim(); aberta.querySelector('.aq-busca').classList.toggle('tem', !!t);
    const ok = (h) => filtro === 'todos' || h.tipo === filtro;
    itens = (t ? AQ.buscar(t, Infinity) : todos()).filter(ok); sel = -1;
    if (!itens.length) { lista().innerHTML = `<div class="aq-nada">${AQ.ico('busca')}<b>${t ? `Nenhum fone com “${t.replace(/</g, '&lt;')}”` : 'Nenhum fone deste tipo'}</b><small>${t ? 'Tente só o modelo, sem a marca (ex.: “HD 600”, “Buds2”). ' : ''}${filtro !== 'todos' ? 'Ou volte o filtro para Todos.' : ''}</small></div>`; return escolher(-1); }
    lista().innerHTML = `<div class="aq-sec"><span></span></div><div class="a-roda" style="height:${VIS * ALT}px"><div class="a-trilho">${'<div class="a-it"></div>'.repeat(VIS)}</div></div>`;
    const atual = AQ.atual ? itens.findIndex((h) => h.nome === AQ.atual.nome) : -1, d = topo === true ? 0 : atual >= 0 ? atual : t ? 0 : itens.indexOf(det); escolher(Math.max(0, d)); if (topo === true && ant > 0) pintarRoda(-ant); };

  // fills the VIS rows around sel; passo = how many rows it moved (slides the rail from there to 0)
  const pintarRoda = (passo = 0) => { const tr = lista().querySelector('.a-trilho'); if (!tr) return; const t = campo().value.trim();
    lista().querySelector('.aq-sec span').textContent = `${sel + 1} de ${itens.length} ${itens.length > 1 ? 'fones' : 'fone'}`;
    [...tr.children].forEach((e, k) => { const i = sel - MEIO + k, h = itens[i], dist = Math.abs(k - MEIO);
      e.className = 'a-it' + (h ? '' : ' vazio') + (k === MEIO ? ' sel' : ''); e.dataset.i = h ? i : ''; e.style.opacity = h ? [1, .8, .55, .3][dist] : 0;
      e.innerHTML = h ? linha(h, i, t, h === det ? 'sug' : '').replace(/^<div[^>]*>|<\/div>$/g, '') : ''; });
    if (passo) { const p = Math.max(-2, Math.min(2, passo)); tr.style.transition = 'none'; tr.style.transform = `translateY(${p * ALT}px)`;
      void tr.offsetHeight; tr.style.transition = ''; tr.style.transform = ''; } };

  const escolher = (i) => { const antes = sel; sel = i; fonte = 0;
    const h = itens[i], a = AQ.atual; if (h && a && a.nome === h.nome) fonte = Math.max(0, h.fontes.findIndex((f) => f.origem === a.fonte));
    if (h) pintarRoda(antes >= 0 && antes !== i ? i - antes : 0); detalhe(); };
  const andar = (n) => itens.length && escolher(Math.max(0, Math.min(itens.length - 1, sel + n)));

  const detalhe = () => { const dir = aberta.querySelector('.a-dir'), h = itens[sel]; aberta.querySelector('[data-a=usar]').disabled = !h;
    if (!h) { dir.innerHTML = `<div class="aq-nada">${AQ.ico('over')}<b>Escolha um fone na lista</b><small>A correção dele aparece aqui antes de você usar.</small></div>`; return; }
    const f = h.fontes[fonte];
    if (!dir.querySelector('.a-cab')) dir.innerHTML = `<div class="a-cab">${UI.icone('fone', 44, 'var(--som-ic)')}<div><h3></h3><p></p></div></div>
      <div class="a-med"><span class="aq-sec">Medido por</span><div class="a-fontes"></div><small></small></div>
      <div class="aq-graf"></div><dl class="a-fatos"><div><dt>Filtros</dt><dd data-f="n"></dd></div><div><dt>Pré-amplificação</dt><dd data-f="pre"></dd></div><div><dt>Maior ajuste</dt><dd data-f="max"></dd></div></dl>
      <p class="a-nota">Deixa o fone neutro, como foi medido em laboratório. O seu equalizador continua valendo por cima.</p>`;
    dir.querySelector('h3').textContent = h.nome; dir.querySelector('.a-cab p').textContent = AQ.TIPO[h.tipo];
    dir.querySelector('.a-fontes').innerHTML = h.fontes.map((x, i) => `<button class="${i === fonte ? 'on' : ''}" data-f="${i}">${x.origem}</button>`).join('');
    dir.querySelector('.a-med small').textContent = h.fontes.length > 1 ? 'A primeira é a mais confiável para este fone.' : 'Só existe esta medição para este fone.';
    const pre = AQ.pre(f.b), mx = f.b.reduce((m, b) => (Math.abs(b[3]) > Math.abs(m) ? b[3] : m), 0);
    dir.querySelector('[data-f=n]').textContent = f.b.length; dir.querySelector('[data-f=pre]').textContent = AQ.num(pre); dir.querySelector('[data-f=max]').textContent = AQ.num(mx);
    AQ.pintar(dir.querySelector('.aq-graf'), f.b); provar(); };

  // "Aplicar ao selecionar" (user, 27/Oct): while on, the chosen row is heard right away (short debounce);
  // closing without "Aplicar esta EQ" puts back what was applied before the sheet opened
  let antes = null, vt = 0;
  const vivo = () => !!App.estado.aqVivo;
  const provar = () => { clearTimeout(vt); if (!vivo() || !aberta) return; const h = itens[sel]; if (!h) return; const f = h.fontes[fonte];
    vt = setTimeout(() => aberta && vivo() && AQ.aplicar(h, f), 120); };
  const devolver = () => { clearTimeout(vt); AQ.atual = antes ? { ...antes } : null; repor(); desenhar(); };

  const fechar = (ficar) => { if (!aberta) return; if (ficar !== true) devolver(); const v = aberta; aberta = null; v.classList.add('sai'); setTimeout(() => v.remove(), 200); AQ.tela()?.querySelector('.aq-bt')?.classList.remove('ab'); };
  const usar = () => { const h = itens[sel]; if (!h) return; clearTimeout(vt); AQ.aplicar(h, h.fontes[fonte]); fechar(true); AQ.aviso(`AutoEQ ligado: ${h.nome}`); };

  AQ.abrir = (texto = busca) => { if (aberta) return; AQ.tela()?.querySelector('.aq-bt')?.classList.add('ab'); det = AQ.detectado(); antes = AQ.atual ? { ...AQ.atual } : null;
    aberta = document.createElement('div'); aberta.className = 'a-veu';
    aberta.innerHTML = `<div class="a-folha" role="dialog" aria-label="AutoEQ do seu fone"><aside class="a-esq">${AQ.campo('Procurar fone', texto)}
      ${UI.segmento([['todos', 'Todos'], ['over', 'Headphone'], ['in', 'In-ear'], ['bud', 'Earbud']], filtro, { campo: 'aqfiltro' })}<div class="a-lista"></div></aside>
      <main class="a-dir"></main>
      <footer class="a-rod"><div class="aq-dica"><span>${AQ.kbd('↑')}${AQ.kbd('↓')} escolher</span><span>${AQ.kbd('Enter')} aplicar</span><span>${AQ.kbd('Esc')} fechar</span></div>
      <label class="a-vivo" title="Ouça cada fone ao passar por ele. Só fica valendo se clicar em Aplicar esta EQ">Aplicar ao selecionar${UI.switch(vivo(), { aqvivo: 1 })}</label>
      <button class="aq-b" data-a="fechar">Cancelar</button><button class="aq-b pri" data-a="usar">Aplicar esta EQ</button></footer></div>`;
    document.getElementById('app').append(aberta);
    montarLista(); requestAnimationFrame(() => { const c = campo(); c.focus(); c.setSelectionRange(c.value.length, c.value.length); detalhe(); });
    campo().addEventListener('input', montarLista);
    aberta.addEventListener('c:mudou', (e) => { if (e.detail.dados.campo === 'aqfiltro') { filtro = e.detail.valor; montarLista(true); campo().focus(); }
      if (e.detail.dados.aqvivo) { App.estado.aqVivo = e.detail.valor; e.detail.valor ? provar() : devolver(); campo().focus(); } });
    aberta.addEventListener('click', (e) => { const t = e.target;
      if (t === aberta) return fechar();
      const it = t.closest('.a-it'); if (it) { if (it.dataset.i !== '') escolher(+it.dataset.i); return campo().focus(); }
      const f = t.closest('[data-f]'); if (f && f.tagName === 'BUTTON') { fonte = +f.dataset.f; return detalhe(); }
      if (t.closest('.aq-limpa')) { campo().value = ''; montarLista(); return campo().focus(); }
      const a = t.closest('[data-a]'); if (a) a.dataset.a === 'usar' ? usar() : fechar(); });
    // clicks keep the typing focus in the search (pointerdown handlers run before the browser would move focus to the button)
    aberta.addEventListener('mousedown', (e) => { if (!e.target.closest('input')) e.preventDefault(); });
    aberta.addEventListener('dblclick', (e) => e.target.closest('.a-it.sel') && usar());
    // mouse wheel: one row per notch; touchpad (small deltas) adds up to one row
    let acc = 0; aberta.addEventListener('wheel', (e) => { if (!e.target.closest('.a-lista')) return; e.preventDefault();
      if (Math.abs(e.deltaY) >= 50) { acc = 0; return andar(Math.sign(e.deltaY)); }
      acc += e.deltaY; if (Math.abs(acc) >= 40) { andar(Math.sign(acc)); acc = 0; } }, { passive: false }); };

  // keys while open (capture: Esc must not leave the Equalizer)
  addEventListener('keydown', (e) => { if (!aberta) return;
    if (e.key === 'Escape') { e.stopImmediatePropagation(); return fechar(); }
    if (e.key === 'Enter' && !e.target.closest('button')) { e.preventDefault(); return usar(); }
    const n = { ArrowDown: 1, ArrowUp: -1, PageDown: VIS, PageUp: -VIS }[e.key]; if (n && itens.length) { e.preventDefault(); andar(n); } }, true);

  // wiring: AutoEQ button/pill next to the preset menu (only entry, user 27/Oct), re-added when the EQ rebuilds its body
  const ligar = () => { new MutationObserver(pôr).observe(document.getElementById('palco'), { childList: true, subtree: true }); pôr(); };
  document.readyState === 'loading' ? addEventListener('DOMContentLoaded', ligar) : ligar();
  document.addEventListener('click', (e) => { const b = e.target.closest('[data-aq]'); if (!b) return; e.stopPropagation();
    if (b.dataset.aq === 'liga') { AQ.atual = { ...AQ.atual, on: !AQ.atual.on }; repor(); desenhar(); } else if (b.dataset.aq === 'abrir') AQ.abrir(); }, true);
})();
