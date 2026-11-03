# Final jerky colour targets: measured targets (targets.py) plus the user's calls of 2026-11-02:
# Asksvin from the meat part of the cooked tail, and small deliberate nudges so jerkies sharing a shape read apart.
import json, colorsys, numpy as np
t = json.load(open('targets.json'))
KS, KV = 0.877, 0.764
def jerky_of(c):
    h, s, v = colorsys.rgb_to_hsv(*[x / 255 for x in c]); return [round(x * 255) for x in colorsys.hsv_to_rgb(h, min(1, s * KS), v * KV)]
t['asksvin'] = jerky_of((75, 53, 38))  # meat part (hue 0-50) of asksvintailcooked_d
# Nudges, in HSV: (hue degrees, saturation, value) replacing the measured ones, with the reason
nudge = {
 'necktail': (38, None, None, 'more yellow-olive (raw neck tail is greenish)'),
 'volture':  (8, 0.55, 0.31, 'redder (raw volture is pink-red)'),
 'lox':      (4, 0.42, None, 'pinker (raw lox is pink-red)'),
 'horse':    (2, 0.60, None, 'a little redder, apart from Asksvin'),
 'chicken':  (None, 0.30, 0.42, 'paler (poultry)'),
 'hare':     (8, 0.30, 0.42, 'paler and pinker (raw hare is pink)'),
}
final = {}
for k, c in t.items():
    h, s, v = colorsys.rgb_to_hsv(*[x / 255 for x in c])
    if k in nudge:
        nh, ns, nv, why = nudge[k]
        h = nh / 360 if nh is not None else h; s = ns if ns is not None else s; v = nv if nv is not None else v
    final[k] = [round(x * 255) for x in colorsys.hsv_to_rgb(h, s, v)]
json.dump(final, open('targets_final.json', 'w'))
def lab(c):
    c = np.array(c) / 255; c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    X, Y, Z = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]]) @ c
    f = lambda x: x ** (1 / 3) if x > 0.008856 else 7.787 * x + 16 / 116
    X, Y, Z = X / 0.9505, Y, Z / 1.089
    return np.array([116 * f(Y) - 16, 500 * (f(X) - f(Y)), 200 * (f(Y) - f(Z))])
groups = {'crossed': ['deer', 'horse', 'asksvin', 'hare'], 'fanned': ['bloodgill', 'chicken', 'volture', 'necktail'],
          'slabs': ['lox', 'grizzly', 'moose', 'seeker'], 'ribbons': ['serpent', 'bonemaw']}
for g, ks in groups.items():
    for name, tab in [('measured', t), ('final', final)]:
        pairs = [(a, b, np.linalg.norm(lab(tab[a]) - lab(tab[b]))) for i, a in enumerate(ks) for b in ks[i + 1:]]
        worst = min(pairs, key=lambda p: p[2])
        print(f"{g:8s} {name:8s} closest pair {worst[0]}-{worst[1]} dE {worst[2]:.1f}")
print(json.dumps(final))
