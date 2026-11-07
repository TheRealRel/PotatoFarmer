import sys, json, numpy as np
sys.argv_ = sys.argv; sys.argv = ['x', '']
sys.path.insert(0, '/root/rackview'); import jlift as jl
from hanglib import *
sys.argv = sys.argv_
lift = float(sys.argv[2])
loads = [jl.J['Deer'], jl.J['Boar'], jl.J['Wolf'], jl.J['Fan'], jl.J['Lox'], jl.J['Deer'], jl.J['Serpent'], jl.J['BearTW'], jl.J['FoxTW']]
cams = [[2.0, 2.2, -1.8, 0.2, 1.55, -0.94], [-1.0, 1.75, -2.4, -0.1, 1.55, -0.9], [2.9, 1.5, 0.15, 0.0, 1.5, 0.0]]
tiles = []
for cam in cams:
    for lab, at in (('now (1.4.1 test a)', False), (f'proposed: through the hook', True)):
        jl.hp.JERKY_AT = jl.at_fn if at else None
        for l in loads: jl.LIFT[l[1][1]] = lift
        objs, _ = jl.h3.build(2.0, loads, 'done')
        tiles.append({"label": lab, "objects": objs, "cam": cam, "fov": 24, "ground": True})
json.dump({"tiles": tiles, "cols": 2, "w": 760, "h": 470}, open(sys.argv[1], 'w'))
