# The honeycomb hangs down along the meat. It hangs from the smaller curl (as before) and gravity takes it as low as
# the meat lets it: of all poses (12 face directions x tilts 0-90 in 5-degree steps about every level axis in
# 15-degree steps), those with at most BUDGET of the comb inside any meat on the full rack (or the least possible, if
# none is that low) are kept, and the one whose centre hangs lowest wins (then the smaller tilt). With the comb's top
# on a curl that sits inside the bigger meats, some clipping can't be avoided; the budget lets it rest down the
# meat's side instead of sticking out.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
from honeyrest import Solid, comb_samples, rot_axis
S2 = 2.0; BUDGET = float(sys.argv[3]) if len(sys.argv) > 3 else 0.25
POSES = [(0, 0, 0)] + [(yaw, phi, th) for yaw in range(0, 180, 15) for th in range(5, 91, 5) for phi in range(0, 360, 15)] + [(yaw, 0, 0) for yaw in range(15, 180, 15)]

def settle(n):
    h3.HONEY_REST.pop(n, None); h3.HONEY_TILT.pop(n, None)
    objs, _ = h3.build(S2, [(n, None)] * 9, 'raw')
    Vm, Fm = mesh(n); Vh, Fh = mesh('Honey'); Sh = comb_samples(Vh, Fh)
    meats, combs = [], []
    for o in objs[1:]:
        M = np.array(o['m']).reshape(4, 4).T
        (combs if 'Honey' in o['src'] else meats).append(M)
    solids = [Solid((M[:3, :3] @ Vm.T).T + M[:3, 3], Fm) for M in meats]
    Rs = [rot_axis([np.sin(np.radians(phi)), 0, np.cos(np.radians(phi))], th) @ rot_axis([0, 1, 0], yaw) for yaw, phi, th in POSES]
    out = []
    for i in range(9):
        H = hp.HOOKS[i]
        pivot = np.array([H['small']['cup_x'], H['small']['cup_y'] + hp.PIERCE, H['small']['cup_z']]) * S2
        Mb = combs[i]; base = (Mb[:3, :3] @ Sh.T).T + Mb[:3, 3]
        near = [s for s, M in zip(solids, meats) if np.linalg.norm(M[:3, 3][[0, 2]] - pivot[[0, 2]]) < 1.6]
        rows = []
        for (yaw, phi, th), Rr in zip(POSES, Rs):
            Q = (Rr @ (base - pivot).T).T + pivot
            ins = np.mean(np.any([s.inside(Q) for s in near], axis=0))
            rows.append((ins, Q[:, 1].mean(), th, yaw, phi, Rr))
        least = min(r[0] for r in rows)
        pool = [r for r in rows if r[0] <= max(BUDGET, least + 0.02)]
        ins, cy, th, yaw, phi, Rr = min(pool, key=lambda r: (round(r[1], 3), r[2]))
        M4 = np.eye(4); M4[:3, :3] = Rr
        out.append(dict(hook=i, inside=ins, least=least, yaw=yaw, phi=phi, tilt=th, M=(T(pivot) @ M4 @ T(-pivot) @ Mb).tolist()))
    return out

if __name__ == '__main__':
    res = json.load(open(sys.argv[2])) if len(sys.argv) > 4 else {}
    for n in sys.argv[1].split(','):
        r = settle(n); res[n] = r
        print(f"{n:20s} inside worst {max(x['inside'] for x in r)*100:3.0f}% (least possible {max(x['least'] for x in r)*100:3.0f}%)  tilts {[x['tilt'] for x in r]}", flush=True)
        json.dump(res, open(sys.argv[2], 'w'))
