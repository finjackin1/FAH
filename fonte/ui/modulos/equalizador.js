// Module: Equalizer (Som). previa() = small live preview on the card; detalhe = own screen. Later: own .dll.
// Detail screen:
// EQU/TONE/LIMIT toggles, preset button, big response curve, preamp + 10 faders, Bass/Treble knobs,
// parametric = band tabs + focused band panel (eq-bandas.js) + draggable handles on the curve. PC extras: wheel, double/wheel click = reset, right click = value box, A/B hold.
App.prev = App.prev || {};
App.prev.curva = () => { let p = 'M0 50'; for (let x = 0; x <= 300; x += 6) p += ` L${x} ${50 - Math.sin(x / 45) * 18 - Math.cos(x / 20) * 6}`;
  return `<svg viewBox="0 0 300 100" preserveAspectRatio="none"><path d="${p}" fill="none" stroke="var(--ac)" stroke-width="3"/><path d="${p} L300 100 L0 100Z" fill="var(--ac)" opacity=".15"/></svg>`; };
App.modulo({ id: 'eq', lado: 'som', ordem: 1, nome: 'Equalizador', icone: 'eq', desc: 'Gráfico e paramétrico, presets e AutoEQ do seu fone.', previa: App.prev.curva });

(() => {
  const css = `
  .pq{--kbg:#222;--krim:#484848;--kind:#ccc;--arc1:#ffb340;--arc2:#ff6a3d;user-select:none;display:grid;grid-template-columns:minmax(0,1fr);grid-template-rows:minmax(0,1fr) minmax(0,1.5fr);gap:14px}
  .pq>.painel{display:flex;flex-direction:column;min-height:0}
  .pq .topo{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
  .pq .tg{border:1.5px solid var(--krim);background:none;color:var(--fg2);font:700 12px var(--fonte);letter-spacing:1px;padding:7px 14px;border-radius:8px;cursor:pointer;transition:.2s}
  .pq .tg.on{background:var(--ac);border-color:var(--ac);color:#000}
  .pq .preset{margin-left:10px;border:0;background:var(--controle);color:var(--fg);font:600 13px var(--fonte);padding:8px 14px;border-radius:8px;cursor:pointer;display:flex;gap:8px;align-items:center;position:relative}
  .pq .preset small{color:var(--fg2);font-weight:400}
  .pq .pop{position:absolute;top:calc(100% + 6px);left:0;z-index:20;background:var(--bg3);border-radius:12px;padding:6px;min-width:220px;box-shadow:0 12px 40px rgba(0,0,0,.6);text-align:left;opacity:0;visibility:hidden;pointer-events:none;transform:translateY(-6px);transition:opacity .15s,transform .15s,visibility 0s .15s}
  .pq .pop.on{opacity:1;visibility:visible;pointer-events:auto;transform:none;transition:opacity .15s,transform .15s}
  .pq .pop div{padding:7px 12px;border-radius:7px;font-weight:400}.pq .pop div.atual{font-weight:600}.pq .pop div:hover,.pq .pop div.foco{background:var(--ac);color:#000}
  .pq .pop hr{border:0;border-top:.5px solid var(--linha);margin:5px 4px}
  .pq .pop div[data-meu]{display:flex;align-items:center;gap:8px}.pq .pop .x{margin-left:auto;width:22px;height:22px;border-radius:6px;display:grid;place-items:center;font-size:15px;opacity:.6}.pq .pop .x:hover{opacity:1;background:rgba(0,0,0,.2)}
  .pq .nome-preset{margin-left:10px;width:210px;border:1.5px solid var(--ac);background:var(--controle);color:var(--fg);font:600 13px var(--fonte);padding:6px 12px;border-radius:8px;outline:0}
  .pq .nome-preset::placeholder{color:var(--fg3);font-weight:400}.pq .nome-preset.erro{border-color:var(--perigo)}
  .pq .dir{margin-left:auto;display:flex;gap:8px;align-items:center}
  .pq .bt{border:0;background:var(--controle);color:var(--fg);font:500 12px var(--fonte);padding:7px 12px;border-radius:8px;cursor:pointer}
  .pq .bt:active,.pq .bt.seg{background:var(--ac);color:#000}
  .pq .vinc{margin-left:8px;display:flex;align-items:center;gap:6px;white-space:nowrap}.pq .vinc svg{width:15px;height:15px;flex:none}
  .pq .curva{position:relative;flex:1;min-height:80px;margin-top:12px;border-radius:var(--r-med);background:#111;overflow:hidden}
  .pq .curva svg{position:absolute;inset:0;width:100%;height:100%}
  .pq .curva text{fill:#666;font:10px var(--fonte)}
  .pq .alca{cursor:grab;stroke:#000;stroke-width:2}.pq .alca.sel{stroke:#fff;stroke-width:3}
  .pq .alca.nasce{transform-box:fill-box;transform-origin:center;animation:alNasce .35s var(--curva) both}@keyframes alNasce{from{opacity:0;transform:scale(0)}60%{transform:scale(1.25)}}
  .pq.equ-off .curva .resp,.pq.equ-off .faders{opacity:.3}.pq.tone-off .tone .kn{opacity:.3}.pq.limit-off .kn[data-k=teto]{opacity:.3}
  .pq .baixo{display:flex;gap:14px;align-items:stretch;min-height:0}
  .pq .faders.entra-modo,.pq .curva.entra-modo{animation:eqModo .3s var(--curva) both}@keyframes eqModo{from{opacity:0;transform:translateY(10px)}}
  .pq .faders{flex:1;min-width:0;display:flex;gap:4px;background:var(--bg2);border-radius:var(--r-card);padding:14px 10px;transition:opacity .3s}
  .pq .faders .sep{width:1px;background:var(--linha);margin:20px 6px}
  .pq .fd{flex:1;display:flex;flex-direction:column;align-items:center;gap:8px;font-variant-numeric:tabular-nums;cursor:ns-resize;min-width:38px}
  .pq .fd.pre{background:#111;border-radius:10px;padding:0 4px;flex:none;width:60px}
  .pq .fd .v{font-size:11px;color:var(--fg);min-height:14px}.pq .fd small{font-size:11px;color:var(--fg2)}
  .pq .fd .tr{position:relative;width:4px;flex:1;min-height:40px;border-radius:2px;background:#333;margin:14px 0}
  .pq .fd .tr::before{content:"";position:absolute;left:-8px;right:-8px;top:50%;height:1px;background:#555}
  .pq .fd .pe{position:absolute;left:0;right:0;min-height:0;background:linear-gradient(var(--arc1),var(--arc2));border-radius:2px}
  .pq .fd .po{position:absolute;left:50%;width:24px;height:44px;margin:-22px 0 0 -12px;border-radius:12px;background:var(--kbg);box-shadow:inset 0 0 0 2px var(--krim),0 3px 8px rgba(0,0,0,.5);transition:box-shadow .15s}
  .pq .fd .po::after{content:"";position:absolute;left:7px;right:7px;top:21px;height:2px;border-radius:1px;background:var(--kind)}
  .pq .fd:hover .po,.pq .fd.ar .po{box-shadow:inset 0 0 0 3px #6a6a6a,0 3px 8px rgba(0,0,0,.5)}
  .pq .tone{width:230px;flex:none;background:var(--bg2);border-radius:var(--r-card);padding:14px;display:grid;grid-template-columns:1fr 1fr;gap:10px;align-content:center}
  .pq .tone h5{grid-column:1/-1;margin:0;font-size:11px;color:var(--fg2);letter-spacing:1px;text-align:center}
  .pq .kn{display:flex;flex-direction:column;align-items:center;gap:2px;cursor:ns-resize;transition:opacity .3s}
  .pq .kn svg{width:76px;height:76px}.pq .kn b{font-size:11px;font-weight:500;color:var(--fg2)}.pq .kn .v{font-size:11px;font-variant-numeric:tabular-nums}
  .pq .kn.peq svg{width:58px;height:58px}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);

  const HZ = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
  const PRESETS_SOM = { Plano: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0], 'Graves+': [6, 5, 3, 1, 0, 0, 0, 0, 0, 0], Voz: [-3, -2, -1, 1, 3, 4, 3, 1, 0, -1],
    Jogo: [2, 1, 0, -1, 0, 2, 4, 5, 3, 1], Cinema: [5, 3, 1, 0, -1, 0, 2, 3, 4, 3], Rock: [4, 3, 1, -1, -2, 0, 2, 3, 4, 4], 'Agudos+': [0, 0, 0, 0, 0, 1, 3, 5, 6, 6] };
  const MAXB = 15;                                   // parametric band limit
  const CORES = ['#ff9f0a', '#30d158', '#0a84ff', '#bf5af2', '#ff375f', '#64d2ff', '#ffd60a', '#5e5ce6', '#66d4cf', '#ac8e68', '#ff8fab', '#a2e04f', '#d0a0ff', '#ff6a3d', '#c7c7cc'];   // one per band (MAXB)
  const TIPOS = [['pico', 'Pico'], ['grave', 'Prat. grave'], ['agudo', 'Prat. aguda'], ['pb', 'Passa-baixa'], ['pa', 'Passa-alta']];
  // factory: same EQ screen for Som and Mic (cfg: id, chave, presets, padrao, autoeq, extra(anima) html, liga(el) events)
  App.criaEq = (cfg) => { const PRESETS = cfg.presets;
  const E = (App.estado[cfg.chave] = App.estado[cfg.chave] || { modo: 'grafico', equ: true, tone: true, limit: false, pre: 0, g: PRESETS[cfg.padrao].slice(), bass: 2, treble: 1, teto: -1, preset: cfg.padrao, sel: 0,
    bandas: [{ t: 'grave', f: 90, g: 4, q: .7, on: 1 }, { t: 'pico', f: 900, g: -2, q: 1.4, on: 1 }, { t: 'pico', f: 3500, g: 3, q: 1, on: 1 }, { t: 'agudo', f: 10000, g: 2, q: .7, on: 1 }] });
  // user presets: name -> snapshot of the whole EQ (mode, graphic, bands, pre, tone); saved with the rest of the state
  E.meus = E.meus || {};
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  // band color follows the band (b.c = color slot), so a dragged/deleted band keeps its color; old bands: slot = position
  const cor = (i) => CORES[(E.bandas[i]?.c ?? i) % CORES.length];
  const fixaCores = (exceto) => E.bandas.forEach((b, i) => { if (b !== exceto && b.c == null) b.c = i % CORES.length; });
  const mexeu = () => { if (E.meus[E.preset]) E.preset = ''; };   // edits break the link to a user preset
  // sound per output device (old FAH "EQ do dispositivo"): E.perfis[device] = whole sound (EQ + AutoEQ + Tone), E.alvo = device
  // whose sound is live ('' = general), E.geral = general sound parked while a device's is live. Engine output -> swap.
  const CAMPOS = ['modo', 'equ', 'tone', 'limit', 'pre', 'g', 'bandas', 'bass', 'treble', 'teto', 'preset', 'autoeq'];
  const foto = () => JSON.parse(JSON.stringify(Object.fromEntries(CAMPOS.map((k) => [k, E[k]]))));
  const carregar = (s) => { Object.assign(E, JSON.parse(JSON.stringify(s))); E.sel = 0; };
  let redesenha = null;
  if (cfg.porSaida) { E.perfis = E.perfis || {}; E.alvo = E.alvo || '';
    const saida = () => (App.estado.motor?.saidas || [])[0] || '';
    const trocar = (nome) => { if (!nome) return; const d = E.perfis[nome] ? nome : ''; if (d === E.alvo) return;
      if (E.alvo) E.perfis[E.alvo] = foto(); else E.geral = foto();
      carregar(d ? E.perfis[d] : E.geral); if (!d) delete E.geral; E.alvo = d; };
    App.eqSaida = { saida, trocar,
      // link the current sound to the current output (general keeps a copy) / unlink (back to general)
      alternar() { const n = saida(); if (!n) return;
        if (E.alvo === n) { delete E.perfis[n]; if (E.geral) carregar(E.geral); delete E.geral; E.alvo = ''; }
        else if (!E.alvo) { E.geral = foto(); E.perfis[n] = foto(); E.alvo = n; }
        redesenha?.(); } };
    addEventListener('fah:motor', (e) => { trocar((e.detail?.saidas || [])[0]); redesenha?.(); });
    trocar(saida()); }
  const curto = (n) => n.replace(/\s*\([^)]*\)\s*$/, '') || n;   // "VK S1 S (VK S1)" -> "VK S1 S" (parens = adapter/driver)
  const popHtml = () => Object.keys(PRESETS).map((k) => `<div data-preset="${k}">${k}</div>`).join('')
    + (Object.keys(E.meus).length ? '<hr>' + Object.keys(E.meus).map((k) => `<div data-preset="${esc(k)}" data-meu="1">${esc(k)}<span class="x" data-apaga="1" title="Apagar este preset">×</span></div>`).join('') : '')
    + '<hr><div data-salvar="1">Salvar como preset…</div>';
  let elEq = null;                                   // mounted EQ screen (set in montar)
  if (cfg.porSaida) {
    // Peace / Equalizer APO text ("Preamp: X dB", "Filter N: ON PK Fc F Hz Gain G dB Q Q") -> user preset in parametric mode.
    // Peace LP/HP (cut above/below Fc) have no exact twin here: our pb/pa boost/cut their band, so HP -> pb -15 dB, LP -> pa -15 dB.
    const TP = { PK: 'pico', PEQ: 'pico', MODAL: 'pico', LS: 'grave', LSC: 'grave', LSQ: 'grave', HS: 'agudo', HSC: 'agudo', HSQ: 'agudo', LP: 'pa', LPQ: 'pa', HP: 'pb', HPQ: 'pb' };
    const n = (s) => { const x = parseFloat(String(s).replace(',', '.')); return Number.isFinite(x) ? x : null; };   // "." or "-" alone -> null
    const lim = (v, a, b) => Math.max(a, Math.min(b, v));
    App.eqImportar = (texto, nome) => { let pre = 0, achouPre = false, fora = 0; const bandas = [];
      String(texto || '').split(/\r?\n/).forEach((l) => {
        const p = l.match(/^\s*Preamp\s*:\s*([-+\d.,]+)\s*dB/i); if (p) { if (n(p[1]) != null) { pre += n(p[1]); achouPre = true; } return; }
        const m = l.match(/^\s*Filter\s*\d*\s*:\s*(ON|OFF)\s+([A-Z]+)\b(.*)$/i); if (!m || /^NONE$/i.test(m[2])) return;   // "OFF None" = empty Peace slot
        const v = (r) => { const x = m[3].match(r); return x ? n(x[1]) : null; };
        const t = TP[m[2].toUpperCase()], fc = v(/Fc\s+([-+\d.,]+)/i), g = v(/Gain\s+([-+\d.,]+)/i), q = v(/\bQ\s+([-+\d.,]+)/i);
        if (!t || fc == null || bandas.length >= MAXB) { fora++; return; }
        const pp = t === 'pb' || t === 'pa';
        bandas.push({ t, f: lim(Math.round(fc), 20, 20000), g: pp ? -15 : lim(g ?? 0, -15, 15), q: lim(q ?? (pp || t !== 'pico' ? .7 : 1), .1, 10), on: m[1].toUpperCase() === 'ON' ? 1 : 0 }); });
      if (!bandas.length) return achouPre ? 'Esse arquivo só tem o pré-amp, sem nenhum filtro para importar.' : 'Não achei filtros do Peace ou do Equalizer APO nesse arquivo.';
      let nm = String(nome || 'Importado').trim().slice(0, 24) || 'Importado'; if (PRESETS[nm]) nm = nm.slice(0, 13) + ' (importado)';
      const s = { modo: 'param', g: E.g.slice(), bandas, pre: lim(pre, -15, 15), bass: E.bass, treble: E.treble };
      E.meus[nm] = s; Object.assign(E, JSON.parse(JSON.stringify(s)), { sel: 0, preset: nm }); redesenha?.();
      return `Importei "${nm}" com ${bandas.length} ${bandas.length > 1 ? 'bandas' : 'banda'}. Ele já está tocando e ficou salvo nos presets do Equalizador.`
        + (fora ? ` ${fora} ${fora > 1 ? 'linhas ficaram' : 'linha ficou'} de fora (tipo de filtro que o FAH não tem ou mais de ${MAXB} bandas).` : ''); };
    // global hotkey "next preset" (Janela.cs -> 'atalho' {acao:'preset'}): factory presets, then the user's, in a loop
    addEventListener('fah:atalho', (e) => { if (e.detail?.acao !== 'preset') return;
      const l = [...Object.keys(PRESETS), ...Object.keys(E.meus)]; if (!l.length) return;
      const nx = l[(l.indexOf(E.preset) + 1) % l.length], b = elEq?.isConnected && elEq.querySelector(`.pop [data-preset="${CSS.escape(nx)}"]`);
      if (b) b.click();                              // screen mounted: same path as the menu (glide/morph)
      else if (E.meus[nx]) { const s = E.meus[nx]; Object.assign(E, { modo: s.modo, g: s.g.slice(), bandas: s.bandas.map((x) => ({ ...x })), pre: s.pre, bass: s.bass, treble: s.treble, sel: 0, preset: nx }); }
      else Object.assign(E, { g: PRESETS[nx].slice(), modo: 'grafico', preset: nx });
      App.toast?.('Preset: ' + nx); });
  }

  // ---- values: key path ("bass", "g.3", "b.1.f") + range ----
  const RG = { pre: [-15, 15, .5], g: [-15, 15, .5], bass: [-10, 10, .5], treble: [-10, 10, .5], teto: [-12, 0, .5], f: [20, 20000, 0, 1], q: [.1, 10, .05], bg: [-15, 15, .5] };
  const PADRAO = { pre: 0, g: 0, bass: 0, treble: 0, teto: -1, f: 1000, q: 1, bg: 0 };
  const tipoDe = (k) => (k.startsWith('b.') ? (k.endsWith('.g') ? 'bg' : k.split('.')[2]) : k.split('.')[0]);
  const ler = (k) => { const p = k.split('.'); if (p[0] === 'b') return E.bandas[p[1]][p[2]]; if (p[0] === 'g') return E.g[p[1]]; return E[k]; };
  // exato = typed in the value box: keep the decimals, only clamp
  const gravar = (k, v, exato) => { const [a, b, c] = RG[tipoDe(k)]; v = Math.max(a, Math.min(b, c && !exato ? Math.round(v / c) * c : v)); const p = k.split('.');
    if (p[0] === 'b') E.bandas[p[1]][p[2]] = v; else if (p[0] === 'g') E.g[p[1]] = v; else E[k] = v;
    if (p[0] === 'g') E.preset = ''; mexeu(); };
  const norm = (k, v = ler(k)) => { const [a, b, , lg] = RG[tipoDe(k)]; return lg ? Math.log(v / a) / Math.log(b / a) : (v - a) / (b - a); };
  const desnorm = (k, n) => { const [a, b, , lg] = RG[tipoDe(k)]; n = Math.max(0, Math.min(1, n)); return lg ? a * Math.pow(b / a, n) : a + n * (b - a); };
  const fmt = (k, v = ler(k)) => { const t = tipoDe(k); if (t === 'f') return UI.hz(v);
    if (t === 'q') return v.toFixed(2).replace('.', ','); return (v > 0 ? '+' : '') + UI.num(v) + ' dB'; };
  // right-click value box: real value; frequency in k from 1 kHz ("11.745k"); a bare number under 20 is read as kHz ("11.745" = 11745 Hz)
  const texto = (k, v = ler(k)) => (tipoDe(k) === 'f' && v >= 1000 ? +(v / 1000).toFixed(6) + 'k' : String(+v.toFixed(6)).replace('.', ','));
  const lerTexto = (k, t) => { let v = UI.ler(t); if (tipoDe(k) === 'f' && !/k/i.test(t) && v < 20) v *= 1000; return Math.round(v * 1e6) / 1e6; };
  const hz = (f) => (f >= 1000 ? f / 1000 + 'k' : f);

  // ---- response: same math as the engine (Modulos\Equalizador\Filtros.cs) ----
  // LP/HP/BP = parallel y = x + (G-1)H(x); peak/shelves in series; graphic = BP Q 1.5; tone = LP 90 Q .8 / HP 10k Q .6
  const TX = 48000, QG = 1.5;
  const coef = (t, f, q, db) => { f = Math.min(f, TX / 2 - 1); const w = 2 * Math.PI * f / TX, c = Math.cos(w), s = Math.sin(w), al = s / (2 * Math.max(q, 1e-3)); let b, a;
    if (t === 'pb') { b = [(1 - c) / 2, 1 - c, (1 - c) / 2]; a = [1 + al, -2 * c, 1 - al]; }
    else if (t === 'pa') { b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]; a = [1 + al, -2 * c, 1 - al]; }
    else if (t === 'bp') { b = [al, 0, -al]; a = [1 + al, -2 * c, 1 - al]; }
    else { const A = Math.pow(10, db / 40), r = 2 * Math.sqrt(A) * al;
      if (t === 'pico') { b = [1 + al * A, -2 * c, 1 - al * A]; a = [1 + al / A, -2 * c, 1 - al / A]; }
      else if (t === 'grave') { b = [A * ((A + 1) - (A - 1) * c + r), 2 * A * ((A - 1) - (A + 1) * c), A * ((A + 1) - (A - 1) * c - r)]; a = [(A + 1) + (A - 1) * c + r, -2 * ((A - 1) + (A + 1) * c), (A + 1) + (A - 1) * c - r]; }
      else { b = [A * ((A + 1) + (A - 1) * c + r), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - r)]; a = [(A + 1) - (A - 1) * c + r, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - r]; } }
    return [b[0] / a[0], b[1] / a[0], b[2] / a[0], a[1] / a[0], a[2] / a[0]]; };
  // complex H at f: returns [re, im]
  const Hc = (k, f) => { const w = 2 * Math.PI * f / TX, c1 = Math.cos(w), s1 = -Math.sin(w), c2 = Math.cos(2 * w), s2 = -Math.sin(2 * w);
    const nr = k[0] + k[1] * c1 + k[2] * c2, ni = k[1] * s1 + k[2] * s2, dr = 1 + k[3] * c1 + k[4] * c2, di = k[3] * s1 + k[4] * s2, d = dr * dr + di * di;
    return [(nr * dr + ni * di) / d, (ni * dr - nr * di) / d]; };
  const resp = (f) => { const par = [], ser = [];
    if (E.tone) { if (Math.abs(E.bass) > .01) par.push([coef('pb', 90, .8, 0), Math.pow(10, E.bass / 20) - 1]); if (Math.abs(E.treble) > .01) par.push([coef('pa', 10000, .6, 0), Math.pow(10, E.treble / 20) - 1]); }
    if (E.equ) { if (E.modo === 'grafico') HZ.forEach((c, i) => E.g[i] && par.push([coef('bp', c, QG, 0), Math.pow(10, E.g[i] / 20) - 1]));
      else E.bandas.forEach((b) => { if (!b.on || Math.abs(b.g) < .005) return;
        if (b.t === 'pb' || b.t === 'pa') par.push([coef(b.t, b.f, b.q, 0), Math.pow(10, b.g / 20) - 1]); else ser.push(coef(b.t, b.f, b.q, b.g)); }); }
    let tr = 1, ti = 0; par.forEach(([k, g]) => { const [r, i] = Hc(k, f); tr += g * r; ti += g * i; });
    let m = Math.hypot(tr, ti); ser.forEach((k) => { const [r, i] = Hc(k, f); m *= Math.hypot(r, i); });
    return 20 * Math.log10(Math.max(m, 1e-9)) + (E.equ ? E.pre : 0); };
  // drawn at the box's real pixel size (no stretch: round handles, sharp text); atualiza() reads the size
  let W = 1000, H = 230; const fx = (f) => Math.log(f / 20) / Math.log(1000) * W, fy = (g) => H / 2 - g / 15 * (H / 2 - 18);
  const ffx = (x) => 20 * Math.pow(1000, x / W), ffy = (y) => (H / 2 - y) / (H / 2 - 18) * 15;
  let nasce = -1;   // band just created: its handle pops in once (next draw only)
  let morre = null; // band being deleted {i, p}: its handle shrinks away (p = 0..1)
  // curve morph (29/10): mode switch, EQU/TONE, A/B, user preset glide from the last drawn curve (morfo.q 0..1)
  let morfo = null, ultimoY = [];
  const svgCurva = () => { let p = ''; const ys = [];
    for (let x = 0, i = 0; x <= W; x += 4, i++) { let y = fy(resp(ffx(x))); if (morfo && morfo.de.length > i) y = morfo.de[i] + (y - morfo.de[i]) * morfo.q; ys.push(y); p += `${x ? 'L' : 'M'}${x} ${y.toFixed(1)}`; }
    ultimoY = ys;
    const grade = [20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000].map((f) => `<line x1="${fx(f)}" x2="${fx(f)}" y1="0" y2="${H}" stroke="#262626"/><text x="${f === 20000 ? fx(f) - 4 : fx(f) + 4}" y="${H - 6}"${f === 20000 ? ' text-anchor="end"' : ''}>${hz(f)}</text>`).join('')
      + [-12, -6, 0, 6, 12].map((g) => `<line x1="0" x2="${W}" y1="${fy(g)}" y2="${fy(g)}" stroke="${g ? '#262626' : '#444'}"/><text x="6" y="${fy(g) - 4}">${g > 0 ? '+' : ''}${g}</text>`).join('');
    const alcas = E.modo === 'param' ? E.bandas.map((b, i) => `<circle class="alca ${i === E.sel ? 'sel' : ''} ${i === nasce ? 'nasce' : ''}" data-b="${i}" cx="${fx(b.f)}" cy="${fy(b.t === 'pb' || b.t === 'pa' ? 0 : b.g)}" r="${(i === E.sel ? 11 : 9) * (morre?.i === i ? 1 - morre.p : 1)}" fill="${cor(i)}" opacity="${(b.on ? 1 : .35) * (morre?.i === i ? 1 - morre.p : 1)}"/>`).join('') : ''; nasce = -1;
    return `<svg viewBox="0 0 ${W} ${H}">${grade}<g class="resp"><defs><linearGradient id="pqg" x1="0" x2="1"><stop offset="0" stop-color="#ffb340"/><stop offset="1" stop-color="#ff6a3d"/></linearGradient></defs>
      <path d="${p} L${W} ${H} L0 ${H}Z" fill="url(#pqg)" opacity=".12"/><path d="${p}" fill="none" stroke="url(#pqg)" stroke-width="2.5" vector-effect="non-scaling-stroke"/></g>${alcas}</svg>`; };

  // ---- controls ----
  const fader = (k, rot, cls = '') => `<div class="fd ${cls}" data-k="${k}" title="Roda do mouse ajusta · duplo clique zera"><span class="v"></span><div class="tr"><div class="pe"></div><div class="po"></div></div><small>${rot}</small></div>`;
  const knob = (k, rot, cls = '') => `<div class="kn ${cls}" data-k="${k}" title="Arraste ou use a roda · duplo clique zera"><svg viewBox="0 0 70 70"></svg><b>${rot}</b><span class="v"></span></div>`;
  const arco = (a0, a1, r = 31) => { const pt = (a) => [35 + r * Math.sin(a), 35 - r * Math.cos(a)], [x0, y0] = pt(a0), [x1, y1] = pt(a1);
    return `M${x0} ${y0} A${r} ${r} 0 ${Math.abs(a1 - a0) > Math.PI ? 1 : 0} ${a1 > a0 ? 1 : 0} ${x1} ${y1}`; };
  const pintaKnob = (el, cor) => { const k = el.dataset.k, n = norm(k), A = 2.36, a = -A + n * 2 * A, bip = RG[tipoDe(k)][0] < 0 && RG[tipoDe(k)][1] > 0;
    const ini = bip ? -A + (0 - RG[tipoDe(k)][0]) / (RG[tipoDe(k)][1] - RG[tipoDe(k)][0]) * 2 * A : -A;
    el.querySelector('svg').innerHTML = `<path d="${arco(-A, A)}" stroke="#2c2c2c" stroke-width="3" fill="none"/>${Math.abs(a - ini) > .01 ? `<path d="${arco(ini, a)}" stroke="${cor || 'url(#pqk)'}" stroke-width="3" fill="none" stroke-linecap="round"/>` : ''}
      <defs><linearGradient id="pqk"><stop offset="0" stop-color="#ffb340"/><stop offset="1" stop-color="#ff6a3d"/></linearGradient></defs>
      <circle cx="35" cy="35" r="25" fill="var(--kbg)" stroke="var(--krim)" stroke-width="2"/><rect x="32.5" y="13" width="5" height="10" rx="2" fill="var(--kind)" transform="rotate(${a * 57.3} 35 35)"/>`;
    el.querySelector('.v').textContent = fmt(k); };
  const pintaFader = (el) => { const k = el.dataset.k, v = ler(k), n = norm(k), pos = (1 - n) * 100, z = (1 - norm(k, 0)) * 100;
    el.querySelector('.po').style.top = pos + '%'; const pe = el.querySelector('.pe'); pe.style.top = Math.min(pos, z) + '%'; pe.style.height = Math.abs(pos - z) + '%';
    el.querySelector('.v').textContent = (v > 0 ? '+' : '') + UI.num(v); };

  // "linked to this headphone" chip: only when the engine is playing to a real output
  const botaoVinc = () => { const n = cfg.porSaida && App.eqSaida.saida(); if (!n) return ''; const c = esc(curto(n)), on = E.alvo === n;
    return `<button class="bt vinc ${on ? 'seg' : ''}" data-vinc="1" title="${on ? `Este som (EQ, AutoEQ e Tone) é só do ${c}. Clique para voltar ao som geral.` : `Guarda este som (EQ, AutoEQ e Tone) só para o ${c}. Os outros aparelhos ficam com o som geral.`}">${UI.ico('fone')}${on ? 'Som do ' : 'Ligar ao '}${c}</button>`; };
  App.detalhe({ id: cfg.id, montar(el) { elEq = el; let vista = 'eq';   // 'esp' = Espaço view (espaco-eq.js), Som only
    // parametric band area lives in eq-bandas.js (App.eqParam): html + live paint get these helpers
    const HH = () => ({ E, CORES, cor, TIPOS, MAXB, fader, knob, fmt, coef, Hc, tp: (t) => TIPOS.find((x) => x[0] === t)[1] });
    // full rebuild only for structure changes (mode, new band); entry animation only on first mount
    const corpo = (anima) => {
      const baixo = E.modo === 'grafico'
        ? `<div class="faders">${fader('pre', 'Pré-amp', 'pre')}<div class="sep"></div>${HZ.map((f, i) => fader('g.' + i, hz(f))).join('')}</div>`
        : App.eqParam.html(HH(), fader('pre', 'Pré-amp', 'pre'));
      el.innerHTML = `<div class="pq ${cfg.autoeq ? 'com-aq' : ''} ${E.equ ? '' : 'equ-off'} ${E.tone ? '' : 'tone-off'} ${E.limit ? '' : 'limit-off'}">
        <div class="painel ${anima ? 'entra' : ''}"><div class="topo">${['equ', 'tone', 'limit'].map((t) => `<button class="tg ${E[t] ? 'on' : ''}" data-tg="${t}">${t.toUpperCase()}</button>`).join('')}
          <button class="preset" data-pop="1">${esc(E.preset || 'Personalizado')} <small>▾</small><div class="pop">${popHtml()}</div></button>${botaoVinc()}
          <div class="dir">${UI.segmento([['grafico', 'Gráfico'], ['param', 'Paramétrico'], ...(cfg.espaco ? [['esp', 'Espaço']] : [])], vista === 'esp' ? 'esp' : E.modo, { campo: 'eqmodo' })}<button class="bt" data-ab="1" title="Segure para ouvir sem o equalizador">Segure: A/B</button><button class="bt" data-reset="1">Zerar</button></div></div>
          <div class="curva" ${vista === 'esp' ? 'hidden' : ''}></div>${cfg.espaco ? `<div class="esp-eq" ${vista === 'esp' ? '' : 'hidden'}>${EspEq.html()}</div>` : ''}</div>
        <div class="baixo ${anima ? 'entra' : ''}" style="animation-delay:.06s">${baixo}
          <div class="tone"><h5>TONE</h5>${knob('bass', 'Graves')}${knob('treble', 'Agudos')}<h5 style="margin-top:6px">LIMIT</h5><div style="grid-column:1/-1;display:flex;justify-content:center">${knob('teto', 'Teto')}</div></div></div>${cfg.extra ? cfg.extra(anima) : ''}</div>`;
      atualiza(); medida.observe(el.querySelector('.curva'));
      const ee = el.querySelector('.esp-eq'); if (ee) { EspEq.liga(ee); EspEq.pinta(ee); }
    };
    const medida = new ResizeObserver(() => requestAnimationFrame(() => el.isConnected && atualiza()));
    const atualiza = () => { const cv = el.querySelector('.curva'), pb = el.querySelector('.preset'); if (pb) pb.firstChild.textContent = (E.preset || 'Personalizado') + ' '; W = cv.clientWidth || W; H = cv.clientHeight || H; cv.innerHTML = svgCurva();
      if (E.modo === 'param') App.eqParam.pinta(el, HH());   // first: may swap parts that the loops below paint
      el.querySelectorAll('.fd').forEach(pintaFader);
      el.querySelectorAll('.kn').forEach((k) => pintaKnob(k, k.closest('[style*="--cor"]')?.style.getPropertyValue('--cor'))); };
    corpo(true); cfg.liga && cfg.liga(el);
    // ---- transitions (29/10, "animate everything in the place"): values glide, the curve morphs, the lower part slides in
    const ease = (p) => 1 - Math.pow(1 - p, 3);
    let anim = 0, fimAnim = null;
    const acabar = () => { if (fimAnim) fimAnim(); };   // user grabs a control mid-glide: jump to the end first
    // alvo: {pre, bass, treble, g: [10], bg: [gain per band]}
    const deslizar = (alvo, T = 280) => { acabar(); const de = { pre: E.pre, bass: E.bass, treble: E.treble, g: E.g.slice(), bg: E.bandas.map((b) => b.g) }, t0 = performance.now();
      const pos = (q) => { const L = (a, b) => (q >= 1 ? b : a + (b - a) * q);
        ['pre', 'bass', 'treble'].forEach((k) => { if (k in alvo) E[k] = L(de[k], alvo[k]); });
        if (alvo.g) E.g = alvo.g.map((v, i) => L(de.g[i] ?? 0, v));
        if (alvo.bg) E.bandas.forEach((b, i) => { if (i in alvo.bg) b.g = L(de.bg[i], alvo.bg[i]); }); };
      fimAnim = () => { cancelAnimationFrame(anim); anim = 0; fimAnim = null; pos(1); if (el.isConnected) atualiza(); };
      const passo = (t) => { if (!el.isConnected) return fimAnim?.(); const p = Math.min(1, (t - t0) / T); if (p >= 1) return fimAnim();
        pos(ease(p)); atualiza(); anim = requestAnimationFrame(passo); };
      anim = requestAnimationFrame(passo); };
    let animM = 0;
    const morfar = (T = 240) => { cancelAnimationFrame(animM); morfo = { de: ultimoY.slice(), q: 0 }; const t0 = performance.now();
      const passo = (t) => { if (!el.isConnected || !morfo) { morfo = null; return; } const p = Math.min(1, (t - t0) / T); morfo.q = ease(p);
        if (p >= 1) { morfo = null; return atualiza(); } atualiza(); animM = requestAnimationFrame(passo); };
      animM = requestAnimationFrame(passo); };
    // mode / user preset: swap only the faders-or-bands block (top, curve and Tone stay), new block slides in, curve morphs
    const trocarBaixo = () => { acabar(); const velho = el.querySelector('.baixo > .faders'); if (!velho) return corpo();
      morfar();
      velho.insertAdjacentHTML('afterend', E.modo === 'grafico'
        ? `<div class="faders">${fader('pre', 'Pré-amp', 'pre')}<div class="sep"></div>${HZ.map((f, i) => fader('g.' + i, hz(f))).join('')}</div>`
        : App.eqParam.html(HH(), fader('pre', 'Pré-amp', 'pre')));
      const novo = velho.nextElementSibling; velho.remove(); novo.classList.add('entra-modo');
      novo.addEventListener('animationend', () => novo.classList.remove('entra-modo'), { once: true });
      el.querySelectorAll('.topo .c-segmento button').forEach((b) => b.classList.toggle('on', b.dataset.v === (vista === 'esp' ? 'esp' : E.modo)));
      rotulo(); atualiza(); };
    // drag a band tab to another place (eq-bandas.js): reorder = same sound, colors stay with their band
    App.eqParam.arrastar(el, HH, (de, para) => { fixaCores(); const [b] = E.bandas.splice(de, 1); E.bandas.splice(para, 0, b); E.sel = para; mexeu(); atualiza(); });
    redesenha = () => el.isConnected && !ar && corpo();
    const rotulo = () => { el.querySelector('.preset').firstChild.textContent = (E.preset || 'Personalizado') + ' '; menu(false); };
    // preset menu: closes on any click outside it, Esc, the window losing focus; arrows + Enter pick; current preset in bold
    const menu = (on) => { const p = el.querySelector('.pop'); if (!p) return; p.classList.toggle('on', on);
      p.querySelectorAll('[data-preset],[data-salvar]').forEach((d) => { d.classList.toggle('atual', !!d.dataset.preset && d.dataset.preset === E.preset); d.classList.remove('foco'); }); };
    // "Salvar como preset…": name field takes the button's place; Enter saves (same name = overwrite), Esc/click away cancels
    const salvar = () => { menu(false); const b = el.querySelector('.preset'); b.style.display = 'none';
      b.insertAdjacentHTML('afterend', '<input class="nome-preset" maxlength="24" placeholder="Nome do preset" spellcheck="false" autocomplete="off">');
      const c = b.nextElementSibling; let feito = false; c.focus();
      const fim = (ok) => { if (feito) return; const n = c.value.trim();
        if (ok && (!n || PRESETS[n])) { c.classList.add('erro'); c.title = n ? 'Esse nome já é de um preset de fábrica' : 'Digite um nome'; return; }
        feito = true; document.removeEventListener('pointerdown', fora, true); if (ok) { E.meus[n] = { modo: E.modo, g: E.g.slice(), bandas: E.bandas.map((x) => ({ ...x })), pre: E.pre, bass: E.bass, treble: E.treble }; E.preset = n; el.querySelector('.pop').innerHTML = popHtml(); }
        c.remove(); b.style.display = ''; rotulo(); if (ok !== null) b.focus(); };
      c.addEventListener('keydown', (e) => { e.stopPropagation(); if (e.key === 'Enter' || e.key === 'Escape') { e.preventDefault(); fim(e.key === 'Enter'); } });
      c.addEventListener('input', () => c.classList.remove('erro'));
      const fora = (e) => e.target !== c && fim(null); document.addEventListener('pointerdown', fora, true);
      c.addEventListener('blur', () => fim(null)); };
    const aberto = () => el.isConnected && el.querySelector('.pop.on');
    document.addEventListener('pointerdown', (e) => { if (aberto() && !e.target.closest('.preset')) menu(false); }, true);
    addEventListener('blur', () => aberto() && menu(false));
    addEventListener('keydown', (e) => { const p = aberto(); if (!p) return;
      if (e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); return menu(false); }
      const l = [...p.querySelectorAll('[data-preset],[data-salvar]')], i = l.findIndex((d) => d.classList.contains('foco'));
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); const n = e.key === 'ArrowDown' ? (i + 1) % l.length : (i < 1 ? l.length : i) - 1; l.forEach((d, k) => d.classList.toggle('foco', k === n)); }
      if (e.key === 'Enter' && i >= 0) { e.preventDefault(); e.stopImmediatePropagation(); l[i].click(); } }, true);

    // drag: faders (vertical position), knobs (vertical delta), curve handles (x = freq, y = gain)
    let ar = null;
    el.addEventListener('pointerdown', (e) => { if (e.target.closest('[data-c]')) return;
      const z = e.target.closest('.fd,.kn'); if (e.button === 1 && z) { e.preventDefault(); acabar(); gravar(z.dataset.k, PADRAO[tipoDe(z.dataset.k)]); return atualiza(); }   // wheel click = reset
      if (e.button !== 0) return;
      const a = e.target.closest('.alca'), f = e.target.closest('.fd'), k = e.target.closest('.kn');
      if (a) { E.sel = +a.dataset.b; ar = { tipo: 'alca', i: E.sel }; 
        el.querySelector(`[data-card="${E.sel}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' }); }
      else if (f) { ar = { tipo: 'fd', el: f }; f.classList.add('ar'); }
      else if (k) ar = { tipo: 'kn', el: k, y: e.clientY, n: norm(k.dataset.k) };
      if (ar) { e.preventDefault(); mover(e); } });
    const mover = (e) => { if (!ar || !el.isConnected) return; acabar();
      if (ar.tipo === 'fd') { const r = ar.el.querySelector('.tr').getBoundingClientRect(); gravar(ar.el.dataset.k, desnorm(ar.el.dataset.k, 1 - (e.clientY - r.top) / r.height)); }
      if (ar.tipo === 'kn') gravar(ar.el.dataset.k, desnorm(ar.el.dataset.k, ar.n + (ar.y - e.clientY) / 180));
      // curve box, not the svg: atualiza() replaces the svg on every move
      if (ar.tipo === 'alca') { const r = el.querySelector('.curva').getBoundingClientRect(); gravar(`b.${ar.i}.f`, ffx((e.clientX - r.left) / r.width * W)); gravar(`b.${ar.i}.g`, ffy((e.clientY - r.top) / r.height * H)); }
      atualiza(); if (ar.tipo === 'fd' && ar.el.dataset.k.startsWith('g.')) el.querySelector('.preset').firstChild.textContent = 'Personalizado '; };
    addEventListener('pointermove', mover);
    addEventListener('pointerup', () => { if (!ar) return; ar.el?.classList.remove('ar'); ar = null; });
    el.addEventListener('contextmenu', (e) => { const c = e.target.closest('.fd,.kn'); if (!c) return; e.preventDefault(); const k = c.dataset.k;
      UI.editar(c, { texto: texto(k), ler: (t) => lerTexto(k, t), aplicar: (v) => { gravar(k, v, true); atualiza(); } }); });
    el.addEventListener('wheel', (e) => { const c = e.target.closest('.fd,.kn'); if (!c) return; e.preventDefault(); const k = c.dataset.k;
      gravar(k, tipoDe(k) === 'f' ? ler(k) * (e.deltaY < 0 ? 1.06 : 1 / 1.06) : ler(k) + (e.deltaY < 0 ? 1 : -1) * (RG[tipoDe(k)][2] || .1)); atualiza(); }, { passive: false });
    el.addEventListener('dblclick', (e) => { const c = e.target.closest('.fd,.kn');
      // parametric: double-click on the curve (not on a handle) = new peak band right there
      if (!c) { const cv = e.target.closest('.curva'); if (!cv || E.modo !== 'param' || e.target.closest('.alca') || E.bandas.length >= MAXB) return;
        const r = cv.getBoundingClientRect(); E.bandas.push({ t: 'pico', f: 1000, g: 0, q: 1, on: 1 }); const i = E.bandas.length - 1;
        gravar(`b.${i}.f`, ffx((e.clientX - r.left) / r.width * W)); gravar(`b.${i}.g`, ffy((e.clientY - r.top) / r.height * H)); E.sel = i; return novaBanda(); }
      gravar(c.dataset.k, PADRAO[tipoDe(c.dataset.k)]); atualiza(); });

    el.addEventListener('click', (e) => { const t = e.target;
      const tg = t.closest('[data-tg]'); if (tg) { const k = tg.dataset.tg; E[k] = !E[k]; tg.classList.toggle('on', E[k]); el.querySelector('.pq').classList.toggle(k + '-off', !E[k]); return k === 'limit' ? atualiza() : morfar(); }
      if (t.closest('[data-salvar]')) { e.stopPropagation(); return salvar(); }
      if (t.closest('[data-vinc]')) return App.eqSaida.alternar();
      const pr = t.closest('[data-preset]'); if (pr) { e.stopPropagation(); const n = pr.dataset.preset;
        if (t.closest('[data-apaga]')) { delete E.meus[n]; if (E.preset === n) E.preset = ''; el.querySelector('.pop').innerHTML = popHtml(); menu(true); return atualiza(); }
        if (pr.dataset.meu) { const s = E.meus[n];
          // both graphic: faders glide; otherwise the bands/faders block is swapped and the curve morphs
          if (s.modo === 'grafico' && E.modo === 'grafico') { E.preset = n; rotulo(); E.bandas = s.bandas.map((x) => ({ ...x })); return deslizar({ g: s.g.slice(), pre: s.pre, bass: s.bass, treble: s.treble }); }
          acabar(); Object.assign(E, { modo: s.modo, g: s.g.slice(), bandas: s.bandas.map((x) => ({ ...x })), pre: s.pre, bass: s.bass, treble: s.treble, sel: 0, preset: n }); return trocarBaixo(); }
        E.preset = n; if (E.modo !== 'grafico') { acabar(); E.g = PRESETS[n].slice(); E.modo = 'grafico'; return trocarBaixo(); } rotulo(); return deslizar({ g: PRESETS[n].slice() }); }
      if (t.closest('[data-pop]')) return menu(!el.querySelector('.pop.on'));
      if (t.closest('[data-reset]')) { mexeu(); E.preset = E.modo === 'grafico' ? 'Plano' : E.preset; rotulo();
        return deslizar(E.modo === 'grafico' ? { g: PRESETS.Plano.slice(), pre: 0, bass: 0, treble: 0 } : { bg: E.bandas.map(() => 0), pre: 0, bass: 0, treble: 0 }); }
      const ts = t.closest('[data-tset]'); if (ts) { const [i, tt] = ts.dataset.tset.split(':'); E.bandas[i].t = tt; E.sel = +i; mexeu(); return atualiza(); }
      const dl = t.closest('[data-del]'); if (dl) return excluir(+dl.dataset.del);
      if (t.closest('[data-novo]')) { if (E.bandas.length >= MAXB) return; const u = E.bandas[E.bandas.length - 1];
        // + button: first band at 31 Hz, each new one at double the previous band's frequency
        E.bandas.push({ t: 'pico', f: u ? Math.min(20000, Math.round(u.f * 2)) : 31, g: 0, q: 1, on: 1 }); E.sel = E.bandas.length - 1; mexeu(); return novaBanda(); }
      const cd = t.closest('[data-card]'); if (cd && E.sel !== +cd.dataset.card) { E.sel = +cd.dataset.card; atualiza(); } });
    // new band (last one, selected): grows in place, no full rebuild (scroll stays, row follows the new tab)
    const novaBanda = () => { const b = E.bandas[E.sel]; fixaCores(b); const usadas = new Set(E.bandas.filter((x) => x !== b).map((x) => x.c));
      b.c = [...CORES.keys()].find((k) => !usadas.has(k)) ?? E.sel % CORES.length; nasce = E.sel; if (!App.eqParam.novo(el, HH())) return corpo(); atualiza(); };
    // delete band (29/10, no rebuild): its gain glides to 0 (curve and sound flatten together, handle shrinks), its tab
    // folds up while the others slide in, the panel fades out; then the next band's panel fades in (or "Nenhuma banda")
    const excluir = (i) => { if (morre || !E.bandas[i]) return; fixaCores(); const b = E.bandas[i], g0 = b.g, T = 260, t0 = performance.now();
      if (!App.eqParam.remover(el, i, T)) { E.bandas.splice(i, 1); mexeu(); E.sel = Math.max(0, Math.min(E.sel > i ? E.sel - 1 : E.sel, E.bandas.length - 1)); return corpo(); }
      morre = { i, p: 0 };
      const passo = (agora) => { if (!el.isConnected) { morre = null; return; }
        const p = Math.min(1, (agora - t0) / T), q = 1 - Math.pow(1 - p, 3); morre.p = q; b.g = g0 * (1 - q); atualiza();
        if (p < 1) return requestAnimationFrame(passo);
        morre = null; E.bandas.splice(i, 1); mexeu(); E.sel = Math.max(0, Math.min(E.sel > i ? E.sel - 1 : E.sel, E.bandas.length - 1));
        App.eqParam.removido(el, HH(), i); atualiza(); };
      requestAnimationFrame(passo); };
    addEventListener('keydown', (e) => { if (e.key !== 'Delete' || !el.isConnected || E.modo !== 'param' || !E.bandas.length || e.target.closest?.('input,textarea')) return;
      if (!el.closest('.tela:not(.fora)')) return; e.preventDefault(); excluir(E.sel); });
    // A/B: hold to hear without EQ (curve goes flat while held)
    el.addEventListener('pointerdown', (e) => { const b = e.target.closest('[data-ab]'); if (!b) return; const antes = E.equ; E.equ = false; b.classList.add('seg'); morfar(150);
      addEventListener('pointerup', () => { E.equ = antes; b.classList.remove('seg'); morfar(150); }, { once: true }); });
    el.addEventListener('c:mudou', (e) => { const { tipo, valor, dados } = e.detail;
      if (tipo === 'segmento' && dados.campo === 'eqmodo') { const cv = el.querySelector('.curva'), ee = el.querySelector('.esp-eq');
        if (valor === 'esp') { vista = 'esp'; cv.hidden = true; ee.hidden = false; ee.classList.add('entra-esp'); ee.addEventListener('animationend', () => ee.classList.remove('entra-esp'), { once: true }); EspEq.pinta(ee); return; }
        const volta = vista === 'esp'; vista = 'eq'; if (ee) ee.hidden = true; cv.hidden = false;
        if (valor !== E.modo) { E.modo = valor; trocarBaixo(); } else if (volta) { cv.classList.add('entra-modo'); cv.addEventListener('animationend', () => cv.classList.remove('entra-modo'), { once: true }); atualiza(); } }
      if (tipo === 'switch' && dados.banda !== undefined) { E.bandas[dados.banda].on = valor; mexeu(); el.querySelectorAll(`[data-card="${dados.banda}"]`).forEach((c) => c.classList.toggle('off', !valor)); atualiza(); } });
  } });
  };
  App.criaEq({ id: 'eq', chave: 'eq2', presets: PRESETS_SOM, padrao: 'Jogo', autoeq: 'AutoEQ do seu fone…', porSaida: true, espaco: true });
})();
