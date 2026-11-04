# Raw meat solid volume (vertical-ray parity on a 1 cm grid, at the item's own size, game space) and longest side.
import sys, json, numpy as np
sys.path.insert(0, '/root/rackview'); from anchor2 import mesh
import anchor2
def volume(n, step=0.01):
    V, F = mesh(n); anchor2.STEP = step
    P, _, hits = anchor2.columns(V, F)
    m = sum(sum(h[k] - h[k + 1] for k in range(0, len(h) - 1, 2)) for h in hits)
    return m * step * step, float((V.max(0) - V.min(0)).max())
if __name__ == '__main__':
    out = {}
    for n in sys.argv[1].split(','):
        v, L = volume(n); out[n] = [v, L]; print(f"{n:20s} volume {v*1000:7.2f} L   longest {L:.3f} m", flush=True)
    json.dump(out, open('/root/rackview/meat_sizes.json', 'w'), indent=0)
