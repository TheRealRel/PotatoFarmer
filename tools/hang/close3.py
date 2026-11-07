import sys, json, numpy as np
sys.argv_ = sys.argv; sys.argv = ['x', '']
sys.path.insert(0, '/root/rackview'); import jlift as jl
from hanglib import *
sys.argv = sys.argv_
lift = float(sys.argv[2]); HI = int(sys.argv[3]); names = sys.argv[4].split(',')
tiles = []
for n in names:
    load = jl.J[n]; jl.LIFT[load[1][1]] = lift
    loads = [None] * 9; loads[HI] = load
    objs, rep = jl.h3.build(2.0, loads, 'done')
    objs[0] = {"kind": "ptm", "src": f"hang/hook{HI}.mesh", "tex": "hang/laradr_rack_d.png", "m": colmajor(np.diag([2.0, 2, 2, 1]))}
    H = jl.hp.HOOKS[HI]; c = np.array([H['x'], H['big']['cup_y'], H['z']]) * 2
    g = [dict(o) for o in objs]
    for o in g[1:]: o['opacity'] = 0.4
    for lab, ob in (('', objs), ('ghost', g)):
        tiles.append({"label": f"{n} side {lab}", "objects": ob, "cam": [c[0] - 6, c[1] - 0.12, c[2], c[0], c[1] - 0.12, c[2]], "fov": 5.5, "ground": False})
    tiles.append({"label": f"{n} front", "objects": objs, "cam": [c[0], c[1] - 0.12, c[2] - 6, c[0], c[1] - 0.12, c[2]], "fov": 5.5, "ground": False})
    tiles.append({"label": f"{n} 3/4", "objects": objs, "cam": [c[0] + 1.2, c[1] + 0.5, c[2] - 1.4, c[0], c[1] - 0.12, c[2]], "fov": 22, "ground": False})
json.dump({"tiles": tiles, "cols": 4, "w": 300, "h": 380}, open(sys.argv[1], 'w'))
