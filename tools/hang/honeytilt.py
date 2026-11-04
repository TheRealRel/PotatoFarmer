# The least sideways swing (about the axis across the bar, pivoting on the smaller curl) that lets each meat's
# honeycomb be seen at eye level from outside its row: at least GOAL of the comb unhidden by its own meat on every
# outer-row hook. Either direction along the bar; up to 60 degrees.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3; import honeyvis as hv
GOAL, STEP, MAX = 0.30, 5, 60

def inside(tris, P):
    A, B, C = tris; out = np.zeros(len(P), bool)
    for k, p in enumerate(P):
        h = hv.occluded(tris, p + np.array([0, 50.0, 0]), [p])  # ray straight up: odd hits = inside (approx: any hit)
    return out

def honey_at(hW, pivot, deg):
    Rz = R('z', deg)[:3, :3]
    return (Rz @ (hW - pivot).T).T + pivot

def search(n):
    meats, honeys = hv.rack(n)
    pivots = [np.array([hp.HOOKS[i]['small'][k] for k in ('cup_x', 'cup_y', 'cup_z')]) * 2.0 + [0, hp.PIERCE * 2.0, 0] for i in range(9)]
    outer = [0, 1, 2, 6, 7, 8]
    def score(deg):
        v = []
        for i in outer:
            W, F = meats[i]; tris = (W[F[:, 0]], W[F[:, 1]], W[F[:, 2]])
            P = honey_at(honeys[i][0], pivots[i], deg)[::2]
            cam = hv.VIEWS['front' if i < 3 else 'back']
            v.append(1 - hv.occluded(tris, cam, P).mean())
        return min(v), float(np.mean(v))
    base = score(0)
    if base[0] >= GOAL: return 0, base, base
    best = (0, base)
    for deg in range(STEP, MAX + 1, STEP):
        for sgn in (1, -1):
            s = score(sgn * deg)
            if s[0] > best[1][0] + 1e-9: best = (sgn * deg, s)
        if best[1][0] >= GOAL: break
    return best[0], best[1], base

if __name__ == '__main__':
    out = {}
    for n in sys.argv[1].split(','):
        deg, s, base = search(n); out[n] = deg
        print(f"{n:20s} swing {deg:+3d} deg   eye level from outside: worst hook {base[0]*100:3.0f}% -> {s[0]*100:3.0f}%  (mean {base[1]*100:3.0f}% -> {s[1]*100:3.0f}%)")
    json.dump(out, open(sys.argv[2], 'w'))
