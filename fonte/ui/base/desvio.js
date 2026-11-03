// Dock dodge. The dock floats above every screen (screens reserve no room for it), so it may cover
// something clickable. One trigger only: the pointer is over the dock and stays still for 500ms exactly
// on top of an option that is behind it (a quick pass, or a stop beside the option, never counts).
// Then the dock slides (animated) to the predefined spot that covers the fewest options, nearest first:
// bottom/top rows, left/center/right. Tab onto a covered option does the same. Goes home (bottom center)
// once the pointer has left that option for a moment.
(() => {
  const dock = document.getElementById('dock');
  const CLICAVEL = 'button,a[href],input,select,textarea,label,[role=button],[tabindex]:not([tabindex="-1"])';
  const FOLGA = 14, MARGEM = 18, PARADO = 500, VOLTA = 800;
  let zona = null, tParado = 0, tVolta = 0, ponto = null, ancora = null, arrastando = false;

  const dentro = (r, x, y, m = 0) => x >= r.left - m && x <= r.right + m && y >= r.top - m && y <= r.bottom + m;
  // topmost clickable for a hit element; inherited cursor climbs to where it was declared
  const clicavel = (el) => {
    for (; el && el !== document.body; el = el.parentElement) {
      if (el.id === 'app' || el.classList.contains('tela') || el.classList.contains('palco')) return null;
      if (el.matches(CLICAVEL)) return el;
      const c = getComputedStyle(el).cursor;
      if (/pointer|resize|grab/.test(c)) { while (el.parentElement && !el.parentElement.classList.contains('tela') && getComputedStyle(el.parentElement).cursor === c) el = el.parentElement; return el; }
    }
    return null;
  };
  // option under this exact point, ignoring the dock
  const atras = (x, y) => { for (const el of document.elementsFromPoint(x, y)) if (!dock.contains(el)) return clicavel(el); return null; };
  const coberto = (el) => { const a = el.getBoundingClientRect(), d = dock.getBoundingClientRect();
    return Math.min(a.right, d.right) > Math.max(a.left, d.left) && Math.min(a.bottom, d.bottom) > Math.max(a.top, d.top); };

  // home = where CSS puts it (bottom center); spots are top-left corners in window px
  const casa = () => ({ x: dock.offsetLeft - dock.offsetWidth / 2, y: dock.offsetTop });
  const lugares = () => { const w = dock.offsetWidth, h = dock.offsetHeight, c = casa(), topo = (document.querySelector('.barra')?.offsetHeight || 40) + 10;
    return [c.y, topo].flatMap((y) => [MARGEM, c.x, innerWidth - w - MARGEM].map((x) => ({ left: x, top: y, right: x + w, bottom: y + h }))); };
  // options on the visible screen (+ title bar), as rects
  const opcoes = () => { const out = [];
    for (const raiz of document.querySelectorAll('.tela:not(.fora), .barra')) for (const e of raiz.querySelectorAll('*')) {
      if (clicavel(e) !== e) continue; const r = e.getBoundingClientRect(); if (r.width && r.height) out.push(r); }
    return out; };
  // option area a spot would hide (with a small halo so near misses count too)
  const ocupa = (r, ops) => ops.reduce((s, o) => s + Math.max(0, Math.min(r.right + 4, o.right) - Math.max(r.left - 4, o.left)) * Math.max(0, Math.min(r.bottom + 4, o.bottom) - Math.max(r.top - 4, o.top)), 0);
  const escolher = (a) => { const d = dock.getBoundingClientRect(), ops = opcoes(); let melhor = null;
    for (const r of lugares()) {
      if (r.left < a.right + FOLGA && r.right > a.left - FOLGA && r.top < a.bottom + FOLGA && r.bottom > a.top - FOLGA) continue;
      if (ponto && dentro(r, ponto.x, ponto.y, FOLGA)) continue;
      const custo = ocupa(r, ops) * 100 + Math.hypot(r.left - d.left, r.top - d.top);
      if (!melhor || custo < melhor.custo) melhor = { ...r, custo };
    }
    return melhor; };
  const ir = (x, y) => { const c = casa(), dx = Math.round(x - c.x), dy = Math.round(y - c.y);
    dock.style.setProperty('--dx', dx + 'px'); dock.style.setProperty('--dy', dy + 'px'); dock.classList.toggle('cima', dy < 0); };

  const sair = (el) => { const a = el.getBoundingClientRect(), r = escolher(a); if (!r) return;
    zona = a; clearTimeout(tVolta); tVolta = 0; ir(r.left, r.top); };
  const voltar = () => { clearTimeout(tVolta); tVolta = 0; zona = null; const c = casa(); ir(c.x, c.y); };

  // pointer stood still PARADO ms: over the dock, and the same option sits right under it the whole time
  const decidir = () => {
    if (!ponto || !ancora || arrastando) return;
    const topo = document.elementFromPoint(ponto.x, ponto.y); if (!topo || !dock.contains(topo)) return;
    const el = atras(ponto.x, ponto.y); if (el && el === atras(ancora.x, ancora.y)) sair(el);
  };

  addEventListener('pointermove', (e) => {
    ponto = { x: e.clientX, y: e.clientY }; arrastando = e.buttons !== 0;
    if (!ancora || Math.hypot(ponto.x - ancora.x, ponto.y - ancora.y) > 3) { ancora = ponto; clearTimeout(tParado); tParado = setTimeout(decidir, PARADO); }
    if (!zona) return;
    if (dentro(zona, ponto.x, ponto.y, FOLGA) || dock.contains(e.target)) { clearTimeout(tVolta); tVolta = 0; }
    else if (!tVolta) tVolta = setTimeout(voltar, VOLTA);
  }, { passive: true });
  addEventListener('pointerdown', () => clearTimeout(tParado), true);
  document.documentElement.addEventListener('pointerleave', () => { ponto = null; clearTimeout(tParado); if (zona && !tVolta) tVolta = setTimeout(voltar, VOLTA); });
  // keyboard: Tab onto a covered option uncovers it (mouse focus never counts: a click on a big card under
  // the dock used to fling it aside right before the screen changed)
  addEventListener('focusin', (e) => { if (dock.contains(e.target) || !e.target.matches(':focus-visible')) return; const el = clicavel(e.target);
    if (el && coberto(el)) sair(el); else if (zona && !(ponto && dentro(zona, ponto.x, ponto.y, FOLGA))) voltar(); });
  addEventListener('resize', voltar);
  // screen change: back home
  const desenhar = App.desenharDock; App.desenharDock = () => { desenhar(); voltar(); };
})();
