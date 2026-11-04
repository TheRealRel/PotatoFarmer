import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
tiles = []
for n, cam, fov in (
    ('BjornMeat', [2.25, 1.45, -0.55, 0.0, 1.62, -0.35], 55),
    ('BjornMeat', [2.6, 1.55, -0.2, 0.9, 1.62, 0.0], 30),
    ('HareMeat', [1.6, 1.75, -2.2, 0.2, 1.45, -0.8], 45),
    ('HareMeat', [1.2, 1.7, -1.9, 0.2, 1.55, -1.0], 30)):
    objs, rep = h3.build(2.0, [(n, None)] * 9, 'raw')
    tiles.append({"label": n, "objects": objs, "cam": cam, "fov": fov, "ground": True})
json.dump({"tiles": tiles, "cols": 2, "w": 640, "h": 420}, open(sys.argv[1], 'w'))
