# How far each raw meat must come up so the lower half of its curl is inside it: for every bit of the bigger curl's
# metal from below its lowest point up to HALF above it (stem side to tip, on the curl's own side of the stem), the
# meat's top surface right above that bit must be at least MARGIN higher. Where the meat doesn't reach (narrow pieces)
# that bit is left out: the curl comes out of the meat's side there. Worst of the nine hooks.
import sys, json, struct, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
from anchor2 import mesh
HALF, MARGIN, S2 = 0.06, 0.01, 2.0
d = open('/root/rackview/hang/laradr_rack.mesh', 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12])
RV = np.frombuffer(d, '<f4', nv * 3, 12).reshape(-1, 3).astype(float) * S2

def curl_points(i):
    H = hp.HOOKS[i]; c = np.array([H['big'][k] for k in ('cup_x', 'cup_y', 'cup_z')]) * S2
    stem_z = H['z'] * S2; side = 1 if c[2] >= stem_z else -1
    m = (np.abs(RV[:, 0] - c[0]) < 0.08) & (RV[:, 1] >= c[1] - 0.03) & (RV[:, 1] <= c[1] + HALF) & ((RV[:, 2] - stem_z) * side >= -0.01) & (np.abs(RV[:, 2] - c[2]) < 0.16)
    return RV[m], c

def tops(W, F, P):
    """For each curl point: the top of the meat's solid part around it (the solid stretch of its vertical line that
    reaches within 10 cm below the point or higher, and starts no more than 30 cm above it); nan where there is none."""
    A, B, C = W[F[:, 0]], W[F[:, 1]], W[F[:, 2]]; out = np.full(len(P), np.nan)
    v0 = (C - A)[:, [0, 2]]; v1 = (B - A)[:, [0, 2]]
    d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); den = d00 * d11 - d01 * d01
    ok = np.abs(den) > 1e-12; den = np.where(ok, den, 1.0)
    for k, p in enumerate(P):
        v2 = p[[0, 2]] - A[:, [0, 2]]
        d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
        u = (d11 * d20 - d01 * d21) / den; v = (d00 * d21 - d01 * d20) / den
        ins = ok & (u >= 0) & (v >= 0) & (u + v <= 1)
        if not ins.any(): continue
        h = np.sort(np.unique(np.round(A[ins, 1] + u[ins] * (C - A)[ins, 1] + v[ins] * (B - A)[ins, 1], 4)))[::-1]
        best = np.nan
        for j in range(0, len(h) - 1, 2):
            top, bottom = h[j], h[j + 1]
            if top >= p[1] - 0.10 and top <= p[1] + 0.30 and bottom <= p[1] + 0.02:
                best = top if np.isnan(best) else max(best, top)
        out[k] = best
    return out

def raise_for(n):
    V, F = mesh(n); worst = 0.0; cover = []; top_max = -9
    for i in range(9):
        loads = [None] * 9; loads[i] = (n, None)
        objs, _ = h3.build(S2, loads, 'raw')
        M = np.array(objs[1]['m']).reshape(4, 4).T
        W = (M[:3, :3] @ V.T).T + M[:3, 3]
        P, c = curl_points(i)
        t = tops(W, F, P)
        need = P[:, 1] + MARGIN - t
        cover.append(np.mean(~np.isnan(t)))
        if np.any(~np.isnan(need)): worst = max(worst, np.nanmax(need))
        top_max = max(top_max, W[:, 1].max() - c[1])
    return max(worst, 0.0), min(cover), top_max

if __name__ == '__main__':
    names = sys.argv[1].split(',')
    for n in names:
        r, cov, tm = raise_for(n)
        print(f"{n:20s} raise {r:.3f} m   curl under meat {cov*100:3.0f}%   item top now {tm:.3f} -> {tm+r:.3f} above cup")

def inside_frac(W, F, P):
    """Share of the curl points that are inside the meat's closed mesh, among those under or over the meat at all."""
    A, B, C = W[F[:, 0]], W[F[:, 1]], W[F[:, 2]]
    v0 = (C - A)[:, [0, 2]]; v1 = (B - A)[:, [0, 2]]
    d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); den = d00 * d11 - d01 * d01
    ok = np.abs(den) > 1e-12; den = np.where(ok, den, 1.0)
    n_in = n_all = 0
    for p in P:
        v2 = p[[0, 2]] - A[:, [0, 2]]
        d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
        u = (d11 * d20 - d01 * d21) / den; v = (d00 * d21 - d01 * d20) / den
        ins = ok & (u >= 0) & (v >= 0) & (u + v <= 1)
        if not ins.any(): continue
        h = np.unique(np.round(A[ins, 1] + u[ins] * (C - A)[ins, 1] + v[ins] * (B - A)[ins, 1], 4))
        n_all += 1; n_in += (np.count_nonzero(h > p[1]) % 2 == 1)
    return n_in / max(n_all, 1), n_all / max(len(P), 1)

def raise_search(n, goal=0.9, step=0.005, top_room=0.24):
    """Least raise (0 .. top_room-limited) at which, on every hook, at least `goal` of the curl's lower half (where the
    meat reaches) is inside the meat; if never, the raise with the best worst-hook share. Base placement = RAISE 0."""
    V, F = mesh(n); base = []
    for i in range(9):
        loads = [None] * 9; loads[i] = (n, None)
        old = h3.RAISE.pop(n, None); objs, _ = h3.build(S2, loads, 'raw')
        if old is not None: h3.RAISE[n] = old
        M = np.array(objs[1]['m']).reshape(4, 4).T
        P, c = curl_points(i)
        base.append(((M[:3, :3] @ V.T).T + M[:3, 3], P, c))
    best = None
    for r in np.arange(0, 0.16 + 1e-9, step):
        if max(W[:, 1].max() + r - c[1] for W, P, c in base) > top_room: break
        fr = [inside_frac(W + [0, r, 0], F, P) for W, P, c in base]
        worst = min(f for f, cov in fr); cov = min(cv for f, cv in fr)
        if best is None or worst > best[1] + 1e-9: best = (r, worst, cov)
        if worst >= goal: return r, worst, cov, True
    return best[0], best[1], best[2], False

def hidden(W, F, cam, P):
    """Which curl points are hidden by the meat (W,F) seen from cam: inside it, or behind its surface."""
    A, B, C = W[F[:, 0]], W[F[:, 1]], W[F[:, 2]]
    e1, e2 = B - A, C - A; out = np.zeros(len(P), bool)
    for k, p in enumerate(P):
        dvec = p - cam; L = np.linalg.norm(dvec); dvec = dvec / L
        h = np.cross(dvec, e2); a = (e1 * h).sum(1); ok = np.abs(a) > 1e-12; a = np.where(ok, a, 1.0)
        f = 1 / a; s = cam - A; u = f * (s * h).sum(1); q = np.cross(s, e1); v = f * (q * dvec).sum(1); t = f * (e2 * q).sum(1)
        hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-4) & (t < L - 1e-4)
        out[k] = hit.any()
    return out

def cams(i, c):
    # the two ways the user looked: eye level from outside the row, and low from the rack's nearer end
    hz = hp.HOOK_Z[i // 3] * S2; out_z = -1 if hz <= 0 else 1
    front = np.array([c[0] + 0.3, 1.75, c[2] + out_z * 2.2])
    end = np.array([np.sign(c[0] if abs(c[0]) > 0.05 else 1) * 2.3, 1.45, c[2] - 0.3])
    return {'front': front, 'end': end}

def raise_view(n, goal=0.85, step=0.005, top_room=0.24):
    V, F = mesh(n); base = []
    for i in range(9):
        loads = [None] * 9; loads[i] = (n, None)
        old = h3.RAISE.pop(n, None); objs, _ = h3.build(S2, loads, 'raw')
        if old is not None: h3.RAISE[n] = old
        M = np.array(objs[1]['m']).reshape(4, 4).T
        P, c = curl_points(i)
        base.append(((M[:3, :3] @ V.T).T + M[:3, 3], P, c, cams(i, c)))
    best = None
    for r in np.arange(0, 0.16 + 1e-9, step):
        if max(W[:, 1].max() + r - c[1] for W, P, c, cm in base) > top_room: break
        worst = 1.0; where = ''
        for i, (W, P, c, cm) in enumerate(base):
            for nm, cam in cm.items():
                f = hidden(W + [0, r, 0], F, cam, P).mean()
                if f < worst: worst, where = f, f"hook {i} {nm}"
        if best is None or worst > best[1] + 1e-9: best = (r, worst, where)
        if worst >= goal: return r, worst, where, True
    return best[0], best[1], best[2], False
