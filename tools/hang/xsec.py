# xsec.py names out.png [hook]: exact slice through the hook's big curl (plane x = cup_x, and plane z = cup_z) showing
# the rack (black) and the hung raw meat (red) and honey (orange), so contact is measured, not judged by eye.
import sys, json, struct, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
S = 2.0; HI = int(sys.argv[3]) if len(sys.argv) > 3 else 5
d = open('hang/laradr_rack.mesh', 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12])
RV = np.frombuffer(d, '<f4', nv * 3, 12).reshape(-1, 3).astype(float) * S
off = 12 + nv * 3 * 4
# find the index block: try after verts, normals, uvs layouts
for skip in (0, nv * 3 * 4, nv * 5 * 4, nv * 8 * 4):
    try:
        RF = np.frombuffer(d, '<i4', ni, off + skip).reshape(-1, 3)
        if RF.max() < nv and RF.min() >= 0 and len(d) - (off + skip) == ni * 4: break
    except Exception: pass
def slice_(V, F, ax, val):
    segs = []
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    for tri in zip(a, b, c):
        s = [t[ax] - val for t in tri]; pts = []
        for i in range(3):
            j = (i + 1) % 3
            if (s[i] < 0) != (s[j] < 0):
                t = s[i] / (s[i] - s[j]); pts.append(tri[i] + t * (tri[j] - tri[i]))
        if len(pts) == 2: segs.append(pts)
    return segs
names = sys.argv[1].split(',')
fig, axs = plt.subplots(len(names), 2, figsize=(8, 4 * len(names)))
axs = np.atleast_2d(axs)
H = hp.HOOKS[HI]; cup = np.array([H['big']['cup_x'], H['big']['cup_y'], H['big']['cup_z']]) * S
for r, n in enumerate(names):
    loads = [None] * 9; loads[HI] = (n, None)
    objs, rep = h3.build(S, loads, 'raw')
    M = np.array(objs[1]['m']).reshape(4, 4).T
    V, F = mesh(n); W = (M[:3, :3] @ V.T).T + M[:3, 3]
    for col, (ax, hor) in enumerate(((0, 2), (2, 0))):
        A = axs[r, col]
        for segs, colr in ((slice_(RV, RF, ax, cup[ax]), 'k'), (slice_(W, F, ax, cup[ax]), 'r')):
            for p, q in segs: A.plot([p[hor], q[hor]], [p[1], q[1]], colr, lw=0.8)
        A.plot(cup[hor], cup[1], 'bo', ms=4)
        A.set_xlim(cup[hor] - 0.35, cup[hor] + 0.35); A.set_ylim(cup[1] - 0.45, cup[1] + 0.25); A.set_aspect('equal')
        A.set_title(f"{n} {h3.TRACE[n]['tag']} slice {'x' if ax == 0 else 'z'}=cup", fontsize=8)
plt.tight_layout(); plt.savefig(sys.argv[2], dpi=70)
