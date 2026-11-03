# Recolours a converted jerky texture toward a target average colour while keeping its variation (streaks, fat, skin).
#   python3 -I tint.py base.mesh base_d.png out_d.png R,G,B
# The average is what you actually see: texels sampled across the model's surface, weighted by area (point filtering,
# like the game). Each texel's hue is shifted by the same amount and its saturation and brightness scaled by the same
# factors, so the result averages to the target and keeps the base texture's light and dark parts.
import struct, sys
import numpy as np
from PIL import Image

mesh, src, dst, target = sys.argv[1], sys.argv[2], sys.argv[3], [float(x) / 255 for x in sys.argv[4].split(",")]

d = open(mesh, "rb").read()
nv, ni = struct.unpack("<ii", d[4:12]); o = 12
pos = np.frombuffer(d, "<f4", nv * 3, o).reshape(-1, 3).astype(np.float64); o += nv * 12
o += nv * 12  # normals
uv = np.frombuffer(d, "<f4", nv * 2, o).reshape(-1, 2).astype(np.float64); o += nv * 8
idx = np.frombuffer(d, "<i4", ni, o).reshape(-1, 3)

img = np.asarray(Image.open(src).convert("RGB")).astype(np.float64) / 255
h_px, w_px = img.shape[:2]

# Area-weighted surface samples -> texel hit counts.
rng = np.random.default_rng(1)
a, b, c = pos[idx[:, 0]], pos[idx[:, 1]], pos[idx[:, 2]]
area = np.linalg.norm(np.cross(b - a, c - a), axis=1) / 2
counts = rng.multinomial(400000, area / area.sum())
tri = np.repeat(np.arange(len(idx)), counts)
r1, r2 = rng.random(len(tri)), rng.random(len(tri))
flip = r1 + r2 > 1; r1[flip], r2[flip] = 1 - r1[flip], 1 - r2[flip]
t = uv[idx[tri, 0]] * (1 - r1 - r2)[:, None] + uv[idx[tri, 1]] * r1[:, None] + uv[idx[tri, 2]] * r2[:, None]
# Mesh UVs are Unity's (origin bottom-left); the PNG's row 0 is the top.
px = np.clip((t[:, 0] * w_px).astype(int), 0, w_px - 1)
py = np.clip(((1 - t[:, 1]) * h_px).astype(int), 0, h_px - 1)
weight = np.zeros((h_px, w_px)); np.add.at(weight, (py, px), 1)
weight /= weight.sum()

def to_hsv(rgb):
    mx = rgb.max(-1); mn = rgb.min(-1); dd = np.maximum(mx - mn, 1e-9)
    r, g, b_ = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    h = np.select([mx == r, mx == g], [((g - b_) / dd) % 6, (b_ - r) / dd + 2], (r - g) / dd + 4) / 6
    h = np.where(mx - mn < 1e-9, 0, h)
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-9), 0)
    return h, s, mx

def to_rgb(h, s, v):
    i = np.floor(h * 6) % 6; f = h * 6 - np.floor(h * 6)
    p, q, tt = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    out = np.zeros(h.shape + (3,))
    for k, (x, y, z) in enumerate([(v, tt, p), (q, v, p), (p, v, tt), (p, q, v), (tt, p, v), (v, p, q)]):
        sel = i == k; out[sel] = np.stack([x[sel], y[sel], z[sel]], -1)
    return out

def mean_rgb(rgb): return (rgb * weight[..., None]).sum((0, 1))

def mean_hsv(rgb):
    h, s, v = to_hsv(rgb)
    w = weight * s  # hue counts where there is colour
    ang = np.arctan2((w * np.sin(2 * np.pi * h)).sum(), (w * np.cos(2 * np.pi * h)).sum()) / (2 * np.pi) % 1
    return ang, (weight * s).sum(), (weight * v).sum()

th, ts, tv = to_hsv(np.array(target))
h, s, v = to_hsv(img)
out = img
# Iterate: clipping at 0/1 moves the average, so correct a few times against the measured result.
dh, ks, kv = 0.0, 1.0, 1.0
for _ in range(8):
    mh, ms, mv = mean_hsv(out)
    dh += ((th - mh + 0.5) % 1) - 0.5
    ks *= ts / max(ms, 1e-6); kv *= tv / max(mv, 1e-6)
    out = to_rgb((h + dh) % 1, np.clip(s * ks, 0, 1), np.clip(v * kv, 0, 1))
Image.fromarray((out * 255).round().clip(0, 255).astype(np.uint8), "RGB").save(dst)
base_m = mean_rgb(img) * 255; res = mean_rgb(out) * 255
print(f"{dst}: base avg {np.round(base_m).astype(int)}, target {np.round(np.array(target) * 255).astype(int)}, "
      f"result avg {np.round(res).astype(int)}")
