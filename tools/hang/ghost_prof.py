import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
tiles = []; HI = int(sys.argv[3]) if len(sys.argv) > 3 else 1
for n, r in json.loads(sys.argv[1]):
    h3.RAISE[n] = r
    loads = [None] * 9; loads[HI] = (n, None)
    objs, rep = h3.build(2.0, loads, 'raw')
    for o in objs[1:]: o['opacity'] = 0.45
    c = np.array([hp.HOOKS[HI]['big'][k] for k in ('cup_x', 'cup_y', 'cup_z')]) * 2
    tiles.append({"label": f"{n} +{r*100:.0f}cm along bar", "objects": objs, "cam": [c[0] + 9, c[1] + 0.02, c[2], c[0], c[1] + 0.02, c[2]], "fov": 3.2, "ground": False})
    tiles.append({"label": f"{n} +{r*100:.0f}cm front", "objects": objs, "cam": [c[0], c[1] + 0.02, c[2] - 9, c[0], c[1] + 0.02, c[2]], "fov": 3.2, "ground": False})
json.dump({"tiles": tiles, "cols": 4, "w": 360, "h": 360}, open(sys.argv[2], 'w'))
