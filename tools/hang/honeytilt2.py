# Comb swing search, any direction: tilt about a horizontal axis at angle phi (0 = across the bar, i.e. swing along
# the bar), by theta, pivoting on the smaller curl. Keep: eye-level share seen from outside >= GOAL on every outer
# hook. Among those: least comb inside its own meat (rounded to 5 %), then the smallest tilt.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3; import honeyvis as hv
GOAL = 0.30

def inside(W, F, P):
    A, B, C = W[F[:, 0]], W[F[:, 1]], W[F[:, 2]]; v0 = (C - A)[:, [0, 2]]; v1 = (B - A)[:, [0, 2]]
    d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); den = d00 * d11 - d01 * d01; ok = np.abs(den) > 1e-12; den = np.where(ok, den, 1.0)
    out = np.zeros(len(P), bool)
    for k, p in enumerate(P):
        v2 = p[[0, 2]] - A[:, [0, 2]]; d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
        u = (d11 * d20 - d01 * d21) / den; v = (d00 * d21 - d01 * d20) / den; ins = ok & (u >= 0) & (v >= 0) & (u + v <= 1)
        y = np.unique(np.round(A[ins, 1] + u[ins] * (C - A)[ins, 1] + v[ins] * (B - A)[ins, 1], 4)); out[k] = np.count_nonzero(y > p[1]) % 2 == 1
    return out

def tilt_matrix(phi, theta):
    # axis in the horizontal plane: phi 0 -> along z (so the comb swings along x, the bar)
    a = np.radians(phi); ax = np.array([np.sin(a), 0, np.cos(a)])
    t = np.radians(theta); K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    return np.eye(3) + np.sin(t) * K + (1 - np.cos(t)) * K @ K

def evaluate(n, meats, honeys, pivots, phi, theta, every=2):
    Rt = tilt_matrix(phi, theta); vis = []; ins = []
    for i in range(9):
        P = ((Rt @ (honeys[i][0] - pivots[i]).T).T + pivots[i])[::every]
        W, F = meats[i]
        ins.append(inside(W, F, P).mean())
        if i in (0, 1, 2, 6, 7, 8):
            tris = (W[F[:, 0]], W[F[:, 1]], W[F[:, 2]])
            vis.append(1 - hv.occluded(tris, hv.VIEWS['front' if i < 3 else 'back'], P).mean())
    return min(vis), max(ins)

def search(n):
    h3.HONEY_TILT[n] = 0
    meats, honeys = hv.rack(n)
    pivots = [np.array([hp.HOOKS[i]['small'][k] for k in ('cup_x', 'cup_y', 'cup_z')]) * 2.0 + [0, hp.PIERCE * 2.0, 0] for i in range(9)]
    base = evaluate(n, meats, honeys, pivots, 0, 0)
    cands = []
    for theta in range(10, 61, 10):
        for phi in range(0, 360, 30):
            v, i = evaluate(n, meats, honeys, pivots, phi, theta)
            cands.append((phi, theta, v, i))
    ok = [c for c in cands if c[2] >= GOAL]
    pick = min(ok, key=lambda c: (round(c[3] * 20), c[1])) if ok else max(cands, key=lambda c: (c[2], -c[3]))
    return base, pick

if __name__ == '__main__':
    out = {}
    for n in sys.argv[1].split(','):
        base, (phi, theta, v, i) = search(n); out[n] = [phi, theta]
        print(f"{n:20s} base: seen {base[0]*100:3.0f}% inside {base[1]*100:3.0f}%  ->  axis {phi:3d} tilt {theta:2d}: seen {v*100:3.0f}% inside {i*100:3.0f}%", flush=True)
    json.dump(out, open(sys.argv[2], 'w'))
