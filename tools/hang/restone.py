import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
tiles = []; HI = int(sys.argv[4]) if len(sys.argv) > 4 else 1
H = hp.HOOKS[HI]; c = np.array([H['small']['cup_x'], H['small']['cup_y'], H['small']['cup_z']]) * 2
views = (('outside', [c[0] + 0.2, c[1] + 0.15, c[2] - 1.4], 34), ('end', [c[0] + 0.42, c[1] + 0.02, c[2] - 0.05], 60), ('inside, above', [c[0] - 0.3, c[1] + 0.8, c[2] + 1.1], 34))
for tag, path in json.loads(sys.argv[1]):
    data = json.load(open(path))
    for n in sys.argv[2].split(','):
        h3.HONEY_REST[n] = data[n]
        loads = [None] * 9; loads[HI] = (n, None)
        objs, rep = h3.build(2.0, loads, 'raw')
        for view, cam, fov in views:
            tiles.append({"label": f"{n} {tag} {view}", "objects": objs, "cam": cam + [c[0], c[1] - 0.12, c[2]], "fov": fov, "ground": True})
json.dump({"tiles": tiles, "cols": 6, "w": 320, "h": 300}, open(sys.argv[3], 'w'))
