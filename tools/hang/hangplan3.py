# v3: the hook goes into the meat. For each raw item the candidates are the game's spit way, the same flipped over
# (180 deg about the bar's across axis), and the longest-side-down way, each with a quarter turn. For each one the hook
# point is found from the mesh itself (anchor2.hang_point: the highest spot where the meat is thick enough to hold the
# curl 6 cm deep with 3 cm to spare), and its "protrusion" (how far anything, such as a bone, rises above that spot).
import sys, numpy as np
sys.path.insert(0, '/root/rackview')
from hanglib import *
import hangplan2 as hp
from anchor2 import mesh, hang_point, balance
CURL_DEPTH = 0.06      # metres the curl's lowest point sits below the meat's top at the hook point
BAL_OK = 0.04         # metres: the centre of mass this close under the hook counts as hanging straight
PROT_OK = 0.02         # metres: a hang this much or less "below" the item's top counts as hanging by its top
TRACE = {}
USE_TILT_ONLY = False
# Extra height per meat so the lower half of its curl is inside it (raise.py; worst of the nine hooks), metres.
import json as _json, os as _os
HONEY_TILT = _json.load(open('/root/rackview/honey_tilt.json')) if _os.path.exists('/root/rackview/honey_tilt.json') else {}
HONEY_REST = _json.load(open('/root/rackview/honey_rest3.json')) if _os.path.exists('/root/rackview/honey_rest3.json') else {}
RAISE = _json.load(open('/root/rackview/raise_view.json')) if _os.path.exists('/root/rackview/raise_view.json') else {}

def cands(V, room_along, room_across, room_down):
    size0 = V.max(0) - V.min(0); longest = int(np.argmax(size0))
    def low(sign):
        w = (hp._axis_down(longest, sign)[:3, :3] @ V.T).T
        return w[:, 1].mean() - (w[:, 1].min() + w[:, 1].max()) / 2
    bases = [('game', hp.SLOT), ('flip', R('x', 180) @ hp.SLOT),
             ('long', hp._axis_down(longest, 1 if low(1) <= low(-1) else -1))]
    out = []
    for tag, B in bases:
        for turn in (0, 90):
            Rr = R('y', turn) @ B
            w = (Rr[:3, :3] @ V.T).T; size = w.max(0) - w.min(0)
            s = min(1.0, room_along / size[0], room_down / size[1], room_across / size[2])
            out.append(dict(tag=f"{tag}{turn}", base=tag, s=s, R=Rr, broad=size[0] / size[2]))
    return out

def place_meat(name, at, RS, ra, rc, rd, force=None):
    V, F = mesh(name)
    cs = cands(V, ra, rc, rd)
    for c in cs:
        W = (c['R'][:3, :3] @ V.T).T * c['s']
        p, prot, ok = hang_point(W, F)
        c.update(p=p, prot=prot, ok=ok, bal=balance(W, F, p))
    best = max(c['s'] for c in cs)
    pool = [c for c in cs if c['s'] >= best * 0.9]
    if force: pool = [c for c in cs if c['tag'] == force]
    # hook in thick meat first, then little rising above the hook, then the game's way, then the broad side out
    c = min(pool, key=lambda c: (not c['ok'], c['prot'] > PROT_OK, c['bal'] > BAL_OK, round(c['prot'], 2) if c['prot'] > PROT_OK else 0,
                                c['base'] != 'game', c['base'] != 'flip', -round(c['s'], 2), -c['broad']))
    # the hang point (CURL_DEPTH under the column's top) goes on the curl's lowest point
    cup = at - np.array([0, hp.PIERCE * RS, 0])
    off = cup - c['p'] - np.array([0, CURL_DEPTH - 0.06, 0]) + np.array([0, RAISE.get(name, 0.0), 0])
    M = T(off) @ S(c['s']) @ c['R']
    TRACE[name] = {k: (round(float(v), 3) if isinstance(v, (float, np.floating)) else v) for k, v in c.items() if k in ('tag', 's', 'prot', 'ok')}
    TRACE[name]['all'] = [(d['tag'], round(float(d['s']), 2), round(float(d['prot']), 3), round(d['bal'], 3), d['ok']) for d in cs]
    return M, c

_orig = hp.build
JERKY_SIZE = _json.load(open('/root/rackview/jerky_size.json')) if _os.path.exists('/root/rackview/jerky_size.json') else {}
def _done(S_, items):
    # each of our jerkies drawn at its own size (Jerky.cs Kind.Size, from its meat): points and matrices scaled
    out_objs, out_rep = None, []
    for i, load in enumerate(items):
        if not load: continue
        k = JERKY_SIZE.get(load[0], 1.0)
        one = [None] * len(items); one[i] = load
        base = hp.pts_ptm; hp.pts_ptm = lambda path, k=k: base(path) * k
        try: objs, rep = _orig(S_, one, 'done')
        finally: hp.pts_ptm = base
        for o in objs[1:]:
            if o.get('kind') == 'ptm' and o['src'].startswith('jerky/'):
                M = np.array(o['m']).reshape(4, 4).T; o['m'] = colmajor(M @ S(k))
        out_objs = objs if out_objs is None else out_objs + objs[1:]
        out_rep += rep
    return out_objs or _orig(S_, items, 'done')[0], out_rep
def build(S_, items, state, honey_scale=0.55, force=None):
    if state != 'raw': return _done(S_, items)
    objs = [{"kind": "ptm", "src": "hang/laradr_rack.mesh", "tex": "hang/laradr_rack_d.png", "m": colmajor(S(S_))}]
    rep = []
    for i, load in enumerate(items):
        if not load: continue
        meat = load[0]
        ra, rc, rd = hp.SPACE_X * S_ * hp.OVERLAP, hp.SPACE_Z * S_, (hp.HOOK_Y + hp.PIERCE) * S_ - 0.05
        H = hp.HOOKS[i]
        big = np.array([H['big']['cup_x'], H['big']['cup_y'] + hp.PIERCE, H['big']['cup_z']]) * S_
        small = np.array([H['small']['cup_x'], H['small']['cup_y'] + hp.PIERCE, H['small']['cup_z']]) * S_
        Mh, sh, _, hlo, hhi = hp.place(hp.pts_item('Honey'), small, S_, ra, rc, rd, anchor=hp.ANCHOR)
        hs = hp.HONEY_SIZE / max(hhi[1] - hlo[1], 1e-3)
        Mh = T(small) @ S(hs) @ T(-small) @ Mh
        # a comb its meat would hide swings sideways along the bar, pivoting on its curl (honeytilt.py)
        tilt = HONEY_TILT.get(meat)
        if tilt:
            # axis in the floor plane at angle phi from across-the-bar; comb swings about it by theta
            phi, theta = np.radians(tilt[0]), np.radians(tilt[1]); ax = np.array([np.sin(phi), 0, np.cos(phi)])
            K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
            Rt = np.eye(4); Rt[:3, :3] = np.eye(3) + np.sin(theta) * K + (1 - np.cos(theta)) * K @ K
            Mh = T(small) @ Rt @ T(-small) @ Mh
        if meat in HONEY_REST and not USE_TILT_ONLY:
            Mh = np.array(HONEY_REST[meat][i]['M'])
        M, c = place_meat(meat, big, S_, ra, rc, rd, force=force)
        objs.append({"kind": "item", "src": f"hang/{meat}.json", "m": colmajor(M)})
        objs.append({"kind": "item", "src": "hang/Honey.json", "m": colmajor(Mh)})
        rep.append(f"{meat} {c['tag']} s {c['s']:.2f} prot {c['prot']:.3f}")
    return objs, rep
