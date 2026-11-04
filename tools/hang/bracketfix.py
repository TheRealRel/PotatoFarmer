# Wood texels on the corner brackets' rim. The bracket (model y 0.70 to 0.87) is about 4 cm wider than the wooden
# block that sits on it, so a ledge of its top shows around the block; Meshy textured that ledge (and a few side
# faces at the seam) with wood. Fix: those faces get their own copies of their vertices with UVs inside a plain
# metal patch of the same texture (positions and normals unchanged, so the shading doesn't change).
import struct, numpy as np, colorsys, sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
d = open(src, 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12])
o = 12; V = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3).astype(np.float32); o += nv * 12
N = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3).astype(np.float32); o += nv * 12
UV = np.frombuffer(d, '<f4', nv * 2, o).reshape(-1, 2).astype(np.float32); o += nv * 8
F = np.frombuffer(d, '<i4', ni, o).reshape(-1, 3).copy()
T = np.asarray(Image.open(sys.argv[3]).convert('RGB')).astype(float) / 255; H, W = T.shape[:2]
hsv = np.array([colorsys.rgb_to_hsv(*p) for p in T.reshape(-1, 3)]).reshape(H, W, 3)
metal = hsv[:, :, 1] < 0.30
def frac(f, n=7):
    a, b, c = UV[f].astype(float); k = []
    for i in range(n + 1):
        for j in range(n + 1 - i):
            u, v = i / n, j / n; p = a * (1 - u - v) + b * u + c * v
            k.append(metal[int(np.clip((1 - p[1] % 1) * H, 0, H - 1)), int(np.clip(p[0] % 1 * W, 0, W - 1))])
    return np.mean(k)
cent = V[F].mean(1); nrm = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]); nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-12)
bad = []
for sx in (1, -1):
    for sz in (1, -1):
        m = (cent[:, 0] * sx > 0.65) & (cent[:, 2] * sz > 0.25)
        blk = V[F[m & (cent[:, 1] > 0.90) & (cent[:, 1] < 1.0)]].reshape(-1, 3)
        bx = blk[:, 0].max() if sx > 0 else blk[:, 0].min(); bz = blk[:, 2].max() if sz > 0 else blk[:, 2].min()
        seam = np.nonzero(m & (cent[:, 1] >= 0.84) & (cent[:, 1] < 0.90))[0]
        for t in seam:
            P = V[F[t]]
            # any corner of the face outside the block's outline: part of it shows (the ledge, or the rim's outer face)
            outside = ((P[:, 0] - bx) * sx > 0.001).any() or ((P[:, 2] - bz) * sz > 0.001).any()
            if outside and frac(F[t]) < 0.9 and nrm[t, 1] > -0.6:
                bad.append(t)
bad = np.array(sorted(set(bad)))
# a plain metal patch: the 8x8 window of metal texels whose colour varies least
best = None
for y in range(0, H - 8, 2):
    for x in range(0, W - 8, 2):
        if metal[y:y + 8, x:x + 8].all():
            s = T[y:y + 8, x:x + 8].reshape(-1, 3).std(0).sum()
            if best is None or s < best[0]: best = (s, x, y)
_, px, py = best
mean_rgb = (T[py:py + 8, px:px + 8].reshape(-1, 3).mean(0) * 255).round()
# UVs: a small triangle well inside the patch (texel centres), v flipped like the sampling above
u0, u1 = (px + 2.5) / W, (px + 5.5) / W; v0, v1 = 1 - (py + 5.5) / H, 1 - (py + 2.5) / H
tri_uv = np.array([[u0, v0], [u1, v0], [u0, v1]], np.float32)
V2, N2, UV2 = [V], [N], [UV]; nxt = nv
for t in bad:
    V2.append(V[F[t]]); N2.append(N[F[t]]); UV2.append(tri_uv); F[t] = [nxt, nxt + 1, nxt + 2]; nxt += 3
V2 = np.concatenate(V2); N2 = np.concatenate(N2); UV2 = np.concatenate(UV2)
out = b'PTM1' + struct.pack('<ii', len(V2), F.size) + V2.astype('<f4').tobytes() + N2.astype('<f4').tobytes() + UV2.astype('<f4').tobytes() + F.astype('<i4').tobytes()
open(dst, 'wb').write(out)
print(f"faces moved to metal: {len(bad)} (per corner ~{len(bad)//4}); metal patch at texel ({px},{py}) colour {mean_rgb}; vertices {nv} -> {len(V2)}")
np.save('/tmp/bracket_bad.npy', bad)
