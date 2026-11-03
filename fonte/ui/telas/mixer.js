// Screen: Mixer = "Mesa de som" (approved 28/10, docs/prototipos/mixer/ESCOLHIDA). Each device = vertical strip (fader + L/R meter + mute + info; icon click = Windows default, default wears a green banner)
// and its BOX: programs pinned to play/listen ONLY there. Bottom: programs making sound now; drag one onto a strip = pin it there.
// Engine (FAH.exe, Mesa.cs) sends "mixer" (snapshot) and "mixniv" (levels, 10/s) only while this screen is visible.
// Plain browser (prototype tests): sample data + simulated levels. Address #muitos | #vazio | #mudo | #caixa.
(() => {
  const E = App.estado, real = App.motor.conectado, manda = (m) => App.motor.enviar(m);
  const h = location.hash.slice(1).split(','), tem = (x) => h.includes(x);
  let S = { som: [], mic: [], soltos: [] };

  // ---- sample (plain browser only)
  const amostra = () => { const P = (n, lado = 'som', v = 100, m = 0, s = 0) => ({ x: n.toLowerCase().replace(/\W/g, ''), n, lado, v, m, s });
    const D = (id, n, t, ic, f, c, vol, fixos = [], p = 0) => ({ id, n, t, ic, f, c, vol, fixos, p, m: 0 });
    const s = { som: [D('fone', 'Fone de ouvido', 'Saída', 'fone', '48 kHz', 'Estéreo', 72, [], 1), D('caixa', 'Caixas de som', 'Saída', 'caixa', '48 kHz', 'Estéreo', 40),
      D('tv', 'LG HDR 4K', 'Saída', 'caixa', '48 kHz', '7.1', 100, [P('Valorant', 'som', 70, 0, 1), P('Steam', 'som', 50)])],
    mic: [D('micfone', 'Mic do fone', 'Microfone', 'mic', '48 kHz', 'Mono', 80, [], 1), D('cam', 'Webcam C920', 'Microfone', 'mic', '32 kHz', 'Estéreo', 65, [P('OBS', 'mic')])],
    soltos: [P('Spotify'), P('Chrome', 'som', 80), P('Discord'), P('Discord', 'mic')] };
    if (tem('muitos')) { s.som.push(D('bt', 'WH-1000XM4', 'Saída', 'fone', '44,1 kHz', 'Estéreo', 55), D('steam', 'Steam Streaming Speakers', 'Saída', 'caixa', '48 kHz', 'Estéreo', 100),
      D('mon2', 'DELL P2422H', 'Saída', 'caixa', '48 kHz', 'Estéreo', 80, [P('YouTube Music')]), D('ifc', 'Focusrite Scarlett 2i2', 'Saída', 'caixa', '96 kHz', 'Estéreo', 90));
      s.soltos.push(P('Edge', 'som', 60), P('WhatsApp'), P('Spotify Web', 'som', 90)); }
    // two sample icons (real ones come from the exe), the rest keep the letter
    const IC = (c) => 'data:image/svg+xml,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48"><circle cx="24" cy="24" r="22" fill="${c}"/><path d="M14 19c7-2 14-1 20 2M15 25c6-1.5 11-1 16 1.5M16 31c5-1 9-.5 13 1" stroke="#000" stroke-width="3" fill="none" stroke-linecap="round"/></svg>`);
    s.soltos[1].ic = IC('#4c8bf5'); s.mic[1].fixos[0].ic = IC('#a970ff');
    if (tem('vazio')) s.soltos = [];
    if (tem('mudo')) { s.som[0].m = 1; s.soltos[1].m = 1; }
    return s; };
  if (!real) S = amostra();
  if (!real && tem('bypass')) App.estado.bypass = { som: ['tv', 'fone'], mic: ['micfone', 'cam'], ativos: ['som|tv', 'mic|micfone'] };

  document.head.insertAdjacentHTML('beforeend', `<style>
  .t-mesa{display:flex;flex-direction:column;align-items:flex-start;gap:16px;padding-bottom:12px!important}
  /* divider in the middle. Sound devices start at the left edge, mic devices end at the right edge; too many = the strip scrolls (wheel too),
     faded edge where more hide. Each side's Windows default is no card: it is the banner hanging from the top of the divider, its edge 2px
     off it, with the device drawn on the cloth (no box: programs on the default are the row below) */
  .t-mesa .mesa{flex:none;align-self:stretch;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);padding:4px 4px 8px;margin:-4px -4px 0;box-sizing:border-box;--bw:136px}
  .t-mesa .lado{position:relative;display:flex;flex-direction:column;gap:12px;min-width:0}
  .t-mesa .lado:first-child{padding-right:40px;border-right:1px solid var(--linha)}.t-mesa .lado+.lado{padding-left:40px;align-items:flex-end}
  .t-mesa .lado.com-band{min-height:422px}.t-mesa .lado.com-band:first-child{padding-right:calc(var(--bw) + 30px)}.t-mesa .lado.com-band:last-child{padding-left:calc(var(--bw) + 30px)}
  .t-mesa .outros{position:relative;display:flex;gap:12px;max-width:calc(100% + 8px);min-width:0;box-sizing:border-box;overflow-x:auto;overflow-y:hidden;padding:6px 4px 10px;margin:-6px -4px -10px}
  .t-mesa .outros.mais-e{-webkit-mask-image:linear-gradient(to right,transparent,#000 40px)}.t-mesa .outros.mais-d{-webkit-mask-image:linear-gradient(to left,transparent,#000 40px)}
  .t-mesa .outros.mais-e.mais-d{-webkit-mask-image:linear-gradient(to right,transparent,#000 40px calc(100% - 40px),transparent)}
  .t-mesa.quieto .tira{animation:none}.t-mesa.quieto .band,.t-mesa.quieto .band *{transition:none!important}
  .t-mesa h3{display:flex;align-items:center;gap:12px;font-size:var(--t-grupo);font-weight:700;letter-spacing:-.4px;margin:0;height:30px}
  .t-mesa .tiras{display:flex;gap:16px;min-width:0}.t-mesa .sem{width:112px;height:380px;box-sizing:border-box;border-radius:var(--r-grupo);box-shadow:inset 0 0 0 1.5px var(--linha);display:grid;place-items:center;text-align:center;font-size:12px;color:var(--fg3);padding:8px}
  .t-mesa .tira{position:relative;width:112px;height:380px;box-sizing:border-box;background:var(--bg2);border-radius:var(--r-grupo);padding:16px 8px 8px;display:flex;flex-direction:column;align-items:center;gap:8px;
    box-shadow:inset 0 0 0 2px transparent;transition:box-shadow .2s,background .2s,opacity .25s,transform .25s var(--curva);animation:msEntra .4s var(--curva) both}
  @keyframes msEntra{from{opacity:0;transform:translateY(10px)}}
  .t-mesa .tira b{font-size:13px;font-weight:600;text-align:center;line-height:16px;height:32px;overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;word-break:break-word;padding:0 4px}
  .t-mesa .tira .pd{height:16px;font-size:11px;font-weight:600;color:var(--ac)}
  .t-mesa .fd{flex:1;display:flex;gap:12px;align-items:stretch;padding:8px 0}
  .t-mesa .vf{position:relative;width:28px;cursor:ns-resize;touch-action:none}
  .t-mesa .vf::before{content:"";position:absolute;left:12px;width:4px;top:0;bottom:0;border-radius:2px;background:var(--controle-forte)}
  .t-mesa .vf .nv{position:absolute;left:12px;width:4px;bottom:0;border-radius:2px;background:var(--ac)}
  .t-mesa .vf .bt{position:absolute;left:2px;width:24px;height:12px;margin-bottom:-6px;border-radius:4px;background:#fff;box-shadow:0 2px 6px rgba(0,0,0,.35)}
  .t-mesa .vf:focus-visible{outline:0}.t-mesa .vf:focus-visible .bt{box-shadow:0 0 0 3px var(--ac)}
  .t-mesa .med{display:flex;gap:4px}.t-mesa .med i{position:relative;width:4px;border-radius:2px;background:var(--controle)}
  .t-mesa .med i::after{content:"";position:absolute;left:0;right:0;bottom:0;height:calc(var(--n,0)*100%);border-radius:2px;background:linear-gradient(to top,var(--ok) 70%,#ffd60a 88%,var(--perigo));transition:height .09s linear}
  .t-mesa .med i::before{content:"";position:absolute;left:-1px;right:-1px;height:2px;bottom:calc(var(--pk,0)*100%);background:var(--fg2);border-radius:1px;opacity:min(1,calc(var(--pk,0)*40))}
  .t-mesa .vl{font-size:13px;color:var(--fg2);font-variant-numeric:tabular-nums;height:16px}
  .t-mesa .bts{display:flex;gap:8px}
  .t-mesa .eng{border:0;width:28px;height:28px;border-radius:50%;background:var(--controle);color:var(--fg2);cursor:help;display:grid;place-items:center;padding:0}
  .t-mesa .eng:hover,.t-mesa .eng:focus-visible{color:var(--fg)}.t-mesa .eng svg{width:15px;height:15px}
  /* Windows default = war banner (Elden Ring style): soft green cloth with gold embroidered border and damask, torn swallowtail, hung from a gold
     crossbar at the top of the divider, ~78% of its height. The device is drawn on it: gold emblem, cream name, gold-thread fader/meter, ringed buttons.
     Default changes: the cloth rolls up with the old device and falls with the new one */
  .t-mesa .tira.band{position:absolute;top:0;width:var(--bw);height:372px;padding:0;background:none;border-radius:0;box-shadow:none;animation:none;z-index:2;pointer-events:none}
  .t-mesa .lado:first-child .band{right:2px}.t-mesa .lado:last-child .band{left:2px}
  .t-mesa .estandarte{position:absolute;inset:0;pointer-events:none;filter:drop-shadow(0 6px 6px rgba(0,0,0,.5));transition:filter .2s}
  .t-mesa .estandarte .varao{position:absolute;left:4px;right:4px;top:0;height:5px;border-radius:2px;background:linear-gradient(#fbe7a6,#c9a24a 55%,#6e5218);animation:bdVarao .35s var(--curva) both}
  @keyframes bdVarao{from{opacity:0;transform:scaleX(.6)}}
  .t-mesa .estandarte .varao::before,.t-mesa .estandarte .varao::after{content:"";position:absolute;top:-1.5px;left:-4px;width:8px;height:8px;transform:rotate(45deg);border-radius:1px;background:linear-gradient(135deg,#fff3c4,#c9a24a 50%,#5e4513)}
  .t-mesa .estandarte .varao::after{left:auto;right:-4px}
  /* the device is sewn on the cloth (29/10): .pano = cloth + device, so both roll up, fall and sway together */
  .t-mesa .estandarte .pano{position:absolute;left:0;right:0;top:3px;bottom:0;transform:scaleY(0);transform-origin:top;transition:transform .4s cubic-bezier(.55,0,.8,.2)}
  .t-mesa .band.desce .estandarte .pano{transform:none;transition:transform 1s .1s cubic-bezier(.3,1.35,.55,1)}
  .t-mesa .estandarte .brisa{position:absolute;inset:0;transform-origin:50% 0}
  /* sway = JS weather (sopra): each banner calm / breeze / gusts for random stretches; they may overlap, the last one under the mouse stays in front */
  .t-mesa .band.frente{z-index:3}
  .t-mesa .estandarte .brisa>svg{position:absolute;inset:0;width:100%;height:100%;overflow:visible}
  .t-mesa .bd-cont{position:absolute;left:0;right:0;top:15px;height:288px;display:flex;flex-direction:column;align-items:center;gap:4px;padding:0 14px;box-sizing:border-box}
  .t-mesa .band.desce .bd-cont{pointer-events:auto}
  .t-mesa .bd-emb{flex:none;width:44px;height:44px;border-radius:50%;display:grid;place-items:center;color:#f3dc9a;background:radial-gradient(circle at 50% 35%,#356b4b,#1d3f2b);
    box-shadow:inset 0 0 0 1.5px #c9a24a,inset 0 0 0 4px rgba(0,0,0,.3),inset 0 0 0 5px rgba(233,207,130,.35),0 2px 5px rgba(0,0,0,.45)}
  .t-mesa .bd-emb svg{width:24px;height:24px;filter:drop-shadow(0 1px 0 rgba(0,0,0,.7))}
  .t-mesa .band b{margin-top:4px;color:#fbf1cf;text-shadow:0 1px 2px rgba(0,0,0,.75);max-width:100%;box-sizing:border-box}
  .t-mesa .band .pd{height:12px;font-size:9px;font-weight:700;letter-spacing:1.4px;color:#e9cf82;text-shadow:0 1px 2px rgba(0,0,0,.7)}
  .t-mesa .band .fd{padding:6px 0 4px;gap:10px}
  .t-mesa .band .vf::before{background:rgba(0,0,0,.42);box-shadow:0 0 0 1px rgba(233,207,130,.4)}.t-mesa .band .vf .nv{background:linear-gradient(to top,#a8832f,#f6df9a)}
  .t-mesa .band .vf .bt{background:linear-gradient(#fbe7a6,#c9a24a 60%,#8a6a24);box-shadow:0 1px 3px rgba(0,0,0,.6),inset 0 0 0 1px rgba(255,255,255,.3)}
  .t-mesa .band .med i{background:rgba(0,0,0,.42);box-shadow:0 0 0 1px rgba(233,207,130,.28)}
  .t-mesa .band .bts{align-items:center;gap:6px}.t-mesa .band .vl{width:40px;text-align:center;color:#fbf1cf;text-shadow:0 1px 2px rgba(0,0,0,.7);height:auto}
  .t-mesa .band .c-mudo,.t-mesa .band .eng{background:rgba(0,0,0,.3);color:#f3dc9a;box-shadow:inset 0 0 0 1.2px rgba(233,207,130,.6)}
  .t-mesa .band .c-mudo:hover,.t-mesa .band .eng:hover,.t-mesa .band .eng:focus-visible{background:rgba(0,0,0,.48);color:#fff}.t-mesa .band .c-mudo.on{background:var(--perigo);color:#fff;box-shadow:none}
  .t-mesa .bd-fixos{display:flex;gap:3px;flex:none}.t-mesa .bd-fixos .mx-av{background:rgba(0,0,0,.35);color:#fbf1cf;box-shadow:inset 0 0 0 1px rgba(233,207,130,.5)}
  /* dragging a pinned program: the banner takes it (= back to the default, like the row) */
  .t-mesa.arr .band.alvo .estandarte{filter:drop-shadow(0 0 5px rgba(246,223,154,.55)) drop-shadow(0 6px 6px rgba(0,0,0,.5))}
  .t-mesa.arr .band.sobre .estandarte{filter:drop-shadow(0 0 12px rgba(246,223,154,.95)) drop-shadow(0 6px 6px rgba(0,0,0,.5))}
  .t-mesa .pdb{border:0;padding:0;background:none;border-radius:var(--r-icone);cursor:pointer;display:block}.t-mesa .pdb:focus-visible{outline:0}
  .t-mesa .pdb:hover .icone,.t-mesa .pdb:focus-visible .icone{animation:pdPulsa 1.6s ease-in-out infinite}
  @keyframes pdPulsa{0%,100%{box-shadow:0 0 0 2px color-mix(in srgb,var(--ok) 22%,transparent),0 0 6px color-mix(in srgb,var(--ok) 12%,transparent)}50%{box-shadow:0 0 0 4px color-mix(in srgb,var(--ok) 42%,transparent),0 0 16px color-mix(in srgb,var(--ok) 30%,transparent)}}
  .t-mesa .calado .fd,.t-mesa .calado .tira-vl{opacity:.4}
  .mx-av{display:grid;place-items:center;flex:none;border-radius:8px;background:var(--controle);color:var(--fg2);font-weight:700}
  /* the box button: pinned programs (stacked faces + count); empty = quiet slot that wakes up while dragging */
  .t-mesa .cx{width:96px;height:40px;box-sizing:border-box;border:0;border-radius:12px;background:var(--controle);color:var(--fg);display:flex;align-items:center;justify-content:center;gap:4px;cursor:pointer;
    font:600 12px var(--fonte);transition:background .2s,box-shadow .2s,transform .25s var(--curva);padding:0 8px}
  .t-mesa .cx:hover,.t-mesa .cx:focus-visible{background:var(--controle-forte);outline:0}
  .t-mesa .cx .rostos{display:flex}.t-mesa .cx .rostos .mx-av{width:24px;height:24px;font-size:11px;border-radius:7px;box-shadow:0 0 0 2px var(--bg2);background:var(--bg3);color:var(--fg)}
  .t-mesa .cx .rostos .mx-av+.mx-av{margin-left:-4px}.t-mesa .cx .rostos+b{margin-left:4px}
  .t-mesa [data-arr]{cursor:grab;touch-action:none;user-select:none;transition:opacity .2s,transform .2s var(--curva)}.t-mesa .cx [data-arr]:hover{transform:translateY(-2px)}.t-mesa [data-arr].origem{opacity:.25}.t-mesa .cx b{height:auto;display:inline;line-height:normal;padding:0}
  .t-mesa .cx.vazia{background:transparent;box-shadow:inset 0 0 0 1.5px var(--linha);color:var(--fg3);font-weight:500;cursor:default}
  .t-mesa .cx.pop{animation:cxPop .45s var(--curva)}@keyframes cxPop{40%{transform:scale(1.15)}}
  .t-mesa .cx:not(.vazia){box-shadow:0 0 0 2px color-mix(in srgb,var(--ac) calc(var(--n,0)*100%),transparent)}
  /* dragging: valid strips light up, the one under the pointer lifts; wrong side fades */
  .t-mesa.arr .tira.alvo .cx{box-shadow:inset 0 0 0 1.5px var(--ac);color:var(--ac);background:transparent}
  .t-mesa.arr .tira.alvo .cx.vazia::after{content:"Solte aqui"}.t-mesa.arr .tira.alvo .cx.vazia span{display:none}
  .t-mesa.arr .tira.sobre{box-shadow:inset 0 0 0 2px var(--ac);background:var(--bg2-hover);transform:translateY(-4px)}.t-mesa.arr .band.sobre{transform:none;background:none;box-shadow:none}
  .t-mesa.arr .tira.sobre .cx{background:var(--ac);color:#fff;transform:scale(1.08)}
  .t-mesa.arr .tira.nao{opacity:.35}
  /* programs making sound now, each under its own side (29/10): playing on the left, listening to the mic on the right */
  .t-mesa .agoras{flex:1 1 0;min-height:150px;align-self:stretch;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:24px}
  .t-mesa .agora{display:flex;flex-direction:column;align-items:center;gap:8px;min-width:0;min-height:0;padding:12px 0 4px;margin:-12px 0 -4px;border-radius:var(--r-grupo);transition:box-shadow .2s,background .2s}
  .t-mesa .agora h4{margin:0;font-size:12px;font-weight:500;color:var(--fg3);transition:color .2s}
  /* dragging a pinned program: the row takes it back (= follows the default again) */
  .t-mesa.arr .agora.alvo{box-shadow:inset 0 0 0 1.5px var(--ac)}.t-mesa.arr .agora.alvo h4{color:var(--ac)}.t-mesa.arr .agora.sobre{background:var(--bg2-hover);box-shadow:inset 0 0 0 2px var(--ac)}
  .t-mesa .fila{display:flex;justify-content:center;align-content:flex-start;flex-wrap:wrap;row-gap:8px;min-height:84px;flex:1 1 0;overflow-y:auto;overflow-x:hidden;align-self:stretch;padding:6px 0}
  /* hint at the window bottom (31/10): the row takes all the room, only it scrolls; inner side clears the floating dock */
  .t-mesa .nota{flex:none;margin:0;font-size:12px;line-height:16px;color:var(--fg3);text-align:center;padding:0 16px;transition:color .2s}.t-mesa .agora[data-g=som] .nota{padding-right:136px}.t-mesa .agora[data-g=mic] .nota{padding-left:136px}.t-mesa.arr .agora.alvo .nota{color:var(--ac)}
  .t-mesa .chip{width:80px;margin:0 4px;display:flex;flex-direction:column;align-items:center;gap:8px;cursor:grab;touch-action:none;user-select:none;
    transition:width .4s var(--curva),margin .4s var(--curva),opacity .3s,transform .4s var(--curva);animation:chEntra .5s var(--curva) both}
  @keyframes chEntra{from{width:0;margin:0;opacity:0;transform:scale(.4)}}
  .t-mesa .chip.sai{width:0;margin:0;opacity:0;transform:scale(.4);pointer-events:none}
  .t-mesa .chip .rt{position:relative;width:52px;height:52px;border-radius:16px;background:var(--bg2);display:grid;place-items:center;font-size:20px;font-weight:700;color:var(--fg2);
    box-shadow:0 0 0 2px color-mix(in srgb,var(--ac) calc(var(--n,0)*100%),transparent),0 0 calc(var(--n,0)*18px) color-mix(in srgb,var(--ac) 45%,transparent);transition:box-shadow .09s linear,background .2s}
  .t-mesa .chip:hover .rt,.t-mesa .chip:focus-visible .rt{background:var(--bg2-hover);color:var(--fg)}.t-mesa .chip:focus-visible{outline:0}
  .t-mesa .chip .rt .ico{position:absolute;right:-4px;bottom:-4px;width:20px;height:20px;padding:3px;box-sizing:border-box;border-radius:50%;background:var(--ac);color:#fff}
  .t-mesa .chip span{font-size:12px;color:var(--fg2);max-width:80px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .t-mesa .chip.calado .rt{opacity:.45}.t-mesa .chip.calado .rt::after{content:"";position:absolute;inset:0;border-radius:16px;background:linear-gradient(to top right,transparent 47%,var(--perigo) 47% 53%,transparent 53%)}
  .t-mesa .chip.origem{opacity:.25}.t-mesa .chip span.rodavol{color:var(--fg);font-variant-numeric:tabular-nums}
  .t-mesa .nada{font-size:13px;color:var(--fg3);height:84px;display:grid;place-items:center}
  .ms-fant{position:fixed;z-index:90;pointer-events:none;width:52px;height:52px;border-radius:16px;background:var(--bg3);display:grid;place-items:center;font:700 20px var(--fonte);color:var(--fg);
    box-shadow:0 0 0 2px var(--ac),0 14px 34px rgba(0,0,0,.55);transform:translate(-50%,-50%) scale(1.1) rotate(-4deg);transition:transform .2s var(--curva)}
  .ms-fant.voa{transition:left .35s var(--curva),top .35s var(--curva),transform .35s var(--curva),opacity .35s}
  /* popovers: box contents, program volume */
  .ms-pop{position:fixed;z-index:80;width:380px;box-sizing:border-box;background:var(--bg3);border-radius:16px;box-shadow:0 16px 50px rgba(0,0,0,.6);padding:8px 16px 12px;
    opacity:0;pointer-events:none;transform:translateY(6px) scale(.98);transition:opacity .18s,transform .25s var(--curva)}
  .ms-pop.ab{opacity:1;pointer-events:auto;transform:none}.ms-pop h5{margin:8px 0 4px;font-size:13px;font-weight:600}.ms-pop h5 small{display:block;font-size:12px;font-weight:400;color:var(--fg3);margin-top:2px}
  .ms-pop .ap{display:grid;grid-template-columns:24px 76px 1fr 36px 28px;gap:8px;align-items:center;height:44px}.ms-pop .ap+.ap{border-top:1px solid var(--linha)}
  .ms-pop .ap>span{font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.ms-pop .ap .vl{text-align:right;color:var(--fg2);font-size:12px;font-variant-numeric:tabular-nums}
  .ms-pop .lembra{display:flex;align-items:center;gap:12px;height:40px;margin-top:4px;border-top:1px solid var(--linha);font-size:13px;color:var(--fg2);cursor:pointer;user-select:none}
  .ms-pop .lembra i{flex:none;width:18px;height:18px;box-sizing:border-box;border-radius:5px;box-shadow:inset 0 0 0 1.5px var(--fg3);display:grid;place-items:center;transition:background .15s,box-shadow .15s}
  .ms-pop .lembra i svg{width:12px;height:12px;opacity:0;transform:scale(.5);transition:opacity .15s,transform .2s var(--curva)}
  .ms-pop .lembra.on i{background:var(--ac);box-shadow:none;color:#fff}.ms-pop .lembra.on i svg{opacity:1;transform:none}.ms-pop .lembra:hover{color:var(--fg)}.ms-pop .lembra:focus-visible{outline:0}.ms-pop .lembra:focus-visible i{box-shadow:0 0 0 3px var(--ac)}
  .ms-pop.mini{width:236px;padding:4px 6px 4px 4px}.ms-pop.mini .ap{grid-template-columns:1fr 34px 28px 28px;gap:4px;height:36px}
  .ms-pop.mini:has(.ap.cpv){width:268px}.ms-pop.mini .ap.cpv{grid-template-columns:1fr 34px 28px 28px 28px}
  .ms-pop .lembra.bt{height:28px;width:28px;margin:0;border:0;border-radius:8px;padding:0;background:none;color:var(--fg3);display:grid;place-items:center}
  .ms-pop .lembra.bt svg{width:16px;height:16px;opacity:1;transform:none}.ms-pop .lembra.bt:hover{background:var(--bg2-hover);color:var(--fg)}
  .ms-pop .lembra.bt.on{color:var(--ac);background:none}.ms-pop .lembra.bt.on svg{fill:currentColor}.ms-pop .lembra.bt:focus-visible{box-shadow:0 0 0 2px var(--ac)}
  .t-mesa .chip .rt img{width:32px;height:32px;pointer-events:none}.mx-av img,.ms-fant img{width:72%;height:72%;pointer-events:none}.mx-av:has(img){background:none!important}
  /* copies (Alt+drag, 30/10): "+" mark on the face (sound color; mic blue = goes with the voice), box rows with a Parar button */
  .mx-av.cp{position:relative;overflow:visible}.mx-av.cp::after{content:"+";position:absolute;right:-4px;top:-4px;width:12px;height:12px;border-radius:50%;background:var(--som);color:#000;font:800 10px/12px var(--fonte);text-align:center;box-shadow:0 0 0 1.5px var(--bg2)}
  .mx-av.cp.mi::after{background:var(--mic);color:#fff}
  .ms-pop .ap.cp{grid-template-columns:24px 76px 1fr 28px}.ms-pop .ap.cp small{font-size:12px;color:var(--fg3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .ms-pop .ap.cp1{grid-template-columns:1fr 28px;padding-left:8px}.ms-pop .ap.cp1>span{font-size:12px;color:var(--fg2)}
  .ms-pop .cp-onde{font-size:12px;color:var(--fg3);padding:6px 8px 0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.ms-pop .lembra.bt.on[data-segue] svg{fill:none}
  .ms-fant.cp::after{content:"+";position:absolute;right:-6px;top:-6px;width:20px;height:20px;border-radius:50%;background:var(--som);color:#000;font:800 15px/20px var(--fonte);text-align:center;box-shadow:0 2px 6px rgba(0,0,0,.5)}
  .t-mesa .chip .rt .ico.cl{background:var(--som);color:#000}.t-mesa .chip .rt .ico.cl+.ico{right:14px}
  .t-mesa .chip.cpm .rt::before{content:"+";position:absolute;left:-4px;top:-4px;width:16px;height:16px;border-radius:50%;background:var(--mic);color:#fff;font:800 12px/16px var(--fonte);text-align:center;box-shadow:0 0 0 1.5px var(--bg2)}
  /* bypass (30/10): round button by mute/info; on = side color, paused (device is the default / did not open) = outline. The strip's
     empty line under the name says where it goes */
  .t-mesa .tira:not(.band) .bts{gap:6px}
  .t-mesa .byp{border:0;width:28px;height:28px;border-radius:50%;background:var(--controle);color:var(--fg2);cursor:pointer;display:grid;place-items:center;padding:0;flex:none;transition:background .2s,color .2s,box-shadow .2s}
  .t-mesa .byp svg{width:16px;height:16px;transition:transform .3s var(--curva)}.t-mesa .byp:hover,.t-mesa .byp:focus-visible{color:var(--fg);outline:0}.t-mesa .byp:focus-visible{box-shadow:0 0 0 2px var(--ac)}
  .t-mesa .byp.on{background:var(--ac);color:#fff}.t-mesa .byp.on svg{transform:translateY(-1px)}
  .t-mesa .byp.pausa{background:transparent;color:var(--ac);box-shadow:inset 0 0 0 1.5px var(--ac)}
  .t-mesa .band .byp{background:rgba(0,0,0,.3);color:#f3dc9a;box-shadow:inset 0 0 0 1.2px rgba(233,207,130,.6)}.t-mesa .band .byp:hover{background:rgba(0,0,0,.48);color:#fff}
  .t-mesa .band .byp.on{background:linear-gradient(#fbe7a6,#c9a24a);color:#1d3f2b;box-shadow:none}.t-mesa .band .byp.pausa{background:none;box-shadow:inset 0 0 0 1.5px #e9cf82}
  .t-mesa .tira .pd.by{animation:byEntra .3s var(--curva)}.t-mesa .tira .pd.by.pausa{color:var(--fg3)}@keyframes byEntra{from{opacity:0;transform:translateY(-3px)}}
  .ms-pop .c-slider{margin:0 11px}.c-slider .sis{position:absolute;top:-5px;bottom:-5px;width:2px;margin-left:-1px;border-radius:1px;background:var(--fg3);pointer-events:none;transition:left .15s var(--curva)}.ms-pop .c-slider .vu{width:calc(var(--n,0)*100%)}.ms-pop .ap.calado>span,.ms-pop .ap.calado .c-slider{opacity:.4}
</style>`);

  const esc = (t) => String(t).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  // program face: its Windows icon (engine sends a PNG data URI in "ic"), else its first letter
  const cara = (a) => (a.ic ? `<img src="${a.ic}" alt="">` : esc((a.n || '?')[0].toUpperCase()));
  const av = (a, tam = 28, arr) => `<span class="mx-av${a.c ? ' cp' + (a.mi ? ' mi' : '') : ''}"${arr ? ` data-arr="${esc(arr)}"` : ''} style="width:${tam}px;height:${tam}px;font-size:${Math.round(tam * .45)}px">${cara(a)}</span>`;
  const acha = (k) => { const i = k.indexOf(':'), g = k.slice(0, i), id = k.slice(i + 1); return S[g]?.find((d) => d.id === id); };
  const padrao = (g) => S[g].find((d) => d.p);
  const chave = () => { ['som', 'mic'].forEach((g) => S[g].forEach((d) => (d.k = g + ':' + d.id))); S.soltos.forEach((a) => (a.k = a.lado + ':' + a.x)); };
  // structure = what needs a rebuild (devices, default, pinned list); volumes/mute update in place
  // (the default changes in place too: the curtain must fall/rise, not reappear)
  const estrutura = () => JSON.stringify(['som', 'mic'].map((g) => S[g].map((d) => [d.id, d.n, d.fixos.map((a) => a.x)])));
  chave();

  // 30/10 copies (Alt+drag): a program that keeps playing where it is and ALSO plays on this device (engine clones) or, on the mic
  // default, goes into the FAH mic with the voice (mic.injeta). Shown in the box/banner with a "+" mark; dragged to the row = stops.
  const progX = (x) => S.soltos.find((a) => a.lado === 'som' && a.x === x) || S.som.flatMap((d) => d.fixos).find((a) => a.x === x);
  const copias = (d) => { const mic = d.k.startsWith('mic:'), xs = !mic ? Object.keys(App.estado.clones || {}).filter((x) => clonado(x, d.n)) : d.p ? injetados() : [];
    return xs.map((x) => ({ ...(progX(x) || { x, n: x, lado: 'som', v: 100, m: 0 }), c: 1, mi: mic ? 1 : 0 })); };
  const tudo = (d) => [...d.fixos, ...copias(d)], chaveDe = (d, a) => d.k + '|' + a.x + (a.c ? '|c' : '');
  const rostos = (d, L = tudo(d)) => `<span class="rostos">${L.slice(0, 3).map((a) => av(a, 24, chaveDe(d, a))).join('')}</span><b>${L.length > 3 ? '+' + (L.length - 3) : L.length}</b>`;
  const cx = (d) => { const L = tudo(d); return `<button class="cx ${L.length ? '' : 'vazia'}" data-cx="${d.k}" ${L.length ? `title="${esc(L.map((a) => a.n + (a.c ? ' (também toca aqui)' : '')).join(', '))}. Arraste um para outro aparelho ou de volta para a fila; botão direito: volume"` : 'tabindex="-1"'}>${L.length ? rostos(d, L) : '<span>vazio</span>'}</button>`; };
  // banner cloth (drawn once): torn swallowtail outline (shallow notch: the controls need the height), folds, damask, dark medallions behind
  // the emblem and the fader, gold border clipped inside + dashed stitch line
  const BANDEIRA = (() => { const O = 'M3 0H109C111 80 107 160 110 222L111 248L106 254L109 270L103 274L104 284L93 274L87 280L80 264L56 244L33 264L27 278L21 271L8 284L9 268L3 262L6 248L2 238C5 160 1 80 3 0Z';
    const st = (o, c) => o.map(([x, v]) => `<stop offset="${x}" stop-color="${c}" stop-opacity="${v}"/>`).join('');
    return `<svg viewBox="0 0 112 284" preserveAspectRatio="none"><defs>
      <linearGradient id="bdPano" x2="0" y2="1"><stop offset="0" stop-color="#3f7d57"/><stop offset=".45" stop-color="#4f9467"/><stop offset="1" stop-color="#3a7151"/></linearGradient>
      <linearGradient id="bdOnda">${st([[0, .3], [.2, 0], [.42, .2], [.6, 0], [.8, .16], [1, .32]], '#000')}</linearGradient>
      <linearGradient id="bdBrilho">${st([[0, 0], [.22, .1], [.34, 0], [.64, .08], [.72, 0]], '#fff')}</linearGradient>
      <linearGradient id="bdSombra" x2="0" y2="1">${st([[0, .45], [.07, 0], [.85, 0], [1, .3]], '#000')}</linearGradient>
      <linearGradient id="bdOuro" x2="0" y2="1"><stop offset="0" stop-color="#f6df9a"/><stop offset=".4" stop-color="#c9a24a"/><stop offset=".7" stop-color="#8a6a24"/><stop offset="1" stop-color="#e3c777"/></linearGradient>
      <radialGradient id="bdMedalha">${st([[0, .28], [.6, .12], [1, 0]], '#0b2416')}</radialGradient>
      <pattern id="bdDamasco" width="22" height="30" patternUnits="userSpaceOnUse"><g fill="none" stroke="#f1dfa8" stroke-opacity=".16" stroke-width=".9">
        <path d="M11 3C16 8 16 13 11 17C6 13 6 8 11 3ZM11 17C14 21 19 21 21 27M11 17C8 21 3 21 1 27M11 17V25"/><circle cx="0" cy="0" r="2"/><circle cx="22" cy="0" r="2"/><circle cx="0" cy="30" r="2"/><circle cx="22" cy="30" r="2"/></g></pattern>
      <clipPath id="bdCorte"><path d="${O}"/></clipPath></defs>
      <g><g clip-path="url(#bdCorte)"><path d="${O}" fill="url(#bdPano)"/><rect width="112" height="284" fill="url(#bdDamasco)"/><ellipse cx="56" cy="34" rx="30" ry="26" fill="url(#bdMedalha)"/><ellipse cx="56" cy="150" rx="42" ry="74" fill="url(#bdMedalha)"/>
        <rect width="112" height="284" fill="url(#bdOnda)"/><rect width="112" height="284" fill="url(#bdBrilho)"/><rect width="112" height="284" fill="url(#bdSombra)"/>
        <path d="${O}" fill="none" stroke="#2a1d06" stroke-opacity=".55" stroke-width="10"/><path d="${O}" fill="none" stroke="url(#bdOuro)" stroke-width="6.5"/></g>
        <path d="${O}" transform="translate(56 136) scale(.84 .9) translate(-56 -136)" fill="none" stroke="#e9cf82" stroke-opacity=".55" stroke-width=".8" stroke-dasharray="2 1.6"/></g></svg>`; })();
  const DICA_PAD = (g) => `Tornar padrão|Clique: o Windows passa a ${g === 'mic' ? 'usar este microfone' : 'tocar por este aparelho'}`;
  const info = (d, g) => [d.n, (g === 'mic' ? 'Microfone' : 'Saída de som') + (d.p ? ' · padrão do Windows' : ''), d.f && 'Formato: ' + d.f, d.c && 'Canais: ' + d.c].filter(Boolean).join('|');
  const vf = (d) => `<div class="fd"><div class="vf" tabindex="0" data-vf="${d.k}" title="Volume do Windows (clique na rodinha: 50%)"><i class="nv" style="height:${d.vol}%"></i><i class="bt" style="bottom:${d.vol}%"></i></div>
      <div class="med"><i></i><i></i></div></div>`;
  const infoBt = (d, g) => `<button class="eng" data-info="${d.k}" aria-label="Informações do aparelho" data-dica-ja data-dica="${esc(info(d, g))}">${UI.ico('info')}</button>`;
  // bypass (30/10): engine "bypass" = { som: [id], mic: [id], ativos: ["lado|id"] }. On = everything this device plays (or this mic hears)
  // also comes out of the default sound, at this strip's volume/mute. The sound default has none (it already plays there)
  const BY = () => App.estado.bypass || { som: [], mic: [], ativos: [] };
  const byLig = (d) => (BY()[d.k.split(':')[0]] || []).includes(d.id), byAtivo = (d) => (BY().ativos || []).includes(d.k.replace(':', '|'));
  const byEstado = (d) => (!byLig(d) ? '' : byAtivo(d) ? 'on' : 'pausa');
  const byDica = (d) => { const mic = d.k.startsWith('mic:'), pd = padrao('som')?.n || 'som padrão', e = byEstado(d);
    return 'Bypass|' + (e === 'on' ? (mic ? 'Ligado: você ouve este microfone em ' : 'Ligado: o som deste aparelho também sai em ') + pd + ', no volume desta faixa. Clique para desligar'
      : e === 'pausa' ? (!mic && d.p ? 'Pausado: este aparelho é o som padrão. Volta sozinho quando outro virar o padrão' : 'Pausado: o aparelho não respondeu. O FAH tenta de novo sozinho') + '. Clique para desligar'
      : (mic ? 'Clique: você ouve este microfone em ' + pd + ' (use fone para não microfonar)' : 'Clique: tudo que toca neste aparelho também sai em ' + pd) + ', no volume desta faixa'); };
  const bypBt = (d) => { const e = byEstado(d); return `<button class="byp ${e}" data-byp="${d.k}" aria-label="Bypass" aria-pressed="${!!e}" data-dica="${esc(byDica(d))}">${UI.ico('desvio')}</button>`; };
  const byPd = (d) => { const e = byEstado(d); return e ? `<span class="pd by ${e}">${e === 'on' ? 'No som padrão' : 'Bypass pausado'}</span>` : '<span class="pd"></span>'; };
  // strip card (not default): icon = "make default" button, box = programs pinned here
  const tira = (d, g, i) => `<div class="tira ${d.m ? 'calado' : ''}" data-k="${d.k}" data-lado="${g}" style="animation-delay:${i * .04}s">
    <button class="pdb" data-pad="${d.k}" aria-label="Tornar padrão" data-dica="${DICA_PAD(g)}">${UI.icone(d.ic, 32, `var(--${g}-ic)`)}</button><b title="${esc(d.n)}">${esc(d.n)}</b>${byPd(d)}
    ${vf(d)}<span class="vl tira-vl">${d.vol}%</span><div class="bts">${UI.mudo(d.m, { k: d.k })}${bypBt(d)}${infoBt(d, g)}</div>${cx(d)}</div>`;
  // the default drawn on the banner. Pinned programs only show here if Windows made a device with pins the default (FAH unpins them itself)
  const corpo = (d, g, L = g === 'mic' ? d.fixos : tudo(d)) => `<span class="bd-emb">${UI.ico(d.ic)}</span><b title="${esc(d.n)}">${esc(d.n)}</b><span class="pd">PADRÃO</span>
    ${L.length ? `<span class="bd-fixos" title="${d.fixos.length ? 'Fixados aqui: arraste para a fila (segue o padrão) ou para outro aparelho' : ''}${L.length > d.fixos.length ? (d.fixos.length ? '. ' : '') + 'Com «+»: também toca aqui; arraste para a fila para parar' : ''}">${L.slice(0, 4).map((a) => av(a, 20, chaveDe(d, a))).join('')}</span>` : ''}
    ${vf(d)}<span class="vl tira-vl">${d.vol}%</span><div class="bts">${UI.mudo(d.m, { k: d.k })}${g === 'mic' ? bypBt(d) : ''}${infoBt(d, g)}</div>`;
  const banda = (d, g, desce) => `<div class="tira band ${d.m ? 'calado' : ''} ${desce ? 'desce' : ''}" data-k="${d.k}" data-lado="${g}"><div class="estandarte"><i class="varao"></i><div class="pano"><div class="brisa">${BANDEIRA}<div class="bd-cont">${corpo(d, g)}</div></div></div></div></div>`;
  // program sound sent into the FAH mic (engine "mic".injeta = exe names): blue mic badge on its chip
  const injetados = () => App.estado.mic?.injeta || [], injetado = (x) => injetados().includes(x);
  // program cloned to more devices (engine "clones" = { exe: [device name] }): sound-colored badge
  const clonesDe = (x) => (App.estado.clones || {})[x] || [], clonado = (x, n) => clonesDe(x).includes(n);
  const selo = (a) => a.lado === 'mic' ? UI.ico('mic') : (clonesDe(a.x).length ? UI.ico('clone').replace('class="ico"', 'class="ico cl"') : '')
    + (injetado(a.x) ? UI.ico('mic').replace('<svg', '<svg data-inj style="background:var(--mic)"') : '');
  const marca = (a) => clonesDe(a.x).join('|') + (injetado(a.x) ? '|mic' : '');
  const chip = (a, i = 0) => `<div class="chip ${a.m ? 'calado' : ''}" tabindex="0" data-chip="${a.k}" style="--ac:var(--${a.lado});animation-delay:${i * .05}s">
    <div class="rt">${cara(a)}${selo(a)}</div><span title="${esc(a.n)}">${esc(a.n)}</span></div>`;
  // 30/10: a sound program dropped on the mic row = plays into the default mic (FAH mic, the call hears it): its chip sits in the mic row
  // with a blue "+"; dragged back to the sound row = stops. data-arr = the mic default's copy key (fixoDe/copias handle it)
  const chipInj = (x) => { const d = padrao('mic'); if (!d) return ''; const a = progX(x) || { x, n: x, lado: 'som', v: 100, m: 0 };
    return `<div class="chip cpm" tabindex="0" data-arr="${esc(d.k + '|' + x + '|c')}" data-injc="${esc(x)}" style="--ac:var(--som)"><div class="rt">${cara(a)}</div><span title="${esc(a.n)} toca no microfone padrão: quem está na chamada ouve">${esc(a.n)}</span></div>`; };
  const conteudo = (g) => ((g === 'mic' ? injetados().map(chipInj).join('') : '') + soltosDe(g).map(chip).join('')) || NADA(g);
  const NADA = (g) => `<div class="nada">${g === 'mic' ? 'Nenhum programa usando o microfone' : 'Nenhum programa tocando agora'}</div>`;
  const soltosDe = (g) => S.soltos.filter((a) => a.lado === g);

  // ---- levels: engine "mixniv" {d: {deviceId: [L, R]}, a: {"lado:exe": peak}}; browser = simulated
  const pico = {};
  const pinta = (e, k, v) => { pico[k] = Math.max(v, (pico[k] || 0) - .025); e.style.setProperty('--n', v.toFixed(3)); e.style.setProperty('--pk', pico[k].toFixed(3)); };
  const niveis = (N) => { const el = App.$('.t-mesa'); if (!el || E.tela !== 'mixer') return;
    el.querySelectorAll('.tira').forEach((t) => { const d = acha(t.dataset.k), lr = (d && !d.m && N.d[d.id]) || [0, 0];
      t.querySelectorAll('.med i').forEach((e, i) => pinta(e, t.dataset.k + i, lr[i])); });
    el.querySelectorAll('[data-chip]').forEach((c) => c.style.setProperty('--n', (N.a[c.dataset.chip] || 0).toFixed(3)));
    el.querySelectorAll('[data-injc]').forEach((c) => c.style.setProperty('--n', (N.a['som:' + c.dataset.injc] || 0).toFixed(3)));
    el.querySelectorAll('.cx:not(.vazia)').forEach((b) => { const d = acha(b.dataset.cx); b.style.setProperty('--n', Math.max(0, ...d.fixos.map((a) => N.a[a.lado + ':' + a.x] || 0)).toFixed(3)); });
    document.querySelectorAll('.ms-pop [data-vu]').forEach((s) => s.style.setProperty('--n', (N.a[s.dataset.vu] || 0).toFixed(3))); };
  addEventListener('fah:mixniv', (e) => niveis(e.detail));
  if (!real) setInterval(() => { const r = (v) => v * (.35 + Math.random() * .55), a = {}, d = {};
    const ok = (p) => !p.m && Math.random() > .08;
    S.soltos.forEach((p) => (a[p.k] = ok(p) ? r(p.v / 100) : 0));
    ['som', 'mic'].forEach((g) => S[g].forEach((x) => { x.fixos.forEach((p) => (a[g + ':' + p.x] = ok(p) ? r(p.v / 100) : 0));
      const toca = x.fixos.some((p) => !p.m) || (x.p && S.soltos.some((p) => p.lado === g && !p.m)), v = toca ? r(x.vol / 100) : 0; d[x.id] = [v, v * .96]; }));
    niveis({ d, a }); }, 90);

  App.tela({ id: 'mixer', classe: 't-mesa rolagem',
    montar(el) {
      if (real) { manda({ tipo: 'mixer', on: true }); manda({ tipo: 'clones?' }); }   // clones? also answers bypass
      // quieto = rebuild while open (device list changed): no entrance animations, banner already down
      const monta = (quieto) => { const x = {}; el.querySelectorAll('.outros').forEach((o) => (x[o.dataset.g] = o.scrollLeft)); el.classList.toggle('quieto', !!quieto);
        el.innerHTML = `<div class="mesa">${App.lados().map((l) => { const L = S[l.id], pd = L.find((d) => d.p);
          return `<div class="lado ${pd ? 'com-band' : ''}" data-g="${l.id}" style="--ac:var(--${l.id})"><h3>${UI.icone(l.icone, 30, `var(--${l.id}-ic)`)}${l.nome}</h3>${L.length
            ? `<div class="outros" data-g="${l.id}">${L.filter((d) => d !== pd).map((d, i) => tira(d, l.id, i)).join('')}</div>${pd ? banda(pd, l.id, quieto) : ''}`
            : `<div class="sem">${real && !E.mixer ? 'Lendo aparelhos…' : 'Nenhum aparelho ligado'}</div>`}</div>`; }).join('')}</div>
        <div class="agoras">${['som', 'mic'].map((g) => `<div class="agora" data-g="${g}"><h4></h4><div class="fila">${conteudo(g)}</div><p class="nota"></p></div>`).join('')}</div><div class="ms-pop"></div>`;
        el.querySelectorAll('.outros').forEach((o) => (o.scrollLeft = x[o.dataset.g] || 0)); bordas(); titulo();
        requestAnimationFrame(() => requestAnimationFrame(() => (quieto ? el.classList.remove('quieto') : el.querySelectorAll('.band').forEach((b) => b.classList.add('desce'))))); };
      const bordas = () => el.querySelectorAll('.outros').forEach((o) => { o.classList.toggle('mais-e', o.scrollLeft > 1); o.classList.toggle('mais-d', o.scrollLeft + o.clientWidth < o.scrollWidth - 1); });
      const titulo = () => el.querySelectorAll('.agora').forEach((ag) => { const mic = ag.dataset.g === 'mic';
        ag.querySelector('h4').textContent = mic ? 'Ouvindo o microfone' : 'Tocando agora';
        ag.querySelector('.nota').textContent = [soltosDe(ag.dataset.g).length && (mic ? 'Arraste para um microfone e ele ouve só esse' : 'Arraste para um aparelho e ele toca só lá (com Alt: toca lá também) · na fila do microfone, vai para a chamada') + ' · botão direito: volume',
          mic && padrao('mic') && injetados().length && 'Com «+»: toca no microfone, arraste de volta para parar'].filter(Boolean).join(' · '); });
      if (el._ok) { el._sig = estrutura(); return monta(); }
      el._ok = 1; el._sig = estrutura(); monta();
      const pop = () => el.querySelector('.ms-pop'), fila = (g) => el.querySelector(`.agora[data-g="${g}"] .fila`);
      const fechaPop = () => { pop().classList.remove('ab'); pop()._k = null; };
      const abrePop = (k, html, alvo, cima, mini) => { const p = pop(), r = alvo.getBoundingClientRect(); p.innerHTML = html; p._k = k; p.classList.toggle('mini', !!mini); p._alvo = alvo; p.style.setProperty('--ac', getComputedStyle(alvo).getPropertyValue('--ac'));
        p.classList.add('ab'); const ph = p.offsetHeight, pw = p.offsetWidth;
        p.style.left = Math.max(16, Math.min(cima ? r.left + r.width / 2 - pw / 2 : r.right + 8, innerWidth - pw - 16)) + 'px';
        p.style.top = (cima ? Math.max(48, r.top - ph - 8) : Math.min(r.top, innerHeight - ph - 16)) + 'px'; };
      // default on one side changed: its card leaves the strip toward the banner, the old default comes out of it, the rest slide (FLIP);
      // the cloth rolls up with the old device and falls with the new one drawn on it
      const marcaPadrao = (g) => { const lado = el.querySelector(`.lado[data-g="${g}"]`), out = lado?.querySelector('.outros'), band = lado?.querySelector('.band'), pd = padrao(g); if (!out) return;
        if (!band !== !pd) return monta(1); if (!pd || (band._k || band.dataset.k) === pd.k) return; band._k = pd.k;
        const cards = [...out.querySelectorAll(':scope>.tira:not(.saindo)')], antes = new Map(cards.map((t) => [t, t.getBoundingClientRect()])), br = band.getBoundingClientRect();
        const rumo = (r) => Math.sign(br.left + br.width / 2 - r.left - r.width / 2) * 48, quer = S[g].filter((d) => d !== pd);
        cards.forEach((t) => (t.style.animation = 'none'));
        const vai = cards.filter((t) => !quer.some((d) => d.k === t.dataset.k)), pos = vai.map((t) => [t.offsetLeft, t.offsetTop]);
        vai.forEach((t, i) => { Object.assign(t.style, { position: 'absolute', left: pos[i][0] + 'px', top: pos[i][1] + 'px' }); t.classList.add('saindo');
          t.animate([{ opacity: 1, transform: 'none' }, { opacity: 0, transform: `translateX(${rumo(antes.get(t))}px) scale(.85)` }], { duration: 300, easing: 'ease-in', fill: 'forwards' }).onfinish = () => t.remove(); });
        let ant = null, novo = null; quer.forEach((d) => { let t = cards.find((c) => c.dataset.k === d.k && !vai.includes(c));
          if (!t) { out.insertAdjacentHTML('beforeend', tira(d, g, 0)); t = novo = out.lastElementChild; t.style.animation = 'none'; }
          if (ant) { if (ant.nextElementSibling !== t) ant.after(t); } else if (out.firstElementChild !== t) out.prepend(t); ant = t; });
        if (novo) { const o = out.getBoundingClientRect(), v = novo.getBoundingClientRect(); if (v.left < o.left || v.right > o.right) out.scrollLeft += v.left < o.left ? v.left - o.left - 8 : v.right - o.right + 8; }
        out.querySelectorAll(':scope>.tira:not(.saindo)').forEach((t) => { const b = t.getBoundingClientRect();
          if (t === novo) return t.animate([{ opacity: 0, transform: `translateX(${rumo(b)}px) scale(.85)` }, { opacity: 1, transform: 'none' }], { duration: 520, delay: 380, easing: 'cubic-bezier(.3,1.15,.5,1)', fill: 'backwards' });
          const dx = antes.get(t).left - b.left; if (Math.abs(dx) > .5) t.animate([{ transform: `translateX(${dx}px)` }, { transform: 'none' }], { duration: 480, easing: 'cubic-bezier(.3,1.15,.5,1)' }); });
        band.classList.remove('desce'); clearTimeout(band._t);
        band._t = setTimeout(() => { const d = padrao(g); if (!d) return; band.dataset.k = d.k; band._k = d.k; band.classList.toggle('calado', !!d.m); band.querySelector('.bd-cont').innerHTML = corpo(d, g);
          band.offsetWidth; band.classList.add('desce'); }, 420);
        bordas(); };
      const caixa = (d) => `${d.fixos.length ? `<h5>Só em ${esc(d.n)}<small>${d.k.startsWith('mic') ? 'Estes programas ouvem só este microfone.' : 'Estes programas tocam só aqui, mesmo se o padrão mudar.'} Arraste um para outro aparelho, ou para a fila de baixo para voltar ao padrão.</small></h5>` : ''}
        ${d.fixos.map((a) => `<div class="ap ${a.m ? 'calado' : ''}">${av(a, 24, d.k + '|' + a.x)}<span data-arr="${esc(d.k + '|' + a.x)}" title="${esc(a.n)} (arraste para mover)">${esc(a.n)}</span>${slProg(a, { fx: d.k + '|' + a.x })}<span class="vl">${a.v}%</span>${UI.mudo(a.m, { fx: d.k + '|' + a.x })}</div>`).join('')}
        ${copias(d).length ? `<h5>Também em ${esc(d.n)}<small>Continuam onde estavam e tocam aqui também. Arraste para a fila de baixo ou clique no X para parar.</small></h5>` : ''}${copias(d).map((a) => `<div class="ap cp">${av(a, 24, chaveDe(d, a))}<span data-arr="${esc(chaveDe(d, a))}" title="${esc(a.n)} (arraste para outro aparelho ou para a fila)">${esc(a.n)}</span><small>${a.mi ? 'vai junto com a sua voz' : 'toca aqui também'}</small>${PARAR(d, a)}</div>`).join('')}`;
      const PARAR = (d, a) => `<button class="lembra bt" data-tira="${esc(d.k + '|' + a.x)}" aria-label="Parar" data-dica-ja data-dica="Parar|${esc(a.n)} ${a.mi ? 'para de ir para o microfone' : 'para de tocar em ' + esc(d.n)}"><svg class="ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg></button>`;
      // program volume (right click on a program; left click too on the row): same volume on any device, until the program closes unless saved
      // 30/10: the "Clonar" checklist is gone: Alt+drag makes a copy (see solta); a copy's face opens just "Parar"
      // copy volume/mute (02/11): follows the original (volume and mute) until moved or muted; the link button brings it back to following
      const dicaSegue = (proprio, n) => 'Seguir o original|' + (proprio ? 'Tem volume e mudo próprios. Clique para voltar a mudar junto com ' + n : 'Muda junto com o volume e o mudo de ' + n + '. Mexa no volume ou no mudo para esta cópia ter os seus');
      const SEGUE = (k, n, proprio) => `<button class="lembra bt ${proprio ? '' : 'on'}" role="checkbox" aria-checked="${!proprio}" aria-label="Seguir o original" data-segue="${esc(k)}" data-dica-ja data-dica="${esc(dicaSegue(proprio, n))}">${UI.ico('elo')}</button>`;
      // 31/10 program bar = level heard. Untouched it sits on the mark (system volume) and follows it; moved below = own volume, kept when the
      // system volume changes; brought back to the mark (snaps within 2) = follows again. Can't go above the system volume (Windows limit)
      const IMA = 2, sisDica = (a) => a.pr ? `Volume próprio: não muda com o volume do sistema. Arraste até a marca (${a.pd}%) para voltar a acompanhar` : 'Acompanha o volume do sistema (a marca). Arraste para baixo para ter um volume só dele';
      const slProg = (a, dd) => UI.slider(a.v, { vu: a.lado + ':' + a.x, padrao: a.pd ?? 100, ...dd }).replace('<div class="vu">', `<i class="sis" style="left:${a.pd ?? 100}%"></i><div class="vu">`);
      const baseCp = (x, ap) => { const p = progX(x); return ap === 'mic' ? (p?.q ?? 100) : p ? (p.q ?? p.v) : 100; };   // copies ride on the session volume (system volume is not in them); mic copies are leveled to the voice: 100 = as loud as it
      const volProg = (a, d) => { if (a.c) { const ap = a.mi ? 'mic' : d.n, k = ap + '|' + a.x, c = cpVer(k);
          return `<div class="cp-onde">${a.mi ? 'Vai no microfone, no volume da sua voz' : 'Também toca em ' + esc(d.n)}</div><div class="ap cpv ${c.m ? 'calado' : ''}" title="${esc(a.n)}: volume desta cópia${a.mi ? ' (100% = mesmo volume da sua voz)' : ''}">${UI.slider(c.vol, { vu: 'som:' + a.x, cv: k })}<span class="vl">${c.vol}%</span>${UI.mudo(c.m, { cv: k })}${SEGUE(k, a.n, !c.segue)}${PARAR(d, a)}</div>`; }
        const dd = d ? { fx: d.k + '|' + a.x } : { solto: a.k }, mic = a.lado === 'mic';
        const onde = d ? (mic ? 'Ouve só ' : 'Toca só em ') + d.n : (mic ? 'Usa o microfone padrão' : 'Toca no aparelho padrão') + ' (' + (padrao(a.lado)?.n || 'nenhum') + ')';
        return `<div class="ap ${a.m ? 'calado' : ''}" title="${esc(a.n + ' · ' + onde + '. ' + sisDica(a))}">${slProg(a, dd)}<span class="vl">${a.v}%</span>${UI.mudo(a.m, dd)}
          <button class="lembra bt ${a.s ? 'on' : ''}" role="checkbox" aria-checked="${!!a.s}" aria-label="Salvar para as próximas vezes" data-lembra="${esc(a.lado + ':' + a.x)}" data-dica-ja data-dica="Salvar para as próximas vezes|Sem marcar, o volume volta ao normal quando ${esc(a.n)} fechar">${UI.ico('salva')}</button></div>`; };
      const qualquer = (k) => progDe(k) || (() => { for (const g of ['som', 'mic']) for (const d of S[g]) { const a = d.fixos.find((p) => p.lado + ':' + p.x === k); if (a) return a; } })();
      const abreProg = (alvo) => { let a, d = null; if (alvo.dataset.arr) [d, a] = fixoDe(alvo.dataset.arr); else a = progDe(alvo.dataset.chip); if (!a) return;
        const k = 'p:' + a.lado + ':' + a.x; if (pop()._k === k) return fechaPop();
        const casa = d && !alvo.closest('.ms-pop') ? alvo : d ? el.querySelector(`[data-cx="${d.k}"]`) : alvo; abrePop(k, volProg(a, d), casa, 1, 1); Object.assign(pop(), { _a: a, _d: d }); };
      const refazCx = (d, pula) => { const b = el.querySelector(`[data-cx="${d.k}"]`); if (!b) return; b.outerHTML = cx(d); if (!pula) el.querySelector(`[data-cx="${d.k}"]`).classList.add('pop'); };
      const progDe = (k) => S.soltos.find((a) => a.k === k);
      // copy state: engine "copiavol" = { "ap|x": {vol, m, segue} }; absent = follows the original. Own vol/m stay stored while following
      const cvDe = (k) => (App.estado.copiavol || {})[k], parte = (k) => { const i = k.lastIndexOf('|'); return [k.slice(0, i), k.slice(i + 1)]; };
      const cpVer = (k) => { const p = cvDe(k), [ap, x] = parte(k);   // what the copy plays now
        return !p || p.segue ? { segue: 1, vol: baseCp(x, ap), m: !!progX(x)?.m } : { segue: 0, vol: p.vol ?? baseCp(x, ap), m: !!p.m }; };
      const volCopia = (k, mud) => { const cv = (App.estado.copiavol = { ...(App.estado.copiavol || {}) }), [ap, x] = parte(k), o = cv[k] || { vol: baseCp(x, ap), m: 0, segue: 1 };
        const n = (cv[k] = { vol: mud.vol ?? o.vol, m: (mud.m ?? o.m) ? 1 : 0, segue: (mud.segue ?? o.segue) ? 1 : 0 }); mexeu('cv:' + k);
        manda({ tipo: 'copiavol', ap, x, vol: n.vol, m: !!n.m, segue: !!n.segue }); };
      const poeSegue = (b, proprio, n) => { b.classList.toggle('on', !proprio); b.setAttribute('aria-checked', !proprio); b.dataset.dica = dicaSegue(proprio, n); };
      // the original moved here: its open following copies show it now (no wait for the engine snapshot)
      const segueX = (a) => a.lado === 'som' && document.querySelectorAll('.ms-pop [data-c=slider][data-cv]').forEach((sl) => parte(sl.dataset.cv)[1] === a.x && cpVer(sl.dataset.cv).segue && mostraCopia(sl.dataset.cv));
      // every open control of copy k shows its state in place (slider, %, mute, link)
      const mostraCopia = (k) => { const c = cpVer(k), x = parte(k)[1], n = progX(x)?.n || x;
        document.querySelectorAll(`.ms-pop [data-c=slider][data-cv="${CSS.escape(k)}"]`).forEach((sl) => { const ap = sl.closest('.ap'); if (!ap) return;
          if (+sl.dataset.v !== c.vol) UI.poe(sl, c.vol); ap.querySelector('.vl').textContent = c.vol + '%'; ap.classList.toggle('calado', c.m);
          const mb = ap.querySelector('[data-c=mudo]'); if (mb && mb.classList.contains('on') !== c.m) { mb.classList.toggle('on', c.m); mb.innerHTML = UI.ico(c.m ? 'mudo' : 'alto'); }
          const sg = ap.querySelector('[data-segue]'); if (sg) poeSegue(sg, !c.segue, n); }); };
      let sigCp = ''; const sigCopias = () => JSON.stringify([App.estado.clones || {}, injetados(), padrao('mic')?.k]);
      // k = device that just got a copy (its box pops); the mic default banner shows what goes into the mic
      const refazCopias = (k) => { const s = sigCopias(); if (s === sigCp && !k) return marcaInj(); sigCp = s;
        ['som', 'mic'].forEach((g) => S[g].forEach((d) => { refazCx(d, d.k !== k); const bc = d.p && el.querySelector(`.band[data-k="${d.k}"] .bd-cont`); if (bc) bc.innerHTML = corpo(d, g); }));
        refazInj(); marcaInj(); };
      // mic row: injected chips follow mic.injeta (and the default mic: their key carries it)
      const refazInj = () => { const f = fila('mic'); if (!f) return; const pd = padrao('mic'), quer = pd ? injetados() : [], tem = [...f.querySelectorAll('[data-injc]')];
        const fica = tem.filter((c) => quer.includes(c.dataset.injc) && c.dataset.arr.startsWith(pd.k + '|'));
        tem.filter((c) => !fica.includes(c)).forEach((c) => { c.classList.add('sai'); c.removeAttribute('data-injc'); setTimeout(() => { c.remove(); if (!f.children.length) f.innerHTML = NADA('mic'); }, 420); });
        quer.filter((x) => !fica.some((c) => c.dataset.injc === x)).forEach((x) => { f.querySelector('.nada')?.remove(); const u = [...f.querySelectorAll('[data-injc]')].pop();
          u ? u.insertAdjacentHTML('afterend', chipInj(x)) : f.insertAdjacentHTML('afterbegin', chipInj(x)); });
        titulo(); };

      // row of programs: leaves animated, arrives animated
      const sai = (a) => { const c = el.querySelector(`[data-chip="${a.k}"]`); S.soltos.splice(S.soltos.indexOf(a), 1); titulo(); if (pop()._k === 'p:' + a.k) fechaPop();
        if (c) { c.classList.add('sai'); setTimeout(() => { c.remove(); const f = fila(a.lado); if (!soltosDe(a.lado).length && !f.children.length) f.innerHTML = NADA(a.lado); }, 420); } };
      const entra = (a) => { a.k = a.lado + ':' + a.x; S.soltos.push(a); const f = fila(a.lado); f.querySelector('.nada')?.remove(); f.insertAdjacentHTML('beforeend', chip(a)); titulo(); };

      // engine snapshot: rebuild only if the structure changed; volumes/mute in place; row diffed
      const recente = {}; const mexeu = (k) => (recente[k] = Date.now()), fresco = (k) => Date.now() - (recente[k] || 0) < 1200;
      el._novo = (N) => { if (arr || fad) return (el._pend = N);
        const velhos = S.soltos; S = { som: N.som, mic: N.mic, soltos: velhos }; chave();
        const sig = estrutura();
        if (sig !== el._sig) { el._sig = sig; fechaPop(); S.soltos = []; N.soltos.forEach((a) => S.soltos.push({ ...a, k: a.lado + ':' + a.x })); monta(1); return; }
        ['som', 'mic'].forEach((g) => S[g].forEach((d) => { const t = el.querySelector(`.tira[data-k="${d.k}"]`); if (!t) return;
          if (!fresco(d.k)) { poeVf(t.querySelector('.vf'), d.vol); t.classList.toggle('calado', !!d.m); const mb = t.querySelector('.c-mudo'); if (mb.classList.contains('on') !== !!d.m) { mb.classList.toggle('on', !!d.m); mb.innerHTML = UI.ico(d.m ? 'mudo' : 'alto'); } } }));
        ['som', 'mic'].forEach((g) => fresco('pad:' + g) || marcaPadrao(g));
        const novos = N.soltos.map((a) => ({ ...a, k: a.lado + ':' + a.x }));
        velhos.filter((a) => !novos.some((b) => b.k === a.k)).forEach(sai);
        novos.forEach((b) => { const a = velhos.find((x) => x.k === b.k); if (!a) return entra(b); if (fresco(b.k)) return;
          a.v = b.v; a.q = b.q; a.pd = b.pd; a.pr = b.pr; a.m = b.m; a.n = b.n; a.s = b.s; const c = el.querySelector(`[data-chip="${a.k}"]`); c?.classList.toggle('calado', !!a.m);
          if ((a.ic || '') !== (b.ic || '')) { a.ic = b.ic; const rt = c?.querySelector('.rt'); if (rt) rt.innerHTML = cara(a) + selo(a); } });
        vivos(); };
      // 31/10: program volume/mute changed outside (Windows mixer, Windhawk, taskbar wheel): every open program control follows in place
      // (popup, device box). A copy that follows the main volume shows it too. Untouched while the user is moving it (fresco)
      const vivos = () => el.querySelectorAll('[data-c=slider][data-vu]').forEach((sl) => { const k = sl.dataset.vu, ap = sl.closest('.ap'); if (fresco(k) || !ap) return;
        if (sl.dataset.cv) { if (cpVer(sl.dataset.cv).segue && progX(parte(sl.dataset.cv)[1])) mostraCopia(sl.dataset.cv); return; }
        const a = qualquer(k); if (!a) return;
        if (+sl.dataset.v !== a.v) { UI.poe(sl, a.v); ap.querySelector('.vl').textContent = a.v + '%'; }
        const pd = a.pd ?? 100, mk = sl.querySelector('.sis'); sl.dataset.padrao = pd; if (mk && mk.style.left !== pd + '%') mk.style.left = pd + '%';
        if (ap.title) { const t = ap.title.replace(/(Volume próprio|Acompanha o volume)[^]*$/, sisDica(a)); if (t !== ap.title) ap.title = t; }
        ap.classList.toggle('calado', !!a.m); const mb = ap.querySelector('.c-mudo');
        if (mb && mb.classList.contains('on') !== !!a.m) { mb.classList.toggle('on', !!a.m); mb.innerHTML = UI.ico(a.m ? 'mudo' : 'alto'); } });
      addEventListener('fah:mixer', (e) => { E.mixer = 1; if (E.tela === 'mixer') el._novo(e.detail); else { S = e.detail; S.soltos = S.soltos || []; chave(); } });

      // browser: a program comes and goes now and then (engine does this for real)
      const POOL = ['Edge', 'WhatsApp', 'Teams', 'VLC'];
      if (!real && !tem('parado')) setInterval(() => { if (E.tela !== 'mixer' || el.classList.contains('arr')) return;
        const livre = POOL.find((n) => !S.soltos.some((a) => a.n === n)), extra = S.soltos.filter((a) => POOL.includes(a.n));
        if (livre && (extra.length < 2 || Math.random() < .4)) entra({ x: livre.toLowerCase(), n: livre, lado: 'som', v: 80, m: 0 }); else if (extra.length) sai(extra[0]); }, 3500);

      // wind (29/10): each banner has its own weather for random stretches: calm (still), breeze (slow sway) or gusts (wide, fast flutter,
      // leaning one way). Amplitude/speed/lean ease between them, so changes are never a jump. [amp deg, speed rad/s, lean, min s, max s]
      const CLIMA = { calma: [0, .5, 0, 6, 25], brisa: [1.3, .9, 0, 15, 60], vento: [3.6, 2.2, 1.6, 4, 12] }, tempo = {};
      const sorteia = (w, t) => { const r = Math.random(), m = r < .25 ? 'calma' : r < .72 ? 'brisa' : 'vento', c = tem('ventania') ? CLIMA.vento : CLIMA[m], k = .75 + Math.random() * .5;
        w.m = m; w.aA = c[0] * k; w.aV = c[1]; w.aI = c[2] * k * (Math.random() < .5 ? -1 : 1); w.fim = t + (c[3] + Math.random() * (c[4] - c[3])) * 1000; };
      let tAnt = 0;
      const sopra = (t) => { requestAnimationFrame(sopra); const dt = Math.min(.1, (t - tAnt) / 1000), q = Math.min(1, dt * .5); tAnt = t;
        if (E.tela !== 'mixer' || document.hidden) return;
        el.querySelectorAll('.band.desce .brisa').forEach((b) => { const g = b.closest('.band').dataset.lado, w = tempo[g] || (tempo[g] = { a: 0, v: .9, i: 0, f: Math.random() * 6, f2: 0 });
          if (!w.fim || t > w.fim) sorteia(w, t);
          w.a += (w.aA - w.a) * q; w.v += (w.aV - w.v) * q; w.i += (w.aI - w.i) * q; w.f += dt * w.v; w.f2 += dt * w.v * 2.7;
          b.style.transform = `skewX(${(w.i + w.a * (Math.sin(w.f) * .75 + Math.sin(w.f2 + 1) * .25)).toFixed(3)}deg)`; }); };
      requestAnimationFrame(sopra);
      el.addEventListener('pointerover', (e) => { const b = e.target.closest('.band'); if (!b || b.classList.contains('frente')) return;
        el.querySelectorAll('.band.frente').forEach((x) => x.classList.remove('frente')); b.classList.add('frente'); });

      // vertical fader: drag, wheel (2%), arrows; middle click = 50%. Windows device volume.
      const poeVf = (vf, v) => { vf.querySelector('.nv').style.height = v + '%'; vf.querySelector('.bt').style.bottom = v + '%'; vf.closest('.tira').querySelector('.tira-vl').textContent = v + '%'; };
      const poe = (vf, v) => { v = Math.max(0, Math.min(100, Math.round(v))); const d = acha(vf.dataset.vf); if (d.vol === v) return; d.vol = v; mexeu(d.k);
        poeVf(vf, v); manda({ tipo: 'mixvol', id: d.id, vol: v }); };
      let fad = null, arr = null;
      const solto = () => { fad = null; if (el._pend && !arr) { const n = el._pend; el._pend = null; el._novo(n); } };
      el.addEventListener('pointerdown', (e) => { const vf = e.target.closest('.vf');
        if (vf) { if (e.button === 1) return poe(vf, 50); fad = vf; vf.setPointerCapture(e.pointerId); const r = vf.getBoundingClientRect(); return poe(vf, (r.bottom - e.clientY) / r.height * 100); }
        const c = e.target.closest('.chip,[data-arr]'); if (c && e.button === 0) arr = { c, x: e.clientX, y: e.clientY, ok: false }; });
      // drag: a loose chip onto a strip = pin it there; a pinned face (box or its popover) onto another strip = move it, onto the row = back to the default.
      // Dropped anywhere else = flies home, nothing changes (a pin is only undone by dragging it out).
      // Alt held = copy (30/10): the program stays where it is and also plays on the strip it's dropped on; on any mic strip it goes into
      // the FAH mic (the calls hear it; the FAH mic is switched on if needed). A copy's face: onto another strip = move the copy, onto the row = stop.
      const fixoDe = (v) => { const [dk, x, c] = v.split('|'), d = acha(dk); return [d, c ? copias(d || { k: dk }).find((p) => p.x === x) : d?.fixos.find((p) => p.x === x)]; };
      const ondeToca = (x) => S.som.find((d) => d.fixos.some((p) => p.x === x)) || padrao('som');
      const clonar = (x, ap, on) => { const c = (App.estado.clones = { ...(App.estado.clones || {}) });
        c[x] = on ? [...new Set([...clonesDe(x), ap])] : clonesDe(x).filter((y) => y !== ap); if (!c[x].length) delete c[x]; manda({ tipo: 'clonar', x, ap, on }); };
      const injetar = (x, on) => { const m = (App.estado.mic = { ...(App.estado.mic || {}) }); m.injeta = on ? [...new Set([...injetados(), x])] : injetados().filter((y) => y !== x);
        manda({ tipo: 'micinjetar', x, on });
        if (on && !m.ligado) { if (real) manda({ tipo: 'micligar' }); App.toast?.('Liguei o microfone do FAH: quem está na chamada ouve junto com a sua voz'); } };
      const alvos = () => { const { a, de } = arr, cp = arr.alt && a.lado === 'som' && !a.c, aqui = de || padrao('som'); arr.cp = cp; arr.f.classList.toggle('cp', cp);
        el.querySelectorAll('.tira,.agora').forEach((t) => t.classList.remove('alvo', 'nao', 'sobre'));
        el.querySelectorAll('.tira').forEach((t) => { const d = acha(t.dataset.k), mic = t.dataset.lado === 'mic'; let ok;
          // a sound program onto the mic side: the default mic (banner, or its row below) = into the call, Alt or not; other mics: not yet (needs a 3rd cable)
          if (mic && a.lado === 'som' && !a.c) ok = t.classList.contains('band') && !injetado(a.x);
          else if (cp) ok = d !== aqui && !clonado(a.x, d.n);
          else if (a.c) ok = !a.mi && !mic && d !== de && d !== ondeToca(a.x) && !clonado(a.x, d.n);
          else { if (t.dataset.k === de?.k) return; ok = t.dataset.lado === a.lado && (de || !t.classList.contains('band')); }
          t.classList.add(ok ? 'alvo' : 'nao'); });
        titulo(); const ag = el.querySelector(`.agora[data-g="${a.lado}"]`), h = ag.querySelector('.nota'); ag.style.setProperty('--ac', `var(--${a.lado})`);
        if (cp) h.textContent = a.n + ' continua aqui e toca também onde você soltar · num microfone, vai para a chamada';
        else if (a.c) { ag.classList.add('alvo'); h.textContent = 'Solte aqui e ' + a.n + (a.mi ? ' para de ir para o microfone' : ' para de tocar em ' + de.n); }
        else if (de) { ag.classList.add('alvo'); h.textContent = 'Solte aqui e ' + a.n + ' volta a seguir o padrão' + (a.lado === 'som' ? ' · segure Alt para tocar também em outro lugar' : ''); }
        else if (a.lado === 'som') h.textContent = 'Segure Alt para ' + a.n + ' tocar também onde soltar, sem sair daqui · na fila do microfone, vai para a chamada';
        const am = el.querySelector('.agora[data-g="mic"]');
        if (a.lado === 'som' && !a.c && padrao('mic') && !injetado(a.x)) { am.classList.add('alvo'); am.style.setProperty('--ac', 'var(--mic)'); am.querySelector('.nota').textContent = 'Solte aqui e ' + a.n + ' toca no microfone padrão: quem está na chamada ouve'; } };
      addEventListener('keydown', (e) => { if (e.key === 'Alt' && arr?.ok) { e.preventDefault(); if (!arr.alt) { arr.alt = true; alvos(); } } }, true);
      addEventListener('keyup', (e) => { if (e.key === 'Alt' && arr?.ok) { e.preventDefault(); if (arr.alt) { arr.alt = false; alvos(); } } }, true);
      const sobre = (x, y) => [...el.querySelectorAll('.tira.alvo,.agora.alvo')].find((t) => { const r = t.getBoundingClientRect(), o = t.closest('.outros')?.getBoundingClientRect();
        if (o && (x < o.left || x > o.right)) return false; return x >= r.left && x <= r.right && y >= r.top && y <= r.bottom; });
      const comeca = (x, y, alt) => { const v = arr.c.dataset.arr, [de, a] = v ? fixoDe(v) : [null, progDe(arr.c.dataset.chip)]; if (!a) return (arr = null);
        // home = where it flies back: the chip, its face in the box, or the box when the face is hidden behind "+N"
        const o = v ? el.querySelector(`[data-cx="${de.k}"] [data-arr="${CSS.escape(v)}"]`) || el.querySelector(`[data-cx="${de.k}"]`) || arr.c : arr.c.querySelector('.rt');
        Object.assign(arr, { a, de, o, ok: true }); fechaPop();
        const f = document.createElement('div'); f.className = 'ms-fant'; f.innerHTML = cara(a); f.style.setProperty('--ac', `var(--${a.lado})`); f.style.left = x + 'px'; f.style.top = y + 'px'; document.body.append(f); arr.f = f;
        (v ? o : arr.c).classList.add('origem'); el.classList.add('arr');
        // the banner only takes a pinned program (= back to the default); a loose one already plays there
        arr.alt = !!alt; alvos(); };
      const move = (x, y, alt) => { if (alt !== undefined && !!alt !== arr.alt) { arr.alt = !!alt; alvos(); } arr.f.style.left = x + 'px'; arr.f.style.top = y + 'px'; const t = sobre(x, y);
        el.querySelectorAll('.sobre').forEach((s) => s !== t && s.classList.remove('sobre')); t && t.classList.add('sobre'); };
      const limpa = () => { el.classList.remove('arr'); el.querySelectorAll('.tira,.agora').forEach((t) => t.classList.remove('alvo', 'nao', 'sobre')); titulo(); };
      let arrastou = 0;   // the click that follows a drop must not open the box
      const solta = (x, y, cancela) => { const { f, a, c, de, o, cp } = arr, t = !cancela && sobre(x, y); arr = null; limpa(); f.classList.add('voa'); arrastou = 1; setTimeout(() => (arrastou = 0));
        const voa = (r, some) => { Object.assign(f.style, { left: r.left + r.width / 2 + 'px', top: r.top + r.height / 2 + 'px' }); f.style.transform = some ? 'translate(-50%,-50%) scale(.4)' : 'translate(-50%,-50%)'; if (some) f.style.opacity = 0; };
        if (!t) { voa(o.getBoundingClientRect()); return setTimeout(() => { f.remove(); o.classList.remove('origem'); c.classList.remove('origem'); solto(); }, 350); }
        const pronto = (k) => setTimeout(() => { f.remove(); o.classList.remove('origem'); c.classList.remove('origem'); refazCopias(k); solto(); }, 330);
        if (a.lado === 'som' && !a.c && (t.dataset.lado || t.dataset.g) === 'mic') {   // default mic (banner or row): its chip lands in the mic row
          const fm = fila('mic'), u = [...fm.querySelectorAll('[data-injc]')].pop(); voa((u || fm).getBoundingClientRect(), 1); injetar(a.x, true); return pronto(); }
        if (cp || a.c) { const d = t.classList.contains('agora') ? null : acha(t.dataset.k);
          const casa = !d ? el.querySelector(`[data-chip="som:${CSS.escape(a.x)}"] .rt`) || o : t.querySelector('.cx') || t.querySelector('.bd-emb');
          voa(casa.getBoundingClientRect(), 1);
          if (a.c) { if (a.mi) injetar(a.x, false); else clonar(a.x, de.n, false); }
          if (d) clonar(a.x, d.n, true);
          return pronto(d && d.k); }
        const tirar = (d) => { d.fixos.splice(d.fixos.indexOf(a), 1); refazCx(d, 1); const bc = d.p && el.querySelector(`.band[data-k="${d.k}"] .bd-cont`); if (bc) bc.innerHTML = corpo(d, d.k.split(':')[0]); };
        if (t.classList.contains('agora') || t.classList.contains('band')) {   // unpin: back in the row, follows the default
          manda({ tipo: 'mixfixar', lado: a.lado, x: a.x, n: a.n, id: null }); tirar(de); el._sig = estrutura(); entra({ ...a });
          voa(fila(a.lado).lastElementChild.getBoundingClientRect(), 1); return setTimeout(() => { f.remove(); solto(); }, 330); }
        const d = acha(t.dataset.k); voa(t.querySelector('.cx').getBoundingClientRect(), 1);
        manda({ tipo: 'mixfixar', lado: a.lado, x: a.x, n: a.n, id: d.id }); if (de) tirar(de);
        setTimeout(() => { f.remove(); d.fixos.push({ x: a.x, n: a.n, lado: a.lado, v: a.v, m: a.m, s: a.s, ic: a.ic }); el._sig = estrutura(); refazCx(d); if (!de) sai(a); solto(); }, 330); };
      addEventListener('pointermove', (e) => { if (fad) { const r = fad.getBoundingClientRect(); return poe(fad, (r.bottom - e.clientY) / r.height * 100); }
        if (!arr) return; if (!arr.ok && Math.hypot(e.clientX - arr.x, e.clientY - arr.y) > 5) comeca(e.clientX, e.clientY, e.altKey); if (arr?.ok) move(e.clientX, e.clientY, e.altKey); });
      addEventListener('pointerup', (e) => { if (fad) return solto(); if (!arr) return; if (arr.ok) return solta(e.clientX, e.clientY);
        if (e.button !== 0 || (arr.c.dataset.arr && !arr.c.closest('.bd-fixos,.chip'))) return (arr = null);   // plain click on a face = the box's own click (banner faces: volume)
        const c = arr.c; arr = null; abreProg(c); });
      el.addEventListener('contextmenu', (e) => { const c = e.target.closest('.chip,[data-arr]'); if (!c) return; e.preventDefault(); if (arr?.ok) return; arr = null; abreProg(c); });
      el.addEventListener('scroll', (e) => e.target.classList?.contains('outros') && bordas(), true); addEventListener('resize', () => E.tela === 'mixer' && bordas());
      // wheel on a program chip (user 31/10) = its volume, 2 per notch, same as its popup slider (which follows if open); no magnet
      const rodaProg = (ch, e) => { const a = progDe(ch.dataset.chip); if (!a) return; const pd = a.pd ?? 100;
        const v = Math.max(0, Math.min(pd, (a.v ?? pd) + (e.deltaY < 0 ? 2 : -2))); if (v === a.v) return; mexeu(a.k);
        a.v = v; a.pr = v < pd ? 1 : 0; a.q = a.pr ? Math.round(v / Math.max(pd, 1) * 100) : 100;
        const sl = document.querySelector(`.ms-pop [data-c=slider][data-solto="${CSS.escape(a.k)}"]`); if (sl) { UI.poe(sl, v); const ap = sl.closest('.ap'); ap.querySelector('.vl').textContent = v + '%'; if (ap.title) ap.title = ap.title.replace(/(Volume próprio|Acompanha o volume)[^]*$/, sisDica(a)); }
        const sp = ch.querySelector(':scope>span'); if (sp) { sp.textContent = v + '%'; sp.classList.add('rodavol'); clearTimeout(sp._t); sp._t = setTimeout(() => { sp.textContent = a.n; sp.classList.remove('rodavol'); }, 1000); }
        manda({ tipo: 'mixapp', lado: a.lado, x: a.x, vol: v }); segueX(a); };
      // wheel on a copy (user 31/10: mic row chip, banner face) = the copy's own volume, like its popup slider
      const rodaCopia = (c, e) => { const [d, a] = fixoDe(c.dataset.arr); if (!d || !a) return; const k = (a.mi ? 'mic' : d.n) + '|' + a.x;
        const v = Math.max(0, Math.min(100, cpVer(k).vol + (e.deltaY < 0 ? 2 : -2))); volCopia(k, { vol: v, m: 0, segue: 0 }); mostraCopia(k);
        const sp = c.querySelector(':scope>span'); if (sp) { sp.textContent = v + '%'; sp.classList.add('rodavol'); clearTimeout(sp._t); sp._t = setTimeout(() => { sp.textContent = a.n; sp.classList.remove('rodavol'); }, 1000); } };
      el.addEventListener('wheel', (e) => { const ch = e.target.closest('.chip[data-chip]'); if (ch) { e.preventDefault(); return rodaProg(ch, e); }
        const cp = e.target.closest('.chip.cpm[data-arr],.bd-fixos [data-arr$="|c"]'); if (cp) { e.preventDefault(); return rodaCopia(cp, e); }
        const vf = e.target.closest('.vf'), o = !vf && e.target.closest('.outros');
        if (o && o.scrollWidth > o.clientWidth) { e.preventDefault(); o.scrollLeft += e.deltaY || e.deltaX; return; } if (!vf) return; e.preventDefault(); poe(vf, acha(vf.dataset.vf).vol + (e.deltaY < 0 ? 2 : -2)); }, { passive: false });
      el.addEventListener('keydown', (e) => { const vf = e.target.closest('.vf'); if (vf) { const s = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1, PageUp: 10, PageDown: -10 }[e.key];
          if (s) { e.preventDefault(); poe(vf, acha(vf.dataset.vf).vol + s); } }
        const c = e.target.closest('.chip'); if (c && (e.key === 'Enter' || e.key === ' ' || e.key === 'ContextMenu')) { e.preventDefault(); abreProg(c); }
        const l = e.target.closest('[data-lembra]'); if (l && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); l.click(); } });
      el.addEventListener('click', (e) => { if (arrastou) return;
        // icon = make this the Windows default (the default's own icon does nothing)
        const pb = e.target.closest('[data-pad]'); if (pb) { const d = acha(pb.dataset.pad), g = pb.dataset.pad.split(':')[0]; if (!d || d.p) return;
          // programs pinned to it go back to the row first: on the default they need no box
          d.fixos.splice(0).forEach((a) => { manda({ tipo: 'mixfixar', lado: a.lado, x: a.x, n: a.n, id: null }); entra({ ...a }); }); el._sig = estrutura();
          S[g].forEach((x) => (x.p = x === d ? 1 : 0)); mexeu('pad:' + g); manda({ tipo: 'mixpadrao', lado: g, id: d.id }); fechaPop(); return marcaPadrao(g); }
        const by = e.target.closest('[data-byp]'); if (by) { const d = acha(by.dataset.byp); if (!d) return; const g = d.k.split(':')[0], on = !byLig(d), B = BY(), cv = d.k.replace(':', '|');
          // optimistic: shown at once, the engine's "bypass" answer corrects it (paused when the device is the default or fails)
          App.estado.bypass = { ...B, [g]: on ? [...(B[g] || []), d.id] : (B[g] || []).filter((x) => x !== d.id), ativos: on ? [...(B.ativos || []), ...(g === 'som' && d.p ? [] : [cv])] : (B.ativos || []).filter((x) => x !== cv) };
          manda({ tipo: 'bypass', lado: g, id: d.id, on }); marcaByp();
          if (on && g === 'mic') App.toast?.('Bypass do microfone ligado: use fone, na caixa de som ele microfona'); return; }
        const b = e.target.closest('[data-cx]'); if (b) { const d = acha(b.dataset.cx); if (!tudo(d).length) return; const k = 'c:' + d.k; if (pop()._k === k) return fechaPop(); return abrePop(k, caixa(d), b, 1); }
        const tr = e.target.closest('[data-tira]'); if (tr) { const [dk, x] = tr.dataset.tira.split('|'), d = acha(dk); if (!d) return;
          if (dk.startsWith('mic:')) injetar(x, false); else clonar(x, d.n, false);
          const ab = pop()._k; fechaPop(); refazCopias(); if (ab === 'c:' + d.k && tudo(d).length) { const b = el.querySelector(`[data-cx="${d.k}"]`); if (b) abrePop(ab, caixa(d), b, 1); } return; }
        // link: following -> own (the stored own vol/mute come back); own -> following (own vol/mute kept for next time)
        const sg = e.target.closest('[data-segue]'); if (sg) { const k = sg.dataset.segue; volCopia(k, { segue: cpVer(k).segue ? 0 : 1 }); mostraCopia(k); return; }
        const l = e.target.closest('[data-lembra]'); if (l) { const a = qualquer(l.dataset.lembra); if (!a) return; a.s = a.s ? 0 : 1; mexeu(l.dataset.lembra);
          l.classList.toggle('on', !!a.s); l.setAttribute('aria-checked', !!a.s); manda({ tipo: 'mixapp', lado: a.lado, x: a.x, s: !!a.s }); return; }
        if (!e.target.closest('.ms-pop,.chip')) fechaPop(); });
      // badges follow the engine (mic list changes, program turned off elsewhere)
      const marcaInj = () => el.querySelectorAll('.agora[data-g=som] .chip').forEach((c) => { const a = S.soltos.find((x) => x.k === c.dataset.chip); if (!a) return;
        const rt = c.querySelector('.rt'), m = marca(a); if (rt._m !== m) { rt._m = m; rt.innerHTML = cara(a) + selo(a); } });
      // copies changed in the engine (or a program closed): boxes/banners redrawn only when the copy list really changed
      addEventListener('fah:mic', () => E.tela === 'mixer' && !arr && refazCopias());
      addEventListener('fah:clones', () => E.tela === 'mixer' && !arr && refazCopias());
      // bypass buttons + the strip line follow the engine, in place (no rebuild)
      const marcaByp = () => el.querySelectorAll('[data-byp]').forEach((b) => { const d = acha(b.dataset.byp); if (!d) return; const e = byEstado(d);
        if (b.classList.contains('on') !== (e === 'on') || b.classList.contains('pausa') !== (e === 'pausa')) { b.classList.toggle('on', e === 'on'); b.classList.toggle('pausa', e === 'pausa'); b.setAttribute('aria-pressed', !!e); }
        b.dataset.dica = byDica(d); const t = b.closest('.tira:not(.band)'), pd = t?.querySelector(':scope>.pd'); if (!pd) return;
        const novo = byPd(d); if (pd.outerHTML !== novo) pd.outerHTML = novo; });
      addEventListener('fah:bypass', () => E.tela === 'mixer' && marcaByp());
      addEventListener('fah:copiavol', () => E.tela === 'mixer' && document.querySelectorAll('.ms-pop [data-c=slider][data-cv]').forEach((sl) => !fresco('cv:' + sl.dataset.cv) && mostraCopia(sl.dataset.cv)));
      addEventListener('keydown', (e) => { if (e.key !== 'Escape' || E.tela !== 'mixer') return;
        if (arr?.ok) { e.stopImmediatePropagation(); return solta(0, 0, 1); } if (pop()?.classList.contains('ab')) { e.stopImmediatePropagation(); fechaPop(); } }, true);
      el.addEventListener('c:mudou', (e) => { const { tipo, valor, dados } = e.detail;
        // copy: moving its volume = own volume, heard (unmutes); muting while following = own, with the stored own volume
        if (dados.cv) { const k = dados.cv, c = cpVer(k);
          if (tipo === 'slider') volCopia(k, { vol: Math.round(valor), m: 0, segue: 0 });
          if (tipo === 'mudo') volCopia(k, c.segue ? { m: !!valor, vol: cvDe(k)?.vol ?? c.vol, segue: 0 } : { m: !!valor });
          mostraCopia(k); return; }
        // program: loose (solto = "lado:exe") or pinned (fx = "deviceKey|exe")
        const a = dados.solto ? progDe(dados.solto) : dados.fx ? (() => { const [dk, x] = dados.fx.split('|'); return acha(dk)?.fixos.find((p) => p.x === x); })() : null;
        if (a) { mexeu(a.lado + ':' + a.x);
          if (tipo === 'slider') { const pd = a.pd ?? 100, ap = e.target.closest('.ap'); let v = Math.round(valor); if (e.target._roda ? v >= pd : v >= pd - IMA) { v = pd; if (+e.target.dataset.v !== v) UI.poe(e.target, v); }
            a.v = v; a.pr = v < pd ? 1 : 0; a.q = a.pr ? Math.round(v / Math.max(pd, 1) * 100) : 100; ap.querySelector('.vl').textContent = v + '%'; if (ap.title) ap.title = ap.title.replace(/(Volume próprio|Acompanha o volume)[^]*$/, sisDica(a));
            manda({ tipo: 'mixapp', lado: a.lado, x: a.x, vol: v }); segueX(a); }
          if (tipo === 'mudo') { a.m = valor ? 1 : 0; e.target.closest('.ap').classList.toggle('calado', !!valor); el.querySelector(`[data-chip="${a.lado}:${a.x}"]`)?.classList.toggle('calado', !!valor); manda({ tipo: 'mixapp', lado: a.lado, x: a.x, m: !!valor }); segueX(a); }
          return; }
        if (dados.k && tipo === 'mudo') { const d = acha(dados.k); d.m = valor ? 1 : 0; mexeu(d.k); e.target.closest('.tira').classList.toggle('calado', !!valor); manda({ tipo: 'mixmudo', id: d.id, m: !!valor }); } });
      // engine reads sessions/peaks only while this screen is on and the window is visible
      document.addEventListener('visibilitychange', () => real && E.tela === 'mixer' && manda({ tipo: 'mixer', on: !document.hidden }));
      if (tem('caixa')) setTimeout(() => el.querySelector('[data-cx="som:tv"]')?.click(), 60);
    },
    aoSair() { if (real) manda({ tipo: 'mixer', on: false }); document.querySelector('.t-mesa .ms-pop')?.classList.remove('ab'); } });
  App.dock({ id: 'mixer', icone: 'mix', dica: 'Mixer e dispositivos', tela: 'mixer', ordem: 3 });
})();
