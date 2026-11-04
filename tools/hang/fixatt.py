# fixatt.py in.json out.json: the exported item vertices are in the item's root space (they include the attach's own
# offset, see export.py); the game places the attach copy, i.e. vertices minus that offset. Every placement matrix is
# worked out for (vertices - offset), so here each item object gets the offset taken off before its matrix.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import ATT, T, colmajor
d = json.load(open(sys.argv[1]))
for t in d['tiles']:
    for o in t['objects']:
        if o.get('kind') == 'item' and not o.get('att_done'):
            n = o['src'].split('/')[-1][:-5]
            M = np.array(o['m']).reshape(4, 4).T
            o['m'] = colmajor(M @ T(-np.array(ATT.get(n, [0, 0, 0]))))
            o['att_done'] = True
json.dump(d, open(sys.argv[2], 'w'))
