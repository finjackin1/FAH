// Parametric EQ band area (proposal B "Banda em foco", picked 28/10; prototype docs\prototipos\eqparam): band tabs on top,
// one big panel for the selected band. Hook App.eqParam {html(H, pre), pinta(el, H)} called by equalizador.js (Som and Mic).
const EQP = (() => {
  // filter shape icons (24x16, one stroke, same weight)
  const ICO = { pico: 'M1 13C7 13 8 3 12 3S17 13 23 13', grave: 'M1 4H6C10 4 11 12 15 12H23', agudo: 'M1 12H9C13 12 14 4 18 4H23',
    pb: 'M1 5H12C16 5 18 8 22 14', pa: 'M2 14C6 8 8 5 12 5H23' };
  const icone = (t) => `<svg class="ico-f" viewBox="0 0 24 16"><path d="${ICO[t]}"/></svg>`;
  // one band alone, in dB at f (same model as the engine: LP/HP parallel, the rest in series)
  const dbBanda = (H, b, f) => { if (b.t === 'pb' || b.t === 'pa') { const [r, i] = H.Hc(H.coef(b.t, b.f, b.q, 0), f), g = Math.pow(10, b.g / 20) - 1;
      return 20 * Math.log10(Math.max(Math.hypot(1 + g * r, g * i), 1e-9)); }
    const [r, i] = H.Hc(H.coef(b.t, b.f, b.q, b.g), f); return 20 * Math.log10(Math.max(Math.hypot(r, i), 1e-9)); };
  // svg path of the band over 20 Hz..20 kHz in a w x h box; faixa = dB at the edges (default ±15). Returns {linha, area, x, y} (x,y = band point)
  const caminho = (H, b, w, h, faixa = 15, passo = 3) => { const fy = (g) => h / 2 - Math.max(-faixa, Math.min(faixa, g)) / faixa * (h / 2 - 3);
    const ff = (x) => 20 * Math.pow(1000, x / w); let d = '';
    for (let x = 0; x <= w; x += passo) d += (d ? 'L' : 'M') + x + ' ' + fy(dbBanda(H, b, ff(x))).toFixed(1);
    const x = Math.log(b.f / 20) / Math.log(1000) * w;
    return { linha: d, area: `${d}L${w} ${h / 2}L0 ${h / 2}Z`, x, y: fy(b.t === 'pb' || b.t === 'pa' ? 0 : b.g), zero: h / 2 }; };
  // draw a band into an <svg> using its real pixel size
  const desenha = (svg, H, b, cor, op = {}) => { const w = svg.clientWidth || svg.parentNode.clientWidth, h = svg.clientHeight || svg.parentNode.clientHeight; if (w < 10 || h < 10) return;
    const c = caminho(H, b, w, h, op.faixa, op.passo); svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
    const id = 'g' + Math.random().toString(36).slice(2, 7);
    svg.innerHTML = `<defs><linearGradient id="${id}" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="${cor}" stop-opacity=".55"/><stop offset="1" stop-color="${cor}" stop-opacity=".05"/></linearGradient></defs>
      ${op.grade ? [50, 200, 1000, 5000].map((f) => { const x = Math.log(f / 20) / Math.log(1000) * w; return `<line x1="${x}" x2="${x}" y1="0" y2="${h}" stroke="#ffffff0d"/>`; }).join('') : ''}
      <line x1="0" x2="${w}" y1="${c.zero}" y2="${c.zero}" stroke="#ffffff1a"/>
      <path d="${c.area}" fill="url(#${id})"/><path d="${c.linha}" fill="none" stroke="${cor}" stroke-width="${op.traco || 2}" stroke-linejoin="round"/>
      ${op.ponto ? `<circle cx="${c.x}" cy="${c.y}" r="${op.ponto}" fill="${cor}" stroke="#111" stroke-width="2"/>` : ''}`; };
  const fmtF = UI.hz;
  const fmtG = (g) => (g > 0 ? '+' : g < 0 ? '−' : '') + String(Math.abs(Math.round(g * 10) / 10)).replace('.', ',') + ' dB';
  const temGanho = () => true;   // LP/HP also use the gain (engine: parallel y = x + (G-1)H)
  // common: selected/off classes on every [data-card], type buttons lit
  const marca = (el, H) => { el.querySelectorAll('[data-card]').forEach((c) => { const i = +c.dataset.card, b = H.E.bandas[i]; if (!b) return;
      c.classList.toggle('sel', i === H.E.sel); c.classList.toggle('off', !b.on); });
    el.querySelectorAll('[data-tset]').forEach((t) => { const [i, tt] = t.dataset.tset.split(':'); t.classList.toggle('on', H.E.bandas[i]?.t === tt); }); };
  document.head.insertAdjacentHTML('beforeend', `<style>.ico-f{width:24px;height:16px;fill:none;stroke:currentColor;stroke-width:1.75;stroke-linecap:round;stroke-linejoin:round}</style>`);
  return { ICO, icone, dbBanda, caminho, desenha, fmtF, fmtG, temGanho, marca };
})();
document.head.insertAdjacentHTML('beforeend', `<style>
.pq .vb{flex:1;min-width:0;display:flex;flex-direction:column;gap:8px}
.pq .vb .tiras{display:flex;gap:8px;overflow-x:auto;flex:none;padding:2px;scrollbar-width:thin;scrollbar-color:#ffffff26 transparent}
.pq .vb .chip{flex:0 0 auto;min-width:112px;height:56px;padding:8px 12px 8px 8px;border-radius:12px;background:var(--bg2);display:grid;grid-template-columns:24px 1fr;grid-template-rows:20px 20px;column-gap:8px;align-items:center;cursor:pointer;
  box-shadow:inset 0 0 0 1px #ffffff0a;transition:box-shadow .2s,background .2s,transform .2s,opacity .2s;animation:vbEntra .3s var(--curva) both;font-variant-numeric:tabular-nums}
.pq .vb .chip:hover{transform:translateY(-1px);background:var(--bg3)}
.pq .vb .chip .num{grid-row:1/3;width:24px;height:24px;border-radius:50%;background:var(--cor);color:#000;font:700 12px var(--fonte);display:grid;place-items:center}
.pq .vb .chip .l1{display:flex;align-items:center;gap:4px;font-size:12px;font-weight:600;color:var(--fg);white-space:nowrap}
.pq .vb .chip .l1 .ico-f{width:20px;height:14px;color:var(--cor)}
.pq .vb .chip .l2{font-size:11px;color:var(--fg2);white-space:nowrap}
.pq .vb .chip.sel{background:color-mix(in srgb,var(--cor) 14%,var(--bg2));box-shadow:inset 0 0 0 2px var(--cor)}
.pq .vb .chip.off{opacity:.45}.pq .vb .chip.off .num{background:#636366}
.pq .vb .chip.novo{min-width:56px;display:grid;place-items:center;grid-template-columns:1fr;grid-template-rows:1fr;background:none;box-shadow:inset 0 0 0 1.5px #ffffff1f;color:var(--fg2);font-size:22px}
.pq .vb .chip.novo:hover{color:var(--fg);box-shadow:inset 0 0 0 1.5px var(--ac)}
@keyframes vbEntra{from{opacity:0;transform:translateY(6px)}}
.pq .vb .chip.nasce{animation:vbNasce .35s var(--curva) both}
@keyframes vbNasce{from{opacity:0;transform:scale(.5)}60%{opacity:1;transform:scale(1.06)}}
.pq .vb{position:relative}
/* drag a tab: .vaga = the gap where it would land (small while held, full on drop), .voa = the lifted tab */
.pq .vb .vaga{flex:0 0 auto;width:0;height:56px;margin-right:-8px;border-radius:12px;transition:width .2s var(--curva),margin .2s var(--curva)}
.pq .vb .vaga.abre{width:16px;margin-right:0}
.pq .vb .chip.voa{position:absolute;margin:0;z-index:6;pointer-events:none;animation:vbErgue .18s var(--curva) both;box-shadow:inset 0 0 0 2px var(--cor),0 14px 30px #000b}
.pq .vb .chip.voa.pousa{animation:vbPousa .2s var(--curva) both}
@keyframes vbErgue{to{transform:translateY(-6px) scale(1.06)}}
@keyframes vbPousa{from{transform:translateY(-6px) scale(1.06)}}
.pq.arrasta,.pq.arrasta *{cursor:grabbing!important}
.pq .vb .insp{flex:1;min-height:0;border-radius:var(--r-card);background:radial-gradient(80% 90% at 0% 100%,color-mix(in srgb,var(--cor) 14%,transparent),transparent 70%),var(--bg2);
  display:grid;grid-template-columns:minmax(0,1fr) auto;grid-template-rows:auto minmax(0,1fr);gap:12px 24px;padding:16px;transition:background .3s}
.pq .vb .insp.troca>*{animation:vbTroca .18s ease-out both}
@keyframes vbTroca{from{opacity:0}}
.pq .vb .insp header{grid-column:1/-1;display:flex;align-items:center;gap:12px}
.pq .vb .insp header .num{width:28px;height:28px;border-radius:50%;background:var(--cor);color:#000;font:700 13px var(--fonte);display:grid;place-items:center;box-shadow:0 0 16px -2px var(--cor)}
.pq .vb .insp header b{font-size:15px}.pq .vb .insp header small{color:var(--fg2);font-size:12px;margin-left:4px}
.pq .vb .insp header>div{white-space:nowrap;min-width:0;overflow:hidden;text-overflow:ellipsis}
.pq .vb .insp header .esp{flex:1}
.pq .vb .insp .del{border:0;background:none;color:var(--fg3);height:28px;padding:0 8px;border-radius:8px;display:flex;align-items:center;gap:4px;font:500 12px var(--fonte);cursor:pointer;transition:.15s}
.pq .vb .insp .del svg{width:15px;height:15px}.pq .vb .insp .del:hover{background:rgba(255,69,58,.18);color:var(--perigo)}
.pq .vb .grande{position:relative;border-radius:12px;background:#0000004d;overflow:hidden;min-height:0}
.pq .vb .grande svg{position:absolute;inset:0;width:100%;height:100%}
.pq .vb .grande .gv{position:absolute;left:12px;top:8px;font:700 24px var(--fonte);color:var(--cor);font-variant-numeric:tabular-nums;text-shadow:0 1px 8px #000}
.pq .vb .grande .fv{position:absolute;left:12px;top:40px;font-size:12px;color:var(--fg2);font-variant-numeric:tabular-nums}
.pq .vb .tps{display:flex;gap:4px;background:#0000004d;border-radius:10px;padding:4px;margin-left:16px}
.pq .vb .tps button{width:40px;height:28px;border:0;border-radius:6px;background:none;color:var(--fg3);display:grid;place-items:center;cursor:pointer;transition:background .15s,color .15s}
.pq .vb .tps button:hover{background:#ffffff0d;color:var(--fg)}
.pq .vb .tps button.on{background:color-mix(in srgb,var(--cor) 22%,transparent);color:var(--cor)}
.pq .vb .kns{display:flex;gap:16px;align-items:center;padding-bottom:8px}
.pq .vb .insp .grande,.pq .vb .insp .kns,.pq .vb .insp .tps{transition:opacity .2s,filter .2s}
.pq .vb .insp.off .grande,.pq .vb .insp.off .kns,.pq .vb .insp.off .tps{opacity:.35;filter:saturate(0)}
/* delete: tab folds to nothing (others slide in), panel fades down; empty note fades in */
.pq .vb .chip.morre{animation:none;overflow:hidden;min-width:0;width:0!important;padding-left:0;padding-right:0;margin-right:-8px;opacity:0;transform:scale(.7);pointer-events:none;
  transition:width .26s var(--curva),padding .26s var(--curva),margin .26s var(--curva),opacity .18s,transform .26s var(--curva)}
.pq .vb .insp.sai{animation:vbSai .2s ease-in both;pointer-events:none}
@keyframes vbSai{to{opacity:0;transform:translateY(6px)}}
.pq .vb .vazio{animation:vbTroca .25s ease-out both}
.pq .vb .vazio{flex:1;border-radius:var(--r-card);background:var(--bg2);display:grid;place-items:center;color:var(--fg2);font-size:13px;text-align:center;line-height:20px}
</style>`);
(() => {
  let feito = { sel: -1, n: -1 };
  const insp = (H, i, anima) => { const E = H.E, b = E.bandas[i];
    return `<div class="insp ${anima ? 'troca' : ''} ${b.on ? '' : 'off'}" data-card="${i}" style="--cor:${H.cor(i)}">
      <header><span class="num">${i + 1}</span><div><b>Banda ${i + 1}</b><small data-nm>${H.tp(b.t)}</small></div>
        <div class="tps">${H.TIPOS.map(([t, n]) => `<button data-tset="${i}:${t}" title="${n}">${EQP.icone(t)}</button>`).join('')}</div><span class="esp"></span>
        ${UI.switch(b.on, { banda: i })}<button class="del" data-del="${i}" title="Excluir esta banda (Delete)">${UI.ico('lixo')}Excluir</button></header>
      <div class="grande"><svg data-mini="${i}"></svg><span class="gv" data-gv="${i}"></span><span class="fv" data-fv="${i}"></span></div>
      <div class="kns">${H.knob(`b.${i}.g`, 'Ganho')}${H.knob(`b.${i}.f`, 'Frequência')}${H.knob(`b.${i}.q`, 'Largura (Q)')}</div></div>`; };
  const chip = (H, b, i) => `<div class="chip ${i === H.E.sel ? 'sel' : ''} ${b.on ? '' : 'off'}" data-card="${i}" style="--cor:${H.cor(i)};animation-delay:${i * .03}s">
    <span class="num">${i + 1}</span><span class="l1"><span data-cg="${i}"></span>${EQP.icone(b.t)}</span><span class="l2" data-cf="${i}"></span></div>`;
  App.eqParam = {
    html(H, pre) { const E = H.E; feito = { sel: E.sel, n: E.bandas.length };
      return `<div class="faders" style="padding:0;background:none">${pre}<div class="vb">
        <div class="tiras">${E.bandas.map((b, i) => chip(H, b, i)).join('')}${E.bandas.length < H.MAXB ? '<div class="chip novo" data-novo="1" title="Nova banda (ou duplo clique no gráfico)">+</div>' : ''}</div>
        ${E.bandas.length ? `<div data-slot>${insp(H, E.sel, true)}</div>` : '<div class="vazio">Nenhuma banda ainda.<br>Clique em + ou dê dois cliques no gráfico.</div>'}</div></div>`; },
    // new band without rebuilding the screen (28/10): its tab grows in, the row follows it, pinta() fades the panel in.
    // false = no band area on screen (caller rebuilds)
    novo(el, H) { const E = H.E, i = E.bandas.length - 1, vb = el.querySelector('.vb'), tiras = vb?.querySelector('.tiras'); if (!tiras) return false;
      const mais = tiras.querySelector('[data-novo]'), t = document.createElement('div'); t.innerHTML = chip(H, E.bandas[i], i);
      const c = t.firstElementChild; c.classList.add('nasce'); tiras.insertBefore(c, mais);
      // the grow (scale) shrinks the row while it runs: settle at the real end when it finishes
      c.addEventListener('animationend', () => c.isConnected && E.sel === i && tiras.scrollTo({ left: tiras.scrollWidth, behavior: 'smooth' }), { once: true }); if (E.bandas.length >= H.MAXB) mais?.remove();
      if (!vb.querySelector('[data-slot]')) { vb.querySelector('.vazio')?.remove(); vb.insertAdjacentHTML('beforeend', '<div data-slot></div>'); }
      feito = { sel: -1, n: E.bandas.length }; return true; },
    // drag a tab to reorder (28/10): it lifts and the others close up; a small gap opens where it would land; on drop the
    // gap opens to fit and it settles in; Esc while holding = flies back. fim(de, para) commits (para = index without it).
    arrastar(el, HH, fim) { let d = null, solto = 0; const T = 200, ease = (p) => 1 - Math.pow(1 - p, 3);
      const vaga = (cheia) => { const v = document.createElement('div'); v.className = 'vaga';
        if (cheia) { v.style.transition = 'none'; v.style.width = cheia + 'px'; v.style.marginRight = '0'; } return v; };
      const abrir = (v) => { v.offsetWidth; v.style.transition = ''; v.style.width = ''; v.style.marginRight = ''; v.classList.add('abre'); };
      const fechar = (v) => { v.style.width = ''; v.style.marginRight = ''; v.classList.remove('abre'); setTimeout(() => v.remove(), T + 50); };
      const antesDe = (k) => d.outros[k] || d.t.querySelector('[data-novo]');
      el.addEventListener('pointerdown', (e) => { const c = e.target.closest('.tiras .chip[data-card]');
        if (c && e.button === 0 && !d) d = { c, x0: e.clientX, y0: e.clientY, id: e.pointerId, de: +c.dataset.card }; });
      const erguer = () => { const { c } = d, t = c.parentNode, vb = t.closest('.vb'), r = c.getBoundingClientRect(), rv = vb.getBoundingClientRect(), rt = t.getBoundingClientRect();
        const outros = [...t.querySelectorAll('.chip[data-card]')].filter((x) => x !== c);
        // landing slots measured once, as if the tab were gone (the moving gaps don't feed back into the choice)
        const centros = outros.map((o) => { const ro = o.getBoundingClientRect(); return ro.left - rt.left + t.scrollLeft + ro.width / 2 - (+o.dataset.card > d.de ? r.width + 8 : 0); });
        const f = c.cloneNode(true); f.classList.remove('nasce'); f.classList.add('voa');
        Object.assign(f.style, { left: r.left - rv.left + 'px', top: r.top - rv.top + 'px', width: r.width + 'px', height: r.height + 'px' });
        vb.appendChild(f); const v = vaga(r.width); t.insertBefore(v, c); c.style.display = 'none'; abrir(v);
        el.querySelector('.pq')?.classList.add('arrasta'); try { el.setPointerCapture(d.id); } catch { }
        Object.assign(d, { t, vb, f, v, r, outros, centros, k: d.de, dx: d.x0 - r.left, px: d.x0, ativo: true });
        // held near a row edge: the row keeps scrolling (faster closer to the edge) and the landing gap follows
        const g = d, borda = () => { if (d !== g) return; const rt = t.getBoundingClientRect(), a = rt.left + 48 - g.px, b = g.px - (rt.right - 48);
          if (a > 0 || b > 0) { const antes = t.scrollLeft; t.scrollLeft += (b > 0 ? 1 : -1) * Math.min(14, 2 + Math.max(a, b) / 4); if (t.scrollLeft !== antes) mover({ clientX: g.px }); }
          requestAnimationFrame(borda); };
        requestAnimationFrame(borda); };
      const mover = (e) => { d.px = e.clientX; const { t, f, r } = d, rt = t.getBoundingClientRect(), rv = d.vb.getBoundingClientRect();
        const x = Math.max(rt.left - 8, Math.min(rt.right - r.width + 8, d.px - d.dx)); f.style.left = x - rv.left + 'px';
        const k = d.centros.filter((cx) => cx < x - rt.left + t.scrollLeft + r.width / 2).length;
        if (k !== d.k) { d.k = k; fechar(d.v); d.v = vaga(); t.insertBefore(d.v, antesDe(k)); abrir(d.v); } };
      // settle: the gap opens to the tab's width while the tab glides onto it (follows the gap every frame) and lands
      const pousar = (para) => { const g = d; d = null; solto = performance.now(); el.querySelector('.pq')?.classList.remove('arrasta');
        g.v.style.width = g.r.width + 'px'; g.v.style.marginRight = '0'; g.f.classList.add('pousa');
        const x0 = parseFloat(g.f.style.left), y0 = parseFloat(g.f.style.top), t0 = performance.now();
        const passo = (agora) => { const p = Math.min(1, (agora - t0) / T), rv = g.vb.getBoundingClientRect(), rr = g.v.getBoundingClientRect();
          g.f.style.left = x0 + (rr.left - rv.left - x0) * ease(p) + 'px'; g.f.style.top = y0 + (rr.top - rv.top - y0) * ease(p) + 'px';
          if (p < 1) return requestAnimationFrame(passo);
          g.c.style.animation = 'none'; g.t.insertBefore(g.c, g.v); g.c.style.display = ''; g.v.remove(); g.f.remove();
          if (para === g.de) return;
          g.t.querySelectorAll('.chip[data-card]').forEach((ch, i) => { ch.dataset.card = i; ch.querySelector('.num').textContent = i + 1; });
          feito.sel = -1; fim(g.de, para); };
        requestAnimationFrame(passo); };
      const voltar = () => { if (d.k !== d.de) { fechar(d.v); d.v = vaga(); d.t.insertBefore(d.v, antesDe(d.de)); abrir(d.v); } pousar(d.de); };
      addEventListener('pointermove', (e) => { if (!d) return; if (!d.ativo) { if (Math.hypot(e.clientX - d.x0, e.clientY - d.y0) < 6) return; erguer(); } mover(e); });
      addEventListener('pointerup', () => { if (d?.ativo) pousar(d.k); else d = null; });
      addEventListener('pointercancel', () => { if (d?.ativo) voltar(); else d = null; });
      addEventListener('keydown', (e) => { if (d?.ativo && e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); voltar(); } }, true);
      el.addEventListener('click', (e) => { if (performance.now() - solto < 300) { e.stopPropagation(); e.preventDefault(); } }, true); },
    // delete band i, step 1 (before the data changes): tab folds, panel fades. false = not on screen (caller rebuilds)
    remover(el, i, T) { const c = el.querySelector(`.tiras .chip[data-card="${i}"]`); if (!c) return false;
      c.style.width = c.offsetWidth + 'px'; c.style.animation = 'none'; c.offsetWidth; c.classList.add('morre'); c.style.transitionDuration = T + 'ms';
      el.querySelector('[data-slot] .insp')?.classList.add('sai'); return true; },
    // step 2 (band already gone from E.bandas): drop the tab, renumber the rest, + comes back if the row was full,
    // next band's panel fades in on the next pinta (or the empty note)
    removido(el, H, i) { const E = H.E, vb = el.querySelector('.vb'), t = vb.querySelector('.tiras');
      t.querySelector(`.chip[data-card="${i}"]`)?.remove();
      t.querySelectorAll('.chip[data-card]').forEach((ch, k) => { ch.dataset.card = k; ch.querySelector('.num').textContent = k + 1;
        ch.querySelector('[data-cg]').dataset.cg = k; ch.querySelector('[data-cf]').dataset.cf = k; ch.style.setProperty('--cor', H.cor(k)); });
      if (E.bandas.length < H.MAXB && !t.querySelector('[data-novo]')) { t.insertAdjacentHTML('beforeend', '<div class="chip novo nasce" data-novo="1" title="Nova banda (ou duplo clique no gráfico)">+</div>'); }
      if (!E.bandas.length) { vb.querySelector('[data-slot]')?.remove(); vb.insertAdjacentHTML('beforeend', '<div class="vazio">Nenhuma banda ainda.<br>Clique em + ou dê dois cliques no gráfico.</div>'); }
      feito = { sel: -1, n: E.bandas.length }; },
    pinta(el, H) { const E = H.E, slot = el.querySelector('[data-slot]');
      // selection changed (tab click or handle on the curve): swap the panel with a short slide
      if (slot && feito.sel !== E.sel) { feito.sel = E.sel; slot.innerHTML = insp(H, E.sel, true);
        // last band (just created): row to the end, so the new tab and + both show
        const t = el.querySelector('.tiras'); if (E.sel === E.bandas.length - 1) t?.scrollTo({ left: t.scrollWidth, behavior: 'smooth' });
        else el.querySelector(`.chip[data-card="${E.sel}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' }); }
      EQP.marca(el, H);
      E.bandas.forEach((b, i) => { const c = el.querySelector(`.chip[data-card="${i}"]`); if (!c) return;
        c.querySelector('[data-cg]').textContent = EQP.fmtG(b.g); c.querySelector('[data-cf]').textContent = EQP.fmtF(b.f);
        const ic = c.querySelector('.ico-f path'); if (ic) ic.setAttribute('d', EQP.ICO[b.t]); });
      const b = E.bandas[E.sel], s = slot?.querySelector('[data-mini]'); if (!b || !s) return;
      EQP.desenha(s, H, b, H.cor(E.sel), { ponto: 6, faixa: 15, traco: 2.5, grade: 1 });
      slot.querySelector('[data-gv]').textContent = EQP.fmtG(b.g); slot.querySelector('[data-fv]').textContent = EQP.fmtF(b.f) + ' · Q ' + String(b.q.toFixed(2)).replace('.', ',');
      slot.querySelector('[data-nm]').textContent = H.tp(b.t); },
  };
})();
