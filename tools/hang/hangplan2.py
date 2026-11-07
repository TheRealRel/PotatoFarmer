# Hanging layout v2: each item hangs the way the game's cooking spits hang it (CookingStation.SetSlotVisual: the item's
# attach at the slot, slot rotation (-0.5,0.5,0.5,0.5): the item's +Z points down, +X along the bar), turned a quarter
# turn about the vertical when that fits better, shrunk only when it can't fit its hook's share of the rack (the iron
# spit lets meats overlap their neighbours by about a third, so the same allowance here).
import sys, json, struct, numpy as np
sys.path.insert(0, '/root/rackview'); from hanglib import *
HOOK_X = [-0.307, 0.100, 0.448]; HOOK_Z = [-0.47, 0.0, 0.475]; HOOK_Y = 0.825
SPACE_X, SPACE_Z = 0.41, 0.47
SLOT = Q((-0.5, 0.5, 0.5, 0.5))
OVERLAP = 1.05
PIERCE = 0.035   # model units: an item's top sits this far above the hook's lowest point, so the hook goes into it
JERKY_SCALE, JERKY_FAN = 1.12, 6.0
import json as _json
HOOKS = _json.load(open('/root/rackview/hooks.json'))
TWO_LOBES = True
HONEY_SIDE = False
TOP_BAND = 0.015   # model units (3 cm at 2x)
ANCHOR = True
HONEY_SIZE, HONEY_TUCK = 0.30, 0.5   # metres the comb hangs down; how much of it the meat's top tucks behind
JERKY_MODE = {}
JERKY_AT = None   # fn(hook, S) -> (big, small) points where a jerky's top goes (test c of 1.4.1: the hook's valley + lift)

def pts_item(name): return verts(name) - np.array(ATT.get(name, [0, 0, 0]))
def pts_ptm(path):
    d = open(path, 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12]); return np.frombuffer(d, '<f4', nv * 3, 12).reshape(-1, 3).astype(float)

def _axis_down(a, sign):
    """Rotation taking item axis a (0,1,2), signed, to world down (-y); the other two axes to x and z."""
    Rm = np.zeros((3, 3)); others = [k for k in range(3) if k != a]
    Rm[1, a] = -sign; Rm[0, others[0]] = 1; Rm[2, others[1]] = 1
    if np.linalg.det(Rm) < 0: Rm[2] *= -1
    M = np.eye(4); M[:3, :3] = Rm; return M

def place(pts, hook, S, room_along, room_across, room_down, turn_ok=True, extra=np.eye(4), game_ok=True, anchor=False):
    """Returns matrix (item local -> rack) and the hung bounds. pts: item points in the attach's space.
    Candidates: the game's own spit hanging (two quarter turns), and the longest side hanging straight down with the
    heavier end lowest (two quarter turns). Biggest fit wins; the game's way is kept when it fits within 10 %."""
    size0 = pts.max(0) - pts.min(0); longest = int(np.argmax(size0))
    cands = []
    bases = [('game', SLOT @ extra)] if game_ok else []
    # longest side down, the end holding more of the model lowest (a hanging piece's weight pulls that end down)
    def low(sign):
        w = (_axis_down(longest, sign)[:3, :3] @ pts.T).T
        return w[:, 1].mean() - (w[:, 1].min() + w[:, 1].max()) / 2
    bases.append(('long', _axis_down(longest, 1 if low(1) <= low(-1) else -1)))
    for tag, B in bases:
        for turn in ((0, 90) if turn_ok else (0,)):
            Rr = R_('y', turn) @ B
            w = (Rr[:3, :3] @ pts.T).T
            lo, hi = w.min(0), w.max(0); size = hi - lo
            s = min(1.0, room_along / size[0], room_down / size[1], room_across / size[2])
            cands.append((s, tag, turn, Rr, lo * s, hi * s, size[0] / size[2]))
    best = max(c[0] for c in cands)
    game = [c for c in cands if c[1] == 'game' and c[0] >= best * 0.9]
    pool = game if game else [c for c in cands if c[0] >= best - 0.03]
    s, tag, turn, Rr, lo, hi, _ = max(pool, key=lambda c: (round(c[0], 2), c[6]))
    off = np.array([hook[0] - (lo[0] + hi[0]) / 2, hook[1] - hi[1], hook[2] - (lo[2] + hi[2]) / 2])
    if anchor:
        # hang from the item's real top: the middle of its highest 3 cm of vertices goes on the hook
        w = (Rr[:3, :3] @ pts.T).T * s
        topz = w[:, 1].max(); near = w[w[:, 1] >= topz - TOP_BAND * S]
        a = near.mean(0); a[1] = topz
        off = hook - a
        lo, hi = w.min(0), w.max(0)
    M = T(off) @ S_(s) @ Rr
    return M, s, f"{tag}{turn}", lo + off, hi + off
def R_(a, d): return R(a, d)
def S_(s): return S(s)

def build(S, items, state, honey_scale=0.55):
    objs = [{"kind": "ptm", "src": "hang/laradr_rack.mesh", "tex": "hang/laradr_rack_d.png", "m": colmajor(S_(S))}]
    rep = []
    for i, load in enumerate(items):
        if not load: continue
        meat, jerky = load
        hook = np.array([HOOK_X[i % 3] * S, (HOOK_Y + PIERCE) * S, HOOK_Z[i // 3] * S])
        ra, rc, rd = SPACE_X * S * OVERLAP, SPACE_Z * S, (HOOK_Y + PIERCE) * S - 0.05
        out = 1 if hook[2] >= 0 else -1  # honey on the outside of the outer rows
        H = HOOKS[i]
        big = np.array([H['big']['cup_x'], H['big']['cup_y'] + PIERCE, H['big']['cup_z']]) * S
        small = np.array([H['small']['cup_x'], H['small']['cup_y'] + PIERCE, H['small']['cup_z']]) * S
        if state == 'raw':
            # each hook has two curls: the meat on the bigger one, the honeycomb on the smaller one
            Mh, sh, _, hlo, hhi = place(pts_item('Honey'), small, S, ra, rc, rd, anchor=ANCHOR)
            hs = HONEY_SIZE / max(hhi[1] - hlo[1], 1e-3)
            Mh = T(small) @ S_(hs) @ T(-small) @ Mh
            M, s, turn, lo, hi = place(pts_item(meat), big, S, ra, rc, rd, anchor=ANCHOR)
            objs.append({"kind": "item", "src": f"hang/{meat}.json", "m": colmajor(M)})
            objs.append({"kind": "item", "src": "hang/Honey.json", "m": colmajor(Mh)})
            rep.append(f"{meat:20s} scale {s:.2f} {turn:7s}  down {hi[1]-lo[1]:.2f} along {hi[0]-lo[0]:.2f} across {hi[2]-lo[2]:.2f}  bottom {lo[1]:.2f} m")
        else:
            kind, src, tex = jerky
            p = pts_ptm(src) if kind == 'ptm' else pts_item(src.split('/')[-1][:-5])
            # two jerkies on the one hook, 12 % bigger, their tops together on the hook and fanned out a little
            M, s, turn, lo, hi = place(p, hook, S, ra / 2, rc, rd, game_ok=False)
            wdt = hi[0] - lo[0]
            mode = JERKY_MODE.get(src, 'bundle')
            pair = JERKY_AT(i, S, src) if JERKY_AT else (big, small)
            for k, at in enumerate(pair):
                Mj, s, turn, lo, hi = place(p, at, S, ra / 2, rc, rd, game_ok=False, anchor=ANCHOR)
                wdt = hi[0] - lo[0]
                if mode == 'diag':
                    Rz = R('z', 45 if k == 0 else 35)
                    Mk = Rz @ S_(JERKY_SCALE) @ Mj
                    w = (Mk[:3, :3] @ p.T).T + Mk[:3, 3]
                    top = w[np.argmax(w[:, 1])]
                    Mk = T(at - top + np.array([(k - 0.5) * 0.04 * S, 0, 0])) @ Mk
                else:
                    Mk = T(at) @ S_(JERKY_SCALE) @ T(-at) @ Mj
                    Mk = T([(k - 0.5) * 2 * wdt * 0.12, 0, 0]) @ Mk
                o = {"kind": kind, "src": src, "m": colmajor(Mk)}
                if tex: o["tex"] = tex
                objs.append(o)
            rep.append(f"{src:28s} jerky scale {s:.2f} each {wdt:.2f} wide, {hi[1]-lo[1]:.2f} down")
    return objs, rep
