import struct, numpy as np, json, sys
d = open('hang/laradr_rack.mesh', 'rb').read(); head = d[:4]; nv, ni = struct.unpack('<ii', d[4:12]); o = 12
P = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3); o += nv * 12
N = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3); o += nv * 12
U = np.frombuffer(d, '<f4', nv * 2, o).reshape(-1, 2); o += nv * 8
I = np.frombuffer(d, '<i4', ni, o).reshape(-1, 3)
H = json.load(open('hooks.json'))
for i, h in enumerate(H):
    keep = (np.abs(P[:, 0] - h['x']) < 0.13) & (P[:, 1] > 0.74) & (P[:, 1] < 1.0) & (np.abs(P[:, 2] - h['z']) < 0.13)
    T = I[keep[I].all(1)]
    used = np.unique(T); remap = -np.ones(nv, int); remap[used] = np.arange(len(used))
    out = head + struct.pack('<ii', len(used), T.size) + P[used].astype('<f4').tobytes() + N[used].astype('<f4').tobytes() + U[used].astype('<f4').tobytes() + remap[T].astype('<i4').tobytes()
    open(f'hang/hook{i}.mesh', 'wb').write(out)
print('ok')
