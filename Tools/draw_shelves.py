"""Shelves upgrade art: the wall shelf prop (17x8: 3 px plank, brackets over a fading translucent wall shadow, 8 PPU, drawn in the interior sheet's bookcase palette without an outline) and the
20px Guild-card icon (upgrade-shelves.png: two planks with goods, in the queue-room icon's palette).
Run: uv run --with pillow python3 Tools/draw_shelves.py <sprites_dir> <icons_dir>"""
import os, sys
from PIL import Image

PAL = {
    # world plank: the interior sheet's bookcase (VP2_Decor_Bookcase) wood — no outline, like every decor sprite there
    "H": (137, 104, 58),    # corner peg highlight
    "h": (80, 48, 29),      # peg shade
    "L": (93, 70, 50),      # plank top
    "M": (75, 55, 38),      # plank face
    "D": (57, 39, 27),      # plank underside
    "P": (41, 32, 29),      # bracket
    "p": (29, 22, 20),      # bracket shadow
    # card icon: the queue-room icon's inked palette
    "K": (30, 26, 26),
    "W": (134, 88, 48),
    "I": (168, 118, 70),
    "d": (96, 60, 34),
    "R": (150, 40, 40),
    "r": (100, 26, 26),
    "G": (232, 196, 96),
    "B": (70, 110, 150),
}

# 17×6 at 8 PPU (fits the wall panels between the posts over the wander floor): 3 px plank + 3 px brackets
SHELF = [
    "HLLLLLLLLLLLLLLLH",
    "hMMMMMMMMMMMMMMMh",
    "DDDDDDDDDDDDDDDDD",
    "..PPp.......PPp..",
    "...Pp........Pp..",
    "....p.........p..",
]

ICON = [
    "....................",
    "....................",
    "...KKK..KK...KKK....",
    "...KRK.KGGK..KBK....",
    "...KRK.KGGK..KBK....",
    "..KKKKKKKKKKKKKKKK..",
    "..KIIIIIIIIIIIIIIK..",
    "..KWWWWWWWWWWWWWWK..",
    "..KKKKKKKKKKKKKKKK..",
    "...KdK........KdK...",
    "....K..........K....",
    "....KKK..KKK..KK....",
    "...KGGK..KRK.KBBK...",
    "...KGGK..KRK.KBBK...",
    "..KKKKKKKKKKKKKKKK..",
    "..KIIIIIIIIIIIIIIK..",
    "..KWWWWWWWWWWWWWWK..",
    "..KKKKKKKKKKKKKKKK..",
    "...KdK........KdK...",
    "....K..........K....",
]


def draw(rows):
    w, h = len(rows[0]), len(rows)
    assert all(len(r) == w for r in rows)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); px = im.load()
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c != ".": px[x, y] = PAL[c] + (255,)
    return im


# wall shadow under the plank: SHADOW_ROWS translucent rows of the bracket-shadow tone, fading and insetting downward,
# laid under the brackets. Sprite = 3 plank + 5 shadow rows; pivot y 5/8 keeps the plank's old centre (set on import).
SHADOW_ALPHA = (120, 90, 60, 35, 15)


def shelf():
    w = len(SHELF[0]); h = 3 + len(SHADOW_ALPHA)
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); px = im.load()
    for r, a in enumerate(SHADOW_ALPHA):
        for x in range(1 + r, w - 1 - r): px[x, 3 + r] = PAL["p"] + (a,)
    for y, row in enumerate(SHELF):
        for x, c in enumerate(row):
            if c != ".": px[x, y] = PAL[c] + (255,)
    return im


if __name__ == "__main__":
    sprites, icons = sys.argv[1], sys.argv[2]
    os.makedirs(sprites, exist_ok=True); os.makedirs(icons, exist_ok=True)
    shelf().save(os.path.join(sprites, "shelf_plank.png"))
    draw(ICON).save(os.path.join(icons, "upgrade-shelves.png"))
    S = 8
    prev = Image.new("RGBA", (17 * S + 20 * S + 48, 20 * S + 32), (120, 100, 85, 255))
    prev.alpha_composite(shelf().resize((17 * S, 8 * S), Image.NEAREST), (16, 16))
    prev.alpha_composite(draw(ICON).resize((20 * S, 20 * S), Image.NEAREST), (17 * S + 32, 16))
    prev.save(os.path.join(sprites, "_preview.png"))
    print("wrote shelf_plank.png, upgrade-shelves.png")
