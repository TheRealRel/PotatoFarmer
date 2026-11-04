# onehook3.py names out.json [force-tag] [ghost]: raw meat + honey on one hook; front (front row, from outside) and side
# (middle row, from the rack's end); with "ghost" the meat is see-through so the curl inside it shows.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
S = 2.0; tiles = []
force = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] != '-' else None
ghost = len(sys.argv) > 4
for n in sys.argv[1].split(','):
    for view, HI in (('front', 1), ('side', 5)):
        loads = [None] * 9; loads[HI] = (n, None)
        objs, rep = h3.build(S, loads, 'raw', force=force)
        if ghost:
            for o in objs[1:]: o['opacity'] = 0.35
        c = np.array([hp.HOOKS[HI]['big']['cup_x'], hp.HOOKS[HI]['big']['cup_y'], hp.HOOKS[HI]['big']['cup_z']]) * S
        cam = [c[0] + 0.15, c[1] - 0.05, c[2] - 1.5, c[0], c[1] - 0.12, c[2]] if view == 'front' else [2.7, c[1] - 0.05, c[2] + 0.1, c[0], c[1] - 0.12, c[2]]
        tiles.append({"label": f"{n} {h3.TRACE[n]['tag']} {view}", "objects": objs, "cam": cam, "fov": 40 if view == 'front' else 28, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 300, "h": 360}, open(sys.argv[2], 'w'))
