# Rebuilds a .mesh file's normals with a crease angle, for small low-poly items.
#   python3 -I renormal.py in.mesh out.mesh [crease_degrees=50]
# After heavy reduction a thin strip's tip shares vertices between its top and bottom faces, and smooth normals there
# average to sideways or inward-pointing directions (dark, blotchy tips). Here each triangle corner takes the average of
# only the neighbouring faces that bend less than the crease angle from its own face; corners that end up different are
# split into separate vertices (same position and UV), so hard edges stay hard and gentle curves stay smooth.
import struct, sys
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
crease = np.cos(np.radians(float(sys.argv[3]) if len(sys.argv) > 3 else 50.0))
d = open(src, "rb").read()
nv, ni = struct.unpack("<ii", d[4:12]); o = 12
pos = np.frombuffer(d, "<f4", nv * 3, o).reshape(-1, 3).astype(np.float64); o += nv * 12
o += nv * 12
uv = np.frombuffer(d, "<f4", nv * 2, o).reshape(-1, 2).astype(np.float64); o += nv * 8
idx = np.frombuffer(d, "<i4", ni, o).reshape(-1, 3)

a, b, c = pos[idx[:, 0]], pos[idx[:, 1]], pos[idx[:, 2]]
fn = np.cross(b - a, c - a)  # area-weighted; this winding's cross product points out (checked against the old normals)
unit = fn / np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)

# Faces around each position (vertices split on UV seams still share a position, so group by position).
key = {tuple(p): k for k, p in enumerate(np.unique(pos.round(6), axis=0))}
pid = np.array([key[tuple(p)] for p in pos.round(6)])
faces_at = {}
for f, t in enumerate(idx):
    for v in t: faces_at.setdefault(pid[v], []).append(f)

new_pos, new_nrm, new_uv, lookup, new_idx = [], [], [], {}, []
for f, t in enumerate(idx):
    tri = []
    for v in t:
        near = [g for g in faces_at[pid[v]] if unit[g] @ unit[f] >= crease]
        n = fn[near].sum(0); n /= max(np.linalg.norm(n), 1e-12)
        k = (v, tuple(np.round(n, 4)))
        if k not in lookup:
            lookup[k] = len(new_pos); new_pos.append(pos[v]); new_nrm.append(n); new_uv.append(uv[v])
        tri.append(lookup[k])
    new_idx.append(tri)
new_pos, new_nrm, new_uv, new_idx = map(np.array, (new_pos, new_nrm, new_uv, new_idx))
with open(dst, "wb") as f:
    f.write(b"PTM1"); f.write(struct.pack("<ii", len(new_pos), new_idx.size))
    f.write(new_pos.astype("<f4").tobytes()); f.write(new_nrm.astype("<f4").tobytes())
    f.write(new_uv.astype("<f4").tobytes()); f.write(new_idx.astype("<i4").tobytes())
bad = (np.einsum("ij,ij->i", new_nrm[new_idx].mean(1), unit) < 0).sum()
print(f"{dst}: vertices {nv} -> {len(new_pos)}, triangles {len(new_idx)}, corners facing inward: {bad}")
