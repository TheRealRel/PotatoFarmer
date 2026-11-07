# jerkyhook.py out.json [ghost]: each jerky (done) on one hook; front view (front row from outside) and side view (middle row from the rack's end)
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *; import hangplan2 as hp; import hangplan3 as h3
S = 2.0; tiles = []
ghost = len(sys.argv) > 2
J = [('RawMeat', ('item', 'hang/BoarJerky.json', None)), ('WolfMeat', ('item', 'hang/WolfJerky.json', None)),
     ('DeerMeat', ('ptm', 'hang/jerkyshape_deer.mesh', 'hang/jerky_deer_d.png')), ('NeckTail', ('ptm', 'hang/jerkyshape_bloodgill.mesh', 'hang/jerky_necktail_d.png')),
     ('LoxMeat', ('ptm', 'hang/jerkyshape_lox.mesh', 'hang/jerky_lox_d.png')), ('SerpentMeat', ('ptm', 'hang/jerkyshape_serpent.mesh', 'hang/jerky_serpent_d.png'))]
hp.JERKY_MODE['hang/jerkyshape_deer.mesh'] = 'diag'
for meat, jerky in J:
    for view, HI in (('front', 1), ('side', 5)):
        loads = [None] * 9; loads[HI] = (meat, jerky)
        objs, rep = h3.build(S, loads, 'done')
        if ghost:
            for o in objs[1:]: o['opacity'] = 0.45
        c = np.array([hp.HOOKS[HI]['big']['cup_x'], hp.HOOKS[HI]['big']['cup_y'], hp.HOOKS[HI]['big']['cup_z']]) * S
        cam = [c[0] + 0.1, c[1] - 0.02, c[2] - 1.1, c[0], c[1] - 0.12, c[2]] if view == 'front' else [2.7, c[1] - 0.0, c[2] + 0.05, c[0], c[1] - 0.12, c[2]]
        tiles.append({"label": f"{jerky[1].split('/')[-1]} {view}", "objects": objs, "cam": cam, "fov": 34 if view == 'front' else 18, "ground": True})
json.dump({"tiles": tiles, "cols": 4, "w": 320, "h": 380}, open(sys.argv[1], 'w'))
