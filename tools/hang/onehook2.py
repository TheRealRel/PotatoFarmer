import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp
exec(open('/root/rackview/hangview.py').read().split("def loads")[0].split("from hangplan import *")[1])
S = 2.0; tiles = []
hp.JERKY_MODE['jerky/jerky_deer.mesh'] = 'diag'
for n in sys.argv[1].split(','):
    for state in ('raw', 'done'):
        # front: front-row middle hook seen from outside; side: middle row's right-hand hook seen from the rack's end
        for view, HI in (('front', 1), ('side', 5)):
            loads = [None] * 9; loads[HI] = (n, JERKY[n])
            objs, rep = hp.build(S, loads, state)
            c = np.array([hp.HOOKS[HI]['big']['cup_x'], hp.HOOKS[HI]['big']['cup_y'], hp.HOOKS[HI]['big']['cup_z']]) * S
            cam = [c[0] + 0.15, c[1] - 0.05, c[2] - 1.5, c[0], c[1] - 0.12, c[2]] if view == 'front' else [2.7, c[1] - 0.05, c[2] + 0.1, c[0], c[1] - 0.12, c[2]]
            tiles.append({"label": f"{n} {state} {view}", "objects": objs, "cam": cam, "fov": 40 if view == 'front' else 28, "ground": True})
json.dump({"tiles": tiles, "cols": 8, "w": 250, "h": 320}, open(sys.argv[2], 'w'))
