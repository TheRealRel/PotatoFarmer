# Evens out a converted model's pixel size. Meshy gives some UV islands far more texture than others, so after
# convert.py some parts (the trough's underside) come out chunkier than the rest. Convert at a larger size first, then
# this merges each island's texels into square blocks sized so every island lands near the same pixels per metre.
# Blocks never mix islands, and the gaps between islands are refilled from their nearest island afterwards.
# Usage: python3 -I even_pixels.py assets_dir name target_px_per_m
import struct, sys, numpy as np
from PIL import Image
from scipy import ndimage
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
d, name, target = sys.argv[1], sys.argv[2], float(sys.argv[3])
f = open(f"{d}/{name}.mesh", "rb"); assert f.read(4) == b"PTM1"; nv, ni = struct.unpack("<ii", f.read(8))
P = np.frombuffer(f.read(nv * 12), "<f4").reshape(-1, 3); f.read(nv * 12)
UV = np.frombuffer(f.read(nv * 8), "<f4").reshape(-1, 2); I = np.frombuffer(f.read(ni * 4), "<i4").reshape(-1, 3)
col = np.asarray(Image.open(f"{d}/{name}_d.png").convert("RGB")).astype(np.float64)
H, W = col.shape[:2]
# Islands: triangles joined through shared vertices (convert.py splits vertices at UV seams).
r = np.concatenate([I[:, 0], I[:, 1], I[:, 2]]); c = np.concatenate([I[:, 1], I[:, 2], I[:, 0]])
_, vlab = connected_components(coo_matrix((np.ones(len(r)), (r, c)), shape=(nv, nv)), directed=False)
tlab = vlab[I[:, 0]]
T = P[I]; A3 = np.linalg.norm(np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]), axis=1) / 2
U = UV[I] * [W, H]; e1 = U[:, 1] - U[:, 0]; e2 = U[:, 2] - U[:, 0]; A2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) / 2
n_isl = tlab.max() + 1
dens = np.sqrt(np.bincount(tlab, A2, n_isl) / np.maximum(np.bincount(tlab, A3, n_isl), 1e-12))
k = np.clip(np.round(dens / target), 1, 8).astype(int)
# Which island owns each texel (same raster rule as texels.py: V flipped).
own = np.full((H, W), -1, np.int64)
for t, lab in zip(I, tlab):
    uv = UV[t] * [W, H]; uv[:, 1] = H - uv[:, 1]
    x0, y0 = np.floor(uv.min(0)).astype(int); x1, y1 = np.ceil(uv.max(0)).astype(int)
    gx, gy = np.meshgrid(np.arange(max(0, x0), min(W, x1 + 1)) + .5, np.arange(max(0, y0), min(H, y1 + 1)) + .5)
    if gx.size == 0: continue
    (ax, ay), (bx, by), (cx, cy) = uv
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-12: continue
    a = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den; b = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
    m = (a >= -0.02) & (b >= -0.02) & (1 - a - b >= -0.02)
    own[(gy[m] - .5).astype(int), (gx[m] - .5).astype(int)] = lab
covered = own >= 0
kt = np.where(covered, k[np.maximum(own, 0)], 1)
yy, xx = np.mgrid[0:H, 0:W]
key = np.where(covered, ((own * 4096 + yy // kt) * 4096 + xx // kt) * 16 + kt, -1)
uk, inv = np.unique(key[covered], return_inverse=True)
def even(img):
    a = img.astype(np.float64).copy(); flat = a[covered]
    sums = np.stack([np.bincount(inv, flat[:, ch], len(uk)) for ch in range(a.shape[2])], 1)
    a[covered] = sums[inv] / np.bincount(inv, minlength=len(uk))[inv, None]
    _, (iy, ix) = ndimage.distance_transform_edt(~covered, return_indices=True)
    return a[iy, ix]
out = {"d": even(col)}
out["m"] = even(np.asarray(Image.open(f"{d}/{name}_m.png").convert("RGBA")))
nm = even(np.asarray(Image.open(f"{d}/{name}_n.png").convert("RGBA")))
v = nm[..., 1:4] / 127.5 - 1; v /= np.linalg.norm(v, axis=2, keepdims=True).clip(1e-6); nm[..., 1:4] = (v + 1) * 127.5; nm[..., 0] = 255
out["n"] = nm
for s, img in out.items():
    Image.fromarray(img.round().clip(0, 255).astype(np.uint8), "RGB" if s == "d" else "RGBA").save(f"{d}/{name}_{s}.png")
w = np.bincount(tlab, A3, n_isl)
print(f"{n_isl} islands; px/m before {np.percentile(dens, [10, 50, 90]).round(0)} (area-weighted mean {np.sum(dens * w) / w.sum():.0f}); "
      f"after ~{np.sum(dens / k * w) / w.sum():.0f}, block sizes used {sorted(set(k.tolist()))}")
