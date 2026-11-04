# Writes the C# table for Rack.cs: for each raw meat, the rotation, scale and hang point (own space x own scale) that
# hangplan3 chose, and checks that rebuilding the placement from the rounded numbers matches within 2 mm.
import sys, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
names = 'RawMeat,WolfMeat,BjornMeat,FoxMeat_TW,DeerMeat,rae_HorseMeat,AsksvinMeat,HareMeat,SharkMeat_TW,ChickenMeat,VoltureMeat,NeckTail,LoxMeat,BearSteak_TW,MooseMeat,BugMeat,SerpentMeat,BoneMawSerpentMeat'.split(',')
def quat(M):
    m = M[:3, :3]; t = np.trace(m)
    if t > 0:
        s = np.sqrt(t + 1) * 2; w = s / 4; x = (m[2, 1] - m[1, 2]) / s; y = (m[0, 2] - m[2, 0]) / s; z = (m[1, 0] - m[0, 1]) / s
    else:
        i = int(np.argmax(np.diag(m)))
        if i == 0: s = np.sqrt(1 + m[0, 0] - m[1, 1] - m[2, 2]) * 2; w = (m[2, 1] - m[1, 2]) / s; x = s / 4; y = (m[0, 1] + m[1, 0]) / s; z = (m[0, 2] + m[2, 0]) / s
        elif i == 1: s = np.sqrt(1 + m[1, 1] - m[0, 0] - m[2, 2]) * 2; w = (m[0, 2] - m[2, 0]) / s; x = (m[0, 1] + m[1, 0]) / s; y = s / 4; z = (m[1, 2] + m[2, 1]) / s
        else: s = np.sqrt(1 + m[2, 2] - m[0, 0] - m[1, 1]) * 2; w = (m[1, 0] - m[0, 1]) / s; x = (m[0, 2] + m[2, 0]) / s; y = (m[1, 2] + m[2, 1]) / s; z = s / 4
    q = np.array([x, y, z, w]); return q if q[3] >= 0 else -q
lines = []; worst = 0
for n in names:
    for HI in range(9):
        loads = [None] * 9; loads[HI] = (n, None)
        objs, rep = h3.build(2.0, loads, 'raw')
        M = np.array(objs[1]['m']).reshape(4, 4).T
        tr = h3.TRACE[n]; s = tr['s']; Rr = M[:3, :3] / s
        cup = np.array([hp.HOOKS[HI]['big'][k] for k in ('cup_x', 'cup_y', 'cup_z')]) * 2.0
        p_own = Rr.T @ (cup - M[:3, 3]) / s           # hang point in the item's own space
        if HI == 0:
            q = quat(np.vstack([np.hstack([Rr, [[0], [0], [0]]]), [0, 0, 0, 1]])); snap = np.array([0, .5, np.sqrt(.5), 1]); q = np.array([np.sign(v) * snap[np.argmin(np.abs(snap - abs(v)))] for v in q]); q = np.round(q, 7); sr = round(float(s), 3); pr = np.round(p_own, 4); p0 = p_own
            lines.append(f'            {{ "{n}", new Hang(new Quaternion({q[0]:.7g}f, {q[1]:.7g}f, {q[2]:.7g}f, {q[3]:.7g}f), {sr:.3f}f, new Vector3({pr[0]:.4f}f, {pr[1]:.4f}f, {pr[2]:.4f}f)) }},   // {tr["tag"]}')
        # same choice on every hook? and does the rounded table rebuild the same placement?
        assert np.allclose(p_own, p0, atol=1e-6), (n, HI)
        Rq = Q(tuple(q))[:3, :3]
        V, F = mesh(n)
        W1 = (M[:3, :3] @ V.T).T + M[:3, 3]
        W2 = (Rq @ (V * sr).T).T + (cup - sr * (Rq @ pr))
        worst = max(worst, np.abs(W1 - W2).max())
print("\n".join(lines)); print("worst rebuild error (m):", worst)
