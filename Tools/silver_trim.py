"""Silver-tier trim for a class rig: moves trim pixels out of the recoloured shirt / hat sheets into the fixed-colour
armour / helmet sheets, painted silver (light or shade by the source pixel's lightness). Same layouts and cells as the
source sheets, so the class's .meta slices and library entries fit the outputs unchanged.
  hat brim  = the bottom-most hat pixel in each column
  robe hem  = the bottom-most shirt-body pixel in each column
  cuffs     = sleeve pixels touching the hand (the arm sheet under that sleeve)
Mage: hat brim + robe hem + cuffs. Rogue (fixed grey cloak + hood): hood lip (per column, the hood pixel right above the
topmost skin: one line across the brow) + cloak hem (bottom-most armour-body pixel per column) recoloured silver in place; no cuffs (a silver
sleeve end next to the dagger hand read as an uncoloured patch, as did a rim all round the face).
Run: uv run --with pillow --with numpy python3 Tools/silver_trim.py <Mage|Rogue> <class sheet prefix, e.g. …/Classes/Mage/Mage > <out dir>"""
import sys, os
import numpy as np
from PIL import Image

C = 32
SILVER = {True: (222, 228, 236), False: (150, 160, 178)}   # light / shade


def bottom_per_col(a):
    m = np.zeros_like(a)
    for x in range(a.shape[1]):
        ys = np.nonzero(a[:, x])[0]
        if len(ys): m[ys.max(), x] = True
    return m


def touching(a, b):
    return a & (b | np.roll(b, 1, 0) | np.roll(b, -1, 0) | np.roll(b, 1, 1) | np.roll(b, -1, 1))


def trim(cls, prefix, out):
    L = lambda n: np.array(Image.open(prefix + n + ".png").convert("RGBA"))
    shirt, hat, armour, helmet, armf, armb, body = L("Shirt"), L("Hat"), L("Armour"), L("Helmet"), L("Arm Front"), L("Arm Back"), L("Body")

    def paint(sheet, mask, y0, x0):   # silver in place (already a fixed-colour sheet)
        c = sheet[y0:y0 + C, x0:x0 + C]
        for y, x in zip(*np.nonzero(mask)): c[y, x] = SILVER[c[y, x, :3].mean() > 110] + (255,)

    def move(src, dst, mask, y0, x0):
        cs, cd = src[y0:y0 + C, x0:x0 + C], dst[y0:y0 + C, x0:x0 + C]
        for y, x in zip(*np.nonzero(mask)):
            cd[y, x] = SILVER[cs[y, x, :3].mean() > 200] + (255,)
        cs[mask] = 0

    for r in range(hat.shape[0] // C):
        for f in range(hat.shape[1] // C):
            x0 = f * C
            if cls == "Mage":
                h = hat[r * C:r * C + C, x0:x0 + C, 3] > 1
                if h.any(): move(hat, helmet, bottom_per_col(h), r * C, x0)
            else:
                hd = helmet[r * C:r * C + C, x0:x0 + C, 3] > 1
                sk = body[r * C:r * C + C, x0:x0 + C, 3] > 1
                top = np.zeros_like(sk); brow = np.nonzero(sk)[0].min() if sk.any() else 0
                for x in range(C):   # only columns whose skin starts at the brow line, not the cheeks down the face's sides
                    ys = np.nonzero(sk[:, x])[0]
                    if len(ys) and 0 < ys.min() <= brow + 1: top[ys.min() - 1, x] = True
                paint(helmet, hd & top, r * C, x0)
                y0 = (r * 3 + 1) * C; a = armour[y0:y0 + C, x0:x0 + C, 3] > 1
                if a.any(): paint(armour, bottom_per_col(a), y0, x0)
            for sub, arm in ((0, armb), (1, None), (2, armf)):
                if cls != "Mage": continue
                y0 = (r * 3 + sub) * C; s = shirt[y0:y0 + C, x0:x0 + C, 3] > 1
                if not s.any(): continue
                m = bottom_per_col(s) if arm is None else touching(s, arm[r * C:r * C + C, x0:x0 + C, 3] > 1)
                move(shirt, armour, m, y0, x0)
    os.makedirs(out, exist_ok=True)
    for n, a in (("Shirt", shirt), ("Hat", hat), ("Armour", armour), ("Helmet", helmet)):
        Image.fromarray(a).save(os.path.join(out, n + ".png"))


if __name__ == "__main__":
    trim(sys.argv[1], sys.argv[2], sys.argv[3])
