# The honeycomb lies along the meat. Same comb, same curl as honeyrest.py; what's scored changed (test i's combs
# kicked out into the air on top of the big meats). Around each hook a 1 cm grid of signed distance to the meat
# surface (all nine meats on a full rack; + outside, - inside). Each pose's comb sample points look it up:
#   cost = mean distance out in the air + FAR_WEIGHT x its 90th percentile + IN_WEIGHT x mean depth inside the meat
# so the comb wants to lie on the surface, sinking in a little where it must. Poses: 12 face directions (15-degree
# steps) x tilts 0 to 90 degrees (5-degree steps) about every level axis (15-degree steps). Kept: the least cost (to
# 5 mm), then within 5 mm of it the pose whose centre hangs lowest (where gravity leaves it), then the smaller tilt.
import sys, json, numpy as np
from scipy import ndimage
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
from honeyrest import Solid, comb_samples, rot_axis
S2 = 2.0; G = 0.01; IN_WEIGHT = 2.0; FAR_WEIGHT = 0.5
POSES = [(0, 0, 0)] + [(yaw, phi, th) for yaw in range(0, 180, 15) for th in range(5, 91, 5) for phi in range(0, 360, 15)] + [(yaw, 0, 0) for yaw in range(15, 180, 15)]

def sdf(solids, lo, hi):
    xs = np.arange(lo[0], hi[0], G); ys = np.arange(lo[1], hi[1], G); zs = np.arange(lo[2], hi[2], G)
    X, Y, Z = np.meshgrid(xs, ys, zs, indexing='ij'); P = np.stack([X.ravel(), Y.ravel(), Z.ravel()], 1)
    occ = np.zeros(len(P), bool)
    for s in solids: occ |= s.inside(P)
    occ = occ.reshape(X.shape)
    d = (ndimage.distance_transform_edt(~occ) - ndimage.distance_transform_edt(occ)) * G
    return d, np.array(lo)

def look(d, lo, Q):
    i = np.clip(np.rint((Q - lo) / G).astype(int), 0, np.array(d.shape) - 1)
    return d[i[:, 0], i[:, 1], i[:, 2]]

def settle(n):
    h3.HONEY_REST.pop(n, None); h3.HONEY_TILT.pop(n, None)
    objs, _ = h3.build(S2, [(n, None)] * 9, 'raw')
    Vm, Fm = mesh(n); Vh, Fh = mesh('Honey'); Sh = comb_samples(Vh, Fh)
    meats, combs = [], []
    for o in objs[1:]:
        M = np.array(o['m']).reshape(4, 4).T
        (combs if 'Honey' in o['src'] else meats).append(M)
    solids = [Solid((M[:3, :3] @ Vm.T).T + M[:3, 3], Fm) for M in meats]
    out = []
    for i in range(9):
        H = hp.HOOKS[i]
        pivot = np.array([H['small']['cup_x'], H['small']['cup_y'] + hp.PIERCE, H['small']['cup_z']]) * S2
        d, lo = sdf(solids, pivot - [0.45, 0.5, 0.45], pivot + [0.45, 0.25, 0.45])
        Mb = combs[i]; base = (Mb[:3, :3] @ Sh.T).T + Mb[:3, 3]
        rows = []
        for yaw, phi, th in POSES:
            Rr = rot_axis([np.sin(np.radians(phi)), 0, np.cos(np.radians(phi))], th) @ rot_axis([0, 1, 0], yaw) if th else rot_axis([0, 1, 0], yaw)
            Q = (Rr @ (base - pivot).T).T + pivot
            v = look(d, lo, Q)
            # far end sticking out into the air (the 90th-percentile gap) counts too: that is the 'kick out'
            cost = np.mean(np.maximum(v, 0)) + FAR_WEIGHT * np.percentile(np.maximum(v, 0), 90) + IN_WEIGHT * np.mean(np.maximum(-v, 0))
            rows.append((cost, Q[:, 1].mean(), th, yaw, phi, Rr, np.mean(v < 0), np.mean(np.maximum(v, 0))))
        least = min(r[0] for r in rows)
        pool = [r for r in rows if r[0] <= least + 0.005]
        cost, cy, th, yaw, phi, Rr, inside, gap = min(pool, key=lambda r: (r[1], r[2]))
        M4 = np.eye(4); M4[:3, :3] = Rr
        out.append(dict(hook=i, cost=cost, inside=inside, gap=gap, yaw=yaw, phi=phi, tilt=th, M=(T(pivot) @ M4 @ T(-pivot) @ Mb).tolist()))
    return out

if __name__ == '__main__':
    res = json.load(open(sys.argv[2])) if len(sys.argv) > 3 else {}
    for n in sys.argv[1].split(','):
        r = settle(n); res[n] = r
        print(f"{n:20s} inside worst {max(x['inside'] for x in r)*100:3.0f}%  mean gap to meat worst {max(x['gap'] for x in r)*100:4.1f} cm  tilts {[x['tilt'] for x in r]}", flush=True)
        json.dump(res, open(sys.argv[2], 'w'))
