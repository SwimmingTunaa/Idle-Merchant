"""Rank badges 2-5 (Bronze, Iron, Silver, Gold) in the hand of Sprites/Icons/ranks/Rank 1.png (Wood): 16x16, the
same shield (field cols 5-10 rows 3-11, where the UI lays the rank number), escalating frame like a rank ladder:
  bronze  Wood's silhouette in bronze
  iron    steel, a top spike, wings one feather taller
  silver  silver, spike, two-tier wings, a blue gem at the shield's tip
  gold    gold, three-point crown, big swept wings, a ruby at the tip
Field tones stay mid-dark so the cream rank numeral reads on every tier.
Run: uv run --with pillow python3 Tools/draw_rank_badges.py <out_dir> [--preview preview.png]"""
import os, sys
from PIL import Image

TIERS = {
    "Rank 2": ({  # bronze
        "F": (176, 92, 40), "H": (250, 206, 150), "L": (214, 140, 72), "D": (120, 62, 30),
    }, [
        "................",
        "................",
        ".......HH.......",
        ".....HHFFHH.....",
        "....LFFFFFFL....",
        "HH..LFFFFFFL..HH",
        ".LDDLFFFFFFLDDL.",
        ".DLLDFFFFFFDLLD.",
        "..DDLFFFFFFLDD..",
        "...DLFFFFFFLD...",
        "....DLFFFFLD....",
        ".....DLFFLD.....",
        ".......LL.......",
        "................",
        "................",
        "................",
    ]),
    "Rank 3": ({  # iron
        "F": (72, 80, 94), "H": (214, 222, 230), "L": (140, 152, 166), "D": (64, 68, 78),
    }, [
        "................",
        ".......LL.......",
        ".......HH.......",
        ".....HHFFHH.....",
        "HH..LFFFFFFL..HH",
        "DLDDLFFFFFFLDDLD",
        "HLLDLFFFFFFLDLLH",
        ".DLLDFFFFFFDLLD.",
        "..DDLFFFFFFLDD..",
        "...DLFFFFFFLD...",
        "....DLFFFFLD....",
        ".....DLFFLD.....",
        ".......LL.......",
        "................",
        "................",
        "................",
    ]),
    "Rank 4": ({  # silver
        "F": (74, 98, 136), "H": (236, 246, 255), "L": (168, 192, 222), "D": (96, 118, 152),
        "G": (70, 150, 230), "g": (170, 220, 255),
    }, [
        "................",
        ".......LL.......",
        "H......HH......H",
        "LH...HHFFHH...HL",
        "DLH.LFFFFFFL.HLD",
        "HDLDLFFFFFFLDLDH",
        "LLDDLFFFFFFLDDLL",
        ".DLLDFFFFFFDLLD.",
        "..DDLFFFFFFLDD..",
        "...DLFFFFFFLD...",
        "....DLFFFFLD....",
        ".....DLFFLD.....",
        "......DgGD......",
        ".......GG.......",
        "................",
        "................",
    ]),
    "Rank 5": ({  # gold
        "F": (150, 92, 20), "H": (255, 236, 140), "L": (240, 184, 40), "D": (168, 110, 10),
        "G": (200, 40, 60), "g": (255, 140, 150),
    }, [
        "....H..HH..H....",
        "H...L..LL..L...H",
        "LH..LLLHHLLL..HL",
        "DLH..HHFFHH..HLD",
        "HDLHLFFFFFFLHLDH",
        "LHDLLFFFFFFLLDHL",
        "DLLDLFFFFFFLDLLD",
        ".DDLDFFFFFFDLDD.",
        "..DDLFFFFFFLDD..",
        "...DLFFFFFFLD...",
        "....DLFFFFLD....",
        ".....DLFFLD.....",
        "......DgGD......",
        ".......GG.......",
        "................",
        "................",
    ]),
}


def draw(pal, grid):
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(grid):
        assert len(row) == 16, row
        for x, c in enumerate(row):
            if c != ".": im.putpixel((x, y), pal[c] + (255,))
    return im


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    ims = []
    for name, (pal, grid) in TIERS.items():
        im = draw(pal, grid); im.save(os.path.join(out, name + ".png")); ims.append(im)
    if "--preview" in sys.argv:
        wood = os.path.join(os.path.dirname(__file__), "..", "Assets", "Sprites", "Icons", "ranks", "Rank 1.png")
        ims.insert(0, Image.open(wood).convert("RGBA"))
        S, bgs = 10, [(250, 240, 220), (58, 55, 68)]
        sheet = Image.new("RGBA", (len(ims) * 18 * S, len(bgs) * 18 * S))
        for r, bg in enumerate(bgs):
            for i, im in enumerate(ims):
                tile = Image.new("RGBA", (18, 18), bg + (255,)); tile.alpha_composite(im, (1, 1))
                sheet.paste(tile.resize((18 * S, 18 * S), Image.NEAREST), (i * 18 * S, r * 18 * S))
        sheet.save(sys.argv[sys.argv.index("--preview") + 1])
