"""Chop a Minifolks Humans character into the adventurer rig's layers.
The game's base body is the Minifolks base, so for each of our 25 body frames we take the matching pack frame,
drop its skin (our arms/body supply that), and route every other pixel by our own per-frame masks:
  on the front arm → front sleeve · on the back arm → back sleeve · at or below the pants' top row → pants
  within the tunic's rows → shirt body · above → hat (lives in the hair-top slot) · far outside the body → dropped (weapon)
Outputs the sheets in the rig's layouts (shirt 224x480 tunic layout, pants 224x160, hat 224x160) plus a
reassembled preview. Run: uv run --with pillow --with numpy python3 chop_minifolk.py <MiniName> <out>"""
import sys, os
import numpy as np
from PIL import Image

ROOT = "/Users/tuan/Documents/Game/Idle Merchant Guild Project"
ADV = ROOT + "/Idle Merchant Guild/Assets/Sprites/Adventurer/"
PACK = ROOT + "/Sprites/MinifolksHumans 2/Outline/"
C = 32
# our body sheet: row = animation, col = frame; (cols, frames)
ANIMS = [("Idle", 4), ("Walk", 6), ("Slash", 7), ("Hit", 3), ("Death", 5)]
# our (col,row) → pack (col,row) for the SwordMan-style 6-column layout (idle, walk, block, attack, hurt, death)
FRAME_MAP = {
    (0, 0): (0, 0), (1, 0): (1, 0), (2, 0): (2, 0), (3, 0): (3, 0),
    (0, 1): (0, 1), (1, 1): (1, 1), (2, 1): (2, 1), (3, 1): (3, 1), (4, 1): (4, 1), (5, 1): (5, 1),
    (0, 2): (5, 3), (1, 2): (0, 3), (2, 2): (1, 3), (3, 2): (2, 3), (4, 2): (3, 3), (5, 2): (4, 3), (6, 2): (5, 3),
    (0, 3): (0, 0), (1, 3): (0, 4), (2, 3): (1, 4),
    (0, 4): (0, 0), (1, 4): (0, 5), (2, 4): (1, 5), (3, 4): (2, 5), (4, 4): (3, 5),
}


def cell(im, cx, cy): return np.array(im.crop((cx * C, cy * C, cx * C + C, cy * C + C)))


def dilate(m, n=1):
    for _ in range(n): m = m | np.roll(m, 1, 0) | np.roll(m, -1, 0) | np.roll(m, 1, 1) | np.roll(m, -1, 1)
    return m


def load():
    L = lambda p: Image.open(ADV + p).convert("RGBA")
    return dict(body=L("Base/Body/base body.png"), af=L("Base/Arm Front/base arm front.png"), ab=L("Base/Arm Back/base arm back.png"),
                pants=L("Equipment/Pants/pants 1.png"), tunic=L("Equipment/Shirt/Tunic.png"))


def is_skin(rgb):
    r, g, b = rgb[..., 0].astype(int), rgb[..., 1].astype(int), rgb[..., 2].astype(int)
    return (r > 170) & (g > 110) & (g < 215) & (b > 70) & (b < 185) & (r > g) & (g > b)


def chop(name, out, frame_map=FRAME_MAP):
    S = load(); pack = Image.open(PACK + name + ".png").convert("RGBA")
    shirt = Image.new("RGBA", (7 * C, 15 * C)); pants = Image.new("RGBA", (7 * C, 5 * C)); hat = Image.new("RGBA", (7 * C, 5 * C))
    preview = Image.new("RGBA", (7 * C * 4, 5 * C * 4), (40, 40, 48, 255))
    for row, (anim, n) in enumerate(ANIMS):
        for f in range(n):
            oc = (f, row); tc = frame_map[oc]
            T = cell(pack, *tc); talpha = T[..., 3] > 0
            body = cell(S["body"], *oc)[..., 3] > 0; af = cell(S["af"], *oc)[..., 3] > 0; ab = cell(S["ab"], *oc)[..., 3] > 0
            tun = cell(S["tunic"], f, row * 3 + 1)[..., 3] > 0; pan = cell(S["pants"], *oc)[..., 3] > 0
            whole = dilate(body | af | ab, 2)
            keep = talpha & ~is_skin(T[..., :3]) & whole
            ys, xs = np.mgrid[0:C, 0:C]
            tun_top = ys[tun].min() if tun.any() else 24
            pan_top = ys[pan].min() if pan.any() else 29
            rgb = T[..., :3].astype(int); mx = rgb.max(axis=2); mn = rgb.min(axis=2)
            steel = (mx > 150) & (mx - mn < 40)                                  # bright, unsaturated: blades
            keep &= ~(steel & (ys >= tun_top - 1))                              # weapon pixels below the head go (we have our own)
            keep &= ~((rgb[..., 0] > 150) & (rgb[..., 1] < 90) & (rgb[..., 2] < 90))                      # the pack's hurt-face red eye
            keep &= ~((rgb[..., 0] > 190) & (rgb[..., 1] > 150) & (rgb[..., 2] < 110))                    # staff orbs / gold weapon bits
            front = keep & dilate(af, 1) & ~body
            back = keep & dilate(ab, 1) & ~body & ~front
            rest = keep & ~front & ~back
            legs = rest & (ys >= pan_top)
            torso = rest & ~legs & (ys >= tun_top - 1)
            head = rest & ~legs & ~torso
            def put(sheet, mask, cx, cy):
                px = np.zeros((C, C, 4), np.uint8); px[mask] = T[mask]
                sheet.paste(Image.fromarray(px), (cx * C, cy * C))
            put(shirt, back, f, row * 3); put(shirt, torso, f, row * 3 + 1); put(shirt, front, f, row * 3 + 2)
            put(pants, legs, f, row); put(hat, head, f, row)
            # reassembled preview in the rig's draw order
            comp = Image.new("RGBA", (C, C))
            for layer in (cell(S["ab"], *oc), np.where(back[..., None], T, 0), cell(S["body"], *oc), np.where(legs[..., None], T, 0),
                          np.where(torso[..., None], T, 0), np.where(head[..., None], T, 0), cell(S["af"], *oc), np.where(front[..., None], T, 0)):
                comp.alpha_composite(Image.fromarray(np.asarray(layer, np.uint8)))
            preview.paste(comp.resize((C * 4, C * 4), Image.NEAREST), (f * C * 4, row * C * 4))
    # pad: every cell the source sheets slice must stay non-empty so Unity's auto-slice yields the same sprite set
    def pad(sheet, meta):
        import re
        px = sheet.load(); m = open(meta).read()
        for r in re.finditer(r'      rect:\n        serializedVersion: 2\n        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+)', m):
            x, y, w, h = map(int, r.groups()); top = sheet.height - y - h       # meta rects are bottom-up
            if not np.array(sheet.crop((x, top, x + w, top + h)))[..., 3].any(): px[x, top + h - 1] = (0, 0, 0, 1)
    pad(shirt, ADV + "Equipment/Shirt/Tunic.png.meta"); pad(pants, ADV + "Equipment/Pants/pants 1.png.meta"); pad(hat, ADV + "Hair/Hair Buzz.png.meta")
    os.makedirs(out, exist_ok=True)
    shirt.save(os.path.join(out, "shirt.png")); pants.save(os.path.join(out, "pants.png")); hat.save(os.path.join(out, "hat.png"))
    preview.save(os.path.join(out, "preview.png"))
    return preview


if __name__ == "__main__":
    chop(sys.argv[1], sys.argv[2])
