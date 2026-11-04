import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
tiles = []
for n in sys.argv[1].split(','):
    for view, cam, fov in (('eye level, front', [0.15, 1.75, -3.6, 0.15, 1.45, 0], 50), ('from above', [1.4, 3.0, -2.8, 0.15, 1.4, 0], 50)):
        objs, rep = h3.build(2.0, [(n, None)] * 9, 'raw')
        tiles.append({"label": f"{n} {h3.HONEY_TILT.get(n, 'straight')} {view}", "objects": objs, "cam": cam, "fov": fov, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 470, "h": 330}, open(sys.argv[2], 'w'))
