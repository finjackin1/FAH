// Som 3D (08/11, DEV ONLY until ready): overlay opened from the dock. A head, the current mode's speakers and ONE test source; the user IS that source and
// flies it around ("like creative mode": click the scene, W A S D + mouse, Space up, Shift down, Ctrl fast, V = view from behind);
// the engine renders it live into the headphones (fonte\Modulos\Espacial\Fonte3D.cs, state key "f3d", sent by ponte.js).
// Sound only while the FAH engine is on. Closing the overlay stops the sound. File picker = FAH.exe (msg 'f3darquivo' -> event 'fah:f3darq').
// World: x = right, y = up, z = front, meters, head at the origin. az 0 = front, + = right (same as S71.ANG); el + = up.
(() => {
  if (!window.FAH_DEV) return;                         // not ready: dev mode only (flag usuario\config\dev), hidden from users
  const E = App.estado;
  const PAD = { tocar: false, fonte: 'ruido', arq: '', nome: '', az: 30, el: 0, dist: 1.5, pos: null, vista: '1p', modo: 'direto', cx: '', caixas: [], vol: 50, sala: true, silenciar: false };
  const F = (E.f3d = Object.assign({}, PAD, E.f3d && typeof E.f3d === 'object' ? E.f3d : {}));
  F.tocar = false;                                       // never plays by itself after a restart

  // ---- speakers of each choice (same lists the engine gets) ----
  const N71 = [['FL', 'Frente esq.'], ['FR', 'Frente dir.'], ['C', 'Centro'], ['RL', 'Trás esq.'], ['RR', 'Trás dir.'], ['SL', 'Lado esq.'], ['SR', 'Lado dir.']];
  const EST = [-30, 30, 0, -60, -45, -15, 15, 45, 60];
  const cxPadrao = () => (E.ligado?.som71 !== false ? '71' : E.ligado?.efeitos !== false ? 'est' : 'fone');
  const lista = () => (F.cx === '71' ? N71.map(([k, n]) => ({ a: S71.S.ang[k] ?? S71.ANG[k], n }))
    : F.cx === 'est' ? EST.map((a) => ({ a, n: Math.abs(a) + '°' })) : []);
  const aplicaCx = () => { F.caixas = lista().map((c) => c.a); };

  // constant-power pan between the 2 neighbour speakers (port of Fonte3D.Pan: speakers light up like the engine plays them)
  const NA = (x) => ((((x + 180) % 360) + 360) % 360) - 180;
  const pan = (ang, az) => { const g = ang.map(() => 0); if (!ang.length) return g;
    const ord = ang.map((_, i) => i).sort((i, j) => NA(ang[i]) - NA(ang[j])), a = NA(az);
    for (let k = 0; k < ord.length; k++) { const i = ord[k], j = ord[(k + 1) % ord.length], ai = NA(ang[i]);
      const gap = (NA(ang[j]) - ai + 360) % 360, off = (a - ai + 360) % 360;
      if (gap < 1e-6 || off > gap) continue;
      if (gap > 180) { if (off < gap - off) g[i] = 1; else g[j] = 1; return g; }
      const t = off / gap * Math.PI / 2; g[i] = Math.cos(t); g[j] = Math.sin(t); return g; }
    g[ord[0]] = 1; return g; };

  const descreve = () => { const a = NA(F.az), e = F.el, d = UI.num(F.dist, 1) + ' m';
    if (e >= 87) return 'Bem em cima · ' + d;
    if (e <= -38 && Math.abs(a) < 3) return 'Embaixo, à frente · ' + d;
    const h = Math.abs(a) < 3 ? 'À frente' : Math.abs(a) > 177 ? 'Atrás' : `${Math.round(Math.abs(a))}° à ${a > 0 ? 'direita' : 'esquerda'}`;
    const v = Math.abs(e) < 3 ? '' : ` · ${Math.round(Math.abs(e))}° ${e > 0 ? 'acima' : 'abaixo'}`;
    return h + v + ' · ' + d; };

  UI.escalas.e3dDist = { de: (p) => 0.5 + 9.5 * p / 100, para: (v) => (v - 0.5) / 9.5 * 100 };
  UI.icones.e3d = '<circle cx="12" cy="12" r="3.4"/><path d="M3.2 14.2c-.9-2.4 3-5.6 8.8-7.2s10.9-1 11.8 1.4"/><path d="M20.8 9.8c.9 2.4-3 5.6-8.8 7.2S1.1 18 .2 15.6" opacity=".45"/><circle cx="19.6" cy="8.4" r="2" fill="currentColor" stroke="none"/>';

  document.head.insertAdjacentHTML('beforeend', `<style>
  .e3d-veu{position:fixed;inset:40px 0 0;z-index:45;background:rgba(0,0,0,.5);backdrop-filter:blur(6px);opacity:0;pointer-events:none;transition:opacity .35s}
  .e3d-veu.on{opacity:1;pointer-events:auto}
  .e3d{--ac:var(--som);position:fixed;inset:40px 0 84px;z-index:46;padding:12px 24px 0;opacity:0;transform:translateY(10px) scale(.985);pointer-events:none;transition:opacity .35s,transform .45s var(--curva)}
  .e3d.on{opacity:1;transform:none;pointer-events:auto}
  .e3d .cx3{height:100%;display:flex;background:var(--bg);border:var(--borda-vidro);border-radius:16px;overflow:hidden;box-shadow:0 20px 60px rgba(0,0,0,.55)}
  .e3d .cena{flex:1;min-width:0;position:relative;overflow:hidden}
  .e3d canvas{position:absolute;inset:0;width:100%;height:100%;display:block;cursor:pointer;touch-action:none;outline:0}
  .e3d canvas.arrasta{cursor:grabbing}
  .e3d .leitura{position:absolute;left:18px;top:14px;font-size:15px;font-weight:600;color:var(--fg);pointer-events:none;font-variant-numeric:tabular-nums;text-shadow:0 1px 8px rgba(0,0,0,.8)}
  .e3d .desl{position:absolute;left:50%;top:14px;transform:translateX(-50%);background:#3a1d1d;color:#ffb4a8;padding:6px 14px;border-radius:10px;font-size:12px;white-space:nowrap;transition:opacity .3s}
  .e3d .desl.some{opacity:0;pointer-events:none}
  .e3d .ajuda{position:absolute;right:16px;bottom:14px;font-size:12px;line-height:1.6;color:var(--fg3);pointer-events:none;text-align:right;text-shadow:0 1px 8px rgba(0,0,0,.8);animation:entra .35s var(--curva)}
  .e3d .ajuda b{color:var(--fg2);font-weight:600}
  .e3d .atalhos{position:absolute;left:14px;bottom:12px;display:flex;gap:6px;flex-wrap:wrap;max-width:calc(100% - 28px)}
  .e3d .ch{border:0;height:28px;padding:0 12px;border-radius:14px;background:var(--controle);color:var(--fg);font:500 12px var(--fonte);cursor:pointer;backdrop-filter:blur(8px);transition:background .2s,transform .1s,color .2s,box-shadow .2s}
  .e3d .ch:hover{background:var(--controle-forte)}.e3d .ch:active{transform:scale(.95)}
  .e3d .ch.on{background:var(--ac);color:#000;box-shadow:0 0 14px color-mix(in srgb,var(--ac) 55%,transparent)}
  .e3d .ch.sep{margin-left:8px}
  .e3d .lado{width:300px;flex:none;background:var(--bg2);border-left:.5px solid var(--linha);padding:14px 16px 16px;overflow:auto;display:flex;flex-direction:column;gap:10px}
  .e3d .lado>*{animation:entra .45s var(--curva) backwards}
  .e3d header{display:flex;align-items:center}
  .e3d h2{margin:0;flex:1;font-size:20px;font-weight:700;letter-spacing:-.3px}
  .e3d .x{width:30px;height:30px;border:0;border-radius:50%;background:none;color:var(--fg2);display:grid;place-items:center;cursor:pointer;transition:background .15s,color .15s}
  .e3d .x:hover{background:var(--controle);color:var(--fg)}.e3d .x svg{width:16px;height:16px}
  .e3d .play{height:42px;border:0;border-radius:12px;background:var(--controle);color:var(--fg);font:600 14px var(--fonte);display:flex;align-items:center;justify-content:center;gap:8px;cursor:pointer;transition:background .25s,color .25s,box-shadow .25s,transform .1s}
  .e3d .play:hover{background:var(--controle-forte)}.e3d .play:active{transform:scale(.98)}
  .e3d .play svg{width:16px;height:16px}
  .e3d .play.on{background:var(--ac);color:#000;box-shadow:0 0 22px color-mix(in srgb,var(--ac) 45%,transparent)}
  .e3d .g{background:var(--bg3);border-radius:12px;padding:0 12px}
  .e3d .ln{min-height:40px;display:flex;align-items:center;gap:10px;font-size:13px;position:relative;transition:opacity .25s}
  .e3d .ln+.ln::before,.e3d .arq+.ln::before{content:"";position:absolute;left:0;right:0;top:0;height:.5px;background:var(--linha)}
  .e3d .ln>span:first-child{flex:1;white-space:nowrap}
  .e3d .ln.col{flex-direction:column;align-items:stretch;gap:6px;padding:8px 0}
  .e3d .ln.off{opacity:.35;pointer-events:none}
  .e3d .ln .c-slider{margin:0 11px;flex:1.4}
  .e3d .vl{width:46px;text-align:right;font-size:12px;color:var(--fg2);font-variant-numeric:tabular-nums}
  .e3d .c-segmento button{padding:4px 12px}
  .e3d .ln.col .c-segmento button{flex:1;padding:4px 6px}
  .e3d .arq{display:flex;align-items:center;gap:8px;padding:0 0 10px;font-size:12px;color:var(--fg2);overflow:hidden;max-height:40px;transition:max-height .3s var(--curva),padding .3s,opacity .3s}
  .e3d .arq.some{max-height:0;padding:0;opacity:0}
  .e3d .arq .nm{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
  .e3d .bt{border:0;height:26px;padding:0 10px;border-radius:7px;background:var(--controle);color:var(--fg);font:500 12px var(--fonte);cursor:pointer;flex:none;transition:background .2s}
  .e3d .bt:hover{background:var(--controle-forte)}
  .e3d button:focus-visible,.e3d canvas:focus-visible{outline:2px solid var(--fg3);outline-offset:2px}
  @media (prefers-reduced-motion:reduce){.e3d,.e3d-veu{transition-duration:.01s}}
  </style>`);

  const ICO = { play: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M7 4.5v15l12.5-7.5z"/></svg>', stop: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>',
    x: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>' };
  const PRESETS = [['Frente', 0, 0], ['Trás', 180, 0], ['Esquerda', -90, 0], ['Direita', 90, 0], ['Cima', 0, 90], ['Baixo', 0, -40]];
  const PARA = { Frente: 'a frente', Trás: 'trás', Esquerda: 'a esquerda', Direita: 'a direita', Cima: 'cima', Baixo: 'baixo' };
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  let raiz, veu, cv, ctx, aberto = false, rodando = false, girar = false, info = {};
  const $ = (s) => raiz.querySelector(s);

  const html = () => `<div class="cx3"><div class="cena"><canvas tabindex="0" aria-label="Cena 3D: clique para voar com W A S D e o mouse; setas mudam a direção, mais e menos a distância"></canvas>
      <div class="leitura"></div><div class="desl some">O FAH está desligado: ligue o som no Início para ouvir</div>
      <div class="ajuda"></div>
      <div class="atalhos">${PRESETS.map(([n, a, e]) => `<button class="ch" data-pre="${a},${e}" data-dica-ja data-dica="${n}|Leva o som para ${PARA[n]}">${n}</button>`).join('')}
        <button class="ch sep" data-girar aria-pressed="false" data-dica-ja data-dica="Girar sozinho|O som dá voltas em você, devagar">Girar sozinho</button>
        <button class="ch" data-vista aria-pressed="false" data-dica-ja data-dica="Vista de trás|Vê a bolinha de fora, por trás dela|Atalho: V">Vista de trás</button></div></div>
    <aside class="lado">
      <header><h2>Som 3D</h2><button class="x" data-fechar aria-label="Fechar" data-dica-ja data-dica="Fechar|Atalho: Esc">${ICO.x}</button></header>
      <button class="play" data-tocar style="animation-delay:.04s" data-dica="Tocar|Toca o som no ponto escolhido, no seu fone|Atalho: espaço"></button>
      <div class="g" style="animation-delay:.08s">
        <div class="ln" data-dica="Som|Ruído: rajadas curtas, boas para achar a direção|Arquivo: uma música sua, repetindo"><span>Som</span>${UI.segmento([['ruido', 'Ruído'], ['arquivo', 'Arquivo']], F.fonte, { f3: 'fonte' })}</div>
        <div class="arq some"><span class="nm"></span><button class="bt" data-arq>Trocar</button></div>
        <div class="ln" data-dica="Caminho|Direto: o som vem exatamente desse ponto|Pelas caixas: passa pelas caixas virtuais, como a música passa no FAH"><span>Caminho</span>${UI.segmento([['direto', 'Direto'], ['caixas', 'Pelas caixas']], F.modo, { f3: 'modo' })}</div>
        <div class="ln col" data-cxl data-dica="Caixas|Quais caixas virtuais o som usa no caminho Pelas caixas|Fone comum: só esquerda e direita, como um fone sem nada"><span>Caixas</span>${UI.segmento([['71', '7.1'], ['est', 'Estéreo+'], ['fone', 'Fone comum']], F.cx, { f3: 'cx' })}</div>
      </div>
      <div class="g" style="animation-delay:.12s">
        <div class="ln" data-dica="Distância|Mais longe fica mais baixo e mais abafado|A roda do mouse na cena também muda"><span>Distância</span>${UI.slider(UI.escalas.e3dDist.para(F.dist), { f3: 'dist', escala: 'e3dDist', padrao: UI.escalas.e3dDist.para(1.5) })}<span class="vl" data-vl="dist"></span></div>
        <div class="ln" data-dica="Volume|Só deste som de teste"><span>Volume</span>${UI.slider(F.vol, { f3: 'vol', padrao: 50 })}<span class="vl" data-vl="vol"></span></div>
      </div>
      <div class="g" style="animation-delay:.16s">
        <div class="ln" data-dica="Ambiente|Um pouco de sala em volta: ajuda a ouvir o som fora da cabeça"><span>Ambiente</span>${UI.switch(F.sala, { f3: 'sala' })}</div>
        <div class="ln" data-dica="Silenciar o resto|Abaixa o outro som do PC enquanto este toca"><span>Silenciar o resto</span>${UI.switch(F.silenciar, { f3: 'silenciar' })}</div>
      </div>
    </aside></div>`;

  // ---- panel state -> screen (never redraws the panel) ----
  const pintaPlay = () => { const b = $('[data-tocar]'); b.classList.toggle('on', F.tocar); b.setAttribute('aria-pressed', F.tocar);
    b.innerHTML = F.tocar ? ICO.stop + 'Tocando' : ICO.play + 'Tocar'; };
  const pintaArq = () => { const a = $('.arq'), nm = a.querySelector('.nm'), b = a.querySelector('[data-arq]');
    a.classList.toggle('some', F.fonte !== 'arquivo');
    const n = info.nome || F.nome || 'Nenhuma música';
    nm.textContent = info.carregando ? 'Abrindo ' + n + '…' : n + (info.segundos ? ' · ' + Math.floor(info.segundos / 60) + ':' + String(Math.round(info.segundos % 60)).padStart(2, '0') : '');
    nm.title = F.arq || ''; b.textContent = F.arq ? 'Trocar' : 'Escolher'; };
  const pintaVl = () => { $('[data-vl=dist]').textContent = UI.num(F.dist, 1) + ' m'; $('[data-vl=vol]').textContent = Math.round(F.vol) + '%'; };
  const pintaCx = () => $('[data-cxl]').classList.toggle('off', F.modo !== 'caixas');
  const pintaSeg = (k, v) => raiz.querySelectorAll(`[data-f3="${k}"]`).forEach((b) => b.classList.toggle('on', b.dataset.v === v));
  const pintaGirar = () => { const b = $('[data-girar]'); b.classList.toggle('on', girar); b.setAttribute('aria-pressed', girar); };
  const pintaVista = () => { const b = $('[data-vista]'), on = F.vista === '3p'; b.classList.toggle('on', on); b.setAttribute('aria-pressed', on); };
  const pintaAjuda = () => { if (!raiz) return; const a = $('.ajuda');
    a.innerHTML = voando ? '<b>W A S D</b> anda · <b>Espaço</b> sobe · <b>Shift</b> desce · <b>Ctrl</b> rápido<br><b>V</b> muda a vista · <b>Esc</b> solta o mouse'
      : '<b>Clique na cena</b> para voar: você é o som<br>Roda do mouse: distância';
    a.style.animation = 'none'; void a.offsetWidth; a.style.animation = ''; };
  const trocaVista = () => { F.vista = F.vista === '3p' ? '1p' : '3p'; pintaVista(); };
  const pintaMotor = () => { if (raiz) $('.desl').classList.toggle('some', !!E.motor?.ligado); };
  const poeDist = (d) => { d = Math.max(RMIN, Math.min(RMAX, d)); const d0 = Math.hypot(...pos) || 1; poePos(pos.map((v) => v * d / d0)); };
  const tocar = (v) => { F.tocar = v; pintaPlay(); };
  const pedirArquivo = () => App.motor.enviar({ tipo: 'f3darquivo' });

  function montar() {
    veu = document.createElement('div'); veu.className = 'e3d-veu';
    raiz = document.createElement('div'); raiz.className = 'e3d'; raiz.setAttribute('role', 'dialog'); raiz.setAttribute('aria-label', 'Som 3D');
    raiz.innerHTML = html(); document.getElementById('app').append(veu, raiz);
    cv = $('canvas'); ctx = cv.getContext('2d');
    veu.addEventListener('click', fechar);
    raiz.addEventListener('c:mudou', (e) => { const { valor, dados } = e.detail, k = dados.f3; if (!k) return;
      if (k === 'fonte') { if (valor === 'arquivo' && !F.arq) { pintaSeg('fonte', F.fonte); pedirArquivo(); return; } F.fonte = valor; pintaArq(); }
      else if (k === 'modo') { F.modo = valor; pintaCx(); }
      else if (k === 'cx') { F.cx = valor; aplicaCx(); }
      else if (k === 'dist') { anim = null; poeDist(UI.escalas.e3dDist.de(valor)); }
      else if (k === 'vol') { F.vol = Math.round(valor); pintaVl(); }
      else if (k === 'sala' || k === 'silenciar') F[k] = !!valor; });
    raiz.addEventListener('click', (e) => { const t = e.target.closest('button'); if (!t) return;
      if (t.dataset.fechar !== undefined) fechar();
      else if (t.dataset.tocar !== undefined) tocar(!F.tocar);
      else if (t.dataset.arq !== undefined) pedirArquivo();
      else if (t.dataset.girar !== undefined) { girar = !girar; anim = null; pintaGirar(); }
      else if (t.dataset.vista !== undefined) trocaVista();
      else if (t.dataset.pre) { const [a, el] = t.dataset.pre.split(',').map(Number); irPara(a, el); } });
    // pointer: click = fly (mouse captured; no pointer lock = hold the button and drag to look); wheel = distance
    cv.addEventListener('pointerdown', (e) => { if (e.button !== 0) return; cv.focus({ preventScroll: true });
      if (semTrava) { voando = true; arr = true; ult = [e.offsetX, e.offsetY]; cv.setPointerCapture(e.pointerId); cv.classList.add('arrasta'); pintaAjuda(); return; }
      if (!travado) { try { const r = cv.requestPointerLock(); if (r && r.catch) r.catch(() => { semTrava = true; voando = true; pintaAjuda(); }); }
        catch { semTrava = true; voando = true; pintaAjuda(); } } });
    cv.addEventListener('pointermove', (e) => {
      let dx = 0, dy = 0;
      if (travado) { dx = e.movementX; dy = e.movementY; } else if (arr) { dx = e.offsetX - ult[0]; dy = e.offsetY - ult[1]; ult = [e.offsetX, e.offsetY]; } else return;
      if (anim) anim = null;
      olhar.yaw = NA(olhar.yaw + dx * 0.12); olhar.pitch = Math.max(-89, Math.min(89, olhar.pitch - dy * 0.12)); });
    const solta = () => { arr = null; cv.classList.remove('arrasta'); };
    cv.addEventListener('pointerup', solta); cv.addEventListener('pointercancel', solta);
    cv.addEventListener('wheel', (e) => { e.preventDefault(); anim = null; poeDist(F.dist + (e.deltaY < 0 ? -0.25 : 0.25)); }, { passive: false });
    document.addEventListener('pointerlockchange', () => { travado = document.pointerLockElement === cv;
      if (travado) { voando = true; if (girar) { girar = false; pintaGirar(); } }
      else if (voando && !semTrava) { voando = false; soltouEm = performance.now(); teclas.clear(); }
      pintaAjuda(); });
    document.addEventListener('pointerlockerror', () => { semTrava = true; voando = true; pintaAjuda(); });
    addEventListener('keyup', (e) => { teclas.delete(e.code); if (aberto && voando) { e.preventDefault(); e.stopPropagation(); } }, true);
    addEventListener('blur', () => teclas.clear());
    new ResizeObserver(tamanho).observe($('.cena'));
    addEventListener('fah:motor', pintaMotor);
    addEventListener('fah:f3darq', (e) => { const d = e.detail || {}; if (!d.arq) return;
      F.arq = d.arq; F.nome = d.nome || ''; F.fonte = 'arquivo'; info = { nome: F.nome, carregando: true }; pintaSeg('fonte', 'arquivo'); pintaArq(); });
    addEventListener('fah:f3dinfo', (e) => { info = e.detail || {}; if (info.erro) { App.toast?.(info.erro, 6000); F.arq = ''; F.nome = ''; F.fonte = 'ruido'; info = {}; pintaSeg('fonte', 'ruido'); }
      if (raiz) pintaArq(); });
    // dock clicks to another screen close the overlay (the "7.1" toggle doesn't: it changes the speakers seen here)
    document.getElementById('dock').addEventListener('click', (e) => { const b = e.target.closest('[data-dock]'); if (aberto && b && b.dataset.dock !== 'e3d' && b.dataset.dock !== 's71') fechar(); }, true);
    addEventListener('fah:s71liga', () => { if (aberto && !escolheuCx) { F.cx = cxPadrao(); aplicaCx(); pintaSeg('cx', F.cx); } });
    raiz.addEventListener('c:mudou', (e) => { if (e.detail.dados.f3 === 'cx') escolheuCx = true; });
    addEventListener('keydown', tecla, true);
  }
  let escolheuCx = false;

  function abrir() {
    if (!raiz) montar();
    if (!escolheuCx || !F.cx) { F.cx = cxPadrao(); }
    aplicaCx(); pintaSeg('cx', F.cx); pintaSeg('fonte', F.fonte); pintaSeg('modo', F.modo);
    if (!pos) { pos = limitar(Array.isArray(F.pos) && F.pos.length === 3 && F.pos.every(Number.isFinite) ? F.pos : esf(F.az, F.el, F.dist)); dePos(); mirar(); }
    if (F.vista !== '3p') F.vista = '1p'; tras = F.vista === '3p' ? TRAS : 0;
    pintaPlay(); pintaArq(); pintaVl(); pintaCx(); pintaGirar(); pintaVista(); pintaAjuda(); pintaMotor();
    raiz.querySelectorAll('.lado>*').forEach((x) => { x.style.animation = 'none'; void x.offsetWidth; x.style.animation = ''; });
    aberto = true; veu.classList.add('on'); raiz.classList.add('on'); App.desenharDock();
    ac = COR(); fam = getComputedStyle(document.body).fontFamily;
    tamanho(); if (!rodando) { rodando = true; tAnt = performance.now(); requestAnimationFrame(quadro); }
    setTimeout(() => aberto && $('[data-tocar]').focus({ preventScroll: true }), 60);
  }
  function fechar() {
    if (!aberto) return; aberto = false; tocar(false); girar = false; anim = null; arr = null;
    if (travado) document.exitPointerLock(); voando = false; teclas.clear(); vel = [0, 0, 0];
    veu.classList.remove('on'); raiz.classList.remove('on'); App.desenharDock();
    document.activeElement?.blur?.();
  }

  // keyboard while open. Flying: W A S D / Space / Shift / Ctrl held (by key position, any layout), V = view, Esc frees the mouse
  // (the browser may eat that Esc: a 2nd Esc, not the same one, closes). Not flying: arrows = direction (Shift = bigger steps),
  // + / - = distance, space = play, V = view, Esc closes.
  const VOO = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'Space', 'ShiftLeft', 'ShiftRight', 'ControlLeft', 'ControlRight']);
  function tecla(e) {
    if (!aberto) return;
    const k = e.key;
    if (k === 'Escape') { e.preventDefault(); e.stopPropagation();
      if (travado) document.exitPointerLock();
      else if (voando) { voando = false; arr = null; teclas.clear(); pintaAjuda(); }
      else if (performance.now() - soltouEm > 250) fechar();
      return; }
    if (voando) {   // everything is swallowed while flying (Ctrl+W, Space on a button...)
      if (VOO.has(e.code)) teclas.add(e.code);
      if (e.code === 'KeyV' && !e.repeat) trocaVista();
      e.preventDefault(); e.stopPropagation(); return; }
    const passo = e.shiftKey ? 15 : 5; let ok = true;
    if (k === 'ArrowLeft' || k === 'ArrowRight') irDireto(NA(F.az + (k === 'ArrowRight' ? passo : -passo)), F.el);
    else if (k === 'ArrowUp' || k === 'ArrowDown') irDireto(F.az, Math.max(-40, Math.min(90, F.el + (k === 'ArrowUp' ? passo : -passo))));
    else if (k === '+' || k === '=' || k === 'PageUp') poeDist(F.dist + 0.25);
    else if (k === '-' || k === 'PageDown') poeDist(F.dist - 0.25);
    else if (k === ' ' && !e.target.closest?.('button')) tocar(!F.tocar);
    else if (k === ' ') return;
    else if (e.code === 'KeyV' && !e.repeat) trocaVista();
    else ok = k === 'c' || k === 'C';   // swallow the Início "hold C to compare" while this is open
    if (ok) { e.preventDefault(); e.stopPropagation(); }
  }

  // ---- the room and the flying source. World: x right, y up, z front, meters, head at the origin (ears at y 0).
  // Room 12 x 12 m, 6 m high, floor 1.2 m under the ears (seated). The engine gets az / el (-40..90, its HRTF range) / dist (0.5..10).
  const LADO = 6, CHAO = -1.2, TETO = CHAO + 6, RMIN = 0.5, RMAX = 10, RBOLA = 0.18, CAB = 0.25, RCX = 2.2, TRAS = 2.4, NEAR = 0.05, D2R = Math.PI / 180;
  let pos = null, vel = [0, 0, 0], olhar = { yaw: 0, pitch: 0 }, tras = 0, travado = false, semTrava = false, voando = false, soltouEm = 0;
  const teclas = new Set();
  const esf = (az, el, d) => { const a = az * D2R, e = el * D2R; return [d * Math.cos(e) * Math.sin(a), d * Math.sin(e), d * Math.cos(e) * Math.cos(a)]; };
  function limitar(p) {
    let [x, y, z] = p;
    x = Math.max(-LADO + RBOLA, Math.min(LADO - RBOLA, x)); z = Math.max(-LADO + RBOLA, Math.min(LADO - RBOLA, z));
    y = Math.max(CHAO + RBOLA, Math.min(TETO - RBOLA, y));
    const d = Math.hypot(x, y, z);
    if (d < RMIN) { if (d < 1e-6) return [0, 0, RMIN]; const k = RMIN / d; x *= k; y *= k; z *= k; }   // can't fly into the head
    return [x, y, z];
  }
  // position -> what the engine and the panel get (rounded: no message while still)
  function dePos() {
    const [x, y, z] = pos, d = Math.hypot(x, y, z);
    F.pos = pos.map((v) => Math.round(v * 1000) / 1000);
    const nd = Math.round(Math.min(RMAX, d) * 100) / 100;
    if (nd !== F.dist) { F.dist = nd; if (raiz) { UI.poe($('[data-f3=dist]'), UI.escalas.e3dDist.para(F.dist)); pintaVl(); } }
    F.el = Math.round(Math.max(-40, Math.min(90, Math.asin(Math.max(-1, Math.min(1, y / d))) / D2R)) * 10) / 10;
    if (Math.hypot(x, z) > 1e-3) F.az = Math.round(Math.atan2(x, z) / D2R * 10) / 10;
  }
  const poePos = (p) => { pos = limitar(p); dePos(); };
  const mirar = () => { const [x, y, z] = pos, d = Math.hypot(x, y, z) || 1;   // look at the head
    olhar.yaw = Math.atan2(-x, -z) / D2R; olhar.pitch = Math.max(-89, Math.min(89, Math.asin(-y / d) / D2R)); };
  const irDireto = (az, el) => { anim = null; poePos(esf(az, el, Math.hypot(...pos))); mirar(); };

  // ---- smooth moves: presets (teleport), auto-rotate ----
  let anim = null, tAnt = 0;
  const irPara = (az, el) => { girar = false; pintaGirar(); vel = [0, 0, 0];
    anim = { a0: F.az, e0: F.el, da: NA(az - F.az), de: el - F.el, d: Math.hypot(...pos), y0: olhar.yaw, p0: olhar.pitch, t0: performance.now() }; };
  const suave = (t) => 1 - Math.pow(1 - Math.min(1, t), 3);

  function mover(dt) {
    const f = (c) => (teclas.has(c) ? 1 : 0);
    const fr = f('KeyW') - f('KeyS'), ld = f('KeyD') - f('KeyA'), vt = f('Space') - Math.max(f('ShiftLeft'), f('ShiftRight'));
    if (fr || ld || vt) { anim = null; if (girar) { girar = false; pintaGirar(); } }
    const y = olhar.yaw * D2R, v = f('ControlLeft') || f('ControlRight') ? 7 : 2.5;
    let w = [Math.sin(y) * fr + Math.cos(y) * ld, vt, Math.cos(y) * fr - Math.sin(y) * ld];
    const l = Math.hypot(...w); if (l > 1) w = w.map((x) => x / l);
    const k = 1 - Math.exp(-dt * 9);
    vel = vel.map((x, i) => x + (w[i] * v - x) * k);
    if (Math.hypot(...vel) < 0.01 && !l) { vel = [0, 0, 0]; return; }
    poePos(pos.map((x, i) => x + vel[i] * dt));
  }

  // ---- camera: first person at the source, or TRAS m behind it (V); perspective with near-plane clipping ----
  let W = 0, H = 0, dpr = 1, foco = 1, cx0 = 0, cy0 = 0, cam = [0, 0, 0], fw = [0, 0, 1], rt = [1, 0, 0], up = [0, 1, 0], arr = null, ult = [0, 0];
  const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]], dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const norm = (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };
  function camera() {
    const y = olhar.yaw * D2R, p = olhar.pitch * D2R;
    fw = [Math.cos(p) * Math.sin(y), Math.sin(p), Math.cos(p) * Math.cos(y)];
    rt = norm(cross([0, 1, 0], fw)); up = cross(fw, rt);
    const h = 0.12 * tras;
    cam = [pos[0] - fw[0] * tras + up[0] * h, pos[1] - fw[1] * tras + up[1] * h, pos[2] - fw[2] * tras + up[2] * h];
  }
  const emCam = (P) => { const v = sub(P, cam); return [dot(v, rt), dot(v, up), dot(v, fw)]; };
  const tela = (c) => [cx0 + c[0] * foco / c[2], cy0 - c[1] * foco / c[2]];
  const proj = (P) => { const c = emCam(P), z = c[2]; if (z < NEAR) return { vis: false, x: 0, y: 0, z, s: 0 };
    const [x, y] = tela(c); return { vis: true, x, y, z, s: foco / z }; };
  const cortar = (a, b) => { const t = (a[2] - NEAR) / (a[2] - b[2]); return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, NEAR]; };
  function traco(pts, fechado) {   // world polyline into the current path, clipped at the near plane
    const cs = pts.map(emCam), n = fechado ? cs.length : cs.length - 1;
    for (let i = 0; i < n; i++) {
      let a = cs[i], b = cs[(i + 1) % cs.length];
      if (a[2] < NEAR && b[2] < NEAR) continue;
      if (a[2] < NEAR) a = cortar(b, a); else if (b[2] < NEAR) b = cortar(a, b);
      const p = tela(a), q = tela(b); ctx.moveTo(p[0], p[1]); ctx.lineTo(q[0], q[1]);
    }
  }
  function tamanho() {
    if (!cv) return; const r = cv.parentNode.getBoundingClientRect(); dpr = devicePixelRatio || 1; W = r.width; H = r.height;
    cv.width = Math.round(W * dpr); cv.height = Math.round(H * dpr);
    foco = (H / 2) / Math.tan(36 * D2R); cx0 = W / 2; cy0 = H / 2;   // 72° vertical view
  }

  // ---- drawing ----
  const COR = () => getComputedStyle(raiz).getPropertyValue('--som').trim() || '#ff9f0a';
  const rgba = (hex, a) => { const h = hex.replace('#', ''), n = parseInt(h.length === 3 ? h.replace(/./g, '$&$&') : h, 16); return `rgba(${n >> 16 & 255},${n >> 8 & 255},${n & 255},${a})`; };
  let ac = '#ff9f0a', fam = 'sans-serif';
  const circ = (c, r, y) => Array.from({ length: 96 }, (_, i) => { const t = i / 96 * 2 * Math.PI; return [c[0] + r * Math.sin(t), y, c[2] + r * Math.cos(t)]; });
  const tinta = (cor, lw, tr) => { ctx.strokeStyle = cor; ctx.lineWidth = lw; ctx.setLineDash(tr || []); ctx.stroke(); ctx.setLineDash([]); };
  const anel = (r, y, cor, lw, tr) => { ctx.beginPath(); traco(circ([0, 0, 0], r, y), true); tinta(cor, lw, tr); };
  const linha = (a, b, cor, lw, tr) => { ctx.beginPath(); traco([a, b]); tinta(cor, lw, tr); };
  const texto = (P, t, cor, tam = 11, peso = 500) => { const p = proj(P); if (!p.vis) return;
    ctx.font = `${peso} ${tam}px ${fam}`; ctx.fillStyle = cor; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(t, p.x, p.y); };

  function quadro(t) {
    if (!aberto) { rodando = false; return; }
    const dt = Math.min(0.1, (t - tAnt) / 1000); tAnt = t;
    if (anim) { const k = suave((t - anim.t0) / 450);
      poePos(esf(NA(anim.a0 + anim.da * k), anim.e0 + anim.de * k, anim.d));
      mirar(); const yA = olhar.yaw, pA = olhar.pitch;   // the look turns to the head along the way
      olhar.yaw = NA(anim.y0 + NA(yA - anim.y0) * k); olhar.pitch = anim.p0 + (pA - anim.p0) * k;
      if (k >= 1) anim = null; }
    else if (girar) { const a = 36 * dt * D2R, [x, y, z] = pos; poePos([x * Math.cos(a) + z * Math.sin(a), y, z * Math.cos(a) - x * Math.sin(a)]); if (!voando) mirar(); }
    if (voando && !anim) mover(dt);
    tras += ((F.vista === '3p' ? TRAS : 0) - tras) * (1 - Math.exp(-dt * 10));
    if (Math.abs(tras - (F.vista === '3p' ? TRAS : 0)) < 1e-3) tras = F.vista === '3p' ? TRAS : 0;
    desenhar(t / 1000);
    requestAnimationFrame(quadro);
  }

  function desenhar(seg) {
    if (W < 20 || H < 20) return;
    camera();
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0); ctx.clearRect(0, 0, W, H);
    const fg2 = 'rgba(235,235,245,.6)', fg3 = 'rgba(235,235,245,.3)';
    const ch = proj([0, 0, 0]);
    const bx = ch.vis ? ch.x : W / 2, by = ch.vis ? ch.y : H / 2;
    const bg = ctx.createRadialGradient(bx, by, 0, bx, by, Math.max(W, H) * 0.7); bg.addColorStop(0, rgba(ac, F.tocar ? 0.09 : 0.05)); bg.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = bg; ctx.fillRect(0, 0, W, H);
    // room: floor grid (1 m), walls' edges, distance rings around the head, directions
    ctx.beginPath();
    for (let i = -LADO + 1; i < LADO; i++) traco([[i, CHAO, -LADO], [i, CHAO, LADO]]), traco([[-LADO, CHAO, i], [LADO, CHAO, i]]);
    tinta('rgba(255,255,255,.045)', 1);
    const cantos = [[-LADO, -LADO], [LADO, -LADO], [LADO, LADO], [-LADO, LADO]];
    ctx.beginPath(); traco(cantos.map(([x, z]) => [x, CHAO, z]), true); tinta('rgba(255,255,255,.16)', 1.2);
    ctx.beginPath(); traco(cantos.map(([x, z]) => [x, TETO, z]), true); cantos.forEach(([x, z]) => traco([[x, CHAO, z], [x, TETO, z]])); tinta('rgba(255,255,255,.07)', 1);
    for (let r = 1; r <= 5; r++) anel(r, CHAO, 'rgba(255,255,255,' + (r === 5 ? 0.1 : 0.06) + ')', 1);
    anel(Math.hypot(pos[0], pos[2]), CHAO, rgba(ac, 0.3), 1.2, [4, 5]);
    linha([0, CHAO, -LADO], [0, CHAO, LADO], 'rgba(255,255,255,.07)', 1); linha([-LADO, CHAO, 0], [LADO, CHAO, 0], 'rgba(255,255,255,.07)', 1);
    texto([0, CHAO, LADO - 0.5], 'Frente', fg2, 12, 600); texto([0, CHAO, -LADO + 0.5], 'Trás', fg3, 12);
    texto([-LADO + 0.7, CHAO, 0], 'Esquerda', fg3, 12); texto([LADO - 0.7, CHAO, 0], 'Direita', fg3, 12);
    [1, 3, 5].forEach((r) => texto([r - 0.12, CHAO, -0.28], r + ' m', 'rgba(235,235,245,.22)', 10));
    // speakers ring at ear height
    const cxs = lista(), caixasOn = F.modo === 'caixas';
    if (cxs.length) anel(RCX, 0, 'rgba(255,255,255,' + (caixasOn ? 0.1 : 0.05) + ')', 1, [2, 6]);
    // source: floor shadow + drop line + line to the head (seen from behind; in first person it is your own eye)
    const P = pos, pf = proj(P), sombra = circ(P, 0.26, CHAO).map(proj);
    if (sombra.every((p) => p.vis)) { ctx.beginPath(); sombra.forEach((p, i) => (i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y))); ctx.closePath(); ctx.fillStyle = rgba(ac, 0.18); ctx.fill(); }
    const fora = Math.min(1, Math.max(0, (tras - 0.4) / 1.2));
    if (fora > 0) { linha(P, [P[0], CHAO, P[2]], rgba(ac, 0.28 * fora), 1, [2, 4]); linha(P, [0, 0, 0], rgba(ac, (F.tocar ? 0.35 : 0.18) * fora), 1, [3, 5]); }
    // depth-sorted things: speakers, head (with ears and nose), source
    const g = caixasOn && cxs.length >= 3 ? pan(cxs.map((c) => c.a), F.az) : cxs.map(() => 0);
    const itens = cxs.map((c, i) => { const a = c.a * D2R, Pc = [RCX * Math.sin(a), 0, RCX * Math.cos(a)]; return { z: proj(Pc).z, f: () => caixa(Pc, c.n, g[i], caixasOn) }; });
    itens.push({ z: ch.z, f: () => cabeca() });
    if (fora > 0 && pf.vis) itens.push({ z: pf.z - 0.01, f: () => { ctx.save(); ctx.globalAlpha = fora; fonte(pf, seg); ctx.restore(); } });
    itens.filter((i) => i.z >= NEAR).sort((a, b) => b.z - a.z).forEach((i) => i.f());
    if (fora < 1) mira(1 - fora);
    if (!ch.vis || ch.x < 0 || ch.x > W || ch.y < 0 || ch.y > H) seta();
    $('.leitura').textContent = descreve();
    if (F.modo === 'caixas' && !cxs.length) texto([0, 0.95, 0], 'Fone comum: só esquerda e direita', fg3, 12);
  }

  // first person: small sight in the middle; head off screen: arrow on the edge pointing to it
  function mira(a) {
    ctx.save(); ctx.globalAlpha = a; ctx.beginPath(); ctx.arc(cx0, cy0, 5, 0, 2 * Math.PI);
    ctx.strokeStyle = voando ? 'rgba(255,255,255,.55)' : 'rgba(255,255,255,.25)'; ctx.lineWidth = 1.5; ctx.stroke(); ctx.restore();
  }
  function seta() {
    const c = emCam([0, 0, 0]); let dx = c[0], dy = -c[1]; if (c[2] < 0) { dx = -dx; dy = -dy; }
    if (Math.hypot(dx, dy) < 1e-6) dy = 1;
    const ang = Math.atan2(dy, dx), m = 34, rx = W / 2 - m, ry = H / 2 - m, k = Math.min(rx / Math.abs(Math.cos(ang) || 1e-9), ry / Math.abs(Math.sin(ang) || 1e-9));
    const x = cx0 + Math.cos(ang) * k, y = cy0 + Math.sin(ang) * k;
    ctx.save(); ctx.translate(x, y); ctx.rotate(ang);
    ctx.beginPath(); ctx.moveTo(11, 0); ctx.lineTo(-7, -8); ctx.lineTo(-3, 0); ctx.lineTo(-7, 8); ctx.closePath();
    ctx.fillStyle = 'rgba(235,235,245,.55)'; ctx.fill(); ctx.restore();
    ctx.font = `500 11px ${fam}`; ctx.fillStyle = 'rgba(235,235,245,.5)'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText('Cabeça', x - Math.cos(ang) * 26, y - Math.sin(ang) * 18);
  }

  function caixa(Pc, nome, ganho, vivo) {
    const p = proj(Pc); if (!p.vis) return; const s = 0.3 * p.s, aceso = vivo ? ganho : 0;
    ctx.save(); ctx.globalAlpha = vivo ? 1 : 0.35;
    if (aceso > 0.02) { ctx.shadowColor = rgba(ac, 0.9); ctx.shadowBlur = 22 * aceso; }
    ctx.beginPath(); ctx.roundRect(p.x - s / 2, p.y - s * 0.6, s, s * 1.2, Math.max(2, s * 0.18));
    ctx.fillStyle = aceso > 0.02 ? rgba(ac, 0.25 + 0.65 * aceso) : '#3a3a3c'; ctx.fill(); ctx.shadowBlur = 0;
    ctx.strokeStyle = aceso > 0.02 ? rgba(ac, 0.9) : 'rgba(255,255,255,.18)'; ctx.lineWidth = 1; ctx.stroke();
    ctx.beginPath(); ctx.arc(p.x, p.y + s * 0.12, s * 0.24, 0, 2 * Math.PI); ctx.strokeStyle = aceso > 0.02 ? 'rgba(0,0,0,.45)' : 'rgba(255,255,255,.22)'; ctx.stroke();
    ctx.restore();
    ctx.save(); ctx.globalAlpha = vivo ? 0.9 : 0.35;
    ctx.font = `500 11px ${fam}`; ctx.textAlign = 'center'; ctx.textBaseline = 'top';
    ctx.fillStyle = aceso > 0.02 ? '#fff' : 'rgba(235,235,245,.6)'; ctx.fillText(nome, p.x, p.y + s * 0.6 + 5); ctx.restore();
  }

  function cabeca() {
    const c = proj([0, 0, 0]); if (!c.vis) return; const r = CAB * c.s, K = CAB;
    const orelha = (p) => { ctx.beginPath(); ctx.ellipse(p.x, p.y, 0.18 * K * p.s, 0.3 * K * p.s, 0, 0, 2 * Math.PI); ctx.fillStyle = '#4a4a4e'; ctx.fill(); };
    const partes = [
      { P: [-K * 1.07, 0, 0], f: orelha }, { P: [K * 1.07, 0, 0], f: orelha },
      { P: [0, -0.04 * K, K * 1.2], f: () => { const a = proj([-0.24 * K, 0.04 * K, 0.9 * K]), b = proj([0.24 * K, 0.04 * K, 0.9 * K]), n = proj([0, -0.07 * K, 1.3 * K]);
        if (!a.vis || !b.vis || !n.vis) return;
        ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(n.x, n.y); ctx.lineTo(b.x, b.y); ctx.closePath(); ctx.fillStyle = '#5a5a5f'; ctx.fill(); } },
    ].map((x) => ({ ...x, p: proj(x.P) })).filter((x) => x.p.vis);
    partes.filter((x) => x.p.z > c.z).forEach((x) => x.f(x.p));
    const gr = ctx.createRadialGradient(c.x - r * 0.35, c.y - r * 0.4, r * 0.1, c.x, c.y, r);
    gr.addColorStop(0, '#5c5c61'); gr.addColorStop(1, '#232325');
    ctx.beginPath(); ctx.arc(c.x, c.y, r, 0, 2 * Math.PI); ctx.fillStyle = gr; ctx.fill(); ctx.strokeStyle = 'rgba(255,255,255,.16)'; ctx.lineWidth = 1; ctx.stroke();
    partes.filter((x) => x.p.z <= c.z).forEach((x) => x.f(x.p));
  }

  function fonte(p, seg) {
    const r = RBOLA * p.s * (F.tocar ? 1 + 0.08 * Math.sin(seg * 7) : 1);
    if (F.tocar) for (let k = 0; k < 3; k++) { const f = (seg * 0.9 + k / 3) % 1;   // sound waves going out
      ctx.beginPath(); ctx.arc(p.x, p.y, r * (1.2 + 3 * f), 0, 2 * Math.PI); ctx.strokeStyle = rgba(ac, 0.5 * (1 - f)); ctx.lineWidth = 1.5; ctx.stroke(); }
    const gl = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, r * 3.2); gl.addColorStop(0, rgba(ac, F.tocar ? 0.55 : 0.35)); gl.addColorStop(1, rgba(ac, 0));
    ctx.fillStyle = gl; ctx.beginPath(); ctx.arc(p.x, p.y, r * 3.2, 0, 2 * Math.PI); ctx.fill();
    const gr = ctx.createRadialGradient(p.x - r * 0.35, p.y - r * 0.35, r * 0.1, p.x, p.y, r); gr.addColorStop(0, '#fff3dc'); gr.addColorStop(0.45, ac); gr.addColorStop(1, rgba(ac, 0.85));
    ctx.beginPath(); ctx.arc(p.x, p.y, r, 0, 2 * Math.PI); ctx.fillStyle = gr; ctx.fill();
  }

  App.dock({ id: 'e3d', icone: 'e3d', dica: 'Som 3D', ordem: 6, aceso: () => aberto, acao: () => (aberto ? fechar() : abrir()) });
})();
