// Title bar (house pattern): drag area + cells right->left [close 40][maximize 34][minimize 34], full bar height.
// Close: click = hide to tray, HOLD 1 s = quit (fill grows from center, white icon). Tip after 0.6 s rest.
// Maximize: click = full screen/back, HOLD 1 s = swap size Máx. 1440x810 / Mín. 1080x608 (FCC pattern; layout zooms to fit).
(() => {
  const wv = window.chrome && chrome.webview, manda = (tipo) => wv && wv.postMessage({ tipo });
  const IC = { min: '<path d="M3 8h10"/>', max: '<rect x="3.5" y="3.5" width="9" height="9" rx="1"/>',
    rest: '<rect x="3.5" y="5.5" width="7" height="7" rx="1"/><path d="M5.5 5.5v-2h7v7h-2"/>', fechar: '<path d="M4 4l8 8M12 4l-8 8"/>' };
  const svg = (k) => `<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round">${IC[k]}</svg>`;
  const b = document.createElement('header'); b.className = 'barra'; b.id = 'barra';
  b.innerHTML = `<div class="b-arrasta"><span class="b-nome">FAH</span></div>
    <button class="b-cel" id="b-min" data-dica="MINIMIZAR|Clique: esconde na barra de tarefas">${svg('min')}</button>
    <button class="b-cel b-max" id="b-max" data-dica="MAXIMIZAR|Clique: tela cheia ou volta ao tamanho|Segure 1 s: troca entre Máx. 1440 × 810 e Mín. 1080 × 608">${svg('max')}<i class="b-enche"></i></button>
    <button class="b-cel b-fechar" id="b-fechar" data-dica="FECHAR|Clique: esconde perto do relógio, o som continua|Segure 1 s: sai do FAH">${svg('fechar')}<i class="b-enche"></i></button>`;
  document.getElementById('app').prepend(b);
  const arr = b.querySelector('.b-arrasta');
  arr.addEventListener('mousedown', (e) => { if (e.button === 0 && e.detail === 1) manda('arrastar'); });
  arr.addEventListener('dblclick', () => manda('maximizar'));
  b.querySelector('#b-min').onclick = () => manda('minimizar');
  const mx = b.querySelector('#b-max');
  addEventListener('fah:janela', (e) => { mx.firstElementChild.outerHTML = svg(e.detail.max ? 'rest' : 'max'); if (e.detail.tam) App.estado.tam = e.detail.tam; });
  // hold 1 s = long action, release early = click action
  const segurar = (f, clique, longo) => { let t0 = 0, raf = 0;
    const solta = (cancela) => { cancelAnimationFrame(raf); f.classList.remove('segura'); f.style.setProperty('--p', 0); if (!cancela && t0) clique(); t0 = 0; };
    f.addEventListener('pointerdown', (e) => { if (e.button !== 0) return; f.setPointerCapture(e.pointerId); t0 = performance.now(); f.classList.add('segura');
      const passo = () => { const p = Math.min(1, (performance.now() - t0) / 1000); f.style.setProperty('--p', p);
        if (p >= 1) { t0 = 0; f.classList.remove('segura'); f.style.setProperty('--p', 0); longo(); return; } raf = requestAnimationFrame(passo); }; raf = requestAnimationFrame(passo); });
    f.addEventListener('pointerup', () => t0 && solta(false));
    f.addEventListener('pointercancel', () => solta(true));
    f.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); clique(); } }); };
  segurar(mx, () => manda('maximizar'), () => manda('tamanho'));
  segurar(b.querySelector('#b-fechar'), () => manda('esconder'), () => manda('sair'));
  // tip after resting 0.6 s
  const dica = document.createElement('div'); dica.className = 'b-dica'; document.body.append(dica); let td = 0;
  b.querySelectorAll('[data-dica]').forEach((el) => {
    el.addEventListener('mouseenter', () => { clearTimeout(td); td = setTimeout(() => { const [t, ...l] = el.dataset.dica.split('|');
      dica.innerHTML = `<b>${t}</b>${l.map((x) => `<span>${x}</span>`).join('')}`; const r = el.getBoundingClientRect();
      dica.style.top = r.bottom + 6 + 'px'; dica.style.right = Math.max(6, innerWidth - r.right) + 'px'; dica.classList.add('on'); }, 600); });
    el.addEventListener('mouseleave', () => { clearTimeout(td); dica.classList.remove('on'); });
    el.addEventListener('pointerdown', () => { clearTimeout(td); dica.classList.remove('on'); }); });
})();
