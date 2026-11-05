// "Espaço" view of the Som EQ (prototype C, chosen 04/11/2026: docs\prototipos\eq-espaco\proposta-C.html).
// Replaces the curve area when the 3rd segment is picked: room drawing (left) + crossfeed/reverb controls (right).
// State = App.estado.efeitos (cf, rv) from efeitos.js; engine gates cf/rv by ligado.eq, cf also paused by Som 7.1.
const EspEq = (() => {
  const E = Efx.E, S = App.estado;
  const pausa = () => Efx.s71();
  const eqOn = () => (S.ligado || {}).eq !== false;
  document.head.insertAdjacentHTML('beforeend', `<style>
.esp-eq{position:relative;flex:1;min-height:80px;margin-top:12px;border-radius:var(--r-med);background:#111;display:flex;align-items:center}
.esp-eq[hidden]{display:none}.esp-eq.entra-esp{animation:eqModo .3s var(--curva) both}
.esp-eq svg{flex:1;height:100%;min-width:0}
.esp-eq .sala{fill:none;stroke:#3a3a3c;stroke-dasharray:4 6;transition:x .35s var(--curva),y .35s var(--curva),width .35s var(--curva),height .35s var(--curva),stroke .3s,fill .3s}
.esp-eq .sala.on{fill:rgba(255,159,10,.08);stroke:#ff9f0a;stroke-dasharray:none}
.esp-eq .eco{fill:none;stroke:rgba(255,159,10,.25);transition:x .35s var(--curva),y .35s var(--curva),width .35s var(--curva),height .35s var(--curva),opacity .3s;animation:espEco 1.6s ease-in-out infinite}
.esp-eq .eco.b{animation-delay:.4s}@keyframes espEco{50%{stroke-opacity:.15}0%,100%{stroke-opacity:1}}
.esp-eq .arco{fill:none;stroke:#ffd60a;stroke-dasharray:4 3;transition:opacity .3s;animation:espArco 1s linear infinite}@keyframes espArco{from{stroke-dashoffset:14}to{stroke-dashoffset:0}}
.esp-eq .ctl{width:420px;flex:none;margin-right:20px;display:grid;grid-template-columns:96px 1fr 44px;gap:8px 12px;align-items:center}
.esp-eq .ctl b{font-weight:600}.esp-eq .ctl .sl-r{color:var(--fg2)}.esp-eq .ctl small{color:var(--fg3,#636366);font-size:12px;font-variant-numeric:tabular-nums}
.esp-eq .apaga{transition:opacity .3s,filter .3s}.esp-eq .apaga.off{opacity:.35;filter:grayscale(1);pointer-events:none}
.esp-eq .dica{grid-column:1/-1;margin-top:-2px;line-height:16px;font-size:12px;color:#636366;transition:color .3s}.esp-eq .dica.av{color:var(--ac)}
.esp-eq .rv-sel{position:relative;display:flex;align-items:center;justify-content:space-between;height:32px;padding:0 12px;border-radius:8px;background:var(--controle,#3a3a3c);font-weight:600;cursor:pointer}
.esp-eq .rv-sel small{color:#636366}
.esp-eq .rv-pop{position:absolute;left:0;right:0;top:36px;z-index:20;background:var(--bg3,#2c2c2e);border-radius:10px;padding:4px;box-shadow:0 12px 40px rgba(0,0,0,.6);display:none;max-height:220px;overflow:auto}
.esp-eq .rv-pop.aberto{display:block;animation:cEd .15s var(--curva) both}
.esp-eq .rv-pop div{padding:7px 10px;border-radius:6px;font-weight:500}.esp-eq .rv-pop div:hover,.esp-eq .rv-pop div:focus-visible{background:rgba(255,255,255,.08);outline:0}.esp-eq .rv-pop div.on{color:var(--ac)}
.esp-eq .rv-pop::-webkit-scrollbar{width:6px}.esp-eq .rv-pop::-webkit-scrollbar-thumb{background:#48484a;border-radius:3px}
</style>`);
  const html = () => `<svg class="esp-svg"><rect class="sala" rx="10"/><rect class="eco a" rx="8"/><rect class="eco b" rx="8"/><g class="arcos"></g>
      <g class="cab"><circle r="13" fill="#3a3a3c"/><rect x="-17" y="-6" width="4" height="12" rx="2" fill="#636366"/><rect x="13" y="-6" width="4" height="12" rx="2" fill="#636366"/></g></svg>
    <div class="ctl"><b>Crossfeed</b><div class="apaga cf-niv">${UI.segmento(Efx.NIVEIS, E.cf.nivel, { espnivel: 1 })}</div>${UI.switch(E.cf.on, { esp: 'cf' })}
      <b>Reverb</b><div class="apaga rv-a"><div class="rv-sel" tabindex="0" data-rvsel="1"><span class="rv-nome">${E.rv.preset}</span><small>▾</small>
        <div class="rv-pop">${Object.keys(Efx.PRESETS).map((n) => `<div tabindex="0" data-rvp="${n}">${n}</div>`).join('')}</div></div></div>${UI.switch(E.rv.on, { esp: 'rv' })}
      <span class="sl-r">Sala</span><div class="apaga rv-b">${UI.slider(E.rv.p[0], { espsala: 1, padrao: Efx.deP(E.rv.preset)[0] })}</div><small class="sala-v">${E.rv.p[0]}%</small><div class="dica"></div></div>`;
  // in-place update: geometry by CSS transition, nothing rebuilt
  const pinta = (el) => { const svg = el.querySelector('.esp-svg'); if (!svg) return;
    const w0 = svg.clientWidth || 700, h0 = svg.clientHeight || 142, cx = w0 / 2, cy = h0 / 2, t = E.rv.p[0];
    const w = Math.min(w0 - 24, 160 + t * 3), h = Math.min(h0 - 16, 40 + t * 1.6), on = eqOn();
    const caixa = (r, f) => { r.style.x = cx - w * f / 2 + 'px'; r.style.y = cy - h * f / 2 + 'px'; r.style.width = w * f + 'px'; r.style.height = h * f + 'px'; };
    const sala = svg.querySelector('.sala'); caixa(sala, 1); sala.classList.toggle('on', E.rv.on && on);
    svg.querySelectorAll('.eco').forEach((r, i) => { caixa(r, [.6, .8][i]); r.style.opacity = E.rv.on && on ? 1 : 0; });
    svg.querySelector('.cab').setAttribute('transform', `translate(${cx} ${cy + 3})`);
    const cfOn = E.cf.on && !pausa() && on, lv = { suave: 1, medio: 2, forte: 3 }[E.cf.nivel] || 2, g = svg.querySelector('.arcos');
    g.innerHTML = [0, 1, 2].map((i) => `<path class="arco" d="M${cx - 16} ${cy - 5 - i * 6} Q${cx} ${cy - 21 - i * 8} ${cx + 16} ${cy - 5 - i * 6}" stroke-opacity="${.8 - i * .2}" style="opacity:${cfOn && i < lv ? 1 : 0}"/>`).join('');
    el.querySelector('.cf-niv').classList.toggle('off', !E.cf.on || pausa() || !on);
    el.querySelectorAll('.rv-a,.rv-b').forEach((d) => d.classList.toggle('off', !E.rv.on || !on));
    el.querySelector('.rv-nome').textContent = E.rv.preset; el.querySelector('.sala-v').textContent = E.rv.p[0] + '%';
    el.querySelectorAll('[data-rvp]').forEach((d) => d.classList.toggle('on', d.dataset.rvp === E.rv.preset));
    const dc = el.querySelector('.dica'), av = !on || (E.cf.on && pausa());
    dc.textContent = !on ? 'Equalizador desligado: crossfeed e reverb também param' : av ? 'Crossfeed pausado pelo Som 7.1' : 'Quanto maior a sala, mais longe o som parece';
    dc.classList.toggle('av', av); };
  const fechaPop = (el) => el.querySelector('.rv-pop')?.classList.remove('aberto');
  const liga = (el) => { if (el._esp) return; el._esp = 1;
    el.addEventListener('c:mudou', (e) => { const { tipo, valor, dados } = e.detail;
      if (dados.esp) { E[dados.esp].on = valor; pinta(el); }
      if (dados.espnivel) { E.cf.nivel = valor; pinta(el); }
      if (dados.espsala && tipo === 'slider') { E.rv.p[0] = Math.round(valor); pinta(el); } });
    el.addEventListener('click', (e) => { const p = e.target.closest('[data-rvp]');
      if (p) { E.rv.preset = p.dataset.rvp; E.rv.p = Efx.deP(E.rv.preset); const s = el.querySelector('[data-espsala]'); UI.poe(s, E.rv.p[0]); s.dataset.padrao = E.rv.p[0]; fechaPop(el); pinta(el); return; }
      if (e.target.closest('[data-rvsel]')) el.querySelector('.rv-pop').classList.toggle('aberto'); });
    el.addEventListener('keydown', (e) => { if (e.key === 'Escape') return fechaPop(el); if (e.key !== 'Enter' && e.key !== ' ') return;
      const t = e.target.closest('[data-rvp],[data-rvsel]'); if (t) { e.preventDefault(); t.click(); } });
    document.addEventListener('pointerdown', (e) => { if (!e.target.closest('.esp-eq .rv-sel')) fechaPop(el); }, true);
    new ResizeObserver(() => el.isConnected && !el.hidden && pinta(el)).observe(el);
    addEventListener('fah:ligado', () => el.isConnected && pinta(el)); };
  return { html, pinta, liga };
})();
