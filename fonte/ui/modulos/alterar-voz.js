// Module: voice changer (Mic).
App.modulo({ id: 'voz', lado: 'mic', ordem: 2, nome: 'Alterar voz', icone: 'voz', desc: 'Muda o tom e o timbre da sua voz.',
  previa: () => `<svg viewBox="0 0 300 100">${[...Array(24)].map((_, i) => { const d = 1 + i % 5 * .2;
    return `<rect x="${i * 12.5 + 2}" y="30" width="8" height="40" rx="3" fill="var(--ac)"><animate attributeName="height" values="10;60;20;45;10" dur="${d}s" repeatCount="indefinite"/><animate attributeName="y" values="45;20;40;27;45" dur="${d}s" repeatCount="indefinite"/></rect>`; }).join('')}</svg>` });

// detalhe: XY pad (tone x timbre) + preset grid + extra effects + live wave + monitor/test.
(() => {
  const css = `
  .vz{display:grid;grid-template-columns:1.2fr 1fr;grid-template-rows:minmax(0,1fr);gap:16px}
  .vz .esq{display:flex;flex-direction:column;gap:16px;min-height:0}.vz .esq>.painel:first-child{flex:1;min-height:0;display:flex;flex-direction:column}
  .vz .lado{display:grid;gap:16px;align-content:start}
  .vz .pres{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}
  .vz .pres button{border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:12px 6px;border-radius:var(--r-med);cursor:pointer;display:flex;flex-direction:column;align-items:center;gap:4px;transition:background .2s,transform .15s}
  .vz .pres button span{font-size:22px}.vz .pres button:hover{transform:translateY(-2px)}
  .vz .pres button.on{background:var(--ac);color:#000}
  .vz .pad{position:relative;flex:1;min-height:140px;border-radius:var(--r-med);background:linear-gradient(rgba(255,255,255,.04) 1px,transparent 1px) 0 0/100% 25%,linear-gradient(90deg,rgba(255,255,255,.04) 1px,transparent 1px) 0 0/25% 100%,#111;cursor:crosshair;touch-action:none}
  .vz .pad::before,.vz .pad::after{content:"";position:absolute;background:#333}.vz .pad::before{left:50%;top:0;bottom:0;width:1px}.vz .pad::after{top:50%;left:0;right:0;height:1px}
  .vz .pad .pt{position:absolute;width:26px;height:26px;margin:-13px;border-radius:50%;background:var(--ac);box-shadow:0 0 0 6px color-mix(in srgb,var(--ac) 25%,transparent),0 0 24px var(--ac);transition:left .25s,top .25s;z-index:1}
  .vz .pad.arr .pt{transition:none}
  .vz .pad em{position:absolute;font:normal 11px var(--fonte);color:var(--fg3)}
  .vz .vals{display:flex;justify-content:space-between;font-size:12px;color:var(--fg2);margin-top:8px;font-variant-numeric:tabular-nums}
  .vz .onda3{height:56px;display:flex;align-items:center;gap:3px;padding:0 6px;border-radius:var(--r-med);background:rgba(0,0,0,.25)}
  .vz .onda3 i{flex:1;background:var(--ac);border-radius:2px;transition:height .12s}
  .vz .bts{display:flex;gap:8px;margin-top:12px}
  .vz .bt{border:0;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);padding:8px 14px;border-radius:8px;cursor:pointer}
  .vz .bt.on{background:var(--ac);color:#000}
  .vz .bts small{margin-left:auto;align-self:center;font-size:12px;color:var(--fg2)}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  // preset: [name, emoji, tone semitones -12..12, timbre -100..100, robot, echo, radio]
  const PRE = [['Natural', '🙂', 0, 0], ['Grave', '🧔', -5, -30], ['Fina', '👧', 6, 40], ['Esquilo', '🐿️', 11, 70], ['Monstro', '👹', -11, -80, 0, 30],
    ['Robô', '🤖', 0, 0, 80], ['Rádio', '📻', 0, 10, 0, 0, 90], ['Caverna', '🕳️', -2, -10, 0, 80]];
  const S = (App.estado.voz = App.estado.voz || { pre: 'Natural', tom: 0, tim: 0, rob: 0, eco: 0, rad: 0, mix: 100 });
  const barras = Array.from({ length: 48 }, () => '<i></i>').join('');

  const micOn = () => !!App.estado.mic?.ligado;
  App.detalhe({ id: 'voz', montar(el) {
    const real = App.motor.conectado;
    el.innerHTML = `<div class="vz"><div class="esq">
      <div class="painel entra"><h4>Tom e timbre · arraste o ponto</h4><div class="pad"><div class="pt"></div>
        <em style="top:8px;left:50%;transform:translateX(-50%)">mais agudo</em><em style="bottom:8px;left:50%;transform:translateX(-50%)">mais grave</em>
        <em style="left:10px;top:50%;transform:translateY(-140%)">escuro</em><em style="right:10px;top:50%;transform:translateY(-140%)">brilhante</em></div>
        <div class="vals"><span data-v="tom"></span><span>duplo clique volta ao centro</span><span data-v="tim"></span></div></div>
      <div class="painel entra" style="animation-delay:.18s"><h4>Sua voz alterada (ao vivo)</h4><div class="onda3">${barras}</div>
        <div class="bts"><button class="bt" data-ouvir="1">Ouvir minha voz</button><button class="bt" data-grava="1">Gravar 5 s e ouvir</button><small>Use fone para não dar microfonia</small></div></div></div>
      <div class="lado">
        <div class="painel entra" style="animation-delay:.06s"><h4>Vozes prontas</h4><div class="pres">
          ${PRE.map(([n, e]) => `<button data-pre="${n}"><span>${e}</span>${n}</button>`).join('')}</div></div>
        <div class="painel entra" style="animation-delay:.12s"><h4>Efeitos extras</h4>${UI.linha('Robô', UI.slider(S.rob, { fx: 'rob', padrao: 0 }))}${UI.linha('Eco', UI.slider(S.eco, { fx: 'eco', padrao: 0 }))}
          ${UI.linha('Rádio / telefone', UI.slider(S.rad, { fx: 'rad', padrao: 0 }))}${UI.linha('Mistura com a voz real', UI.slider(S.mix, { fx: 'mix', padrao: 100 }))}</div></div></div>`;
    const pad = el.querySelector('.pad'), pt = pad.querySelector('.pt'), ws = el.querySelectorAll('.onda3 i');
    // update nodes only (no rebuild => entry animation not replayed)
    const mostra = () => { pt.style.left = (S.tim + 100) / 2 + '%'; pt.style.top = (12 - S.tom) / 24 * 100 + '%';
      el.querySelector('[data-v=tom]').textContent = `Tom ${S.tom > 0 ? '+' : ''}${S.tom} semitons`;
      el.querySelector('[data-v=tim]').textContent = `Timbre ${S.tim > 0 ? '+' : ''}${S.tim}`;
      el.querySelectorAll('[data-pre]').forEach((b) => b.classList.toggle('on', b.dataset.pre === S.pre));
      for (const k of ['rob', 'eco', 'rad', 'mix']) UI.poe(el.querySelector(`[data-fx=${k}]`), S[k]); };
    mostra();
    let arr = false;
    const poe = (e) => { const r = pad.getBoundingClientRect(), x = Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)), y = Math.min(1, Math.max(0, (e.clientY - r.top) / r.height));
      S.tim = Math.round(x * 200 - 100); S.tom = Math.round(12 - y * 24); S.pre = ''; mostra(); };
    const zera = () => { S.tom = S.tim = 0; S.pre = 'Natural'; mostra(); };
    pad.addEventListener('pointerdown', (e) => { if (e.button === 1) { e.preventDefault(); return zera(); } if (e.button) return; arr = true; pad.classList.add('arr'); poe(e); e.preventDefault(); });
    addEventListener('pointermove', (e) => arr && el.isConnected && poe(e));
    addEventListener('pointerup', () => { arr = false; pad.classList.remove('arr'); });
    pad.addEventListener('dblclick', zera);   // double or wheel click = back to natural voice
    el.addEventListener('click', (e) => {
      const p = e.target.closest('[data-pre]');
      if (p) { const [n, , tom, tim, rob = 0, eco = 0, rad = 0] = PRE.find((x) => x[0] === p.dataset.pre); Object.assign(S, { pre: n, tom, tim, rob, eco, rad }); mostra(); }
      const o = e.target.closest('[data-ouvir]');
      if (o) { if (real && !micOn()) return App.toast?.('Ligue o microfone no Início primeiro');
        const on = o.classList.toggle('on'); if (real) App.motor.enviar({ tipo: 'micouvir', on }); }
      const g = e.target.closest('[data-grava]');
      if (g && !g.classList.contains('on')) {
        if (real) { if (!micOn()) return App.toast?.('Ligue o microfone no Início primeiro'); g.classList.add('on'); g.textContent = 'Gravando… 5'; App.motor.enviar({ tipo: 'micteste' }); return; }
        let s = 5; g.classList.add('on'); g.textContent = `Gravando… ${s}`;
        const t = setInterval(() => { if (--s > 0) return (g.textContent = `Gravando… ${s}`); clearInterval(t); g.textContent = 'Tocando…';
          setTimeout(() => { g.classList.remove('on'); g.textContent = 'Gravar 5 s e ouvir'; }, 2500); }, 1000); } });
    // engine: 5 s test phases; "Ouvir" follows the engine (mic turned off elsewhere = button off)
    const teste = (e) => { if (!el.isConnected) return removeEventListener('fah:micteste', teste); const g = el.querySelector('[data-grava]'), x = e.detail || {};
      if (x.fase === 'gravando') { g.classList.add('on'); g.textContent = `Gravando… ${x.resta}`; }
      else if (x.fase === 'tocando') g.textContent = 'Tocando…';
      else { g.classList.remove('on'); g.textContent = 'Gravar 5 s e ouvir'; } };
    const estado = (e) => { if (!el.isConnected) return removeEventListener('fah:mic', estado); el.querySelector('[data-ouvir]').classList.toggle('on', !!e.detail?.ouvir); };
    if (real) { addEventListener('fah:micteste', teste); addEventListener('fah:mic', estado); el.querySelector('[data-ouvir]').classList.toggle('on', !!App.estado.mic?.ouvir); }
    el.addEventListener('c:mudou', (e) => { const { dados, valor } = e.detail; if (dados.fx) { S[dados.fx] = valor; if (dados.fx !== 'mix') S.pre = ''; mostra(); } });
    // live wave: real = the voice going out (engine "niveis"); browser = fake (higher tone = faster, robot = blocky)
    if (real) { const h = Array(ws.length).fill(0);
      const nv = (e) => { if (!el.isConnected) return removeEventListener('fah:niveis', nv); if (App.estado.tela !== 'modulo') return;
        h.shift(); h.push(e.detail.micSai || 0); ws.forEach((b, i) => (b.style.height = Math.max(6, Math.min(100, (20 * Math.log10(h[i] + 1e-6) + 60) / 60 * 100)) + '%')); };
      addEventListener('fah:niveis', nv); return; }
    const t = setInterval(() => { if (!el.isConnected) return clearInterval(t); if (App.estado.tela !== 'modulo') return;
      const v = 1 + (S.tom + 12) / 12, agora = Date.now() / 300 * v;
      ws.forEach((b, i) => { let h = (Math.sin(i / (4 - v) + agora) * .5 + .5) * 70 + Math.random() * 25;
        if (S.rob > 30) h = Math.round(h / 25) * 25; b.style.height = Math.max(6, h * S.mix / 100 + 5) + '%'; }); }, 120);
  } });
})();
