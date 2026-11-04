# How much of each honeycomb shows on a full rack (nine of the same meat): honey points (its mesh, in the game's
# space) that no meat on the rack hides, from six ways of looking at the rack. Neighbouring honeycombs don't count
# as hiding (they are the same thing).
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
S2 = 2.0
VIEWS = {
    'front':      np.array([0.1, 1.75, -3.2]),   # eye level, outside the front row
    'back':       np.array([0.1, 1.75, 3.2]),
    'end':        np.array([3.6, 1.7, 0.0]),
    'front_high': np.array([1.2, 3.0, -2.6]),    # standing near it, camera above, looking down (the user's shots)
    'back_high':  np.array([-1.2, 3.0, 2.6]),
    'front_34':   np.array([2.6, 1.9, -2.6]),
}
def world(n, M):
    V, F = mesh(n); return (M[:3, :3] @ V.T).T + M[:3, 3], F

def occluded(tris, cam, P):
    A, B, C = tris; e1, e2 = B - A, C - A; out = np.zeros(len(P), bool)
    for k, p in enumerate(P):
        d = p - cam; L = np.linalg.norm(d); d = d / L
        h = np.cross(d, e2); a = (e1 * h).sum(1); ok = np.abs(a) > 1e-12; a = np.where(ok, a, 1.0)
        f = 1 / a; s = cam - A; u = f * (s * h).sum(1); q = np.cross(s, e1); v = f * (q * d).sum(1); t = f * (e2 * q).sum(1)
        out[k] = (ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-4) & (t < L - 1e-3)).any()
    return out

def rack(n, honey_override=None):
    objs, _ = h3.build(S2, [(n, None)] * 9, 'raw')
    meats, honeys = [], []
    for o in objs[1:]:
        M = np.array(o['m']).reshape(4, 4).T
        nm = o['src'].split('/')[-1][:-5]
        (honeys if nm == 'Honey' else meats).append(world(nm, M))
    return meats, honeys

def vis(n, every=3):
    meats, honeys = rack(n)
    A = np.concatenate([m[0][m[1][:, 0]] for m in meats]); B = np.concatenate([m[0][m[1][:, 1]] for m in meats]); C = np.concatenate([m[0][m[1][:, 2]] for m in meats])
    res = {}
    for vn, cam in VIEWS.items():
        per = []
        for i, (W, F) in enumerate(honeys):
            P = W[::every]
            per.append(1 - occluded((A, B, C), cam, P).mean())
        res[vn] = per
    return res

if __name__ == '__main__':
    for n in sys.argv[1].split(','):
        r = vis(n)
        print(f"{n:20s} " + "  ".join(f"{k} {np.mean(v)*100:3.0f}% (min {np.min(v)*100:3.0f})" for k, v in r.items()))

def own_views(i):
    """The ways a player sees hook i: from outside its own row (eye level and from above), and from the rack's end."""
    row = i // 3
    if row == 0: return ['front', 'front_high', 'end']
    if row == 2: return ['back', 'back_high', 'end']
    return ['front_high', 'back_high', 'end']

def vis_own(n, every=2, extra=None):
    """Per hook: share of its honeycomb its own meat leaves showing, from each of the hook's own views."""
    meats, honeys = rack(n) if extra is None else extra
    out = []
    for i in range(9):
        W, F = meats[i]; tris = (W[F[:, 0]], W[F[:, 1]], W[F[:, 2]])
        P = honeys[i][0][::every]
        out.append({v: 1 - occluded(tris, VIEWS[v] if v != 'end' else np.array([np.sign(P[:, 0].mean() or 1) * 3.6, 1.7, P[:, 2].mean()]), P).mean() for v in own_views(i)})
    return out
