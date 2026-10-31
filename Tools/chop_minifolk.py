"""Chop a Minifolks Humans character into a full per-class rig: every layer of a class comes from the same pack
frame, so the layers align by construction. Source = the pack's Aseprite files (`/Sprites/minifolks human`),
exported per layer by the Aseprite CLI on first run: `main` + `hands` are routed (no outline — the game's
render feature draws one); every other layer (`slash` trails, the mage's `Layer 1` staff orb) is an effect, laid
over the weapon sheet as-is (fixed colour, swings with the weapon). Frames picked by the file's tags.
Routing is by the pack's shared palette:
  skin (2 tones) → base body / front arm / back arm (normalised to our base's white + grey, eyes to our ink)
  cloth (blues, browns, dark) → shirt body / sleeves (recoloured in-game), hat above the face, pants below the hips
  steel (3 tones, white, beard greys) → armour body / sleeves, helmet above the face (fixed colour, mirrored renderers)
  what sticks out past the body box and the arms, plus pole wood, plus hands-layer blobs that reach past the body or
  are big (the held staff / bow / shield — the hands layer carries what the hands hold) → weapon; the hurt eye → dropped
Frames: our 25-frame layout (idle 4, walk 6, slash 7, hit 3, death 5) ← tags idle / walk / attack / damage|hit /
death, subsampled evenly when the tag has more frames.
Outputs every sheet in the rig's own layouts (body/arms/pants/hat/helmet/weapon 224x160, shirt/armour 224x480),
each cell padded with one alpha-1 pixel where the source slices a cell, so Unity's auto-slice matches.
Run: uv run --with pillow --with numpy python3 chop_minifolk.py <MiniName> <out>"""
import sys, os, re, json, subprocess
import numpy as np
from PIL import Image

ROOT = "/Users/tuan/Documents/Game/Idle Merchant Guild Project"
ADV = ROOT + "/Idle Merchant Guild/Assets/Sprites/Adventurer/"
ASE_DIR = ROOT + "/Sprites/minifolks human/"
ASE_EXPORT = ASE_DIR + "export/"
ASEPRITE = "/Applications/Aseprite.app/Contents/MacOS/aseprite"
C = 32
ANIMS = [("Idle", 4), ("Walk", 6), ("Slash", 7), ("Hit", 3), ("Death", 5)]
TAG_FOR = {"Idle": ("idle",), "Walk": ("walk",), "Slash": ("attack",), "Hit": ("damage", "hit"), "Death": ("death",)}
USE_LAYERS = ("main", "hands")
NOT_FX = USE_LAYERS + ("outline",)
SKIN = {(248, 192, 144): (255, 255, 255), (200, 136, 80): (199, 199, 199)}
INK = (43, 41, 40); OUR_INK = (16, 20, 31)
STEEL = {(181, 214, 222), (140, 173, 198), (90, 123, 148), (255, 255, 255), (229, 233, 222), (174, 182, 161), (62, 59, 57), (78, 75, 73), (152, 216, 248)}   # steel, beard greys, staff crystal: fixed colour
# Recoloured layers (shirt, hat) go through the Outfit shader: desaturate, then blend the palette colour in, so dark
# source cloth comes out near-black whatever the roll. Packs with dark cloth lift those tones to light greys there
# (pants / cloak / weapon keep the source colours).
CLOTH_TONES = {"MiniThief": {(176, 112, 64): (235, 235, 235), (112, 80, 48): (165, 165, 165), (111, 47, 39): (200, 200, 200)}}
DROP = {(144, 16, 0)}                                                           # the hurt-face red eye


def cell(im, cx, cy): return np.array(im.crop((cx * C, cy * C, cx * C + C, cy * C + C)))


def dilate(m, n=1):
    for _ in range(n): m = m | np.roll(m, 1, 0) | np.roll(m, -1, 0) | np.roll(m, 1, 1) | np.roll(m, -1, 1)
    return m


def components(mask):
    """8-connected blobs of a bool mask, each as its own mask."""
    seen = np.zeros_like(mask); out = []
    for y, x in zip(*np.nonzero(mask)):
        if seen[y, x]: continue
        comp = np.zeros_like(mask); stack = [(y, x)]; seen[y, x] = True
        while stack:
            cy, cx = stack.pop(); comp[cy, cx] = True
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < mask.shape[0] and 0 <= nx < mask.shape[1] and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True; stack.append((ny, nx))
        out.append(comp)
    return out


def rects_of(meta):
    s = open(meta).read()
    return [tuple(map(int, r)) for r in re.findall(r'      rect:\n        serializedVersion: 2\n        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+)', s)]


def pad(sheet, meta):
    px = sheet.load()
    for (x, y, w, h) in rects_of(meta):
        top = sheet.height - y - h
        if not np.array(sheet.crop((x, top, x + w, top + h)))[..., 3].any(): px[x, top + h - 1] = (0, 0, 0, 1)


def load_ase(name):
    """Export the .aseprite per layer (cached) and return (frames: {(layer, index): 32x32 RGBA array}, tags: {name: (from, to)})."""
    os.makedirs(ASE_EXPORT, exist_ok=True)
    src = name if name.endswith(".aseprite") else ASE_DIR + name + ".aseprite"   # a pack name, or any .aseprite path (Rogue = MiniThief from the Villagers pack)
    name = os.path.basename(src)[:-len(".aseprite")]
    png, js = ASE_EXPORT + name + ".png", ASE_EXPORT + name + ".json"
    if not os.path.exists(js) or os.path.getmtime(js) < os.path.getmtime(src):
        subprocess.run([ASEPRITE, "-b", "--split-layers", "--all-layers", src, "--sheet", png, "--sheet-type", "packed", "--sheet-pack",
                        "--data", js, "--format", "json-array", "--list-tags", "--list-layers"], check=True, capture_output=True)
    d = json.load(open(js)); sheet = Image.open(png).convert("RGBA")
    frames = {}
    for fr in d["frames"]:
        m = re.match(r".* \((.+)\) (\d+)\.aseprite$", fr["filename"]); layer, idx = m.group(1), int(m.group(2))
        r = fr["frame"]; s = fr["spriteSourceSize"]
        canvas = Image.new("RGBA", (C, C)); canvas.paste(sheet.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"])), (s["x"], s["y"]))
        frames[(layer, idx)] = np.array(canvas)
    tags = {t["name"]: (t["from"], t["to"]) for t in d["meta"]["frameTags"]}
    return frames, tags


def frame_map(tags):
    """our (col,row) → pack frame index, picking each tag's frames evenly when it has more than we need."""
    m = {}
    for row, (anim, n) in enumerate(ANIMS):
        tag = next(t for t in TAG_FOR[anim] if t in tags); a, b = tags[tag]; count = b - a + 1
        picks = [a + int(round(i * (count - 1) / max(n - 1, 1))) for i in range(n)] if count >= n else [a + min(i, count - 1) for i in range(n)]
        for f, idx in enumerate(picks): m[(f, row)] = idx
    return m


def composite(frames, idx):
    """main + hands for one frame (hands on top), plus the hands mask."""
    T = frames[("main", idx)].copy(); H = frames[("hands", idx)]
    hm = H[..., 3] > 0; T[hm] = H[hm]
    return T, hm


def chop(name, out):
    frames, tags = load_ase(name); fmap = frame_map(tags)
    tones = CLOTH_TONES.get(os.path.basename(name).removesuffix(".aseprite"), {})
    L = lambda p: Image.open(ADV + p).convert("RGBA")
    ours = dict(af=L("Base/Arm Front/base arm front.png"), ab=L("Base/Arm Back/base arm back.png"), pants=L("Equipment/Pants/pants 1.png"))
    S = {k: Image.new("RGBA", (7 * C, 5 * C)) for k in ("body", "armf", "armb", "pants", "hat", "helmet", "weapon")}
    S["shirt"] = Image.new("RGBA", (7 * C, 15 * C)); S["armour"] = Image.new("RGBA", (7 * C, 15 * C))
    preview = Image.new("RGBA", (7 * C * 4, 5 * C * 4), (40, 40, 48, 255))
    ys, xs = np.mgrid[0:C, 0:C]
    for row, (anim, n) in enumerate(ANIMS):
        for f in range(n):
            oc = (f, row); T, hands = composite(frames, fmap[oc]); alpha = T[..., 3] > 0
            key = [[tuple(T[y, x, :3]) for x in range(C)] for y in range(C)]
            skin = np.array([[alpha[y, x] and key[y][x] in SKIN for x in range(C)] for y in range(C)])
            steel = np.array([[alpha[y, x] and key[y][x] in STEEL for x in range(C)] for y in range(C)])
            drop = np.array([[alpha[y, x] and key[y][x] in DROP for x in range(C)] for y in range(C)])
            ink = np.array([[alpha[y, x] and key[y][x] == INK for x in range(C)] for y in range(C)])
            keep = alpha & ~drop
            # face = the topmost skin cluster that isn't a hand; its box anchors head / torso / legs and the body box
            fskin = skin & ~hands
            sy, sx = np.nonzero(fskin)
            if len(sy) > 0:
                face = fskin & (ys <= sy.min() + 7)
                fy, fx = np.nonzero(face); face_bot = fy.max(); face_top = fy.min(); face_l, face_r = fx.min(), fx.max()
            else:                                                               # face-down death frames: no skin showing → no head region, body box from the silhouette
                face = np.zeros((C, C), bool); ky, kx = np.nonzero(keep)
                face_bot = -1; face_top = ky.min(); face_l, face_r = (kx.min() + kx.max()) // 2 - 6, (kx.min() + kx.max()) // 2 + 6
            shaft = alpha & (T[..., 0] == 98) & (T[..., 1] == 63) & (T[..., 2] == 42)   # pole / staff wood: only the halberd and the mage use it
            pan = cell(ours["pants"], *oc)[..., 3] > 0; pan_top = ys[pan].min() if pan.any() else face_bot + 8
            af = dilate(cell(ours["af"], *oc)[..., 3] > 0, 2); ab = dilate(cell(ours["ab"], *oc)[..., 3] > 0, 2)
            head = keep & (ys <= face_bot) & ~skin
            eyes = ink & dilate(face, 1) & (ys <= face_bot)
            # weapon = what sticks out past the body and the arms (shield, bow, staff, blade), plus pole wood anywhere
            outside = (xs < face_l - 2) | (xs > face_r + 2) | (ys < face_top - 6)
            # the pack's hands layer is hands + whatever they hold (the mage's staff sits entirely in it); a hands-layer
            # blob that reaches past the body box or is big is a held thing, a small one is just a hand + cuff (stays cloth)
            held = np.zeros((C, C), bool)
            for comp in components(hands):
                if (comp & outside).any() or comp.sum() >= 12: held |= comp
            weapon = (keep & ~skin & ~af & ~ab & ~hands & outside) | (shaft & keep) | (held & keep & ~skin)
            cloth = keep & ~skin & ~steel & ~weapon
            metal = keep & steel & ~weapon
            # skin → body / arms, normalised to our base's tones; eyes to our ink. The pack's hands layer is the
            # authority for what's a hand: a hand off our arm masks still goes to the front arm.
            body_px = np.zeros((C, C, 4), np.uint8)
            for (r, g, b), nc in SKIN.items():
                m = alpha & (T[..., 0] == r) & (T[..., 1] == g) & (T[..., 2] == b); body_px[m] = nc + (255,)
            body_px[eyes] = OUR_INK + (255,)
            skin_all = skin | eyes
            armf = skin_all & ~face & (af | (hands & ~ab)); armb = skin_all & ab & ~face & ~armf; bod = skin_all & ~armf & ~armb
            def put(sheet, mask, cx, cy, src=T):
                px = np.zeros((C, C, 4), np.uint8); px[mask] = src[mask]; sheet.alpha_composite(Image.fromarray(px), (cx * C, cy * C))
            put(S["body"], bod, f, row, body_px); put(S["armf"], armf, f, row, body_px); put(S["armb"], armb, f, row, body_px)
            # cloth / metal by priority so every kept pixel lands somewhere: arms → sleeves, head → hat / helmet,
            # legs → pants, else torso
            c_f = cloth & af; c_b = cloth & ab & ~c_f; rest = cloth & ~c_f & ~c_b
            hat = rest & head; legs = rest & ~head & (ys >= pan_top); c_body = rest & ~hat & ~legs
            m_f = metal & af; m_b = metal & ab & ~m_f; mrest = metal & ~m_f & ~m_b
            helmet = mrest & head; m_body = mrest & ~helmet
            routed = skin_all | weapon | c_f | c_b | hat | legs | c_body | m_f | m_b | helmet | m_body
            lost = keep & ~routed
            if lost.any(): print(f"  {name} frame {oc}: {int(lost.sum())} kept pixels routed nowhere")
            Tc = T.copy()
            for (r, g, b), nc in tones.items(): Tc[(T[..., 0] == r) & (T[..., 1] == g) & (T[..., 2] == b), :3] = nc
            put(S["hat"], hat, f, row, Tc); put(S["helmet"], helmet, f, row); put(S["pants"], legs, f, row); put(S["weapon"], weapon, f, row)
            for (layer, i), px in frames.items():   # effects ride on top of the weapon
                if i == fmap[oc] and layer not in NOT_FX and px[..., 3].any():
                    S["weapon"].alpha_composite(Image.fromarray(px), (f * C, row * C))
            put(S["shirt"], c_b, f, row * 3, Tc); put(S["shirt"], c_body, f, row * 3 + 1, Tc); put(S["shirt"], c_f, f, row * 3 + 2, Tc)
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
    print(f"{name}: tags {dict((a, tags[a]) for a in tags)} → frames {sorted(set(fmap.values()))}")


if __name__ == "__main__":
    chop(sys.argv[1], sys.argv[2])
