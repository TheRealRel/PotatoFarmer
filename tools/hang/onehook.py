import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
S = 2.0; tiles = []
for kv in (sys.argv[3].split(',') if len(sys.argv) > 3 else []):
    k, v = kv.split('='); hp.JERKY_MODE[k] = v
for n in sys.argv[1].split(','):
    for state in ('raw', 'done'):
        loads = [None] * 9; loads[1] = (n, JERKY[n])
        objs, rep = hp.build(S, loads, state)
        hx, hz = hp.HOOK_X[1] * S, hp.HOOK_Z[0] * S
        for view, cam in (('front', [hx + 0.25, 1.45, hz - 1.6, hx, 1.3, hz]), ('side', [hx + 1.6, 1.45, hz - 0.25, hx, 1.3, hz])):
            tiles.append({"label": f"{n} {state} {view}", "objects": objs, "cam": cam, "fov": 40, "ground": True})
json.dump({"tiles": tiles, "cols": 8, "w": 250, "h": 300}, open(sys.argv[2], 'w'))
