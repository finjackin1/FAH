// Core: registries + navigation. Screens, modules, sides and dock items register here;
// nothing else knows about each other. (Later in C#: same idea, each module = 1 .dll.)
const App = (() => {
  const lados = [], modulos = [], telas = {}, dock = [], detalhes = {}, estado = {};
  let atual = null;
  const $ = (s, r = document) => r.querySelector(s);

  const App = {
    estado, $,
    // device side (Som, Microfone, ... more later)
    lado(def) { lados.push(def); },
    lados: () => lados,
    ladoPor: (id) => lados.find((l) => l.id === id),
    // feature module: { id, lado, nome, icone, desc, previa(), ordem }
    modulo(def) { modulos.push(def); modulos.sort((a, b) => (a.ordem ?? 50) - (b.ordem ?? 50)); },
    modulosDo: (lado) => modulos.filter((m) => m.lado === lado),
    // module detail screen (opens when the card is clicked): { id, montar(el, voltarAoLado), aoSair? }
    detalhe(def) { detalhes[def.id] = def; },
    detalhePor: (id) => detalhes[id],
    // screen: { id, classe, montar(el, params), aoMostrar?(el, params), aoSair?(destino) }
    tela(def) { telas[def.id] = def; },
    // dock item: { id, icone | html(), dica, tela | acao, ordem, classe()?, dicaViva()?, aceso()? } (aceso: lit icon for overlays opened by acao) (html/classe/dicaViva: toggles like "7.1")
    dock(def) { dock.push(def); dock.sort((a, b) => (a.ordem ?? 50) - (b.ordem ?? 50)); },

    ir(id, params = {}) {
      const t = telas[id]; if (!t) return;
      if (atual && atual !== t) { atual.el.classList.add('fora'); atual.aoSair?.(id); }
      if (!t.el) { t.el = document.createElement('section'); t.el.className = 'tela fora ' + (t.classe || ''); $('#palco').append(t.el); }
      t.montar(t.el, params);
      requestAnimationFrame(() => { if (atual === t) t.el.classList.remove('fora'); });
      atual = t; estado.tela = id; App.desenharDock();
    },
    voltar() { App.ir('inicio'); },

    // drawn once; screen items (.ti) then only switch on/off with a glow sweep (acende/apaga). html() toggles ("7.1") run their own
    desenharDock() {
      const el = $('#dock');
      if (el.children.length !== dock.length) {
        el.innerHTML = dock.map((d) => { const i = d.html ? d.html() : UI.ico(d.icone);
          return `<button data-dock="${d.id}" data-dica="${d.dicaViva ? d.dicaViva() : d.dica}" aria-label="${d.dica}" class="${(d.aceso ? d.aceso() : estado.tela === d.tela) ? 'on' : ''} ${d.html ? '' : 'ti'} ${d.classe ? d.classe() : ''}">${d.html ? i : `<span class="b">${i}</span><span class="l">${i}</span><span class="f">${i}</span>`}</button>`; }).join('');
        return;
      }
      for (const d of dock) { if (d.html) continue;
        const b = el.querySelector(`[data-dock="${d.id}"]`), v = d.aceso ? d.aceso() : estado.tela === d.tela;
        if (!b || b.classList.contains('on') === v) continue;
        b.classList.remove('acende', 'apaga'); void b.offsetWidth; b.classList.add(v ? 'acende' : 'apaga'); b.classList.toggle('on', v);
      }
    },
    iniciar(id) {
      $('#dock').addEventListener('click', (e) => { const b = e.target.closest('[data-dock]'); if (!b) return;
        const d = dock.find((x) => x.id === b.dataset.dock); d.acao ? d.acao() : App.ir(estado.tela === d.tela && d.tela !== 'inicio' ? 'inicio' : d.tela); });
      addEventListener('keydown', (e) => { if (e.key === 'Escape' && estado.tela !== 'inicio') App.voltar(); });
      App.ir(id);
    },
  };
  return App;
})();
