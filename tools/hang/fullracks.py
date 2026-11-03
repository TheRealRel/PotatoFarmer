import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; from hangplan2 import *
import hangplan2 as hp
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
S = float(sys.argv[1]); names = sys.argv[2].split(','); state = sys.argv[3]; out = sys.argv[4]
cam = [1.2 * S, 1.0 * S, -2.4 * S, 0.1 * S, 0.62 * S, 0]
hp_modes = {'jerky/jerky_deer.mesh': 'diag'}
tiles = []
hp.JERKY_MODE.update({'jerky/jerky_deer.mesh': 'diag'})
for n in names:
    objs, rep = build(S, [(n, JERKY[n])] * 9, state)
    tiles.append({"label": f"{n} x9 ({state}) " + rep[0].split('scale')[1][:6], "objects": objs, "cam": cam, "ground": True})
json.dump({"tiles": tiles, "cols": 3, "w": 620, "h": 460}, open(out, 'w'))
