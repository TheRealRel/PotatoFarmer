# The valley of each curl: the lowest point of the hook block's upper (inner) surface between the stem and the horn,
# on the curl's own side, in the slice through the cup (x = cup_x, averaged over x +-0.5 cm). Model units.
import json, struct, numpy as np
d = open('hang/laradr_rack.mesh', 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12]); o = 12
P = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3).astype(float); o += nv * 32
I = np.frombuffer(d, '<i4', ni, o).reshape(-1, 3)
H = json.load(open('hooks.json'))
A, B, C = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]
v0 = (C - A)[:, [0, 2]]; v1 = (B - A)[:, [0, 2]]
d00 = (v0*v0).sum(1); d01 = (v0*v1).sum(1); d11 = (v1*v1).sum(1); den = d00*d11 - d01*d01
ok = np.abs(den) > 1e-14; den = np.where(ok, den, 1)
def column(x, z):
    v2 = np.array([x, z]) - A[:, [0, 2]]; d20 = (v2*v0).sum(1); d21 = (v2*v1).sum(1)
    u = (d11*d20 - d01*d21)/den; v = (d00*d21 - d01*d20)/den
    ins = ok & (u >= 0) & (v >= 0) & (u+v <= 1)
    return np.unique(np.round(A[ins,1] + u[ins]*(C-A)[ins,1] + v[ins]*(B-A)[ins,1], 5))
out = []
for i, h in enumerate(H):
    row = {}
    for w in ('big', 'small'):
        c = h[w]; side = 1 if c['cup_z'] >= h['z'] else -1
        best = None
        for x in (c['cup_x'] - 0.005, c['cup_x'], c['cup_x'] + 0.005):
            for dz in np.arange(0.0, 0.10, 0.0025):
                z = h['z'] + side * dz
                s = column(x, z); s = s[(s > 0.79) & (s < 1.0)]
                if len(s) < 2: continue
                bottom, top = s[0], s[1]
                above = s[2] if len(s) > 2 else 9
                if top - bottom < 0.01 or above - top < 0.02: continue   # not the block, or no open air above it
                if best is None or top < best[1]: best = (z, top, bottom, x)
        z, top, bottom, x = best
        row[w] = dict(x=round(x, 4), y=round(float(top), 4), z=round(float(z), 4), bottom=round(float(bottom), 4), cup_y=round(c['cup_y'], 4))
    out.append(row); print(i, row)
json.dump(out, open('valley.json', 'w'))
