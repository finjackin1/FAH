// Estéreo+ (04/11, approved docs\prototipos\estereo\cartao.html): Início card only, no module screen. Switch = ligado.efeitos,
// popup button = efeitos.est.modo (auto|jogo|musica|filme); chip beside it = efeitos.est.niv (bool, "Nivelar volume", approved variant B 05/11: docsprototiposestereoESCOLHIDA-nivelador.md). Auto: engine detects what plays and sends "estauto" {modo, fonte}.
// Som 7.1 on = paused. Engine: fonte\Modulos\Efeitos (width per mode, side highpass ~120 Hz). Loads before telas/inicio.js.
const Est = (() => {
  const S = App.estado;
  const MODOS = [['auto', 'Automático'], ['jogo', 'Jogo'], ['musica', 'Música'], ['filme', 'Filme']];
  const NOME = Object.fromEntries(MODOS);
  const ABRE = { jogo: 45, musica: 70, filme: 90 };
  const DIZ = { auto: 'Muda sozinho conforme o que toca', jogo: 'Pouca abertura, direção precisa', musica: 'Aberto, voz e graves no centro', filme: 'Bem amplo, falas no centro' };
  const E = () => { const e = (S.efeitos = S.efeitos || {}); e.est = e.est || { on: true }; if (!NOME[e.est.modo]) e.est.modo = 'auto'; if (typeof e.est.niv !== 'boolean') e.est.niv = false; if (typeof e.est.larg !== 'number') e.est.larg = 100; if (typeof e.est.bal !== 'number') e.est.bal = 0; if (typeof e.est.int !== 'number') e.est.int = 100; return e.est; };
  E();
  const modo = () => E().modo;
  const efetivo = () => (modo() === 'auto' ? (ABRE[S.estauto?.modo] ? S.estauto.modo : 'musica') : modo());
  const on = () => (S.ligado || {}).efeitos !== false;
  const pausa = () => (S.ligado || {}).som71 !== false;
  const parado = () => !on() || pausa();
  const niv = () => E().niv === true;
  let abre = ABRE[efetivo()];
  // 08/11 "Intensidade" (user: effects too subtle): one slider on this card; while Som 7.1 is on it sets the 7.1's own value (s71.int).
  // Engine: Espacial/Intensidade.cs (50% = the approved sound, voice untouched at any value)
  const s71 = () => (S.s71 = S.s71 || {});
  const intV = () => { if (pausa()) { if (typeof s71().int !== 'number') s71().int = 100; return s71().int; } return E().int; };
  const intRot = () => (pausa() ? 'Intensidade do 7.1' : 'Intensidade');
  const intHtml = () => `<div class="es-int"><span class="ir">${intRot()}</span>${UI.slider(intV(), { esint: 1, padrao: 100 })}<span class="vl">${intV()}%</span></div>`;
  const intPinta = () => { const c = card(); if (!c) return; const sl = c.querySelector('[data-esint]'); if (!sl) return;
    UI.poe(sl, intV()); c.querySelector('.es-int .vl').textContent = intV() + '%'; c.querySelector('.es-int .ir').textContent = intRot(); };
  document.addEventListener('c:mudou', (e) => { if (!e.detail?.dados?.esint || !e.target.closest('[data-card=efeitos]')) return;
    const v = Math.round(e.detail.valor); if (pausa()) s71().int = v; else E().int = v; e.target.parentNode.querySelector('.vl').textContent = v + '%'; });

  // fan drawing (same as the prototype, small size only)
  const campo = (w, h, a0, cinza) => {
    const cx = w / 2, cy = h - 16, R = Math.min(h - 28, w / 2 - 24), a = 12 + a0 * .68;
    const p = (ang, r) => { const t = ang * Math.PI / 180; return [cx + Math.sin(t) * r, cy - Math.cos(t) * r].map((n) => n.toFixed(1)); };
    const arco = (r, cls) => { const [x1, y1] = p(-a, r), [x2, y2] = p(a, r); return `<path class="${cls}" d="M${x1} ${y1} A${r} ${r} 0 0 1 ${x2} ${y2}"/>`; };
    const [lx, ly] = p(-a, R), [rx, ry] = p(a, R), N = 7, dur = 2.6;
    const parts = Array.from({ length: N }, (_, i) => { const t = -a + 2 * a * (i + .5) / N, [x0, y0] = p(t, R * .22), [x1, y1] = p(t, R * .98), b = (i * 7 % N) / N * dur;
      return `<circle r="2" fill="#ffb340" opacity="0"><animate attributeName="cx" values="${x0};${x1}" dur="${dur}s" begin="${b}s" repeatCount="indefinite"/>
        <animate attributeName="cy" values="${y0};${y1}" dur="${dur}s" begin="${b}s" repeatCount="indefinite"/><animate attributeName="opacity" values="0;.9;0" dur="${dur}s" begin="${b}s" repeatCount="indefinite"/></circle>`; }).join('');
    const [vx, vy] = p(0, R * .58), [gx, gy] = p(0, R * .3), hr = 10;
    const fan = `<defs><radialGradient id="es-g" cx="${cx}" cy="${cy}" r="${R}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#ff9f0a" stop-opacity=".42"/><stop offset="1" stop-color="#ff9f0a" stop-opacity=".04"/></radialGradient></defs>
      <path d="M${cx} ${cy} L${lx} ${ly} A${R} ${R} 0 0 1 ${rx} ${ry} Z" fill="url(#es-g)"/>${arco(R * .45, 'es-anel')}${arco(R * .75, 'es-anel')}${arco(R, 'es-borda')}${parts}
      <ellipse cx="${gx}" cy="${gy}" rx="${R * .1}" ry="${R * .06}" fill="#ff9f0a" opacity=".35"><animate attributeName="opacity" values=".2;.45;.2" dur="1.2s" repeatCount="indefinite"/></ellipse>
      <circle cx="${vx}" cy="${vy}" r="4" fill="#fff6e0" style="filter:drop-shadow(0 0 6px #ffb340)"/>`;
    const cab = `<circle cx="${cx}" cy="${cy}" r="${hr}" fill="#3a3a3c"/><path d="M${cx - hr * .35} ${cy - hr} L${cx} ${cy - hr * 1.45} L${cx + hr * .35} ${cy - hr}" fill="#3a3a3c"/>
      <rect x="${cx - hr * 1.18}" y="${cy - hr * .35}" width="${hr * .3}" height="${hr * .7}" rx="${hr * .12}" fill="#48484a"/><rect x="${cx + hr * .88}" y="${cy - hr * .35}" width="${hr * .3}" height="${hr * .7}" rx="${hr * .12}" fill="#48484a"/>`;
    return `<g class="es-gr ${cinza ? 'cinza' : ''}">${fan}</g>${cab}`;
  };
  const icone = (g, a) => `<svg class="pr-ic" viewBox="0 0 24 24" data-g="${g}" data-a="${a}" aria-hidden="true">
    <path class="ao l" d="M3.8 5Q.6 12 3.8 19"/><path class="ai l" d="M7 7Q4 12 7 17"/><path class="ai r" d="M17 7Q20 12 17 17"/><path class="ao r" d="M20.2 5Q23.4 12 20.2 19"/>
    <path class="gl g-auto" d="M12 8.4Q12.5 11.5 15.6 12Q12.5 12.5 12 15.6Q11.5 12.5 8.4 12Q11.5 11.5 12 8.4Z"/>
    <g class="gl g-jogo"><circle cx="12" cy="12" r="2.3"/><path d="M12 8.6v1M12 14.4v1M8.6 12h1M14.4 12h1"/></g>
    <g class="gl g-musica"><path d="M13.6 8.4v5.8M13.6 8.4c.9.4 1.9 1.1 1.9 2.4"/><ellipse cx="12" cy="14.3" rx="1.7" ry="1.35"/></g>
    <rect class="gl g-filme" x="8.4" y="9.6" width="7.2" height="4.8" rx="1"/></svg>`;
  const SEL = '<svg class="sel" viewBox="0 0 16 16"><path d="M5 6.5 8 4l3 2.5M5 9.5 8 12l3-2.5"/></svg>';
  const OK = '<svg class="ok" viewBox="0 0 14 14"><path d="M2.5 7.5 5.5 10.5 11.5 3.5"/></svg>';
  // leveler glyph = 4 bars; off: uneven heights, on: all pulled to the same line (morph = the state) + gold ceiling line
  const NG = () => `<svg class="nv-gl ${niv() ? 'on' : ''}" viewBox="0 0 16 16" aria-hidden="true"><line class="b1" x1="3" y1="3" x2="3" y2="13"/><line class="b2" x1="6.33" y1="3" x2="6.33" y2="13"/>`
    + `<line class="b3" x1="9.67" y1="3" x2="9.67" y2="13"/><line class="b4" x1="13" y1="3" x2="13" y2="13"/><line class="teto" x1="1" y1="5.5" x2="15" y2="5.5"/></svg>`;
  const NDICA = 'Nivelar volume|Sons baixos sobem e altos descem devagar: nada estoura, nada some. Bom pra filme de madrugada · Clique: liga ou desliga';
  const nivChip = () => `<button class="nv-bt nv-b ${niv() ? 'on' : ''}" data-nv aria-pressed="${niv()}" data-dica="${NDICA}">${NG()}<span>${niv() ? 'Volume nivelado' : 'Volume livre'}</span></button>`;
  // ceiling arc over the fan (same geometry as campo small: 328x104)
  const teto = () => { const cx = 164, cy = 88, R = 76 * .86, a = (12 + abre * .68) * Math.PI / 180;
    const x1 = cx - Math.sin(a) * R, y1 = cy - Math.cos(a) * R, x2 = cx + Math.sin(a) * R, l = Math.ceil(2 * a * R) + 2;
    return `<path class="nv-teto ${niv() && !parado() ? 'on' : ''} ${parado() ? 'cinza' : ''}" style="--l:${l}" d="M${x1.toFixed(1)} ${y1.toFixed(1)} A${R} ${R} 0 0 1 ${x2.toFixed(1)} ${y1.toFixed(1)}"/>`; };
  const txt2 = () => (!on() ? 'O som passa direto' : pausa() ? 'Pausado pelo Som 7.1' : DIZ[modo()]);

  document.head.insertAdjacentHTML('beforeend', `<style>
.t-inicio [data-card=efeitos]{cursor:default}
.t-inicio [data-card=efeitos]:hover{transform:none;box-shadow:none;background:var(--bg2)}
.t-inicio [data-card=efeitos].off:hover{background:rgba(44,44,46,.35)}
.t-inicio [data-card=efeitos] .vis{height:72px;margin:10px 0 8px}
.es-int{display:flex;align-items:center;gap:10px;height:28px;margin-top:6px;font:13px var(--fonte);color:var(--fg2)}.es-int .c-slider{flex:1;margin:0}.es-int .vl{min-width:36px;text-align:right}
.t-inicio [data-card=efeitos] .res2{transition:opacity .15s}.t-inicio [data-card=efeitos] .res2.troca{opacity:0}
.es-anel{fill:none;stroke:rgba(255,159,10,.28);stroke-dasharray:3 6}.es-borda{fill:none;stroke:rgba(255,159,10,.7);stroke-width:1.5}
.es-gr{transition:opacity .3s,filter .3s;transform-origin:50% 100%;animation:es-gr .6s var(--curva) both}.es-gr.cinza{opacity:.3;filter:grayscale(1)}
@keyframes es-gr{from{transform:scaleX(.2);opacity:0}}
.es-sem-anim .es-gr{animation:none}
.pr-ic{fill:none;stroke:currentColor;stroke-width:1.6;stroke-linecap:round;stroke-linejoin:round;overflow:visible}
.pr-ic .gl{opacity:0;transform:scale(.4) rotate(-30deg);transform-box:fill-box;transform-origin:center;transition:opacity .22s,transform .4s var(--curva)}
.pr-ic[data-g=auto] .g-auto,.pr-ic[data-g=jogo] .g-jogo,.pr-ic[data-g=musica] .g-musica,.pr-ic[data-g=filme] .g-filme{opacity:1;transform:none}
.pr-ic .ai,.pr-ic .ao{transform-box:fill-box;transform-origin:center;transition:transform .45s var(--curva),opacity .3s}
.pr-ic[data-a=jogo] .ai{transform:scaleY(.62)}
.pr-ic[data-a=jogo] .ao.l{opacity:0;transform:translateX(2px) scaleY(.5)}.pr-ic[data-a=jogo] .ao.r{opacity:0;transform:translateX(-2px) scaleY(.5)}
.pr-ic[data-a=musica] .ao.l{opacity:.5;transform:translateX(1px) scaleY(.78)}.pr-ic[data-a=musica] .ao.r{opacity:.5;transform:translateX(-1px) scaleY(.78)}
.pr-ic[data-a=filme] .ai.l{transform:translateX(-.4px)}.pr-ic[data-a=filme] .ai.r{transform:translateX(.4px)}
.es-bt{align-self:flex-start;height:32px;min-width:152px;display:inline-flex;align-items:center;gap:8px;padding:0 8px 0 10px;border:0;border-radius:8px;background:var(--controle);color:var(--fg);
  font:13px var(--fonte);cursor:pointer;transition:background .2s;flex:none}
.es-bt:hover,.es-bt.aberto{background:var(--controle-forte)}.es-bt:focus-visible{outline:2px solid var(--fg3);outline-offset:2px}
.es-bt .pr-ic{width:16px;height:16px;color:var(--fg2);flex:none}
.es-bt span{flex:1;text-align:left;white-space:nowrap}.es-bt span.entra{animation:es-entra .3s var(--curva)}
.es-bt .sel{width:16px;height:16px;fill:none;stroke:var(--fg2);stroke-width:1.6;stroke-linecap:round;stroke-linejoin:round;flex:none}
.t-inicio [data-card=efeitos].off .es-bt span{color:var(--fg2)}
@keyframes es-entra{from{opacity:0;transform:translateY(4px)}}
/* leveler (05/11, variant B) */
.es-linha{display:flex;gap:8px;align-items:center;align-self:stretch}
.nv-gl{fill:none;stroke:currentColor;stroke-width:1.6;stroke-linecap:round;overflow:visible;flex:none}
.nv-gl line{transition:transform .45s var(--curva);transform-box:fill-box;transform-origin:50% 100%}
.nv-gl .b1{transform:scaleY(.5)}.nv-gl .b2{transform:scaleY(1)}.nv-gl .b3{transform:scaleY(.3)}.nv-gl .b4{transform:scaleY(.75)}
.nv-gl.on line{transform:scaleY(.6)}
.nv-gl .teto{transform:none;stroke-width:1.2;opacity:0;transition:opacity .3s}.nv-gl.on .teto{opacity:.9;transform:none}
.nv-bt{height:32px;border:0;border-radius:8px;background:var(--controle);color:var(--fg2);font:13px var(--fonte);cursor:pointer;display:inline-flex;align-items:center;gap:8px;
  transition:background .2s,color .2s;flex:none}
.nv-bt:hover{background:var(--controle-forte)}.nv-bt:focus-visible{outline:2px solid var(--fg3);outline-offset:2px}
.nv-bt.on{background:rgba(255,159,10,.16);color:var(--som)}.nv-bt.on:hover{background:rgba(255,159,10,.24)}
.nv-bt .nv-gl{width:16px;height:16px}
.nv-b{flex:1;min-width:0;padding:0 12px 0 10px}.nv-b span{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.nv-b span.entra{animation:es-entra .3s var(--curva)}
.t-inicio [data-card=efeitos].off .nv-bt.on{background:var(--controle);color:var(--fg2)}
.nv-teto{fill:none;stroke:#ffd27a;stroke-width:1.5;stroke-linecap:round;filter:drop-shadow(0 0 4px #ffb340);stroke-dasharray:var(--l);stroke-dashoffset:var(--l);transition:stroke-dashoffset .5s var(--curva),opacity .3s}
.nv-teto.on{stroke-dashoffset:0}.nv-teto.cinza{opacity:.3;filter:grayscale(1)}
.es-fundo{position:fixed;inset:0;z-index:89}
.es-menu{position:fixed;z-index:90;box-sizing:border-box;padding:6px;border-radius:10px;background:rgba(44,44,46,.97);backdrop-filter:blur(20px);
  box-shadow:0 0 0 .5px rgba(255,255,255,.12),0 14px 40px rgba(0,0,0,.6);outline:none;animation:es-abre .22s var(--curva) both}
.es-menu.sai{animation:es-fecha .12s ease-in both}
@keyframes es-abre{from{opacity:0;transform:scaleY(.3)}}
@keyframes es-fecha{to{opacity:0}}
.es-menu div{height:36px;display:flex;align-items:center;gap:8px;padding:0 12px 0 10px;border-radius:6px;font:13px var(--fonte);color:var(--fg);cursor:default;white-space:nowrap}
.es-menu div .ok{width:14px;height:14px;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round;opacity:0;flex:none}
.es-menu div.sel .ok{opacity:1}
.es-menu div .pr-ic{width:16px;height:16px;color:var(--fg2);flex:none}
.es-menu div.at{background:var(--som);color:#1c1c1e}.es-menu div.at .pr-ic{color:rgba(28,28,30,.7)}
</style>`);

  const card = () => document.querySelector('.t-inicio [data-card=efeitos]');
  // first draw keeps its entrance; Início redraws later (engine events) must not replay it
  let ja = false;
  const vis = () => `<svg viewBox="0 0 328 104" style="width:100%;height:100%">${campo(328, 104, abre, parado())}${teto()}</svg>`;
  const desenha = () => { const v = card()?.querySelector('.vis'); if (v) v.innerHTML = vis(); };

  // whole card for inicio.js no(): left, width, entrance delay
  const html = (left, width, delay) => { exclui(); const o = on(), sem = App.cabo.semSom();
    if (!ja) setTimeout(() => { ja = true; document.querySelectorAll('[data-card=efeitos] .vis').forEach((x) => x.classList.add('es-sem-anim')); }, 650);
    return `<div class="no mod sobe ${o ? '' : 'off'}${sem ? ' sem-cabo' : ''}" tabindex="0" data-card="efeitos" style="left:${left}px;width:${width}px;animation-delay:${delay}s"
      data-dica="${sem ? `Estéreo+|${App.cabo.motivo()}: o som do PC só passa por aqui com um cabo virtual.` : 'Estéreo+|Deixa o som bem mais largo, de um lado do fone ao outro, sem eco nem sala. Escolha o modo no botão · Chave: liga ou desliga|Desligado, o som passa direto por aqui'}">
      <div class="t">${icone(modo(), efetivo())}<b>Estéreo+</b><button class="chave ${o ? 'on' : ''}" data-chave="efeitos" aria-label="Estéreo+: ${o ? 'ligado' : 'desligado'}"></button></div>
      <div class="vis${ja ? ' es-sem-anim' : ''}">${vis()}</div>
      <div class="es-linha"><button class="es-bt" aria-haspopup="listbox" aria-expanded="false" aria-label="Modo do Estéreo+">${icone(modo(), efetivo())}<span>${NOME[modo()]}</span>${SEL}</button>${nivChip()}</div>
      ${intHtml()}
      <div class="res2">${txt2()}</div></div>`; };

  let tw = 0;
  const vai = (de, para, cb, ms) => { cancelAnimationFrame(tw); const t0 = performance.now();
    const f = (t) => { const k = Math.min(1, (t - t0) / ms), e = 1 - Math.pow(1 - k, 3); cb(de + (para - de) * e); if (k < 1) tw = requestAnimationFrame(f); }; tw = requestAnimationFrame(f); };
  const poe2 = (c) => { const r = c.querySelector('.res2'), t = txt2(); if (r.textContent === t) return; r.classList.add('troca'); setTimeout(() => { r.textContent = t; r.classList.remove('troca'); }, 150); };
  // in place: icon morphs, fan opens/closes to the new mode, texts fade
  const aplica = () => { const c = card(); if (!c) { abre = ABRE[efetivo()]; return; }
    c.querySelectorAll('.pr-ic').forEach((s) => { s.dataset.g = modo(); s.dataset.a = efetivo(); }); poe2(c);
    c.querySelector('.es-gr')?.classList.toggle('cinza', parado());
    const tt = c.querySelector('.nv-teto'); if (tt) { tt.classList.toggle('cinza', parado()); tt.classList.toggle('on', niv() && !parado()); }
    intPinta();
    const alvo = ABRE[efetivo()]; if (alvo !== abre) vai(abre, alvo, (x) => { abre = x; desenha(); }, 520); };
  const escolhe = (m) => { if (m === modo()) return; E().modo = m; const b = card()?.querySelector('.es-bt span');
    if (b) { b.textContent = NOME[m]; b.classList.remove('entra'); void b.offsetWidth; b.classList.add('entra'); } aplica(); };
  // leveler toggled in place: glyph bars morph, label fades, the ceiling draws itself
  const nivela = () => { const e = E(); e.niv = !niv(); const c = card(); if (!c) return;
    c.querySelector('.nv-gl')?.classList.toggle('on', niv());
    const b = c.querySelector('[data-nv]'); if (b) { b.classList.toggle('on', niv()); b.setAttribute('aria-pressed', niv());
      const sp = b.querySelector('span'); if (sp) { sp.textContent = niv() ? 'Volume nivelado' : 'Volume livre'; sp.classList.remove('entra'); void sp.offsetWidth; sp.classList.add('entra'); } }
    c.querySelector('.nv-teto')?.classList.toggle('on', niv() && !parado()); };
  // switch toggled in place (animates; no full Início redraw)
  // Estéreo+ and Som 7.1 never on together (04/11): turning one on turns the other off; both saved on -> 7.1 wins
  const exclui = () => { S.ligado = S.ligado || {}; if (Efx.s71() && S.ligado.efeitos !== false) S.ligado.efeitos = false; };
  const pinta = () => { const c = card(), o = on(); if (!c) return;
    c.classList.toggle('off', !o); const k = c.querySelector('[data-chave]'); k.classList.toggle('on', o); k.setAttribute('aria-label', `Estéreo+: ${o ? 'ligado' : 'desligado'}`); aplica(); };
  const alterna = () => { S.ligado = S.ligado || {}; S.ligado.efeitos = !on();
    if (on() && pausa()) { const b = document.querySelector('[data-dock=s71]'); if (b) b.click(); else { S.ligado.som71 = false; dispatchEvent(new CustomEvent('fah:s71liga', { detail: false })); } }
    pinta(); };
  addEventListener('fah:s71liga', (e) => { if (e.detail && on()) { S.ligado.efeitos = false; pinta(); } else aplica(); });

  // menu opens FROM the chosen mode: chosen item sits over the button, list grows up and down
  let menu = null, fundo = null, at = 0;
  const marca = (i) => { at = (i + MODOS.length) % MODOS.length; menu.querySelectorAll('div').forEach((d, j) => d.classList.toggle('at', j === at)); };
  function abrir() { const b = card()?.querySelector('.es-bt'); if (menu || !b) return; const r = b.getBoundingClientRect(), idx = MODOS.findIndex((m) => m[0] === modo());
    b.classList.add('aberto'); b.setAttribute('aria-expanded', 'true');
    fundo = document.createElement('div'); fundo.className = 'es-fundo'; fundo.onpointerdown = (e) => { e.preventDefault(); fechar(); }; document.body.appendChild(fundo);
    menu = document.createElement('div'); menu.className = 'es-menu'; menu.tabIndex = -1; menu.setAttribute('role', 'listbox');
    menu.innerHTML = MODOS.map(([id, n], j) => `<div role="option" data-m="${id}" class="${j === idx ? 'sel' : ''}" aria-selected="${j === idx}">${OK}${icone(id, id === 'auto' ? 'musica' : id)}${n}</div>`).join('');
    document.body.appendChild(menu);
    const W = Math.max(r.width + 40, menu.offsetWidth), H = menu.offsetHeight, y0 = 6 + idx * 36 + 18;
    let top = r.top + r.height / 2 - y0, left = r.left - 28;
    top = Math.max(8, Math.min(innerHeight - 8 - H, top)); left = Math.max(8, Math.min(innerWidth - 8 - W, left));
    Object.assign(menu.style, { top: top + 'px', left: left + 'px', width: W + 'px', transformOrigin: `50% ${r.top + r.height / 2 - top}px` });
    marca(idx); menu.focus();
    menu.onpointermove = (e) => { const d = e.target.closest('[data-m]'); if (d) marca([...menu.children].indexOf(d)); };
    menu.onclick = (e) => { const d = e.target.closest('[data-m]'); if (d) { escolhe(d.dataset.m); fechar(); } };
    menu.onkeydown = (e) => { const k = e.key;
      if (k === 'ArrowDown' || k === 'ArrowUp') { e.preventDefault(); marca(at + (k === 'ArrowDown' ? 1 : -1)); }
      else if (k === 'Home' || k === 'End') { e.preventDefault(); marca(k === 'Home' ? 0 : MODOS.length - 1); }
      else if (k === 'Enter' || k === ' ') { e.preventDefault(); escolhe(MODOS[at][0]); fechar(); }
      else if (k === 'Escape' || k === 'Tab') { e.preventDefault(); fechar(); }
      e.stopPropagation(); }; }
  function fechar() { if (!menu) return; const m = menu; menu = null; fundo.remove(); m.classList.add('sai'); setTimeout(() => m.remove(), 120);
    const b = card()?.querySelector('.es-bt'); if (b) { b.classList.remove('aberto'); b.setAttribute('aria-expanded', 'false'); b.focus(); } }
  addEventListener('resize', fechar);
  addEventListener('fah:estauto', () => { if (modo() === 'auto') aplica(); });

  // inicio.js hands card clicks/keys here: true = handled (card itself opens nothing)
  const clique = (ev) => { if (ev.target.closest('[data-chave]')) alterna(); else if (ev.target.closest('[data-nv]')) nivela(); else if (ev.target.closest('.es-bt')) abrir(); return true; };
  const tecla = (ev) => { if (ev.target.closest('[data-nv]') && (ev.key === 'Enter' || ev.key === ' ')) { ev.preventDefault(); nivela(); return true; }
    const noBt = ev.target.closest('.es-bt');
    if (noBt && ['Enter', ' ', 'ArrowUp', 'ArrowDown'].includes(ev.key)) { ev.preventDefault(); abrir(); return true; }
    if (ev.key === 'Enter') return true;
    if (ev.key === ' ') { ev.preventDefault(); alterna(); return true; }
    return false; };
  return { html, clique, tecla, fechar, aplica, efetivo, NOME };
})();
