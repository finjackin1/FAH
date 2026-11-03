// Reusable controls. Each returns HTML; behavior is wired once here by data-c attribute,
// so screens/modules just call UI.x(...) and listen to 'c:mudou' events.
const UI = (() => {
  const SV = {
    fone: '<path d="M3 14v-2a9 9 0 0 1 18 0v2"/><rect x="2.5" y="13" width="5" height="7" rx="2"/><rect x="16.5" y="13" width="5" height="7" rx="2"/>',
    mic: '<rect x="9" y="2.5" width="6" height="12" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3.5"/>',
    eq: '<rect x="2.5" y="12" width="3" height="9" rx="1.2" fill="currentColor" stroke="none"/><rect x="8" y="5" width="3" height="16" rx="1.2" fill="currentColor" stroke="none"/><rect x="13.5" y="14" width="3" height="7" rx="1.2" fill="currentColor" stroke="none"/><rect x="19" y="8" width="3" height="13" rx="1.2" fill="currentColor" stroke="none"/>',   // EQ = band bars (mixer keeps the faders)
    sur: '<circle cx="12" cy="12" r="2.5"/><circle cx="12" cy="12" r="6.5"/><circle cx="12" cy="12" r="10" stroke-dasharray="3 3"/>',
    ruido: '<path d="M3 12h2l2-5 3 10 3-12 3 9 2-2h3"/><path d="M3 3l18 18"/>',
    voz: '<path d="M4 10v4M8 7v10M12 4v16M16 8v8M20 11v2"/>',
    mix: '<path d="M6 3v18M12 3v18M18 3v18"/><rect x="3.5" y="13" width="5" height="3" rx="1" fill="currentColor"/><rect x="9.5" y="6" width="5" height="3" rx="1" fill="currentColor"/><rect x="15.5" y="10" width="5" height="3" rx="1" fill="currentColor"/>',
    tecla: '<rect x="2.5" y="6" width="19" height="12" rx="2"/><path d="M6 10h1M10 10h1M14 10h1M18 10h0M7 14h10"/>',
    caixa: '<path d="M3 7h18v13H3zM2 3h20v4H2zM10 11h4"/>', info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5v.5"/>',
    rota: '<circle cx="5" cy="6" r="2"/><circle cx="5" cy="18" r="2"/><circle cx="19" cy="12" r="2"/><path d="M7 6c6 0 5 6 10 6M7 18c6 0 5-6 10-6"/>',
    engr: '<path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/>',   // gear (Lucide settings, ISC); the old rays read as a sun
    casa: '<path d="M3 11 12 3l9 8v10h-6v-6H9v6H3z"/>', mais: '<path d="M12 5v14M5 12h14"/>', lixo: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
    volta: '<path d="M15 4 7 12l8 8"/>', efeitos: '<path d="M2 12c2.5 0 2.5-6 5-6s2.5 12 5 12 2.5-12 5-12 2.5 6 5 6"/>', seta: '<path d="M9 5l7 7-7 7"/>',
    clone: '<rect x="8" y="8" width="13" height="13" rx="2"/><path d="M16 8V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h3"/>',
    salva: '<path d="M18 21l-6-4-6 4V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2z"/>',
    desvio: '<path d="M2 17h6.5M15.5 17H22"/><path d="M4.5 17V8.5a2 2 0 0 1 2-2h11a2 2 0 0 1 2 2V17"/><path d="M11 4l2.5 2.5L11 9"/><rect x="8.5" y="13" width="7" height="8" rx="1.5" fill="currentColor" stroke="none" opacity=".45"/>',   // bypass (31/10): line through a block + a path around it
    elo: '<path d="M10 14a4 4 0 0 0 5.66 0l3-3a4 4 0 0 0-5.66-5.66l-1 1"/><path d="M14 10a4 4 0 0 0-5.66 0l-3 3a4 4 0 0 0 5.66 5.66l1-1"/>',
    alto: '<path d="M4 9h4l5-4v14l-5-4H4z"/><path d="M16.5 8.5a5 5 0 0 1 0 7"/>', mudo: '<path d="M4 9h4l5-4v14l-5-4H4z"/><path d="M17 9l5 6M22 9l-5 6"/>',
  };
  const ico = (n) => `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${SV[n] || ''}</svg>`;
  const at = (o) => Object.entries(o).map(([k, v]) => ` data-${k}="${v}"`).join('');

  const UI = {
    ico, icones: SV,
    icone: (nome, tam = 30, grad = '') => `<span class="icone" style="width:${tam}px;height:${tam}px;${grad ? `--grad:${grad}` : ''}">${ico(nome)}</span>`,
    switch: (on, d = {}) => `<span class="c-switch ${on ? 'on' : ''}" data-c="switch"${at(d)}></span>`,
    // d.padrao = value for middle-click reset; d.escala = name in UI.escalas (value shown/typed in another unit, e.g. balance -100..100)
    slider: (v, d = {}) => `<div class="c-slider" data-c="slider" data-v="${v}"${at(d)}><div class="vu"></div><div class="nv" style="width:${v}%"></div></div>`,
    select: (txt, d = {}) => `<span class="c-select" data-c="select"${at(d)}>${txt}</span>`,
    chips: (itens, d = {}) => `<div class="c-chips">${itens.map(([t, on]) => `<span class="${on ? 'on' : ''}" data-c="chip"${at(d)}>${t}</span>`).join('')}</div>`,
    mudo: (on, d = {}) => `<button class="c-mudo ${on ? 'on' : ''}" data-c="mudo"${at(d)}>${ico(on ? 'mudo' : 'alto')}</button>`,
    segmento: (opcoes, ativo, d = {}) => `<div class="c-segmento">${opcoes.map(([id, t]) => `<button class="${id === ativo ? 'on' : ''}" data-c="segmento" data-v="${id}"${at(d)}>${t}</button>`).join('')}</div>`,
    linha: (rotulo, controle, larga) => `<div class="c-linha ${larga ? 'lg' : ''}"><span>${rotulo}</span>${controle}</div>`,
    // number for labels: up to `casas` decimals, comma, no trailing zeros (real value stays untouched in the state)
    num: (v, casas = 1) => String(Math.round(v * 10 ** casas) / 10 ** casas).replace('.', ','),
    // frequency label: below 1 kHz "90 Hz"; from 1 kHz "1k", "1.1k", "11.7k", "20k"
    hz: (f) => (f >= 999.5 ? Math.round(f / 100) / 10 + 'k' : Math.round(f) + ' Hz'),
    // typed text -> number: comma or dot, optional unit; "k" multiplies by 1000. NaN when there is no number.
    ler: (t) => { const m = String(t).trim().replace(',', '.').match(/^([+\-−]?\d*\.?\d+)\s*(k)?/i); if (!m) return NaN;
      return parseFloat(m[1].replace('−', '-')) * (m[2] ? 1000 : 1); },
    escalas: {},
    // move a slider from code (preset etc.): position + real value, no event
    poe: (el, v) => { el.dataset.v = v; el.querySelector('.nv').style.width = v + '%'; },
  };

  // right-click value box (themed): opens over the control with the real value; Enter applies, Esc or click away cancels.
  // o = { texto: current value as text, ler(texto) -> number or NaN, aplicar(number) }
  document.head.insertAdjacentHTML('beforeend', `<style>
  .c-editar{position:fixed;z-index:1000;display:flex;align-items:center;gap:6px;background:var(--bg3);border-radius:10px;padding:6px;box-shadow:0 12px 40px rgba(0,0,0,.6);animation:cEd .15s var(--curva) both}
  .c-editar input{width:110px;border:1.5px solid var(--ac);background:var(--controle);color:var(--fg);font:600 13px var(--fonte);padding:6px 10px;border-radius:8px;outline:0;font-variant-numeric:tabular-nums}
  .c-editar input::selection{background:var(--ac);color:#000}.c-editar input.erro{border-color:var(--perigo)}.c-editar small{font-size:11px;color:var(--fg2);white-space:nowrap}
  @keyframes cEd{from{opacity:0;transform:translateY(-4px)}}</style>`);
  UI.editar = (alvo, o) => { document.querySelector('.c-editar')?._fim(false);
    const r = alvo.getBoundingClientRect(), cx = document.createElement('div'); cx.className = 'c-editar';
    cx.style.setProperty('--ac', getComputedStyle(alvo).getPropertyValue('--ac'));   // lives on body: take the screen's accent (Som/Mic)
    cx.innerHTML = `<input spellcheck="false" autocomplete="off"><small>Enter</small>`; document.body.appendChild(cx);
    const w = cx.offsetWidth, h = cx.offsetHeight;
    cx.style.left = Math.max(8, Math.min(innerWidth - w - 8, r.left + r.width / 2 - w / 2)) + 'px';
    cx.style.top = (r.bottom + 6 + h > innerHeight - 8 ? Math.max(8, r.top - h - 6) : r.bottom + 6) + 'px';
    const c = cx.querySelector('input'); c.value = o.texto; c.focus(); c.select();
    const fora = (e) => !cx.contains(e.target) && fim(false);
    let feito = false;
    const fim = (ok) => { if (feito) return; if (ok) { const v = o.ler(c.value); if (!isFinite(v)) { c.classList.add('erro'); return; } o.aplicar(v); }
      feito = true; document.removeEventListener('pointerdown', fora, true); cx.remove(); };
    cx._fim = fim;
    c.addEventListener('keydown', (e) => { e.stopPropagation(); if (e.key === 'Enter' || e.key === 'Escape') { e.preventDefault(); fim(e.key === 'Enter'); } });
    c.addEventListener('input', () => c.classList.remove('erro'));
    c.addEventListener('blur', () => fim(false));
    setTimeout(() => document.addEventListener('pointerdown', fora, true)); };

  // one place wires every control; emits 'c:mudou' {tipo, valor, dados} on the element
  const avisa = (el, valor) => el.dispatchEvent(new CustomEvent('c:mudou', { bubbles: true, detail: { tipo: el.dataset.c, valor, dados: { ...el.dataset } } }));
  let arr = null;
  // slider value 0..100 (typed values keep their decimals); data-v holds the real value for the edit box
  const pos = (el, v) => { v = Math.max(0, Math.min(100, v)); el.dataset.v = v; el.querySelector('.nv').style.width = v + '%'; avisa(el, v); };
  const mover = (e) => { const r = arr.getBoundingClientRect(); pos(arr, Math.round((e.clientX - r.left) / r.width * 100)); };
  const esc = (el) => UI.escalas[el.dataset.escala] || { de: (v) => v, para: (v) => v };
  document.addEventListener('pointerdown', (e) => {
    const el = e.target.closest('[data-c]'); if (!el) return; e.stopPropagation();
    const t = el.dataset.c;
    if (t === 'slider') { if (e.button === 1 && el.dataset.padrao !== undefined) { e.preventDefault(); pos(el, +el.dataset.padrao); }   // wheel click = reset
      if (e.button === 0) { arr = el; mover(e); } return; }
    if (e.button !== 0) return;
    else if (t === 'switch' || t === 'chip') avisa(el, el.classList.toggle('on'));
    else if (t === 'mudo') { const on = el.classList.toggle('on'); el.innerHTML = ico(on ? 'mudo' : 'alto'); avisa(el, on); }
    else if (t === 'segmento') { el.parentNode.querySelectorAll('button').forEach((b) => b.classList.toggle('on', b === el)); avisa(el, el.dataset.v); }
    else if (t === 'select') avisa(el, null);
  }, true);
  // wheel click must not start the browser's auto-scroll; right click on a slider = value box (in the unit the label shows)
  document.addEventListener('mousedown', (e) => e.button === 1 && e.target.closest('[data-c=slider],.fd,.kn') && e.preventDefault(), true);
  document.addEventListener('contextmenu', (e) => { const el = e.target.closest('[data-c=slider]'); if (!el) return; e.preventDefault(); const s = esc(el);
    UI.editar(el, { texto: String(+s.de(+el.dataset.v).toFixed(6)).replace('.', ','), ler: UI.ler, aplicar: (v) => pos(el, s.para(v)) }); }, true);
  // wheel on any slider = 2 per notch; el._roda tells listeners it came from the wheel (the Mixer skips its magnet then)
  document.addEventListener('wheel', (e) => { const el = e.target.closest('[data-c=slider]'); if (!el) return; e.preventDefault(); e.stopPropagation();
    el._roda = true; try { pos(el, Math.round(+el.dataset.v) + (e.deltaY < 0 ? 2 : -2)); } finally { el._roda = false; } }, { passive: false, capture: true });
  addEventListener('pointermove', (e) => arr && mover(e));
  addEventListener('pointerup', () => (arr = null));
  return UI;
})();
