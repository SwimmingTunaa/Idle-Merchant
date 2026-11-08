"""Purse icons for the customer card (CustomerPanelController.PurseWord): 16x16 drawstring pouches in the book's
leather palette (box border / box bg / text-primary) with thought-bubble gold for coins.
  thin      a small pouch, one coin on the ground
  middling  a taller pouch, a stack of two
  fat       a tall fat pouch, two stacks in front
Run: uv run --with pillow python3 Tools/draw_purse_icons.py <out_dir> [--preview preview.png]"""
import os, sys
from PIL import Image

PAL = {
    "K": (20, 14, 10),      # ink
    "L": (179, 145, 111),   # leather (--color-box-border)
    "H": (215, 181, 148),   # leather highlight (--color-box-bg)
    "D": (128, 75, 24),     # leather shade (--color-text-primary)
    "W": (244, 210, 156),   # drawstring / neck cloth
    "Y": (240, 180, 41),    # coin
    "S": (168, 118, 0),     # coin shade
}

# pouch grows with the purse (7 → 11 → 15 px tall); coins stack on the ground in front of it
POUCHES = {
    "thin": [
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        ".....KKKKKK.....",
        "....KWWWWWWK....",
        ".....KWWWWK.....",
        "......KKKK......",
        "....KDLLLLHK....",
        "...KDLLLLLLHK...",
        "...KDLLLLLLLK...",
        "...KDDDLLLLLK...",
        "....KKKKKKKK....",
        "................",
    ],
    "middling": [
        "................",
        "................",
        ".....KKKKKK.....",
        "....KWWWWWWK....",
        ".....KWWWWK.....",
        "......KKKK......",
        "....KDLLLLHK....",
        "...KDLLLLLLHK...",
        "..KDLLLLLLLLK...",
        "..KDLLLLLLLLK...",
        "..KDDLLLLLLLK...",
        "..KDDDLLLLLLK...",
        "..KDDDDLLLLLK...",
        "..KDDDDLLLLLK...",
        "...KKKKKKKKKK...",
        "................",
    ],
    "fat": [
        ".....KKKKKK.....",
        "....KWWWWWWK....",
        ".....KWWWWK.....",
        "......KKKK......",
        "....KDLLLLHK....",
        "...KDLLLLLLHK...",
        "..KDLLLLLLLLHK..",
        ".KDLLLLLLLLLLHK.",
        ".KDLLLLLLLLLLLK.",
        ".KDLLLLLLLLLLLK.",
        ".KDDLLLLLLLLLLK.",
        ".KDDDLLLLLLLLLK.",
        ".KDDDDLLLLLLLLK.",
        ".KDDDDDLLLLLLLK.",
        "..KKKKKKKKKKKK..",
        "................",
    ],
}

# coin stacks drawn over the pouch's foot: (left x, bottom y, coins); each coin is a 5×2 slab
STACKS = {
    "thin": [(11, 14, 1)],
    "middling": [(10, 14, 2)],
    "fat": [(8, 14, 3), (11, 14, 2)],
}
COIN = ["KYYYK", "KSSSK"]


def purse(name):
    rows = POUCHES[name]
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), name
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0)); px = im.load()
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c != ".": px[x, y] = PAL[c] + (255,)
    for sx, sy, n in STACKS[name]:
        for i in range(n):
            for dy, crow in enumerate(COIN):
                for dx, c in enumerate(crow):
                    px[sx + dx, sy - (n - i) * 2 + 1 + dy] = PAL[c] + (255,)
    return im


if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    ims = {n: purse(n) for n in POUCHES}
    for n, im in ims.items(): im.save(os.path.join(out, f"purse_{n}.png"))
    if "--preview" in sys.argv:
        S = 8; strip = Image.new("RGBA", (len(ims) * (16 * S + 16) + 16, 16 * S + 32), (238, 225, 200, 255))
        for i, im in enumerate(ims.values()): strip.alpha_composite(im.resize((16 * S, 16 * S), Image.NEAREST), (16 + i * (16 * S + 16), 16))
        strip.save(sys.argv[sys.argv.index("--preview") + 1])
    print("wrote", ", ".join(f"purse_{n}.png" for n in ims))
