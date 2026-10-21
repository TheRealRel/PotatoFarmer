# Reduces a high-detail .glb (Meshy's full model) to a game-ready triangle count while keeping the texture lined
# up, and writes a new .glb with the same textures. Use this instead of Meshy's own reduction, which can break
# the texture layout.
#   python3 -I decimate.py full.glb reduced.glb [triangles]
# Needs: pip install pymeshlab (and the libopengl0 system library on Linux).
import io, json, os, struct, sys, tempfile
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
target = int(sys.argv[3]) if len(sys.argv) > 3 else 8000

data = open(src, "rb").read()
jlen = struct.unpack("<I", data[12:16])[0]
gltf = json.loads(data[20:20 + jlen])
blob = data[20 + jlen + 8:]

def accessor(i):
    a = gltf["accessors"][i]; v = gltf["bufferViews"][a["bufferView"]]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
    dtype = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
    start = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    arr = np.frombuffer(blob, dtype, a["count"] * comps, start).copy()
    return arr.reshape(a["count"], comps) if comps > 1 else arr

prim = gltf["meshes"][0]["primitives"][0]
pos = accessor(prim["attributes"]["POSITION"]).astype(np.float64)
uv = accessor(prim["attributes"]["TEXCOORD_0"]).astype(np.float64)
idx = accessor(prim["indices"]).astype(np.int64).reshape(-1, 3)
print(f"in: {len(idx)} triangles, {len(pos)} vertices")

import pymeshlab
tmp = tempfile.mkdtemp()
obj = os.path.join(tmp, "in.obj")
with open(obj, "w") as f:
    f.write("".join(f"v {x:.6f} {y:.6f} {z:.6f}\n" for x, y, z in pos))
    f.write("".join(f"vt {u:.6f} {1 - v:.6f}\n" for u, v in uv))  # OBJ UVs start at the bottom
    f.write("".join(f"f {a+1}/{a+1} {b+1}/{b+1} {c+1}/{c+1}\n" for a, b, c in idx))
ms = pymeshlab.MeshSet()
ms.load_new_mesh(obj)
ms.meshing_merge_close_vertices(threshold=pymeshlab.PercentageValue(0.0001))
ms.meshing_decimation_quadric_edge_collapse_with_texture(targetfacenum=target, qualitythr=0.5, extratcoordw=1.0,
                                                         preserveboundary=True, boundaryweight=1.0, optimalplacement=True,
                                                         preservenormal=True, planarquadric=True)
ms.compute_normal_per_vertex(weightmode="By Angle")
m = ms.current_mesh()
V = m.vertex_matrix(); F = m.face_matrix(); N = m.vertex_normal_matrix()
W = m.wedge_tex_coord_matrix().reshape(-1, 3, 2)  # one UV per triangle corner
# Split vertices wherever a corner needs a different UV (texture seams).
keys = {}
out_pos, out_nrm, out_uv, out_idx = [], [], [], []
for fi, face in enumerate(F):
    tri = []
    for k in range(3):
        v = int(face[k]); t = W[fi, k]
        key = (v, round(float(t[0]), 6), round(float(t[1]), 6))
        if key not in keys:
            keys[key] = len(out_pos)
            out_pos.append(V[v]); out_nrm.append(N[v]); out_uv.append((t[0], 1 - t[1]))
        tri.append(keys[key])
    out_idx.append(tri)
P = np.array(out_pos, np.float32); Nn = np.array(out_nrm, np.float32); U = np.array(out_uv, np.float32)
I = np.array(out_idx, np.uint32).reshape(-1)
Nn /= np.linalg.norm(Nn, axis=1, keepdims=True).clip(1e-9)
print(f"out: {len(I) // 3} triangles, {len(P)} vertices")

# Rebuild the .glb: new geometry, the original images, material and samplers.
images = []
for im in gltf["images"]:
    v = gltf["bufferViews"][im["bufferView"]]
    images.append((blob[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]], im.get("mimeType", "image/png")))
chunks, views = bytearray(), []
def add(raw, target_kind=None):
    while len(chunks) % 4: chunks.append(0)
    view = {"buffer": 0, "byteOffset": len(chunks), "byteLength": len(raw)}
    if target_kind: view["target"] = target_kind
    chunks.extend(raw); views.append(view); return len(views) - 1
vp = add(P.tobytes(), 34962); vn = add(Nn.tobytes(), 34962); vu = add(U.tobytes(), 34962); vi = add(I.tobytes(), 34963)
img_views = [add(raw) for raw, _ in images]
out = {
    "asset": {"version": "2.0", "generator": "PotatoFarmer decimate.py"},
    "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0}],
    "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "indices": 3, "material": 0}]}],
    "accessors": [
        {"bufferView": vp, "componentType": 5126, "count": len(P), "type": "VEC3", "min": P.min(0).tolist(), "max": P.max(0).tolist()},
        {"bufferView": vn, "componentType": 5126, "count": len(P), "type": "VEC3"},
        {"bufferView": vu, "componentType": 5126, "count": len(P), "type": "VEC2"},
        {"bufferView": vi, "componentType": 5125, "count": len(I), "type": "SCALAR"},
    ],
    "materials": gltf["materials"], "textures": gltf["textures"], "samplers": gltf.get("samplers", [{}]),
    "images": [{"bufferView": v, "mimeType": mime} for v, (_, mime) in zip(img_views, images)],
    "bufferViews": views, "buffers": [{"byteLength": len(chunks)}],
}
js = json.dumps(out).encode()
js += b" " * (-len(js) % 4)
while len(chunks) % 4: chunks.append(0)
with open(dst, "wb") as f:
    f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(chunks)))
    f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
    f.write(struct.pack("<II", len(chunks), 0x004E4942)); f.write(bytes(chunks))
print("wrote", dst)
