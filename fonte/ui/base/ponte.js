// Bridge to FAH.exe (WebView2). Loaded right after nucleo.js, before modules/screens.
// Saved state (window.FAH_SALVO) goes into App.estado first, so modules start with it.
// State keys in VIGIA are sent to the engine whenever they change. Engine messages -> App.estado.<tipo> + event 'fah:<tipo>'.
(() => {
  const wv = window.chrome && chrome.webview;
  const VIGIA = ['eq2', 'efeitos', 'ligado', 's71', 'ordem', 'eqmic', 'ruido', 'voz', 'vm', 'cfg'];
  if (window.FAH_SALVO) Object.assign(App.estado, window.FAH_SALVO);
  App.motor = { conectado: !!wv, enviar: (m) => wv && wv.postMessage(m) };
  if (!wv) return;                                  // plain browser (prototype tests): nothing else
  const ultimo = {};
  setInterval(() => VIGIA.forEach((k) => { const v = App.estado[k]; if (v === undefined) return;
    const j = JSON.stringify(v); if (j !== ultimo[k]) { ultimo[k] = j; wv.postMessage({ tipo: 'estado', chave: k, valor: v }); } }), 40);
  wv.addEventListener('message', (e) => { const { tipo, dados } = e.data || {}; if (!tipo) return;
    App.estado[tipo] = dados; dispatchEvent(new CustomEvent('fah:' + tipo, { detail: dados }));
    if (tipo === 'motor') aviso(dados);
    else if (tipo === 'mic') aviso(dados, 'mic', 88, 'micligar');
    else if (tipo === 'aviso' && dados && dados.texto) { if (dados.tipo === 'erro') aviso({ erro: dados.texto }); else if (App.toast) App.toast(dados.texto, 5000); } });
  // engine problem = one line at the top (plain sentence, no code); one line per source (motor / mic), click = that source's retry
  const aviso = (m, id = 'motor', topo = 50, retry = 'ligar') => { let el = document.getElementById('aviso-' + id);
    if (!el) { el = document.createElement('div'); el.id = 'aviso-' + id;
      el.style.cssText = `position:fixed;top:${topo}px;left:50%;transform:translateX(-50%);z-index:99;background:#3a1d1d;color:#ffb4a8;padding:8px 16px;border-radius:10px;font-size:13px;transition:opacity .3s;cursor:pointer;max-width:min(900px,90vw);text-align:center`;
      el.title = 'Clique para tentar ligar de novo'; el.onclick = () => wv.postMessage({ tipo: retry }); document.body.append(el); }
    el.textContent = m.erro || ''; el.style.opacity = m.erro ? 1 : 0; el.style.pointerEvents = m.erro ? 'auto' : 'none'; };
  addEventListener('error', (e) => wv.postMessage({ tipo: 'log', texto: String(e.message) }));
})();
