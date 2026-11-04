import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan3 as h3
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
views = (('close, from above', [0.5, 2.1, -2.3, 0.2, 1.6, -0.9], 40), ('close, eye level', [0.6, 1.6, -2.1, 0.35, 1.7, -0.9], 45))
tiles = []
for tag, path in json.loads(sys.argv[1]):
    data = json.load(open(path))
    for n in sys.argv[2].split(','):
        h3.HONEY_REST[n] = data[n]
        for view, cam, fov in views:
            objs, rep = h3.build(2.0, [(n, None)] * 9, 'raw')
            tiles.append({"label": f"{n} {tag} {view}", "objects": objs, "cam": cam, "fov": fov, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 470, "h": 330}, open(sys.argv[3], 'w'))
