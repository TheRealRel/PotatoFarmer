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
HONEY_SIDE = False
HONEY_SIZE, HONEY_TUCK = 0.30, 0.5   # metres the comb hangs down; how much of it the meat's top tucks behind
JERKY_MODE = {}

def pts_item(name): return verts(name) - np.array(ATT.get(name, [0, 0, 0]))
def pts_ptm(path):
    d = open(path, 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12]); return np.frombuffer(d, '<f4', nv * 3, 12).reshape(-1, 3).astype(float)

def _axis_down(a, sign):
    """Rotation taking item axis a (0,1,2), signed, to world down (-y); the other two axes to x and z."""
    Rm = np.zeros((3, 3)); others = [k for k in range(3) if k != a]
    Rm[1, a] = -sign; Rm[0, others[0]] = 1; Rm[2, others[1]] = 1
    if np.linalg.det(Rm) < 0: Rm[2] *= -1
    M = np.eye(4); M[:3, :3] = Rm; return M

def place(pts, hook, S, room_along, room_across, room_down, turn_ok=True, extra=np.eye(4), game_ok=True):
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
        if state == 'raw':
            # The honeycomb goes on the hook first; the meat hangs from the hook right under it, its top tucked behind
            # the comb's lower part. Nothing hangs off the hook on its own.
            hlo0, hhi0 = pts_item('Honey').min(0), pts_item('Honey').max(0)
            Mh, sh, _, hlo, hhi = place(pts_item('Honey'), hook, S, ra, rc, rd)
            hs = HONEY_SIZE / max(hhi[1] - hlo[1], 1e-3)          # the comb's hanging height, fixed
            Mh = T(hook) @ S_(hs) @ T(-hook) @ Mh
            hlo = hook + (hlo - hook) * hs; hhi = hook + (hhi - hook) * hs
            drop = (hhi[1] - hlo[1]) * HONEY_TUCK
            mhook = hook - np.array([0, drop, 0])
            M, s, turn, lo, hi = place(pts_item(meat), mhook, S, ra, rc, rd - drop)
            # the comb sits just in front of the meat's top (outer side), centred on the hook along the bar
            dz = 0.0  # on the hook itself: its lower half goes into the meat's top
            Mh = T([0, 0, dz]) @ Mh
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
            for k, side in enumerate((-1, 1)):
                if mode == 'fan':
                    Mk = T(hook) @ R('z', side * JERKY_FAN) @ S_(JERKY_SCALE) @ T(-hook) @ M
                    Mk = T([side * wdt * 0.18, 0, (k - 0.5) * 0.03 * S]) @ Mk
                elif mode == 'bundle':
                    Mk = T(hook) @ S_(JERKY_SCALE) @ T(-hook) @ M
                    Mk = T([side * wdt * 0.12, -k * 0.02 * S, (k - 0.5) * 0.04 * S]) @ Mk
                elif mode == 'diag':
                    # turned 45 degrees in its own face (crossed strips read as an X), hung by its top point: the
                    # highest vertex after turning goes on the hook
                    Rz = R('z', 45 if k == 0 else 35)
                    Mk = Rz @ S_(JERKY_SCALE) @ M
                    w = (Mk[:3, :3] @ p.T).T + Mk[:3, 3]
                    top = w[np.argmax(w[:, 1])]
                    Mk = T(hook - top + np.array([(k - 0.5) * 0.04 * S, 0, (k - 0.5) * 0.04 * S])) @ Mk
                o = {"kind": kind, "src": src, "m": colmajor(Mk)}
                if tex: o["tex"] = tex
                objs.append(o)
            rep.append(f"{src:28s} jerky scale {s:.2f} each {wdt:.2f} wide, {hi[1]-lo[1]:.2f} down")
    return objs, rep
