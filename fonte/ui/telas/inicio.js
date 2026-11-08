// Screen: start = approved B3 "Caminho do som" (27/10, docs\prototipos\inicio\ESCOLHIDA). Title tabs Som | Microfone (mic live since 29/10, own Ligar),
// path Tocando -> Equalizador -> Efeitos (Som 7.1 lives inside, 28/10 proposal A) -> Saída, fixed order (all steps are linear, order made no audible difference;
// drag-to-reorder removed 28/10). App.estado.ordem stays for the engine (Motor.Ordenar). Live data: motor, niveis, s71auto, cabo71.
(() => {
  const css = `
  .t-inicio{overflow:hidden}
  .t-inicio .b{position:absolute;left:0;top:0;width:1280px;height:680px}
  .t-inicio .b-topo{position:absolute;left:52px;right:52px;top:32px;height:40px;display:flex;align-items:center;gap:12px}
  .t-inicio .b-topo .sub{font-size:13px;color:var(--fg2);margin-left:4px;transition:color .2s;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0}
  .t-inicio .b-topo .sub.erro{color:#ffb4a8}
  .t-inicio .esp{flex:1}
  .t-inicio .abas{display:flex;align-items:center;gap:20px;flex:none}
  .t-inicio .abas button{border:0;background:none;padding:0;height:40px;font:700 22px var(--fonte);letter-spacing:-.4px;color:var(--fg3);cursor:pointer;display:flex;align-items:center;gap:8px;position:relative;transition:color .2s}
  .t-inicio .abas button:hover{color:var(--fg2)}.t-inicio .abas button.on{color:var(--fg);cursor:default}
  .t-inicio .abas button::after{content:"";position:absolute;left:0;right:0;bottom:0;height:2px;border-radius:1px;background:var(--som);transform:scaleX(0);transition:transform .3s var(--curva)}
  .t-inicio .abas button.mic::after{background:var(--mic)}.t-inicio .abas button.on::after{transform:none}
  .t-inicio .breve-tag{font-size:10px;font-weight:600;letter-spacing:.6px;text-transform:uppercase;color:var(--fg2);background:var(--controle);border-radius:4px;padding:2px 6px;white-space:nowrap}
  .t-inicio .pilula{height:36px;border:0;border-radius:18px;padding:0 16px;display:flex;align-items:center;gap:8px;background:var(--controle);color:var(--fg);font:500 13px var(--fonte);cursor:pointer;transition:background .2s,transform .15s,color .2s;flex:none}
  .t-inicio .pilula:hover{background:var(--controle-forte)}.t-inicio .pilula:active{transform:scale(.97)}
  .t-inicio .pilula svg{width:16px;height:16px;color:var(--fg2)}
  .t-inicio .pilula[disabled]{opacity:.4;pointer-events:none}
  .t-inicio .pilula.apagada{opacity:.4;cursor:default}.t-inicio .pilula.apagada:hover{background:var(--controle);transform:none}
  .t-inicio .res2[data-dica]{cursor:help}
  .t-inicio .pilula .pt{width:8px;height:8px;border-radius:50%;background:var(--fg3);transition:background .25s,box-shadow .25s}
  .t-inicio .pilula.on .pt{background:var(--ok);box-shadow:0 0 8px var(--ok)}
  .t-inicio .pilula.espera .pt{animation:i-pulsa 1s infinite}
  .t-inicio .pilula.segura{background:var(--som);color:#000}.t-inicio .pilula.segura svg{color:#000}
  .t-inicio .pilula.forte{background:var(--fg);color:#000}.t-inicio .pilula.forte svg{color:#000}
  @keyframes i-pulsa{50%{opacity:.35}}
  .t-inicio .fio{position:absolute;left:0;top:0;width:1280px;height:680px;pointer-events:none}
  .t-inicio .fio .base{stroke:var(--bg3);stroke-width:2;fill:none}
  .t-inicio .fio .corre{stroke:var(--som);stroke-width:2;fill:none;stroke-dasharray:2 10;stroke-linecap:round;animation:i-corre .6s linear infinite;transition:opacity .2s}
  @keyframes i-corre{to{stroke-dashoffset:-12}}
  .t-inicio .fio .direto{stroke:var(--fg2);stroke-width:1.5;fill:none;stroke-dasharray:4 6;opacity:0;transition:opacity .3s}
  .t-inicio .fio .rot{fill:var(--fg2);font:12px var(--fonte);opacity:0;transition:opacity .3s}
  .t-inicio .b.cmp .fio .direto,.t-inicio .b.desl .fio .direto,.t-inicio .b.cmp .fio .rot,.t-inicio .b.desl .fio .rot{opacity:1}
  .t-inicio .fio .ghost{stroke:var(--linha);stroke-width:1.5;fill:none;stroke-dasharray:4 5}
  .t-inicio .fio .ghost.m{stroke:var(--mic);opacity:.4}.t-inicio .fio .corre.m{stroke:var(--mic)}
  .t-inicio .pilula.ouve{background:var(--mic);color:#fff}.t-inicio .pilula.ouve svg{color:#fff}
  .t-inicio .fio .quebra{stroke:var(--perigo);stroke-width:2;stroke-dasharray:4 4;fill:none}
  .t-inicio .no{position:absolute;top:132px;height:240px;border-radius:24px;background:var(--bg2);padding:16px;display:flex;flex-direction:column;cursor:pointer;transition:left .35s var(--curva),background .2s,transform .15s,box-shadow .15s,opacity .3s;outline-offset:3px;touch-action:none}
  .t-inicio .no:hover{background:var(--bg2-hover);transform:translateY(-2px);box-shadow:0 12px 28px rgba(0,0,0,.35)}
  .t-inicio .no:active{transform:scale(.99)}
  .t-inicio .no:focus-visible{outline:2px solid var(--fg3)}
  .t-inicio .no .t{display:flex;align-items:center;gap:8px;height:24px}
  .t-inicio .no .t svg{width:20px;height:20px;color:var(--fg2)}
  .t-inicio .no .t b{flex:1;font-size:15px;font-weight:600;white-space:nowrap}
  .t-inicio .no .vis{height:104px;margin:16px 0 12px;border-radius:8px;background:rgba(0,0,0,.28);position:relative;overflow:hidden;transition:opacity .3s,filter .3s}
  .t-inicio .no .res{font-size:13px;color:var(--fg);line-height:18px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .t-inicio .no .res2{font-size:12px;color:var(--fg2);line-height:16px;margin-top:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .t-inicio .no.off{background:rgba(44,44,46,.35);box-shadow:inset 0 0 0 1px var(--linha)}
  .t-inicio .no.off .vis{opacity:.3;filter:grayscale(1)}.t-inicio .no.off .res{color:var(--fg2)}
  /* needs a cable that is not there (04/11): dimmed, whole card is the target (nested tips, switch and mode button off) */
  .t-inicio .no.sem-cabo{opacity:.4;cursor:default}.t-inicio .no.sem-cabo:hover,.t-inicio .no.sem-cabo:active{transform:none;box-shadow:none;background:var(--bg2)}
  .t-inicio .no.sem-cabo.off:hover{background:rgba(44,44,46,.35)}.t-inicio .no.sem-cabo>*{pointer-events:none}
  .t-inicio .b.cmp .no.mod,.t-inicio .b.desl .no.mod{opacity:.35}
  .t-inicio .b.desl .no.ponta .vis{filter:grayscale(1);opacity:.5}
  .t-inicio .ponta{cursor:default}.t-inicio .ponta:hover{transform:none;box-shadow:none;background:var(--bg2)}
  .t-inicio .app{position:absolute;left:50%;top:50%;width:56px;height:56px;margin:-28px;border-radius:14px;background:var(--som-ic);display:grid;place-items:center;color:#fff;transition:filter .3s}
  .t-inicio .app svg{width:28px;height:28px}.t-inicio .app.vazio{background:var(--bg3);color:var(--fg3)}.t-inicio .app.m{background:var(--mic-ic)}
  .t-inicio .vu{position:absolute;inset:12px 0;display:flex;justify-content:center;gap:12px;align-items:flex-end}
  .t-inicio .vu i{width:16px;height:100%;border-radius:4px;background:var(--controle);position:relative;overflow:hidden}
  .t-inicio .vu i b{position:absolute;left:0;right:0;bottom:0;border-radius:4px;height:0;background:linear-gradient(0deg,var(--ok) 0 60%,#ffd60a 80%,var(--perigo));background-size:100% 80px;background-position:bottom}
  .t-inicio .chip{display:inline-flex;align-items:center;gap:6px;height:20px;padding:0 8px;border-radius:10px;font-size:11px;font-weight:600;background:rgba(255,159,10,.16);color:var(--som)}
  .t-inicio .chip svg{width:12px;height:12px}
  .t-inicio .vis svg.full{position:absolute;inset:0;width:100%;height:100%}
  .t-inicio .vis .zero{stroke:rgba(255,255,255,.08)}.t-inicio .vis .cv{fill:none;stroke:var(--som);stroke-width:2}.t-inicio .vis .area{fill:url(#i-gB);opacity:.45}
  .t-inicio .vis .cvm{fill:none;stroke:var(--mic);stroke-width:1.8}
  /* mini versions of the module screens (same pieces, smaller): 7.1 room, effect scenes, EQ toggles */
  .t-inicio .anel71{position:absolute;left:50%;top:50%;border:1px dashed #333;border-radius:50%;transform:translate(-50%,-50%)}
  .t-inicio .cab71{position:absolute;left:50%;top:50%;width:22px;height:22px;margin:-11px;border-radius:50%;background:var(--bg3);display:grid;place-items:center;color:var(--fg2);font-size:13px;line-height:1}
  .t-inicio .cab71::before{content:"";position:absolute;top:-4px;left:50%;margin-left:-4px;border:4px solid transparent;border-bottom-color:var(--fg2);border-top:0}
  .t-inicio .cx71{position:absolute;min-width:16px;height:16px;padding:0 2px;transform:translate(-50%,-50%);border-radius:5px;background:var(--bg3);display:grid;place-items:center;font:700 7px/1 var(--fonte);font-style:normal;color:var(--fg);box-shadow:0 2px 6px rgba(0,0,0,.4);transition:background .15s,color .15s,transform .15s}
  .t-inicio .cx71.toca{background:var(--som);color:#000;transform:translate(-50%,-50%) scale(1.15)}.t-inicio .cx71.mudo{opacity:.3}
  .t-inicio .no .vis.fxs{background:none;border-radius:0;display:grid;grid-template-columns:repeat(3,1fr);gap:8px}
  .t-inicio .fx{display:flex;flex-direction:column;gap:6px;min-width:0}
  .t-inicio .fx .mini{height:60px;flex:none;border-radius:6px;background:radial-gradient(circle at 50% 60%,rgba(255,159,10,.12),#151517 75%);overflow:hidden;transition:opacity .3s,filter .3s}
  .t-inicio .mini svg{display:block;width:100%;height:100%}
  .t-inicio .fx>div{min-width:0;display:flex;flex-direction:column;text-align:center}
  .t-inicio .fx b{font-size:12px;font-weight:600;line-height:15px}
  .t-inicio .fx small{font-size:11px;color:var(--fg2);line-height:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .t-inicio .fx:not(.on) .mini{opacity:.35;filter:grayscale(1)}.t-inicio .fx:not(.on) b{color:var(--fg2)}.t-inicio .fx:not(.on) small{color:var(--fg3)}
  .t-inicio .mini .mfio{fill:none;stroke:var(--som);stroke-linecap:round;vector-effect:non-scaling-stroke}
  .t-inicio .mini .mfio.anda{stroke-dasharray:3 4;animation:fio 1s linear infinite}
  .t-inicio .mini .onda{fill:none;stroke:var(--som);vector-effect:non-scaling-stroke}.t-inicio .mini .sala{fill:none;stroke:#3a3a3c;stroke-width:1.5;vector-effect:non-scaling-stroke}
  .t-inicio .tgs{position:absolute;left:6px;top:6px;display:flex;gap:4px;z-index:1}
  .t-inicio .tgs span{font:700 8px/1 var(--fonte);letter-spacing:.6px;padding:3px 5px;border-radius:4px;box-shadow:inset 0 0 0 1px rgba(255,159,10,.35);color:var(--fg3)}
  .t-inicio .tgs span.on{background:var(--som);box-shadow:none;color:#000}
  .t-inicio .chave{width:40px;height:24px;border-radius:12px;background:var(--controle-forte);position:relative;cursor:pointer;transition:background .25s;flex:none;border:0;padding:0}
  .t-inicio .chave::after{content:"";position:absolute;left:2px;top:2px;width:20px;height:20px;border-radius:50%;background:#fff;box-shadow:0 2px 6px rgba(0,0,0,.25);transition:left .25s var(--curva)}
  .t-inicio .chave.on{background:var(--ok)}.t-inicio .chave.on::after{left:18px}
  .t-inicio .no.fm{background:none;box-shadow:none;border:1px dashed var(--linha);cursor:default}
  .t-inicio .no.fm:hover{transform:none;box-shadow:none;background:rgba(255,255,255,.02);border-color:var(--fg3)}
  .t-inicio .no.fm .t svg{color:var(--fg3)}.t-inicio .no.fm .t b{color:var(--fg2)}
  .t-inicio .no.fm .vis{background:rgba(0,0,0,.2);opacity:.6}.t-inicio .no.fm .res{color:var(--fg2)}.t-inicio .no.fm .res2{color:var(--fg3)}
  .t-inicio .como{position:absolute;top:388px;left:268px;width:744px;text-align:center;font-size:12px;line-height:16px;color:var(--fg3)}
  @keyframes i-sobe{from{opacity:0;transform:translateY(12px)}}
  .t-inicio .sobe{animation:i-sobe .35s var(--curva) backwards}
  @keyframes i-vem-d{from{opacity:0;transform:translateX(48px)}}
  @keyframes i-vem-e{from{opacity:0;transform:translateX(-48px)}}
  .t-inicio .b.vem-d .no,.t-inicio .b.vem-d .fio,.t-inicio .b.vem-d .como{animation:i-vem-d .45s var(--curva) backwards}
  .t-inicio .b.vem-e .no,.t-inicio .b.vem-e .fio,.t-inicio .b.vem-e .como{animation:i-vem-e .45s var(--curva) backwards}
  .i-dica{position:fixed;z-index:99;max-width:300px;background:var(--bg2);border:var(--borda-vidro);border-radius:8px;padding:8px 12px;display:flex;flex-direction:column;gap:2px;font-size:12px;line-height:16px;color:var(--fg2);opacity:0;transform:translateY(-4px);transition:opacity .12s,transform .12s;pointer-events:none;box-shadow:0 8px 24px rgba(0,0,0,.4)}
  .i-dica.on{opacity:1;transform:none}.i-dica b{color:var(--fg);font-size:12px;font-weight:600}
  .i-toast{position:fixed;left:50%;top:56px;transform:translateX(-50%);z-index:70;background:var(--bg3);border-radius:20px;padding:8px 16px;font-size:13px;box-shadow:0 8px 24px rgba(0,0,0,.4);animation:i-toast .3s cubic-bezier(.34,1.56,.64,1);width:max-content;max-width:min(900px,90vw);text-align:center}
  @keyframes i-toast{from{opacity:0;transform:translate(-50%,-8px) scale(.96)}}`;
  document.head.insertAdjacentHTML('beforeend', `<style>${css}</style>`);

  const SV = {
    nota: '<path d="M9 18V5l11-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="17" cy="16" r="3"/>',
    baixa: '<path d="M12 4v11M7 10l5 5 5-5M5 20h14"/>',
    ouvido: '<path d="M7 9a5 5 0 0 1 10 0c0 3-3 4-3 7a3 3 0 0 1-6 0"/><path d="M10 9a2 2 0 0 1 4 0"/>',
    jogo: '<rect x="2.5" y="7" width="19" height="11" rx="5"/><path d="M7 11v3M5.5 12.5h3M16 12h.5M18 13.5h.5"/>',
  };
  const ico = (n) => (SV[n] ? `<svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${SV[n]}</svg>` : UI.ico(n));
  const q = (t) => String(t ?? '').replace(/[&<>"]/g, (c) => `&#${c.charCodeAt(0)};`);
  const S = App.estado;
  S.ligado = S.ligado || {};
  S.ordem = ['eq', 'efeitos'];   // fixed (old saved custom order reset)
  const ordem = () => S.ordem;   // Som 7.1 = dock toggle "7.1" (29/10), not a step

  // x of each card (4 px grid): source 168, 2 steps 360 (gap 24 like the old 3), output 168. Mic tab keeps the 5 places (MX/MW)
  const X = [52, 268, 652, 1060], LW = [168, 360, 360, 168], CY = 132 + 120, VW = LW[1] - 32, VH = 104;
  const MX = [52, 268, 532, 796, 1060], MW = [168, 216, 216, 216, 168], MVW = MW[1] - 32;
  const CH71 = [['FL', -30], ['C', 0], ['FR', 30], ['SR', 100], ['RR', 140], ['RL', -140], ['SL', -100], ['LFE', null]];
  const lista = (l) => new Intl.ListFormat('pt-BR').format(l);
  const curva = (g, w = VW, h = VH, esc = 12) => { if (!g || g.length < 2) g = [0, 0]; const pts = g.map((v, i) => [i / (g.length - 1) * w, Math.max(2, Math.min(h - 2, h / 2 - v / esc * (h / 2)))]);
    let d = `M0 ${pts[0][1]}`; for (let i = 1; i < pts.length; i++) { const [x0, y0] = pts[i - 1], [x1, y1] = pts[i], mx = (x0 + x1) / 2; d += ` C${mx} ${y0} ${mx} ${y1} ${x1} ${y1}`; } return d; };

  // live state (engine): motor {ligado, erro, saidas}, niveis {entrada, saidas[{pico}]}, s71auto {perfil, fonte}, cabo71 {on}
  const V = { vista: 'som', comparar: false, espera: '', somEm: 0, nv: [0, 0] };
  const motor = () => S.motor || {};
  const ligado = () => !!motor().ligado;
  const tocando = () => ligado() && performance.now() - V.somEm < 1500;
  const on = (id) => S.ligado[id] !== false;

  // what each step shows: [icon, name, visual, line 1, line 2, module piece the visual flies to, extra visual class]
  const MOD = {
    eq: () => { const e = S.eq2 || {}, aq = e.autoeq, g = e.modo === 'grafico' || !e.bandas ? e.g : e.bandas.map((b) => b.g);
      return ['eq', 'Equalizador', `<div class="tgs">${['equ', 'tone', 'limit'].map((t) => `<span class="${e[t] ? 'on' : ''}">${t.toUpperCase()}</span>`).join('')}</div><svg class="full" viewBox="0 0 ${VW} ${VH}" preserveAspectRatio="none"><defs><linearGradient id="i-gB" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="var(--som)"/><stop offset="1" stop-color="var(--som)" stop-opacity="0"/></linearGradient></defs>
        <path class="zero" d="M0 ${VH / 2}H${VW}"/><path class="area" d="${curva(g)} L${VW} ${VH} L0 ${VH}Z"/><path class="cv" d="${curva(g)}"/></svg>`,
        aq && aq.on !== false ? aq.nome : e.preset || 'Personalizado', aq && aq.on !== false ? 'AutoEQ do seu fone' : e.preset ? 'Preset' : 'Ajuste seu', '.curva']; },
    som71: () => { const s = S.s71 || {}, c = S.cabo71, ang = s.ang || {}, sala = s.sala ?? 50, rx = 50 + sala * .25, ry = 30 + sala * .1, cx = VW / 2, cy = VH / 2;
      const cxs = CH71.map(([id, a0]) => { const a = id in ang ? ang[id] : a0, r = (a ?? 0) * Math.PI / 180;
        const x = a === null ? cx + rx * .45 : cx + Math.sin(r) * rx, y = a === null ? cy + ry * .45 : cy - Math.cos(r) * ry;
        return `<i class="cx71 ${s.mudo?.[id] ? 'mudo' : ''}" style="left:${x.toFixed(1)}px;top:${y.toFixed(1)}px">${id}</i>`; }).join('');
      return ['sur', 'Som 7.1', `<i class="anel71" style="width:${rx * 2}px;height:${ry * 2}px"></i><i class="anel71" style="width:${rx * 1.1}px;height:${ry * 1.1}px"></i><div class="cab71">☺</div>${cxs}`,
        (s.auto ? 'Automático' : 'Manual') + ' · ' + ((s.auto && S.s71auto?.perfil) || s.perfil || 'Jogo'), c && c.suporte === false ? 'Jogos chegam em estéreo' : c && c.erro ? 'Cabo fora do 7.1' : [s.voz && 'Voz reforçada', s.sub && 'Graves no sub'].filter(Boolean).join(' · ') || 'Sem reforços', '.sala', '', c && c.suporte === false ? 'Seu cabo não recebe 7.1|' + c.motivo : '']; },
    // efeitos = Estéreo+ card (04/11), built whole by modulos/estereo.js (Est.html)
  };
  // mic path (em breve), same 5 places
  const sv = (d) => `<svg class="full" viewBox="0 0 ${MVW} ${VH}" preserveAspectRatio="none"><path class="zero" d="M0 ${VH / 2}H${MVW}"/><path class="cvm" d="${d}"/></svg>`;
  const linha = (f) => { let d = ''; for (let x = 0; x <= MVW; x += 2) d += (d ? 'L' : 'M') + x + ' ' + (VH / 2 + f(x)).toFixed(1); return d; };
  const MIC = [
    ['mic', 'Microfone', `<div class="app m">${ico('mic')}</div>`, 'Seu microfone', 'A sua voz entra aqui'],
    ['efeitos', 'Cancelar ruído', sv(linha((x) => x < MVW / 2 ? Math.sin(x * .35) * 14 + Math.sin(x * 2.1) * 10 + Math.sin(x * 5.3) * 6 : Math.sin(x * .35) * 20)), 'Tira teclado e ventilador', 'Só a sua voz passa'],
    ['ouvido', 'Alterar voz', sv(linha((x) => Math.sin(x / MVW * Math.PI * 2 * (x < MVW / 2 ? 3 : 7)) * 22)), 'Muda como você soa', 'Mais grave ou mais aguda'],
    ['eq', 'Equalizador', sv(curva([-4, -2, 0, 1, 2, 3, 3, 2, 0, -2], MVW)), 'Deixa a voz mais clara', 'Para quem está ouvindo'],
    ['jogo', 'Chamadas', `<div class="app m">${ico('jogo')}</div>`, 'O que os outros ouvem', 'Discord, jogos, chamadas'],
  ];

  // mic steps (modules of FAH.Microfone + mic EQ): [icon, name, visual, line 1, line 2]
  const liga = (v) => v !== false && v !== 0;
  const MODM = {
    ruido: () => { const r = S.ruido || {}; return ['ruido', 'Cancelar ruído', MIC[1][2], liga(r.ia) ? `Força ${r.forca ?? 70}%` : 'Só o portão de ruído', liga(r.gate) ? 'Abafa quando você para de falar' : 'Portão desligado']; },
    voz: () => { const v = S.voz || {}, nat = !v.tom && !v.tim && !v.rob && !v.eco && !v.rad;
      return ['voz', 'Alterar voz', MIC[2][2], nat ? 'Natural' : v.pre || 'Personalizada', nat ? 'Sua voz como ela é' : `Tom ${v.tom > 0 ? '+' : ''}${v.tom || 0} · timbre ${v.tim > 0 ? '+' : ''}${v.tim || 0}`]; },
    eqmic: () => { const e = S.eqmic || {}, m = S.vm || {}, g = e.modo === 'param' && e.bandas ? e.bandas.map((b) => b.g) : e.g;
      return ['eq', 'Equalizador', sv(curva(g || [-6, -4, -2, 0, 1, 2, 4, 4, 2, 0], MVW)), e.preset || 'Voz clara',
        [liga(m.ganho) && 'Volume sozinho', liga(m.corte) && 'Corta graves'].filter(Boolean).join(' · ') || 'Ferramentas desligadas']; },
  };
  const mic = () => S.mic || {};
  const micLig = () => !!mic().ligado;
  const motivoMic = () => mic().erro || 'Falta um cabo virtual para o microfone';
  const motivoSom = () => App.cabo.motivo() + ': o som do PC só passa por aqui com um cabo virtual.';
  // "Line 2 (Virtual Audio Cable)" -> "Line 2"
  const curto = (n) => /^(.*?)\s*\((.+)\)$/.exec(n || '')?.[1] || n || '';
  function noM(i, id) {
    const [ic, nome, vis, r1, r2] = MODM[id](), o = on(id), sem = !!mic().semCabo && !micLig();
    return `<div class="no mod sobe ${o ? '' : 'off'}${sem ? ' sem-cabo' : ''}" tabindex="0" data-card="${id}" data-lado="mic" style="left:${MX[i]}px;width:${MW[i]}px;animation-delay:${.06 + i * .05}s"
      data-dica="${sem ? `${nome}|${q(motivoMic())}` : `${nome}|Clique: abre · Chave: liga ou desliga|Desligado, a voz passa direto por aqui`}">
      <div class="t">${ico(ic).replace('<svg', '<svg data-voa="header .icone"')}<b data-voa="header h2">${nome}</b><button class="chave ${o ? 'on' : ''}" data-voa="header .c-switch" data-chave="${id}" aria-label="${nome}: ${o ? 'ligado' : 'desligado'}"></button></div>
      <div class="vis">${vis}</div><div class="res">${o ? q(r1) : 'Desligado'}</div><div class="res2">${o ? q(r2) : 'A voz passa direto'}</div></div>`;
  }
  function caminhoMic() {
    const m = mic(), lig = micLig(), real = (S.dispositivos?.mic || []).find((d) => d.p)?.n, entra = m.entrada || real || 'Seu microfone', ep = /^(.*?)\s*\((.+)\)$/.exec(entra);
    const sai = m.saida ? curto(m.saida) : 'Line 2';
    return `<div class="no ponta sobe" style="left:${MX[0]}px;width:${MW[0]}px" data-dica="De onde vem|O microfone de verdade. Muda pelo Mixer."><div class="t">${ico('mic')}<b>Microfone</b></div>
        <div class="vis"><div class="vu" data-dica="Sua voz|Entrando no FAH, agora"><i><b data-mm="0"></b></i></div></div><div class="res" title="${q(entra)}">${q(ep ? ep[2] : entra)}</div><div class="res2">${q(ep ? ep[1] : 'Padrão do Windows')}</div></div>
      ${['ruido', 'voz', 'eqmic'].map((id, k) => noM(k + 1, id)).join('')}
      <div class="no ponta sobe" style="left:${MX[4]}px;width:${MW[4]}px;animation-delay:.26s" data-dica="Para onde vai|Discord, jogos e chamadas ouvem a voz limpa pelo microfone «${q(sai)}». Ligado, ele vira o microfone padrão do Windows.">
        <div class="t">${ico('jogo')}<b>Chamadas</b></div><div class="vis"><div class="vu" data-dica="O que os outros ouvem|Saindo do FAH, agora"><i><b data-mm="1"></b></i></div></div>
        <div class="res">${lig ? 'Microfone ' + q(sai) : 'Desligado'}</div><div class="res2">${lig ? 'Discord, jogos, chamadas' : 'Sem limpeza'}</div></div>
      <div class="como sobe" style="animation-delay:.3s">${m.erro && !lig ? q(m.erro) : lig ? `A sua voz passa da esquerda para a direita. No Discord e nos jogos, deixe o microfone como Padrão (ou escolha «${q(sai)}»).` : 'Desligado: os programas ouvem o seu microfone direto, sem limpeza.'}</div>`;
  }

  let el = null;
  function fio() {
    const ini = X[0] + LW[0], fim = X[3], seg = (i) => `M${X[i] + LW[i]} ${CY}H${X[i + 1]}`, erro = motor().erro && !ligado() && !App.cabo.semSom();
    if (V.vista === 'mic') { const a = MX[0] + MW[0], z = MX[4], sg = (i) => `M${MX[i] + MW[i]} ${CY}H${MX[i + 1]}`;
      return micLig() ? `<svg class="fio" viewBox="0 0 1280 680"><path class="base" d="M${a} ${CY}H${z}"/>${[0, 1, 2, 3].map((i) => `<path class="corre m" d="${sg(i)}"/>`).join('')}</svg>`
        : `<svg class="fio" viewBox="0 0 1280 680"><path class="ghost m" d="M${a} ${CY}H${z}"/></svg>`; }
    const arco = `M${ini - 24} 132C${ini + 160} 64 ${fim - 160} 64 ${fim + 24} 132`, corre = tocando() && !V.comparar;
    return `<svg class="fio" viewBox="0 0 1280 680">
      <path class="base" d="M${ini} ${CY}H${fim}"/>
      ${[0, 1, 2].map((i) => erro && i === 0 ? '' : `<path class="corre" data-corre d="${seg(i)}" style="opacity:${corre ? 1 : 0}"/>`).join('')}${erro ? `<path class="quebra" d="${seg(0)}"/>` : ''}
      <path class="direto" d="${arco}"/>${V.comparar ? `<path class="corre" d="${arco}"/>` : ''}
      <text class="rot" x="640" y="116" text-anchor="middle">${V.comparar ? 'Agora o som vai direto, sem o FAH' : 'Desligado: o Windows toca direto no fone'}</text>
</svg>`;
  }
  function no(i, id) {
    if (id === 'efeitos') return Est.html(X[i], LW[i], .06 + i * .05);
    const [ic, nome, vis, r1, r2, voa = '', vc = '', d2 = ''] = MOD[id](), o = on(id), sem = App.cabo.semSom();
    return `<div class="no mod sobe ${o ? '' : 'off'}${sem ? ' sem-cabo' : ''}" tabindex="0" data-card="${id}" style="left:${X[i]}px;width:${LW[i]}px;animation-delay:${.06 + i * .05}s"
      data-dica="${sem ? `${nome}|${q(motivoSom())}` : `${nome}|Clique: abre · Chave: liga ou desliga|Desligado, o som passa direto por aqui`}">
      <div class="t">${ico(ic).replace('<svg', '<svg data-voa="header .icone"')}<b data-voa="header h2">${nome}</b><button class="chave ${o ? 'on' : ''}" data-voa="header .c-switch" data-chave="${id}" aria-label="${nome}: ${o ? 'ligado' : 'desligado'}"></button></div>
      <div class="vis ${vc}"${voa ? ` data-voa="${voa}"` : ''}>${vis}</div><div class="res">${o ? q(r1) : 'Desligado'}</div><div class="res2"${o && d2 && !sem ? ` data-dica="${q(d2)}" data-dica-ja` : ''}>${o ? q(r2) : 'O som passa direto'}</div></div>`;
  }
  const fm = ([ic, nome, vis, r1, r2], i) => `<div class="no fm sobe" style="left:${MX[i]}px;width:${MW[i]}px;animation-delay:${.06 + i * .05}s" data-dica="${nome} · em breve|${r1}" data-toast="O microfone ainda não está pronto">
    <div class="t">${ico(ic)}<b>${nome}</b></div><div class="vis">${vis}</div><div class="res">${r1}</div><div class="res2">${r2}</div></div>`;

  function desenhar() {
    const lig = ligado(), som = V.vista === 'som', m = motor(), semS = App.cabo.semSom(), erro = m.erro && !lig && !semS, toca = tocando();
    // Windows names look like "Fone de ouvido (VK S1 S)": card shows the model big, the kind under it
    const sNome = m.saidas?.[0] || (S.dispositivos?.som || []).find((d) => d.p)?.n || 'Seu fone', sp = /^(.*?)\s*\((.+)\)$/.exec(sNome);
    const saida = sp ? sp[2] : sNome, saidaSub = sp ? sp[1] : 'Padrão do Windows';
    const fonteNome = S.s71auto?.fonte, perfil = S.s71?.auto && on('som71') ? S.s71auto?.perfil || S.s71?.perfil : '';
    const ml = micLig(), mm = mic();
    const semCabo = !som && !!mm.semCabo && !ml;
    const sub = !som ? (V.espera || (semCabo ? 'Falta um cabo virtual para o microfone' : mm.erro && !ml ? 'O microfone não ligou' : ml ? 'Limpando a sua voz para as chamadas e jogos' : 'Cancelar ruído, alterar voz e equalizar o que os outros ouvem de você.')) : V.espera ? V.espera : semS ? App.cabo.motivo() + ': o som vai direto para o fone' : erro ? 'O som não ligou: ' + q(m.erro) : !lig ? 'Desligado'
      : V.comparar ? 'Ouvindo sem o FAH. Solte para voltar.' : toca ? `${q(fonteNome || 'Som tocando')} → ${q(saida)}` : `Nada tocando · pronto em ${q(saida)}`;
    const fonte = toca
      ? `<div class="vis"><div class="app">${ico('nota')}</div></div><div class="res">${q(fonteNome || 'Som do PC')}</div><div class="res2">${perfil ? `<span class="chip" data-dica="Perfil automático do Som 7.1|${q(fonteNome || 'O que está tocando')} pede ${q(perfil)}">${ico('nota')}${q(perfil)}</span>` : 'Tocando agora'}</div>`
      : `<div class="vis"><div class="app vazio">${ico('nota')}</div></div><div class="res" style="color:var(--fg2)">Nada tocando</div><div class="res2">Esperando som</div>`;
    const abas = `<div class="abas" role="tablist"><button class="${som ? 'on' : ''}" role="tab" data-vista="som" data-dica="Som|O caminho do som até o seu fone">Som</button>
      <button class="mic ${som ? '' : 'on'}" role="tab" data-vista="mic" data-dica="Microfone|O caminho da sua voz até as chamadas e jogos">Microfone</button></div>`;
    // no cable: dimmed but hoverable (a native disabled button shows no tip), click repeats the reason
    const trava = (ok, sem) => (ok ? '' : sem ? 'aria-disabled="true"' : 'disabled');
    const comparar = !som && semCabo && mm.podeInstalar ? `<button class="pilula forte" id="micinst" data-dica="Instalar cabo do microfone|O FAH baixa o Virtual Audio Cable Lite, que é grátis, e abre o instalador dele.|Aceite a licença e deixe a pasta como está">${ico('baixa')}Instalar cabo do microfone</button>`
      : !som ? `<button class="pilula ${mm.ouvirSai ? 'ouve' : ''}${semCabo ? ' apagada' : ''}" id="ouvirsai" ${trava(ml, semCabo)} data-dica="${semCabo ? 'Ouvir o que sai no microfone|' + q(motivoMic()) : 'Ouvir o que sai no microfone|Você ouve no fone tudo o que a chamada recebe: sua voz e os programas mandados para o microfone.|Use fone para não dar microfonia'}">${ico('ouvido')}Ouvir o que sai</button><button class="pilula ${mm.ouvir ? 'ouve' : ''}${semCabo ? ' apagada' : ''}" id="ouvir" ${trava(ml, semCabo)} data-dica="${semCabo ? 'Ouvir minha voz|' + q(motivoMic()) : 'Ouvir minha voz|Você ouve no fone a voz como os outros vão ouvir.|Use fone para não dar microfonia'}">${ico('ouvido')}Ouvir minha voz</button>` : erro ? `<button class="pilula forte" id="tenta">Tentar de novo</button>`
      : `<button class="pilula ${V.comparar ? 'segura' : ''}${semS ? ' apagada' : ''}" id="cmp" ${trava(lig, semS)} data-dica="${semS ? 'Comparar|' + q(motivoSom()) : 'Comparar|Segure para ouvir sem o FAH. Solte e ele volta.|Atalho: segure C'}">${ico('ouvido')}Segure para comparar</button>`;
    const caminho = som
      ? `<div class="no ponta sobe" style="left:${X[0]}px;width:${LW[0]}px" data-dica="De onde vem|O programa que está tocando som agora"><div class="t">${ico('nota')}<b>Tocando</b></div>${fonte}</div>
        ${ordem().map((id, k) => no(k + 1, id)).join('')}
        <div class="no ponta sobe" style="left:${X[3]}px;width:${LW[3]}px;animation-delay:.26s" data-dica="Para onde vai|O fone ou caixa onde você ouve. Muda junto com o padrão do Windows."><div class="t">${ico('fone')}<b>Saída</b></div>
          <div class="vis"><div class="vu" data-dica="Nível do som|Entrando no FAH e saindo no fone, agora"><i><b data-m="0"></b></i><i><b data-m="1"></b></i></div></div><div class="res" title="${q(sNome)}">${q(saida)}</div><div class="res2">${q(saidaSub)}</div></div>
        <div class="como sobe" style="animation-delay:.3s">O som passa da esquerda para a direita. Etapa desligada: o som passa direto por ela.</div>`
      : caminhoMic();
    const ld = som ? lig : ml, pl = V.espera ? 'espera' : ld ? 'on' : '';
    el.innerHTML = `<div class="b ${som && V.comparar ? 'cmp' : ''} ${(som ? !lig : !ml) ? 'desl' : ''}">${fio()}
      <div class="b-topo sobe">${abas}<span class="sub ${som && erro && !V.espera ? 'erro' : ''}">${sub}</span><div class="esp"></div>${comparar}
        <button class="pilula ${pl}${semCabo || (som && semS) ? ' apagada' : ''}" id="liga"${semCabo || (som && semS) ? ' aria-disabled="true"' : ''} data-dica="${semCabo ? 'Falta um cabo virtual|' + q(mm.erro || '') : som && semS ? App.cabo.motivo() + '|Clique para ver o que fazer' : som ?(lig ? 'Ligado|Clique para desligar: o Windows toca direto no fone' : 'Desligado|Clique para ligar') : ml ? 'Ligado|Clique para desligar: os programas voltam a ouvir o microfone direto' : 'Desligado|Clique para ligar: o FAH vira o microfone padrão do Windows'}"><i class="pt"></i>${ld ? 'Ligado' : 'Desligado'}</button></div>
      ${caminho}</div>`;
    el.querySelectorAll('.sem-cabo button').forEach((b) => (b.tabIndex = -1));   // Tab stops on the card, not on what is off inside it
    V.toca = toca;
  }
  // redraw without replaying entrances, keeping focus
  function redesenhar() { if (!el) return; const f = document.activeElement?.closest?.('.t-inicio [data-card],.t-inicio [data-vista],.t-inicio #liga');
    const k = f && (f.dataset.card ? `[data-card="${f.dataset.card}"]` : f.dataset.vista ? `[data-vista="${f.dataset.vista}"]` : '#liga');
    const antes = {}; el.querySelectorAll('[data-cx],[data-anel]').forEach((x) => { antes[x.dataset.cx || 'a' + x.dataset.anel] = [x.offsetLeft, x.offsetTop, x.offsetWidth, x.offsetHeight]; });
    desenhar(); el.querySelectorAll('.sobe').forEach((x) => (x.style.animation = 'none')); if (k) el.querySelector(k)?.focus();
    // 7.1 speakers/rings glide from where they were (profile switch moves them)
    el.querySelectorAll('[data-cx],[data-anel]').forEach((x) => { const a = antes[x.dataset.cx || 'a' + x.dataset.anel]; if (!a) return;
      const dx = a[0] - x.offsetLeft, dy = a[1] - x.offsetTop, sx = a[2] / (x.offsetWidth || 1), sy = a[3] / (x.offsetHeight || 1);
      if (Math.abs(dx) < .5 && Math.abs(dy) < .5 && Math.abs(sx - 1) < .01 && Math.abs(sy - 1) < .01) return;
      const base = getComputedStyle(x).transform, t = base === 'none' ? '' : base;
      x.animate([{ transform: `translate(${dx}px,${dy}px) ${t} scale(${sx},${sy})` }, { transform: t || 'none' }], { duration: 700, easing: 'cubic-bezier(.3,0,.2,1)' }); }); }
  function trocar(v) { if (v === V.vista) return; V.vista = v; soltarComparar(); desenhar();
    el.querySelector('.b-topo').style.animation = 'none'; el.querySelector('.b').classList.add(v === 'mic' ? 'vem-d' : 'vem-e'); el.querySelector(`[data-vista="${v}"]`).focus(); }

  // tooltip + toast (own balloon after 600 ms still, 150 ms with data-dica-ja, always inside the window; Mixer uses it too)
  const dica = document.createElement('div'); dica.className = 'i-dica'; document.body.append(dica); let tD = 0, alvo = null;
  const fechaDica = () => { clearTimeout(tD); dica.classList.remove('on'); alvo = null; };
  addEventListener('mouseover', (e) => { const a = e.target.closest?.('.t-inicio [data-dica],.t-mesa [data-dica],.cb [data-dica],.e3d [data-dica]'); if (a === alvo) return; fechaDica(); alvo = a;
    if (!a) return; tD = setTimeout(() => { const [t, ...l] = a.dataset.dica.split('|'); dica.innerHTML = `<b>${t}</b>${l.map((x) => `<span>${x}</span>`).join('')}`;
      const r = a.getBoundingClientRect(); dica.style.left = '0px'; dica.style.top = '0px'; dica.classList.add('on');
      const w = dica.offsetWidth, h = dica.offsetHeight; const x = r.left + r.width / 2 - w / 2; let y = r.bottom + 8; if (y + h > innerHeight - 8) y = r.top - h - 8;
      dica.style.left = Math.max(8, Math.min(innerWidth - w - 8, x)) + 'px'; dica.style.top = y + 'px'; }, a.dataset.dicaJa !== undefined ? 150 : 600); });
  addEventListener('mousedown', fechaDica);
  let tT = 0;
  const toast = (msg, ms = 2200) => { document.querySelector('.i-toast')?.remove(); const t = document.createElement('div'); t.className = 'i-toast'; t.textContent = msg;
    document.body.append(t); clearTimeout(tT); tT = setTimeout(() => t.remove(), ms); };
  App.toast = toast;                                   // other screens (mic) use the same short notice

  // hold = hear without FAH (engine "comparar" = whole chain bypassed)
  const comparar = (v) => { if (V.comparar === v) return; V.comparar = v; App.motor.enviar({ tipo: 'comparar', on: v }); redesenhar(); };
  let segC = false;
  const soltarComparar = () => { segC = false; comparar(false); };
  addEventListener('keydown', (e) => { if (S.tela !== 'inicio' || V.vista !== 'som' || e.key.toLowerCase() !== 'c' || e.repeat || e.ctrlKey || e.altKey || !ligado()) return; segC = true; comparar(true); });
  addEventListener('keyup', (e) => { if (e.key.toLowerCase() === 'c' && segC) soltarComparar(); });
  addEventListener('blur', soltarComparar);

  // live: meters + speakers + "tocando" switch (redraw only when it flips)
  const nv = (x) => Math.max(0, Math.min(1, x || 0));
  addEventListener('fah:niveis', (e) => { const d = e.detail || {}, a = nv(d.entrada), b = nv(d.saidas?.[0]?.pico);
    if (a > .003) V.somEm = performance.now(); V.nv = [Math.max(a, V.nv[0] * .7), Math.max(b, V.nv[1] * .7)];
    V.nm = [Math.max(nv(d.mic), (V.nm?.[0] || 0) * .7), Math.max(nv(d.micSai), (V.nm?.[1] || 0) * .7)]; });
  let ultAviso71 = '';
  addEventListener('fah:cabo71', (e) => { const c = e.detail || {}, t = c.erro || c.msg || ''; if (t === ultAviso71) return; ultAviso71 = t; if (t && App.toast) App.toast(t, 5000); });
  ['motor', 's71auto', 'cabo71', 's71liga', 'mic', 'cabo'].forEach((t) => addEventListener('fah:' + t, () => { if (t === 'motor' || t === 'mic') V.espera = ''; if (!ligado()) V.comparar = false; if (S.tela === 'inicio') redesenhar(); }));
  const vivo = () => { requestAnimationFrame(vivo); if (S.tela !== 'inicio' || !el) return;
    if (V.vista === 'mic') { const lig = micLig(); el.querySelectorAll('[data-mm]').forEach((m) => (m.style.height = (lig ? Math.sqrt(V.nm?.[+m.dataset.mm] || 0) * 100 : 0).toFixed(1) + '%')); return; }
    if (tocando() !== V.toca) redesenhar();
    const t = performance.now(), sem = !tocando() || V.comparar;
    el.querySelectorAll('[data-m]').forEach((m) => (m.style.height = (sem && !V.comparar ? 0 : Math.sqrt(V.nv[+m.dataset.m]) * 100).toFixed(1) + '%'));
    const lv = Math.sqrt(V.nv[0]);
    el.querySelectorAll('.cx71').forEach((x, i) => x.classList.toggle('toca', !sem && on('som71') && lv > .25 && ((t / 180 | 0) + i * 5) % 7 < 2 + lv * 3)); };
  requestAnimationFrame(vivo);

  // card -> module flight (modulo.js): card box + its marked pieces (data-voa = where each lands in the module screen), relative to #palco.
  // Layout boxes, not screen boxes: hover lift/press shrink must not leak into the flight.
  function abrir(c) { const r0 = c.getBoundingClientRect(), k = c.offsetWidth / r0.width, x = c.offsetLeft, y = c.offsetTop;
    const pecas = [...c.querySelectorAll('[data-voa]')].map((p) => { const r = p.getBoundingClientRect();
      return { alvo: p.dataset.voa, src: p, de: { x: x + (r.left - r0.left) * k, y: y + (r.top - r0.top) * k, w: r.width * k, h: r.height * k } }; });
    fechaDica(); App.ir('modulo', { id: c.dataset.card, lado: c.dataset.lado || 'som', de: 'inicio', origem: { id: c.dataset.card, caixa: { x, y, w: c.offsetWidth, h: c.offsetHeight }, pecas } }); }

  App.tela({ id: 'inicio', classe: 't-inicio',
    montar(e, p = {}) {
      const novo = el !== e; el = e;
      if (p.volta && p.lado) V.vista = p.lado === 'mic' ? 'mic' : 'som';       // back from a mic module: mic tab again
      desenhar();
      if (p.volta) el.querySelectorAll('.sobe').forEach((x) => (x.style.animation = 'none'));   // back from a module: card is the landing spot, no entrance
      App.motor.enviar({ tipo: 'cabo71?' });
      if (!novo) return;
      el.addEventListener('pointerdown', (ev) => { const b = ev.target.closest('#cmp'); if (!b || ev.button || !ligado()) return; comparar(true);
        const sol = () => { soltarComparar(); removeEventListener('pointerup', sol); }; addEventListener('pointerup', sol); });
      el.addEventListener('click', (ev) => {
        const t = ev.target.closest('[data-toast]'); if (t) { toast(t.dataset.toast); return; }
        const v = ev.target.closest('[data-vista]'); if (v) { trocar(v.dataset.vista); return; }
        if (ev.target.closest('#cmp.apagada')) { toast(motivoSom(), 5000); return; }
        if (ev.target.closest('#ouvirsai.apagada,#ouvir.apagada')) { toast(motivoMic(), 5000); return; }
        if (ev.target.closest('#ouvirsai')) { App.motor.enviar({ tipo: 'micouvirsai', on: !mic().ouvirSai }); return; }
        if (ev.target.closest('#ouvir')) { App.motor.enviar({ tipo: 'micouvir', on: !mic().ouvir }); return; }
        // Som without cable: opens the cable screen. Mic without 2nd cable: greyed, click only re-checks quietly (cable may have just been installed) and repeats the reason
        if (ev.target.closest('#liga.apagada') && V.vista === 'som') { fechaDica(); App.cabo.abrir(); return; }
        if (ev.target.closest('#liga.apagada')) { toast(mic().erro || 'Falta um cabo virtual para o microfone', 5000); App.motor.enviar({ tipo: 'micligar' }); return; }
        if (ev.target.closest('#liga') && V.vista === 'mic') { const l = micLig(); V.espera = l ? 'Desligando…' : 'Ligando…'; App.motor.enviar({ tipo: l ? 'micdesligar' : 'micligar' });
          if (!App.motor.conectado) V.espera = ''; redesenhar(); return; }
        if (ev.target.closest('#liga')) { const l = ligado(); V.espera = l ? 'Desligando…' : 'Ligando…'; App.motor.enviar({ tipo: l ? 'desligar' : 'ligar' });
          if (!App.motor.conectado) V.espera = ''; redesenhar(); return; }
        if (ev.target.closest('#micinst')) { App.motor.enviar({ tipo: 'cabo-mic-instalar' }); return; }
        if (ev.target.closest('#tenta')) { V.espera = 'Ligando…'; App.motor.enviar({ tipo: 'ligar' }); if (!App.motor.conectado) V.espera = ''; redesenhar(); return; }
        const sc = ev.target.closest('.sem-cabo'); if (sc) { ev.stopPropagation(); toast(sc.dataset.lado === 'mic' ? motivoMic() : motivoSom(), 5000); return; }
        if (ev.target.closest('[data-card=efeitos]')) { ev.stopPropagation(); Est.clique(ev); return; }   // Estéreo+: no screen, handles itself
        const ch = ev.target.closest('[data-chave]'); if (ch) { ev.stopPropagation(); S.ligado[ch.dataset.chave] = !on(ch.dataset.chave); redesenhar(); return; }
        const c = ev.target.closest('[data-card]'); if (c) abrir(c);
      });
      el.addEventListener('keydown', (ev) => {
        const c = ev.target.closest('[data-card]');
        if (!c) { const v = ev.target.closest('[data-vista]'); if (v && (ev.key === 'ArrowRight' || ev.key === 'ArrowLeft')) trocar(ev.key === 'ArrowRight' ? 'mic' : 'som'); return; }
        if (c.classList.contains('sem-cabo') && (ev.key === 'Enter' || ev.key === ' ')) { ev.preventDefault(); toast(c.dataset.lado === 'mic' ? motivoMic() : motivoSom(), 5000); return; }
        if (c.dataset.card === 'efeitos' && !c.classList.contains('sem-cabo') && Est.tecla(ev)) return;
        if (ev.key === 'Enter') { abrir(c); return; }
        if (ev.key === ' ') { ev.preventDefault(); c.querySelector('.chave').click(); return; }
        if (ev.key !== 'ArrowRight' && ev.key !== 'ArrowLeft') return;
        ev.preventDefault(); const d = ev.key === 'ArrowRight' ? 1 : -1;
        const l = [...el.querySelectorAll('[data-card]')], i = l.indexOf(c); l[Math.max(0, Math.min(l.length - 1, i + d))].focus(); });
      el.tabIndex = -1;
    },
    aoSair() { soltarComparar(); fechaDica(); Est.fechar(); } });
  App.dock({ id: 'inicio', icone: 'casa', dica: 'Início', tela: 'inicio', ordem: 1 });
})();
