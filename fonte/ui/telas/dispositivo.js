// Screen: one side (Som/Mic). Cards come from App.modulosDo(lado); segment switches side.
(() => {
  const css = `
  .t-disp header{display:flex;align-items:center;gap:14px;margin-bottom:22px}
  .t-disp .volta{border:0;background:none;color:var(--ac);font-size:26px;cursor:pointer;display:grid;place-items:center;padding:0 4px}
  .t-disp h2{margin:0;font-size:var(--t-tela);font-weight:700;letter-spacing:-.5px}
  .t-disp .dev{color:var(--fg2);font-size:13px}
  .t-disp header .c-segmento{margin-left:auto}
  .t-disp .grade{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:16px}
  .cartao{background:var(--bg2);border-radius:var(--r-card);padding:18px;min-height:190px;transition:background .25s}
  .cartao:hover{background:var(--bg2-hover)}
  .cartao .t{display:flex;align-items:center;gap:10px;font-weight:600;font-size:15px}.cartao .t .c-switch{margin-left:auto}
  .cartao p{color:var(--fg2);font-size:13px;margin:8px 0 14px}
  .cartao .vis{height:80px;border-radius:var(--r-med);background:rgba(0,0,0,.25);overflow:hidden;transition:.3s}.cartao .vis svg{display:block;width:100%;height:100%}
  .cartao.off .vis{opacity:.3;filter:grayscale(1)}
  .cartao.abre{cursor:pointer}.cartao.abre:hover .t::after{opacity:1}.cartao .t::after{content:'›';order:4;font-size:20px;color:var(--fg3);opacity:.6;margin-left:6px}.cartao .t .c-switch{order:3}
  .cartao.novo{background:none;border:1.5px dashed var(--linha);display:grid;place-items:center;align-content:center;gap:8px;color:var(--fg2);cursor:pointer;font-size:26px}
  .cartao.novo span{font-size:13px}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);
  const ligado = App.estado.ligado = App.estado.ligado || {};

  App.tela({ id: 'dispositivo', classe: 't-disp rolagem',
    montar(el, { lado }) {
      lado = lado || App.estado.ultimoLado || App.lados()[0].id; App.estado.ultimoLado = lado;
      const L = App.ladoPor(lado), padrao = (App.estado.dispositivos[lado] || []).find((d) => d.p);
      el.style.setProperty('--ac', `var(--${lado})`);
      el.innerHTML = `<header><button class="volta" title="Voltar (Esc)">${UI.ico('volta')}</button>${UI.icone(L.icone, 44, `var(--${lado}-ic)`)}
        <div><h2>${L.nome}</h2><div class="dev">${padrao ? padrao.n : ''} ${UI.select('', { campo: 'aparelho' })}</div></div>
        ${UI.segmento(App.lados().map((l) => [l.id, l.nome]), lado, { campo: 'lado' })}</header>
        <div class="grade">${App.modulosDo(lado).map((m, i) => `<div class="cartao entra ${ligado[m.id] === false ? 'off' : ''} ${App.detalhePor(m.id) ? 'abre' : ''}" data-mod="${m.id}" style="animation-delay:${i * .07}s">
          <div class="t">${UI.icone(m.icone, 28)}${m.nome}${UI.switch(ligado[m.id] !== false, { modulo: m.id })}</div><p>${m.desc}</p><div class="vis">${m.previa ? m.previa() : ''}</div></div>`).join('')}
          <div class="cartao novo entra" style="animation-delay:.3s">${UI.ico('mais')}<span>Módulo novo</span></div></div>`;
      el.querySelector('.volta').onclick = App.voltar;
      el.onclick = (e) => { const c = e.target.closest('.cartao.abre'); if (c && !e.target.closest('[data-c]')) App.ir('modulo', { id: c.dataset.mod, lado }); };
      el.addEventListener('c:mudou', el._h || (el._h = (e) => { const { tipo, valor, dados } = e.detail;
        if (tipo === 'segmento' && dados.campo === 'lado') App.ir('dispositivo', { lado: valor });
        if (tipo === 'switch' && dados.modulo) { ligado[dados.modulo] = valor; e.target.closest('.cartao').classList.toggle('off', !valor); } }));
    } });
})();
