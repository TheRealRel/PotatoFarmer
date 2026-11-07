# Least lift (model units, above the hook's valley) for each jerky so the hook goes through it: at the valley, the
# jerky's face covers the hook +-3 cm (world) along the bar, on every hook, both copies. At least MIN_LIFT.
import sys, json, struct, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
S2 = 2.0; MIN_LIFT = 0.02; HALF = 0.03
VAL = json.load(open('/root/rackview/valley.json'))
hp.JERKY_MODE['jerky/jerkyshape_deer.mesh'] = 'diag'
LIFT = {}
def at_fn(i, S, src):
    l = LIFT.get(src, MIN_LIFT)
    return tuple(np.array([VAL[i][w]['x'], VAL[i][w]['y'] + l, VAL[i][w]['z']]) * S for w in ('big', 'small'))
hp.JERKY_AT = at_fn
def ptm_vf(path):
    d = open(path, 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12]); o = 12
    V = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3).astype(float); o += nv * 32
    return V, np.frombuffer(d, '<i4', ni, o).reshape(-1, 3)
def placed(load, i):
    loads = [None] * 9; loads[i] = load
    objs, _ = h3.build(S2, loads, 'done'); out = []
    for k, o in enumerate(objs[1:]):
        M = np.array(o['m']).reshape(4, 4).T
        V, F = mesh(o['src'].split('/')[-1][:-5]) if o['kind'] == 'item' else ptm_vf(o['src'])
        out.append(((M[:3, :3] @ V.T).T + M[:3, 3], F, 'big' if k == 0 else 'small'))
    return out
def inside_xy(W, F, pts):
    A, B, C = W[F[:, 0]][:, :2], W[F[:, 1]][:, :2], W[F[:, 2]][:, :2]
    res = []
    for p in pts:
        v0 = C - A; v1 = B - A; v2 = p - A
        d00 = (v0*v0).sum(1); d01 = (v0*v1).sum(1); d11 = (v1*v1).sum(1); d20 = (v2*v0).sum(1); d21 = (v2*v1).sum(1)
        den = d00*d11 - d01*d01; ok = np.abs(den) > 1e-14; den = np.where(ok, den, 1)
        u = (d11*d20 - d01*d21)/den; v = (d00*d21 - d01*d20)/den
        res.append(bool((ok & (u >= -1e-6) & (v >= -1e-6) & (u+v <= 1+1e-6)).any()))
    return np.array(res)
def check(load):
    src = load[1][1]; worst = 1.0; where = ''
    for i in range(9):
        for W, F, w in placed(load, i):
            hole = np.array([VAL[i][w]['x'], VAL[i][w]['y']]) * S2
            pts = np.array([[hole[0] + dx, hole[1]] for dx in np.arange(-HALF, HALF + 1e-9, 0.005)])
            f = inside_xy(W, F, pts).mean()
            if f < worst: worst, where = f, f"hook {i} {w}"
    return worst, where
J = {'Boar': ('RawMeat', ('item', 'hang/BoarJerky.json', None)), 'Wolf': ('WolfMeat', ('item', 'hang/WolfJerky.json', None)),
     'BearTW': ('BearSteak_TW', ('item', 'hang/BearJerky_TW.json', None)), 'FoxTW': ('FoxMeat_TW', ('item', 'hang/FoxJerky_TW.json', None)),
     'Deer': ('DeerMeat', ('ptm', 'jerky/jerkyshape_deer.mesh', 'jerky/jerky_deer_d.png')),
     'Fan': ('NeckTail', ('ptm', 'jerky/jerkyshape_bloodgill.mesh', 'jerky/jerky_necktail_d.png')),
     'Lox': ('LoxMeat', ('ptm', 'jerky/jerkyshape_lox.mesh', 'jerky/jerky_lox_d.png')),
     'Serpent': ('SerpentMeat', ('ptm', 'jerky/jerkyshape_serpent.mesh', 'jerky/jerky_serpent_d.png'))}
if __name__ == '__main__':
    for n in sys.argv[1].split(','):
        load = J[n]; src = load[1][1]; line = []
        for l in np.arange(MIN_LIFT, 0.0601, 0.005):
            LIFT[src] = l; f, wh = check(load); line.append(f"{l:.3f}:{f:.2f}")
            if f >= 0.999: break
        print(n, ' '.join(line), wh, flush=True)
