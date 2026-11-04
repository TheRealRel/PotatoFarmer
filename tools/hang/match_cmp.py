import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
views = {'BjornMeat': ([2.25, 1.45, -0.55, 0.0, 1.62, -0.35], 55), 'HareMeat': ([1.6, 1.75, -2.2, 0.2, 1.45, -0.8], 45)}
tiles = []
for n, raises in json.loads(sys.argv[1]).items():
    cam, fov = views[n]
    for r in raises:
        h3.RAISE[n] = r
        objs, rep = h3.build(2.0, [(n, None)] * 9, 'raw')
        tiles.append({"label": f"{n} raise {r*100:.0f} cm", "objects": objs, "cam": cam, "fov": fov, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 480, "h": 330}, open(sys.argv[2], 'w'))
