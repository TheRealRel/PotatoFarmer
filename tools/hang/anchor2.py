# Where an item should hang from so the hook goes into its meat (not a bone tip or a thin rim), and which way up.
# For a rotated, scaled item: cast vertical rays on a 1.5 cm grid; at each column the top surface t and the next
# surface below b (the item's thickness there). A column can hold the hook if the meat there is at least
# PIERCE + MARGIN thick. The hook point is the highest such column's top minus PIERCE (so the curl sits PIERCE deep in
# the meat); anything higher than that column's top is "protrusion" (it would rise past the hook towards the bar).
import json, numpy as np
H = '/root/rackview/hang/'
ATT = {k: (v.get('attach') or v.get('model'))[0] for k, v in json.load(open(H + 'attach_trs.json')).items()}
PIERCE, MARGIN, STEP = 0.06, 0.03, 0.015   # metres

def mesh(name):
    parts = json.load(open(H + name + '.json'))
    V, F = [], []
    for p in parts:
        base = sum(len(v) for v in V)
        V.append(np.array(p['v']))
        for s in p['subs']: F.append(np.array(s['tris']) + base)
    V = np.concatenate(V) - np.array(ATT.get(name, [0, 0, 0])); F = np.concatenate(F)
    return V, F

RADIUS = 0.03   # metres: meat must surround the curl's lowest point this far in all four directions

def columns(W, F):
    """W: rotated+scaled vertices. Returns grid points (x,z), grid shape, and per column the sorted hit heights (high to low)."""
    lo, hi = W.min(0), W.max(0)
    xs = np.arange(lo[0] + STEP / 2, hi[0], STEP); zs = np.arange(lo[2] + STEP / 2, hi[2], STEP)
    X, Z = np.meshgrid(xs, zs, indexing='ij'); P = np.stack([X.ravel(), Z.ravel()], 1)
    A, B, C = W[F[:, 0]], W[F[:, 1]], W[F[:, 2]]
    hits = [[] for _ in range(len(P))]
    for k in range(0, len(F), 256):
        a, b, c = A[k:k + 256], B[k:k + 256], C[k:k + 256]
        v0 = (c - a)[:, [0, 2]]; v1 = (b - a)[:, [0, 2]]
        d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); den = d00 * d11 - d01 * d01
        ok = np.abs(den) > 1e-12; den = np.where(ok, den, 1.0)
        v2 = P[:, None, :] - a[None, :, [0, 2]]
        d20 = (v2 * v0[None]).sum(2); d21 = (v2 * v1[None]).sum(2)
        u = (d11 * d20 - d01 * d21) / den; v = (d00 * d21 - d01 * d20) / den
        inside = ok[None] & (u >= 0) & (v >= 0) & (u + v <= 1)
        y = a[None, :, 1] + u * (c - a)[None, :, 1] + v * (b - a)[None, :, 1]
        for i, j in zip(*np.nonzero(inside)): hits[i].append(y[i, j])
    hits = [np.sort(np.unique(np.round(h, 4)))[::-1] if h else np.array([]) for h in hits]
    return P, (len(xs), len(zs)), hits

def solid(h, y):
    """Is height y inside the closed mesh along this column? (odd number of surfaces above it)"""
    return len(h) > 0 and (np.count_nonzero(h > y) % 2 == 1)

def hang_point(W, F):
    """(point where the curl's lowest point goes, protrusion of the item above that column's top, found?)
    A column holds the hook if, PIERCE below its top, the meat is solid there and RADIUS away in +-x and +-z."""
    P, (nx, nz), hits = columns(W, F)
    r = max(1, int(round(RADIUS / STEP)))
    good = []
    for i, h in enumerate(hits):
        if not len(h): continue
        y = h[0] - PIERCE; ix, iz = divmod(i, nz)
        ok = solid(h, y)
        for dx, dz in ((r, 0), (-r, 0), (0, r), (0, -r)):
            jx, jz = ix + dx, iz + dz
            if not ok: break
            ok = 0 <= jx < nx and 0 <= jz < nz and solid(hits[jx * nz + jz], y)
        if ok: good.append(i)
    top = W[:, 1].max()
    if not good:
        i = max(range(len(hits)), key=lambda i: hits[i][0] if len(hits[i]) else -9)
        return np.array([P[i, 0], hits[i][0] - PIERCE, P[i, 1]]), 0.0, False
    best = max(hits[i][0] for i in good)
    cand = [i for i in good if hits[i][0] >= best - 0.01]
    c = (W.min(0) + W.max(0)) / 2
    i = min(cand, key=lambda i: np.hypot(P[i, 0] - c[0], P[i, 1] - c[2]))
    return np.array([P[i, 0], hits[i][0] - PIERCE, P[i, 1]]), top - hits[i][0], True

def balance(W, F, p):
    """Horizontal distance (m) from the hook point p to the item's centre of mass (solid, from the same columns)."""
    P, _, hits = columns(W, F)
    m = np.array([sum(h[k] - h[k + 1] for k in range(0, len(h) - 1, 2)) for h in hits])
    if m.sum() <= 0: return 0.0
    c = (P * m[:, None]).sum(0) / m.sum()
    return float(np.hypot(p[0] - c[0], p[2] - c[1]))
