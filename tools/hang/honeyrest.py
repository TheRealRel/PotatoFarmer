# Where each honeycomb comes to rest. The comb hangs from the smaller curl (pivot = its hang point there, as before);
# poses tried: a quarter-turn set of yaws (which way its flat face points) times tilts about every level axis
# (15-degree steps, 0 to 90 degrees). Clipping = share of comb sample points inside any meat on a full rack (solid
# test: vertical-ray parity on a 1 cm grid of each hung meat). Kept: the least clipping (to 1 %), then within 2 %
# of it the pose whose centre hangs lowest (where gravity would leave it), then the smaller tilt.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
import anchor2
S2 = 2.0; STEP = 0.01

class Solid:
    def __init__(self, W, F):
        anchor2.STEP = STEP
        P, (nx, nz), hits = anchor2.columns(W, F)
        self.x0, self.z0 = P[0, 0], P[0, 1]; self.nx, self.nz = nx, nz
        m = max(1, max(len(h) for h in hits))
        self.H = np.full((len(hits), m), -np.inf)
        for i, h in enumerate(hits): self.H[i, :len(h)] = h
    def inside(self, Q):
        ix = np.rint((Q[:, 0] - self.x0) / STEP).astype(int); iz = np.rint((Q[:, 2] - self.z0) / STEP).astype(int)
        ok = (ix >= 0) & (ix < self.nx) & (iz >= 0) & (iz < self.nz)
        out = np.zeros(len(Q), bool)
        idx = ix[ok] * self.nz + iz[ok]
        out[ok] = (np.sum(self.H[idx] > Q[ok, 1][:, None], axis=1) % 2) == 1
        return out

def comb_samples(Vh, Fh):
    # surface: vertices + triangle centres + edge midpoints (the comb is a thin slab)
    c = Vh[Fh].mean(1); e = (Vh[Fh[:, 0]] + Vh[Fh[:, 1]]) / 2
    return np.concatenate([Vh, c, e])

def rot_axis(ax, deg):
    ax = np.asarray(ax, float); ax = ax / np.linalg.norm(ax); t = np.radians(deg)
    K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    return np.eye(3) + np.sin(t) * K + (1 - np.cos(t)) * K @ K

POSES = [(0, 0, 0)] + [(yaw, phi, th) for yaw in (0, 45, 90, 135) for th in range(15, 91, 15) for phi in range(0, 360, 30)] + [(yaw, 0, 0) for yaw in (45, 90, 135)]

def settle(n):
    h3.HONEY_TILT.pop(n, None)
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
        Mb = combs[i]; base = (Mb[:3, :3] @ Sh.T).T + Mb[:3, 3]
        best = None; rows = []
        for yaw, phi, th in POSES:
            Rr = rot_axis([np.sin(np.radians(phi)), 0, np.cos(np.radians(phi))], th) @ rot_axis([0, 1, 0], yaw)
            Q = (Rr @ (base - pivot).T).T + pivot
            clip = np.mean(np.any([s.inside(Q) for s in solids], axis=0))
            rows.append((round(clip, 2), Q[:, 1].mean(), th, yaw, phi, Rr))
        least = min(r[0] for r in rows)
        pool = [r for r in rows if r[0] <= least + 0.02]
        clip, cy, th, yaw, phi, Rr = min(pool, key=lambda r: (r[1], r[2]))
        straight = rows[0]
        M4 = np.eye(4); M4[:3, :3] = Rr
        final = T(pivot) @ M4 @ T(-pivot) @ Mb
        out.append(dict(hook=i, clip=clip, straight_clip=straight[0], yaw=yaw, phi=phi, tilt=th, M=final.tolist()))
    return out

if __name__ == '__main__':
    res = {}
    for n in sys.argv[1].split(','):
        r = settle(n); res[n] = r
        print(f"{n:20s} clipping straight {max(x['straight_clip'] for x in r)*100:3.0f}% -> worst hook {max(x['clip'] for x in r)*100:3.0f}%   tilts {[x['tilt'] for x in r]}", flush=True)
    old = json.load(open(sys.argv[2])) if len(sys.argv) > 3 else {}
    old.update(res); json.dump(old, open(sys.argv[2], 'w'))
