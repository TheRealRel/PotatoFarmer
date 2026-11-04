import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
hp.JERKY_MODE['jerky/jerky_deer.mesh'] = 'diag'
cam = [0.2, 1.75, -3.4, 0.2, 1.35, -0.6]
tiles = []
for label, meat, size in json.loads(sys.argv[1]):
    h3.JERKY_SIZE[meat] = size
    # front row only, so the size reads clearly
    objs, rep = h3.build(2.0, [(meat, JERKY[meat])] * 3 + [None] * 6, 'done')
    tiles.append({"label": label, "objects": objs, "cam": cam, "fov": 42, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 470, "h": 360}, open(sys.argv[2], 'w'))
