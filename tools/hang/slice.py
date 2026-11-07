import json, struct, numpy as np, sys
from PIL import Image, ImageDraw
d = open('hang/laradr_rack.mesh', 'rb').read(); nv, ni = struct.unpack('<ii', d[4:12]); o = 12
P = np.frombuffer(d, '<f4', nv * 3, o).reshape(-1, 3).astype(float); o += nv * 32
I = np.frombuffer(d, '<i4', ni, o).reshape(-1, 3)
H = json.load(open('hooks.json'))
def seg(x0):
    A, B, C = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]; out = []
    for a, b, c in zip(A, B, C):
        pts = []
        for p, q in ((a, b), (b, c), (c, a)):
            if (p[0] - x0) * (q[0] - x0) < 0:
                t = (x0 - p[0]) / (q[0] - p[0]); pts.append(p + t * (q - p))
        if len(pts) == 2: out.append(pts)
    return out
tiles = []
for i in range(9):
    h = H[i]; x0 = h['big']['cup_x']
    im = Image.new('RGB', (300, 300), 'white'); dr = ImageDraw.Draw(im)
    def px(z, y): return (150 + (z - h['z']) * 1500, 300 - (y - 0.80) * 1500)
    for s in seg(x0):
        if abs(s[0][2] - h['z']) < 0.12 and 0.79 < s[0][1] < 0.99: dr.line([px(s[0][2], s[0][1]), px(s[1][2], s[1][1])], fill='black', width=1)
    for w, col in (('big', 'red'), ('small', 'blue')):
        c = h[w]; x, y = px(c['cup_z'], c['cup_y']); dr.ellipse([x - 3, y - 3, x + 3, y + 3], fill=col)
        x, y = px(c['cup_z'], c['cup_y'] + 0.035); dr.ellipse([x - 3, y - 3, x + 3, y + 3], outline=col)
    for k in range(0, 300, 30): dr.line([(0, k), (5, k)], fill='gray')
    dr.text((4, 4), f"hook {i}  slice x={x0:.3f}  1 tick = 2 cm model", fill='black')
    tiles.append(im)
sheet = Image.new('RGB', (900, 900), 'white')
for k, t in enumerate(tiles): sheet.paste(t, ((k % 3) * 300, (k // 3) * 300))
sheet.save('/tmp/slices.png')
