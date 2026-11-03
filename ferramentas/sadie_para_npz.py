"""SADIE II (York, Apache 2.0) subject zips -> horizontal-plane HRIR npz in the same layout as hrtf_kemar.npz.
Stdlib only (no numpy). Usage: python -I sadie_para_npz.py <folder with D1.zip, H3.zip...> <out folder>
Out per subject: elev.npy (f8), az.npy (f8, 0..180, + = right), hrir.npy (f4, [dir][tap][ear]), 48 kHz.
Also prints a front/back score: log-spectral distance between 30 and 150 deg (1-16 kHz), bigger = clearer front/back."""
import io, math, struct, sys, wave, zipfile
from pathlib import Path

TAPS = 256


def ler_wav(b):
    w = wave.open(io.BytesIO(b))
    assert w.getframerate() == 48000 and w.getnchannels() == 2 and w.getsampwidth() == 3
    d = w.readframes(w.getnframes())
    s = [int.from_bytes(d[i:i + 3], 'little', signed=True) / 8388608.0 for i in range(0, len(d), 3)]
    return s[0::2], s[1::2]


def npy(arr_bytes, descr, shape):
    forma = f'{shape[0]},' if len(shape) == 1 else ', '.join(map(str, shape))
    cab = ("{'descr': '%s', 'fortran_order': False, 'shape': (%s), }" % (descr, forma)).encode('ascii')
    pad = 64 - (10 + len(cab) + 1) % 64
    cab += b' ' * pad + b'\n'
    return b'\x93NUMPY\x01\x00' + struct.pack('<H', len(cab)) + cab + arr_bytes


def espectro_db(x, n=512):
    out = []
    for k in range(1, n // 2):
        re = sum(v * math.cos(2 * math.pi * k * i / n) for i, v in enumerate(x))
        im = sum(v * math.sin(2 * math.pi * k * i / n) for i, v in enumerate(x))
        out.append(10 * math.log10(re * re + im * im + 1e-12))
    return out


def nota_frente_tras(dirs):
    def perto(a): return min(dirs, key=lambda d: abs(d[0] - a))
    f, t = perto(30), perto(150)
    soma = cont = 0
    for e in (1, 2):
        a, b = espectro_db(f[e]), espectro_db(t[e])
        for k in range(len(a)):
            hz = (k + 1) * 48000 / 512
            if 1000 <= hz <= 16000:
                # 1/3-octave-ish smoothing via neighbour mean
                lo, hi = max(0, k - 2), min(len(a), k + 3)
                da = sum(a[lo:hi]) / (hi - lo); db = sum(b[lo:hi]) / (hi - lo)
                soma += (da - db) ** 2; cont += 1
    return math.sqrt(soma / cont)


def converter(zp, saida):
    nome = zp.stem
    z = zipfile.ZipFile(zp)
    pre = f'{nome}/{nome}_HRIR_WAV/48K_24bit/'
    dirs = []
    for n in z.namelist():
        if not n.startswith(pre) or not n.endswith('_ele_0,0.wav'): continue
        azi = float(n[len(pre):].split('azi_')[1].split('_')[0].replace(',', '.'))
        a = (360 - azi) % 360                      # SADIE + = left -> ours + = right
        if a > 180.0001: continue
        l, r = ler_wav(z.read(n))
        dirs.append((round(a, 1), l, r))
    dirs.sort(key=lambda d: d[0])
    # drop the shared leading silence (keep 8 samples before the earliest onset)
    ini = min(min(i for i, v in enumerate(e) if abs(v) > 0.05 * max(map(abs, e))) for d in dirs for e in d[1:])
    corte = max(0, ini - 8)
    dirs = [(a, (l[corte:] + [0.0] * TAPS)[:TAPS], (r[corte:] + [0.0] * TAPS)[:TAPS]) for a, l, r in dirs]
    n = len(dirs)
    h = bytearray()
    for _, l, r in dirs:
        for t in range(TAPS): h += struct.pack('<ff', l[t], r[t])
    with zipfile.ZipFile(saida / f'hrtf_{nome.lower()}.npz', 'w', zipfile.ZIP_DEFLATED) as o:
        o.writestr('elev.npy', npy(struct.pack(f'<{n}d', *[0.0] * n), '<f8', (n,)))
        o.writestr('az.npy', npy(struct.pack(f'<{n}d', *[d[0] for d in dirs]), '<f8', (n,)))
        o.writestr('hrir.npy', npy(bytes(h), '<f4', (n, TAPS, 2)))
    return nome, n, corte, nota_frente_tras(dirs)


if __name__ == '__main__':
    ent, sai = Path(sys.argv[1]), Path(sys.argv[2])
    sai.mkdir(parents=True, exist_ok=True)
    for zp in sorted(ent.glob('*.zip')):
        try: print('%s dirs=%d corte=%d frente/tras=%.2f dB' % converter(zp, sai), flush=True)
        except Exception as e: print(zp.name, 'ERRO', e, flush=True)
