# Auðhumla's Lick (Valheim, PotatoFarmer): warms the grey stone a little toward red-brown and leaves the pale salt as it is.
# python3 -I wip/lick_tone.py wip/raw/lick_d_untoned.png assets/lick_d.png
import sys
import numpy as np
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGBA")
a = np.asarray(im).astype(float) / 255.0
rgb = a[..., :3]
lum = rgb @ np.array([0.299, 0.587, 0.114])
sat = rgb.max(-1) - rgb.min(-1)
salt = np.clip((lum - 0.55) / 0.12, 0, 1) * np.clip((0.2 - sat) / 0.1, 0, 1)   # bright and colourless
warm = rgb * np.array([1.10, 1.0, 0.86])
out = rgb * salt[..., None] + warm * (1 - salt[..., None])
a[..., :3] = np.clip(out, 0, 1)
Image.fromarray((a * 255).round().astype(np.uint8), "RGBA").save(sys.argv[2])
print("salt texels:", round(float((salt > 0.5).mean()), 3))
