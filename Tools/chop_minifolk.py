"""Chop a Minifolks Humans character (Without Outline) into a full per-class rig: every layer of a class comes
from the same pack frame, so the layers align by construction. Routing is by the pack's shared palette:
  skin (2 tones) → base body / front arm / back arm (normalised to our base's white + grey, eyes to our ink)
  cloth (blues, browns, dark) → shirt body / sleeves (recoloured in-game), hat above the face, pants below the hips
  steel (3 tones, white) → armour body / sleeves, helmet above the face (fixed colour, new mirrored renderers)
  what sticks out past the body box and the arms, plus pole wood → weapon (halberd, shield, bow, staff); magic glow and the hurt eye → dropped
Frames: our 25-frame layout (idle 4, walk 6, slash 7, hit 3, death 5) ← per-character pack rows.
Outputs every sheet in the rig's own layouts (body/arms/pants/hat/helmet/weapon 224x160, shirt/armour 224x480),
each cell padded with one alpha-1 pixel where the source slices a cell, so Unity's auto-slice matches.
Run: uv run --with pillow --with numpy python3 chop_minifolk.py <MiniName> <out>"""
import sys, os, re
import numpy as np
from PIL import Image

ROOT = "/Users/tuan/Documents/Game/Idle Merchant Guild Project"
ADV = ROOT + "/Idle Merchant Guild/Assets/Sprites/Adventurer/"
PACK = ROOT + "/Sprites/MinifolksHumans 2/Without Outline/"
C = 32
ANIMS = [("Idle", 4), ("Walk", 6), ("Slash", 7), ("Hit", 3), ("Death", 5)]
# our (col,row) → pack (col,row); pack rows: idle, walk, jump, attack…, hurt, death (per character)
def rows(idle, walk, attack, hurt, death):
    m = {}
    for i in range(4): m[(i, 0)] = (i, idle)
    for i in range(6): m[(i, 1)] = (i, walk)
    for i, c in enumerate(attack): m[(i, 2)] = c
    for i, c in enumerate(hurt): m[(i, 3)] = c
    for i, c in enumerate(death): m[(i, 4)] = c
    return m
FRAME_MAPS = {
    "MiniHalberdMan": rows(0, 1, [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (5, 3)], [(0, 4), (1, 4), (2, 4)], [(0, 5), (1, 5), (2, 5), (3, 5), (4, 5)]),
    "MiniSwordMan":   rows(0, 1, [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (5, 3)], [(0, 4), (1, 4), (2, 4)], [(0, 5), (1, 5), (2, 5), (3, 5), (3, 5)]),
    "MiniShieldMan":  rows(0, 1, [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (5, 3), (5, 3)], [(0, 5), (1, 5), (2, 5)], [(0, 6), (1, 6), (2, 6), (3, 6), (3, 6)]),
    "MiniArcherMan":  rows(0, 1, [(0, 3), (2, 3), (4, 3), (5, 3), (6, 3), (8, 3), (10, 3)], [(0, 5), (1, 5), (2, 5)], [(0, 6), (1, 6), (2, 6), (3, 6), (3, 6)]),
    "MiniMage":       rows(0, 1, [(0, 3), (2, 3), (4, 3), (5, 3), (6, 3), (8, 3), (10, 3)], [(0, 6), (1, 6), (1, 6)], [(0, 7), (2, 7), (4, 7), (6, 7), (8, 7)]),
}
SKIN = {(248, 192, 144): (255, 255, 255), (200, 136, 80): (199, 199, 199)}
INK = (43, 41, 40); OUR_INK = (16, 20, 31)
STEEL = {(181, 214, 222), (140, 173, 198), (90, 123, 148), (255, 255, 255), (229, 233, 222), (174, 182, 161), (62, 59, 57), (78, 75, 73)}   # + the mage's beard greys: fixed colour
OTHERS = ["MiniHalberdMan", "MiniShieldMan", "MiniArcherMan", "MiniMage", "MiniSwordMan"]
DROP = {(144, 16, 0), (240, 213, 10), (224, 145, 40), (216, 248, 248), (152, 216, 248)}


def cell(im, cx, cy): return np.array(im.crop((cx * C, cy * C, cx * C + C, cy * C + C)))


def dilate(m, n=1):
    for _ in range(n): m = m | np.roll(m, 1, 0) | np.roll(m, -1, 0) | np.roll(m, 1, 1) | np.roll(m, -1, 1)
    return m


def rects_of(meta):
    s = open(meta).read()
    return [tuple(map(int, r)) for r in re.findall(r'      rect:\n        serializedVersion: 2\n        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+)', s)]


def pad(sheet, meta):
    px = sheet.load()
    for (x, y, w, h) in rects_of(meta):
        top = sheet.height - y - h
        if not np.array(sheet.crop((x, top, x + w, top + h)))[..., 3].any(): px[x, top + h - 1] = (0, 0, 0, 1)


def chop(name, out):
    fmap = FRAME_MAPS[name]
    L = lambda p: Image.open(ADV + p).convert("RGBA")
    ours = dict(af=L("Base/Arm Front/base arm front.png"), ab=L("Base/Arm Back/base arm back.png"), pants=L("Equipment/Pants/pants 1.png"))
    pack = Image.open(PACK + name + ".png").convert("RGBA")
    # the other characters share this base: pixels we have where none of them has anything are our weapon
    S = {k: Image.new("RGBA", (7 * C, 5 * C)) for k in ("body", "armf", "armb", "pants", "hat", "helmet", "weapon")}
    S["shirt"] = Image.new("RGBA", (7 * C, 15 * C)); S["armour"] = Image.new("RGBA", (7 * C, 15 * C))
    preview = Image.new("RGBA", (7 * C * 4, 5 * C * 4), (40, 40, 48, 255))
    ys, xs = np.mgrid[0:C, 0:C]
    for row, (anim, n) in enumerate(ANIMS):
        for f in range(n):
            oc = (f, row); T = cell(pack, *fmap[oc]).copy(); alpha = T[..., 3] > 0
            key = [[tuple(T[y, x, :3]) for x in range(C)] for y in range(C)]
            skin = np.array([[alpha[y, x] and key[y][x] in SKIN for x in range(C)] for y in range(C)])
            steel = np.array([[alpha[y, x] and key[y][x] in STEEL for x in range(C)] for y in range(C)])
            drop = np.array([[alpha[y, x] and key[y][x] in DROP for x in range(C)] for y in range(C)])
            ink = np.array([[alpha[y, x] and key[y][x] == INK for x in range(C)] for y in range(C)])
            keep = alpha & ~drop
            # face = the topmost skin cluster; its box anchors head / torso / legs and the body box
            sy, sx = np.nonzero(skin)
            if len(sy) == 0: continue
            face = skin & (ys <= sy.min() + 7)
            fy, fx = np.nonzero(face); face_bot = fy.max(); face_top = fy.min(); face_l, face_r = fx.min(), fx.max()
            shaft = alpha & (T[..., 0] == 98) & (T[..., 1] == 63) & (T[..., 2] == 42)   # pole / staff wood: only the halberd and the mage use it
            pan = cell(ours["pants"], *oc)[..., 3] > 0; pan_top = ys[pan].min() if pan.any() else face_bot + 8
            af = dilate(cell(ours["af"], *oc)[..., 3] > 0, 2); ab = dilate(cell(ours["ab"], *oc)[..., 3] > 0, 2)
            head = keep & (ys <= face_bot) & ~skin
            eyes = ink & dilate(face, 1) & (ys <= face_bot)
            # weapon = what sticks out past the body and the arms (shield, bow, staff, blade), plus pole wood anywhere
            outside = (xs < face_l - 2) | (xs > face_r + 2) | (ys < face_top - 6)
            weapon = (keep & ~skin & ~af & ~ab & outside) | (shaft & keep)
            cloth = keep & ~skin & ~steel & ~weapon
            metal = keep & steel & ~weapon
            # skin → body / arms, normalised to our base's tones; eyes to our ink
            body_px = np.zeros((C, C, 4), np.uint8)
            for (r, g, b), nc in SKIN.items():
                m = alpha & (T[..., 0] == r) & (T[..., 1] == g) & (T[..., 2] == b); body_px[m] = nc + (255,)
            body_px[eyes] = OUR_INK + (255,)
            skin_all = skin | eyes
            armf = skin_all & af & ~face; armb = skin_all & ab & ~face & ~armf; bod = skin_all & ~armf & ~armb
            def put(sheet, mask, cx, cy, src=T):
                px = np.zeros((C, C, 4), np.uint8); px[mask] = src[mask]; sheet.alpha_composite(Image.fromarray(px), (cx * C, cy * C))
            put(S["body"], bod, f, row, body_px); put(S["armf"], armf, f, row, body_px); put(S["armb"], armb, f, row, body_px)
            # cloth / metal by region: head → hat / helmet, arms → sleeves, legs → pants, else torso
            hat = cloth & head & ~af & ~ab; helmet = metal & head & ~af & ~ab
            legs = cloth & ~head & (ys >= pan_top) & ~af & ~ab
            c_f = cloth & ~head & ~legs & af; c_b = cloth & ~head & ~legs & ab & ~c_f; c_body = cloth & ~head & ~legs & ~c_f & ~c_b
            m_f = metal & ~head & af; m_b = metal & ~head & ab & ~m_f; m_body = metal & ~head & ~m_f & ~m_b
            put(S["hat"], hat, f, row); put(S["helmet"], helmet, f, row); put(S["pants"], legs, f, row); put(S["weapon"], weapon, f, row)
            put(S["shirt"], c_b, f, row * 3); put(S["shirt"], c_body, f, row * 3 + 1); put(S["shirt"], c_f, f, row * 3 + 2)
            put(S["armour"], m_b, f, row * 3); put(S["armour"], m_body, f, row * 3 + 1); put(S["armour"], m_f, f, row * 3 + 2)
            # preview in the rig's draw order: back arm, back sleeve, body, pants, shirt, hat, weapon, front arm, front sleeve
            comp = Image.new("RGBA", (C, C))
            for mask, src in ((armb, body_px), (c_b | m_b, T), (bod, body_px), (legs, T), (c_body | m_body, T), (hat | helmet, T), (weapon, T), (armf, body_px), (c_f | m_f, T)):
                px = np.zeros((C, C, 4), np.uint8); px[mask] = src[mask]; comp.alpha_composite(Image.fromarray(px))
            preview.paste(comp.resize((C * 4, C * 4), Image.NEAREST), (f * C * 4, row * C * 4))
    os.makedirs(out, exist_ok=True)
    metas = dict(body="Base/Body/base body.png.meta", armf="Base/Arm Front/base arm front.png.meta", armb="Base/Arm Back/base arm back.png.meta",
                 pants="Equipment/Pants/pants 1.png.meta", hat="Hair/Hair Buzz.png.meta", helmet="Hair/Hair Buzz.png.meta",
                 weapon="Weapon/Basic Sword.png.meta", shirt="Equipment/Shirt/Tunic.png.meta", armour="Equipment/Shirt/Tunic.png.meta")
    for k, im in S.items():
        pad(im, ADV + metas[k]); im.save(os.path.join(out, k + ".png"))
    preview.save(os.path.join(out, "preview.png"))


if __name__ == "__main__":
    chop(sys.argv[1], sys.argv[2])
