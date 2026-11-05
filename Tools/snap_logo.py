"""Snap the AI-generated logo (Tools/logo_src.png, flat green background) onto its native pixel grid in the logo palette.

uv run --with pillow --with numpy python3 Tools/snap_logo.py Tools/logo_src.png "Assets/Sprites/UI/Main UI/logo.png"

The source draws each art pixel as a ~9 px block: every cell takes the median of its inner 5x5 (skips blended
block edges), background cells go transparent, the rest snap to the nearest palette colour. Result 83x73;
shown at 2x on the main-menu book cover.
"""
import sys
import numpy as np
from PIL import Image

CELL = 9.0
PAL = np.array([(40, 8, 12), (81, 37, 19),                     # outline, gold edge
                (160, 95, 32), (217, 145, 38), (247, 212, 85),  # gold dark / mid / light
                (99, 3, 16), (154, 12, 23), (184, 21, 28)])     # red dark / mid / light

src, out = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert('RGB')).astype(int)
bg = a[5, 5]
ys, xs = np.nonzero(np.abs(a - bg).sum(2) > 60)
x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
W, H = round((x1 - x0) / CELL), round((y1 - y0) / CELL)

px = np.zeros((H, W, 4), np.uint8)
for j in range(H):
    for i in range(W):
        cy = int(y0 + (j + .5) * (y1 - y0) / H); cx = int(x0 + (i + .5) * (x1 - x0) / W)
        c = np.median(a[cy - 2:cy + 3, cx - 2:cx + 3].reshape(-1, 3), 0)
        if np.abs(c - bg).sum() < 60: continue
        px[j, i, :3] = PAL[np.argmin(((PAL - c) ** 2).sum(1))]; px[j, i, 3] = 255
Image.fromarray(px, 'RGBA').save(out)
print(f'{W}x{H} -> {out}')
