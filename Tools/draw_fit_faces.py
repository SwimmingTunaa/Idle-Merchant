"""Rank-fit faces for the Roster (RankFit states), 16x16 pixel emoji in the thought-bubble palette
(Sprites/UI/ThoughtBubble: ink outline, yellow fill, dark-gold shade, cream highlight).
  outmatched  ≥2 ranks short  scared: raised brows, wide eyes, open mouth, big sweat drop
  stretched   1 rank short    nervous: worried brows, grimace, small sweat drop
  fit         matching        content: smile
  easy        1 rank above    bored: half-lidded eyes, flat mouth
  slacking    ≥2 ranks above  asleep: closed eyes, small mouth, z z
Run: uv run --with pillow python3 Tools/draw_fit_faces.py <out_dir> [--preview preview.png]"""
import os, sys
from PIL import Image

PAL = {
    "K": (20, 14, 10),      # ink (bubbles' outline)
    "Y": (240, 180, 41),    # fill
    "S": (168, 118, 0),     # shade
    "H": (244, 210, 156),   # highlight
    "W": (255, 255, 255),
    "R": (216, 100, 60),    # mouth inside
    "B": (110, 190, 240),   # sweat
    "b": (60, 120, 200),    # sweat shade
}

BASE = [
    ".....KKKKKK.....",
    "...KKYYYYYYKK...",
    "..KYHHYYYYYYYK..",
    ".KYHHYYYYYYYYYK.",
    ".KYHYYYYYYYYYYK.",
    "KYYYYYYYYYYYYYYK",
    "KYYYYYYYYYYYYYYK",
    "KYYYYYYYYYYYYYYK",
    "KYYYYYYYYYYYYYYK",
    "KYYYYYYYYYYYYYYK",
    "KYYYYYYYYYYYYYSK",
    ".KYYYYYYYYYYYYSK",
    ".KYYYYYYYYYYYSK.",
    "..KYYYYYYYYYSSK.",
    "...KKSSSSSSSKK..",
    ".....KKKKKKK....",
]

# Feature overlays: (x, y, rows) — rows use the palette letters, '.' = leave the base pixel
FACES = {
    "outmatched": [
        (3, 4, ["KK....KK",          # brows raised + tilted
                ".......",]),
        (3, 6, ["WW....WW",          # wide eyes, pupils
                "WK....KW",]),
        (6, 9, ["KKKK",              # open mouth
                "KRRK",
                "KRRK",
                ".KK.",]),
        (12, 2, [".B.",              # sweat drop on the brow
                 "BBb",
                 "Bbb",
                 ".b.",]),
    ],
    "stretched": [
        (3, 3, ["..K....K..",        # worried brows: inner ends raised
                ".K......K.",]),
        (4, 6, ["K....K",            # dot eyes
                "K....K",]),
        (4, 10, ["KYKYKYK",          # grimace (zigzag teeth line)
                 "YKYKYKY",]),
        (13, 4, ["B",
                 "b",]),
    ],
    "fit": [
        (4, 6, ["K....K",
                "K....K",]),
        (4, 9, ["K.....K",           # smile
                ".K...K.",
                "..KKK..",]),
    ],
    "easy": [
        (3, 6, ["KKK..KKK",          # half-lidded
                ".KK...KK",]),
        (5, 10, ["KKKKK",]),          # flat mouth
    ],
    "slacking": [
        (3, 6, ["K..K.K..K",         # closed eyes (arcs)
                ".KK...KK.",]),
        (6, 10, ["KK",
                 "KK",]),            # small o (snore)
        (10, 2, ["KKK",              # z, inside the face's top-right
                 "..K",
                 ".K.",
                 "KKK",]),
    ],
}


def face(name):
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0)); px = im.load()
    for y, row in enumerate(BASE):
        for x, c in enumerate(row):
            if c != ".": px[x, y] = PAL[c] + (255,)
    for ox, oy, rows in FACES[name]:
        for dy, row in enumerate(rows):
            for dx, c in enumerate(row):
                if c != "." and 0 <= ox + dx < 16 and 0 <= oy + dy < 16: px[ox + dx, oy + dy] = PAL[c] + (255,)
    return im


if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    faces = {n: face(n) for n in FACES}
    for n, im in faces.items(): im.save(os.path.join(out, f"fit_{n}.png"))
    if "--preview" in sys.argv:
        S = 8; strip = Image.new("RGBA", (len(faces) * (16 * S + 16) + 16, 16 * S + 32), (226, 210, 173, 255))
        for i, im in enumerate(faces.values()): strip.alpha_composite(im.resize((16 * S, 16 * S), Image.NEAREST), (16 + i * (16 * S + 16), 16))
        strip.save(sys.argv[sys.argv.index("--preview") + 1])
    print("wrote", ", ".join(f"fit_{n}.png" for n in faces))
