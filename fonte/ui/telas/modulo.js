// Screen: detail of one module. Generic shell (header + on/off); body comes from App.detalhe(id) registered by modulos/*.js.
(() => {
  const css = `
  .tela.t-mod{display:flex;flex-direction:column;overflow:hidden;padding:20px 40px}
  .t-mod header{display:flex;align-items:center;gap:14px;margin-bottom:16px;flex:none}
  .t-mod .volta{border:0;background:none;color:var(--ac);font-size:26px;cursor:pointer;display:grid;place-items:center;padding:0 4px}
  .t-mod h2{margin:0;font-size:var(--t-tela);font-weight:700;letter-spacing:-.5px}
  .t-mod .sub{color:var(--fg2);font-size:13px}
  .t-mod header .c-switch{margin-left:auto}
  .t-mod .corpo{max-width:1200px;width:100%;flex:1;min-height:0;display:flex;flex-direction:column}
  .t-mod .corpo>*{flex:1;min-height:0}
  .painel{background:var(--bg2);border-radius:var(--r-card);padding:18px 20px}
  .painel h4{margin:0 0 12px;font-size:13px;font-weight:600;color:var(--fg2);text-transform:uppercase;letter-spacing:.4px}
  .t-mod .corpo{transition:.3s}.t-mod.off .corpo{opacity:.35;filter:grayscale(1);pointer-events:none}
  .tela.voa{transition:none!important;z-index:2}.tela.voa.fora{opacity:1;transform:none}
  .voo{position:absolute;inset:0;z-index:45;pointer-events:none;overflow:hidden}
  .voo.fundo{z-index:1}.voo.fundo *{animation:none!important;transition:none!important}.voo.fundo [data-voa]{visibility:hidden}
  .t-inicio .no.pousa{visibility:hidden;transform:none;box-shadow:none;background:var(--bg2)}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  // card -> screen flight (28/10, user): the Início card grows into this screen and its pieces (icon, name, switch, drawings)
  // fly to their big versions; back = the reverse. origem = { id, caixa, pecas:[{alvo, de, src}] } from inicio.js, coords relative to #palco.
  // No visible swap (28/10, user "nada de piscar"): the real card is hidden during the flight and a frozen copy of it (minus the
  // flying pieces) sits under this screen, whose background starts/ends transparent; copies carry the card's real opacity/filter.
  const DUR = 520, quieto = matchMedia('(prefers-reduced-motion: reduce)');
  let voo = null, origem = null, tela = null;
  const cor = (v) => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
  const palco = () => document.getElementById('palco').getBoundingClientRect();
  const rel = (e, P) => { const r = e.getBoundingClientRect(); return { x: r.left - P.left, y: r.top - P.top, w: r.width, h: r.height }; };
  const ins = (c, P, r = 24) => `inset(${c.y}px ${P.width - c.x - c.w}px ${P.height - c.y - c.h}px ${c.x}px round ${r}px)`;
  const tudo = (P) => ins({ x: 0, y: 0, w: P.width, h: P.height }, P, 0);
  const cardDe = (id) => document.querySelector(`.tela.t-inicio [data-card="${id}"]`);
  function limpa() { if (!voo) return; const v = voo; voo = null; v.anims.forEach((a) => a.cancel()); v.camada.remove(); v.fundo?.remove();
    v.card?.classList.remove('pousa'); (v.id && cardDe(v.id))?.classList.remove('pousa');
    v.el.style.transition = 'none'; v.el.classList.remove('voa'); void v.el.offsetWidth; v.el.style.transition = ''; }
  // copy of one card piece standing at box r, with the card's look (its styles depend on card ancestors, opacity/filter included)
  function copia(src, r, camada) { const c = src.cloneNode(true), cs = getComputedStyle(src), w = document.createElement('div');
    for (const k of ['font', 'color', 'backgroundColor', 'backgroundImage', 'borderRadius', 'boxShadow', 'whiteSpace']) c.style[k] = cs[k];
    let op = 1; const fl = []; for (let e = src; e; e = e.parentElement) { const s = getComputedStyle(e); op *= +s.opacity; if (s.filter !== 'none') fl.unshift(s.filter); if (e.classList.contains('no')) break; }
    Object.assign(c.style, { margin: 0, width: '100%', height: '100%', position: 'relative', overflow: 'hidden', animation: 'none', transition: 'none', opacity: op, filter: fl.join(' ') || 'none' });
    w.style.cssText = `position:absolute;left:${r.x}px;top:${r.y}px;width:${r.w}px;height:${r.h}px;transform-origin:0 0`; w.append(c); camada.append(w); return w; }
  // frozen copy of the whole card at its place (same board classes so the Início styles match), just under this screen
  function fundoDe(card, el, comPose) { const f = document.createElement('div'), b = document.createElement('div'), c = card.cloneNode(true), cs = getComputedStyle(card);
    f.className = 't-inicio voo fundo'; b.className = card.closest('.b').className; c.classList.remove('pousa');
    if (comPose) for (const k of ['transform', 'boxShadow', 'backgroundColor']) c.style[k] = cs[k];
    b.append(c); f.append(b); el.before(f); return f; }
  // transform that puts box b over box a (small pieces = text, icons, switch: keep proportions)
  const mapa = (a, b) => `translate(${a.x - b.x}px,${a.y - b.y}px) ` + (Math.min(a.h, b.h) < 30 ? `scale(${a.h / b.h})` : `scale(${a.w / b.w},${a.h / b.h})`);
  const camadaNova = () => { const c = document.createElement('div'); c.className = 't-inicio voo'; document.getElementById('palco').append(c); return c; };
  // parts of the screen that only fade (everything except the flying pieces and their ancestors); they fade to their resting opacity
  function soltas(raiz, ts) { const out = []; for (const c of raiz.children) { if (ts.includes(c)) continue;
    if (ts.some((t) => c.contains(t))) out.push(...soltas(c, ts)); else out.push(c); } return out; }
  // resting values come from the page itself (partial keyframes: the missing end/start is the element's own value),
  // so dimmed panels and the module's entrance animations end exactly where they would without the flight
  const SEM = { backgroundColor: 'transparent', borderColor: 'transparent', boxShadow: 'none' };
  const avos = (el, ts) => [...new Set(ts.flatMap((t) => { const r = []; for (let e = t.parentElement; e && e !== el; e = e.parentElement) r.push(e); return r; }))];
  // real piece flies the same path as its copy (FLIP), so the swap happens with both in the same place
  const real = (t, de, a, quadros, op) => t.animate(quadros.map((q) => ({ transformOrigin: '0 0', ...q, transform: q.transform === 'card' ? mapa(de, a) : q.transform })), op);
  function abre(el, o) { limpa(); if (!o || quieto.matches) return;
    const P = palco(), card = cardDe(o.id); el.classList.add('voa');
    // start from where the card is on screen right now (hover lift / press included)
    const caixa = card ? rel(card, P) : o.caixa, des = o.pecas.map((p) => (p.src.isConnected ? rel(p.src, P) : p.de));
    const camada = camadaNova(), fundo = card && fundoDe(card, el, true), op = { duration: DUR, easing: cor('--curva') };
    const anims = [el.animate([{ clipPath: ins(caixa, P), backgroundColor: 'transparent' }, { backgroundColor: cor('--bg2'), offset: .3 }, { clipPath: tudo(P), backgroundColor: cor('--bg') }], op)];
    const ts = o.pecas.map((p) => el.querySelector(p.alvo));
    soltas(el, ts.filter(Boolean)).forEach((c) => anims.push(c.animate([{ opacity: 0 }, { opacity: 0, offset: .35 }], op)));
    avos(el, ts.filter(Boolean)).forEach((c) => anims.push(c.animate([SEM, { ...SEM, offset: .35 }], op)));
    o.pecas.forEach((p, i) => { const t = ts[i]; if (!t) return; const a = rel(t, P), w = copia(p.src, des[i], camada);
      anims.push(real(t, des[i], a, [{ transform: 'card' }, { transform: 'none' }], op), t.animate([{ opacity: 0 }, { opacity: 0, offset: .4 }], { duration: DUR * .5 }));
      anims.push(w.animate([{ transform: 'none', opacity: 1 }, { opacity: 1, offset: .5 }, { opacity: 0, offset: .85 }, { transform: mapa(a, des[i]), opacity: 0 }], op)); });
    card?.classList.add('pousa');
    voo = { el, anims, camada, fundo, card }; anims[0].onfinish = () => voo?.anims === anims && limpa(); }
  function fecha(el) { limpa(); const o = origem; if (!o || quieto.matches) return;
    const P = palco(); el.classList.add('voa'); const ts = o.pecas.map((p) => el.querySelector(p.alvo)), alvos = ts.map((t) => t && rel(t, P));
    const camada = camadaNova(), op = { duration: DUR - 40, easing: cor('--curva'), fill: 'forwards' };
    const anims = [el.animate([{ clipPath: tudo(P), backgroundColor: cor('--bg') }, { backgroundColor: cor('--bg2'), offset: .7 }, { clipPath: ins(o.caixa, P), backgroundColor: 'transparent' }], op)];
    soltas(el, ts.filter(Boolean)).forEach((c) => anims.push(c.animate([{ opacity: 0, offset: .35 }, { opacity: 0 }], op)));
    avos(el, ts.filter(Boolean)).forEach((c) => anims.push(c.animate([{ ...SEM, offset: .35 }, SEM], op)));
    o.pecas.forEach((p, i) => { const t = ts[i]; if (t) anims.push(real(t, p.de, alvos[i], [{ transform: 'none' }, { transform: 'card' }], op),
      t.animate([{ opacity: 0 }], { delay: op.duration * .45, duration: op.duration * .3, fill: 'forwards' })); });
    const v = voo = { el, anims, camada, id: o.id }; anims[0].onfinish = () => voo?.anims === anims && limpa();
    // pieces come from the freshly drawn Início card (it is mounted right after this, same task)
    queueMicrotask(() => { if (voo?.anims !== anims) return; const card = cardDe(o.id); if (!card) return;
      card.classList.add('pousa'); v.card = card; v.fundo = fundoDe(card, el, false);
      const fontes = [...card.querySelectorAll('[data-voa]')];
      o.pecas.forEach((p, i) => { const src = fontes.find((f) => f.dataset.voa === p.alvo); if (!src || !alvos[i]) return; const w = copia(src, p.de, camada);
        anims.push(w.animate([{ transform: mapa(alvos[i], p.de), opacity: 0 }, { opacity: 0, offset: .1 }, { opacity: 1, offset: .45 }, { transform: 'none', opacity: 1 }], op)); }); }); }
  App.tela({ id: 'modulo', classe: 't-mod rolagem',
    aoSair(destino) { if (destino === 'inicio' && App.estado.moduloDe === 'inicio') fecha(tela); else limpa(); },
    montar(el, { id, lado, de, origem: o }) {
      tela = el; origem = o || null;
      App.estado.moduloDe = de || 'dispositivo';
      const m = App.modulosDo(lado).find((x) => x.id === id), d = App.detalhePor(id), lig = App.estado.ligado;
      el.style.setProperty('--ac', `var(--${lado})`); el.classList.toggle('off', lig[id] === false);
      el.innerHTML = `<header><button class="volta" title="Voltar (Esc)">${UI.ico('volta')}</button>${UI.icone(m.icone, 44, `var(--${lado}-ic)`)}
        <div><h2>${m.nome}</h2><div class="sub">${m.desc}</div></div>${UI.switch(lig[id] !== false, { det: id })}</header><div class="corpo"></div>`;
      el.querySelector('.volta').onclick = () => App.ir(App.estado.moduloDe, { lado, volta: id });
      el.onclick = null;
      el.oncontextmenu = null;
      el.removeEventListener('c:mudou', el._h);
      el.addEventListener('c:mudou', el._h = (e) => { if (e.detail.dados.det) { lig[id] = e.detail.valor; el.classList.toggle('off', !e.detail.valor); } });
      d.montar(el.querySelector('.corpo'));
      abre(el, origem);
    } });
  // Esc inside a module goes back to where it was opened from (Início path or its side)
  const antes = App.voltar; App.voltar = () => (App.estado.tela === 'modulo' ? App.ir(App.estado.moduloDe || 'dispositivo', { lado: App.estado.ultimoLado, volta: true }) : antes());
})();
