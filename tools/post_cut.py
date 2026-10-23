# Sleipnir's Post (Valheim, PotatoFarmer): removes the loose rope coil Meshy hung from the right ring, which made no
# sense once the post draws its own ropes. Meshy fused the coil into the ring, so the right ring's lower half goes with
# it and is rebuilt as a mirror copy of the left ring's (the two rings match: x 0.26-0.30 m out, centre 1.664 m up).
# python3 -I tools/post_cut.py assets/post.mesh
import struct, sys
import numpy as np

CUT = 1.675  # below the crossbar's pegs: only ring and rope down here
path = sys.argv[1]
b = open(path, 'rb').read()
assert b[:4] == b'PTM1'
nv, ni = struct.unpack('<ii', b[4:12]); o = 12
P = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3); o += nv * 12
N = np.frombuffer(b, '<f4', nv * 3, o).reshape(-1, 3); o += nv * 12
UV = np.frombuffer(b, '<f4', nv * 2, o).reshape(-1, 2); o += nv * 8
I = np.frombuffer(b, '<i4', ni, o).reshape(-1, 3)
c = P[I].mean(1)
radial = np.hypot(c[:, 0], c[:, 2])
off_pole = (radial > 0.19) & (c[:, 1] < CUT) & (c[:, 1] > 1.1)
right = off_pole & (c[:, 0] > 0)          # right ring's lower half and the coil
left = off_pole & (c[:, 0] < 0)           # left ring's lower half
keep = I[~right]
mirror = I[left][:, ::-1]                 # mirrored, so the winding flips
used_keep = np.unique(keep); used_mirror = np.unique(mirror)
P2 = np.concatenate([P[used_keep], P[used_mirror] * [-1, 1, 1]])
N2 = np.concatenate([N[used_keep], N[used_mirror] * [-1, 1, 1]])
UV2 = np.concatenate([UV[used_keep], UV[used_mirror]])
r1 = -np.ones(nv, np.int64); r1[used_keep] = np.arange(len(used_keep))
r2 = -np.ones(nv, np.int64); r2[used_mirror] = len(used_keep) + np.arange(len(used_mirror))
I2 = np.concatenate([r1[keep], r2[mirror]]).astype('<i4')
with open(path, 'wb') as f:
    f.write(b'PTM1'); f.write(struct.pack('<ii', len(P2), I2.size))
    f.write(P2.astype('<f4').tobytes()); f.write(N2.astype('<f4').tobytes()); f.write(UV2.astype('<f4').tobytes()); f.write(I2.tobytes())
print(f'removed {right.sum()} triangles (coil and right ring half), mirrored {left.sum()} from the left ring; {len(I2)} triangles')
