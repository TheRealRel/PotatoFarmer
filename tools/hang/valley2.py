import json, numpy as np
exec(open('valley.py').read().split("out = []")[0])
out = []
for i, h in enumerate(H):
    row = {}
    for w in ('big', 'small'):
        c = h[w]; side = 1 if c['cup_z'] >= h['z'] else -1
        prof = []
        for dz in np.arange(0.0, 0.11, 0.002):
            z = h['z'] + side * dz; tops = []
            for x in (c['cup_x'] - 0.006, c['cup_x'], c['cup_x'] + 0.006):
                s = column(x, z); s = s[(s > 0.79) & (s < 1.0)]
                if len(s) < 2: continue
                bottom, top = s[0], s[1]; above = s[2] if len(s) > 2 else 9
                if top - bottom < 0.008 or above - top < 0.03: continue
                tops.append(top)
            prof.append((dz, np.median(tops) if tops else np.nan))
        prof = np.array(prof)
        valid = ~np.isnan(prof[:, 1])
        ymin = np.nanmin(prof[:, 1])
        floor = valid & (prof[:, 1] <= ymin + 0.012)
        # contiguous run containing the minimum
        k = int(np.nanargmin(prof[:, 1])); a = k; b = k
        while a - 1 >= 0 and floor[a - 1]: a -= 1
        while b + 1 < len(prof) and floor[b + 1]: b += 1
        mid = (prof[a, 0] + prof[b, 0]) / 2
        j = int(np.argmin(np.abs(prof[:, 0] - mid)))
        y = prof[j, 1] if not np.isnan(prof[j, 1]) else ymin
        row[w] = dict(x=round(c['cup_x'], 4), y=round(float(y), 4), z=round(float(h['z'] + side * mid), 4), span=round(float(prof[b, 0] - prof[a, 0]), 4), cup=[round(c['cup_x'],4), round(c['cup_y'],4), round(c['cup_z'],4)])
    out.append(row); print(i, row)
json.dump(out, open('valley.json', 'w'))
