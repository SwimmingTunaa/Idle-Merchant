"""Chop a Minifolks Humans character into a full per-class rig: every layer of a class comes from the same pack
frame, so the layers align by construction. Source = the pack's Aseprite files (`/Sprites/minifolks human`),
exported per layer by the Aseprite CLI on first run: `main` + `hands` are routed (no outline — the game's
render feature draws one); every other layer (`slash` trails, the mage's `Layer 1` staff orb) is an effect, laid
over the weapon sheet as-is (fixed colour, swings with the weapon). Frames picked by the file's tags.
Routing is by the pack's shared palette:
  skin (2 tones) → base body / front arm / back arm (normalised to our base's white + grey, eyes to our ink)
  cloth (blues, browns, dark) → shirt body / sleeves (recoloured in-game), hat above the face, pants below the hips
  steel (3 tones, white, beard greys) → armour body / sleeves, helmet above the face (fixed colour, mirrored renderers)
  BEARD packs (the mage): beard greys → a face sheet (body layout, lifted to hair tones, recoloured in-game); eyes → body only
  ROBE packs (the mage): robe blues never go to the weapon (unless they rim the orb crystal / an effect) and never to pants
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
TAG_FOR = {"Idle": ("idle",), "Walk": ("walk",), "Slash": ("attack",), "Hit": ("damage", "hit"), "Death": ("death",), "Jump": ("jump",)}
JUMP = ("Jump", 4)
# Packs whose jump row comes from another tag, at picked offsets into it: the lightning warrior's Spell 2 teleport
# (silhouette, streaks, bolt strike, re-form) plays across the Rogue pursuit dash
JUMP_PICKS = {"MiniLightningWarrior": ("spell 2", (2, 4, 8, 11))}   # --jump: an extra row on TOP of every sheet (keeps the existing slices' bottom-up rects), used by the Rogue pursuit dash
USE_LAYERS = ("main", "hands")
NOT_FX = USE_LAYERS + ("outline", "bg")   # layer names are lower-cased on load; the Elements pack's "Weapon" layer loads as "hands" (hand + what it holds, like the Humans pack's)
SKIN = {(248, 192, 144): (255, 255, 255), (200, 136, 80): (199, 199, 199)}
INK = (43, 41, 40); OUR_INK = (16, 20, 31)
STEEL = {(181, 214, 222), (140, 173, 198), (90, 123, 148), (255, 255, 255), (229, 233, 222), (174, 182, 161), (62, 59, 57), (78, 75, 73), (152, 216, 248)}   # steel, beard greys, staff crystal: fixed colour
# Recoloured layers (shirt, hat) go through the Outfit shader: desaturate, then blend the palette colour in, so dark
# source cloth comes out near-black whatever the roll. Packs with dark cloth lift those tones to light greys there
# (pants / cloak / weapon keep the source colours).
CLOTH_TONES = {"MiniThief": {(176, 112, 64): (235, 235, 235), (112, 80, 48): (165, 165, 165), (111, 47, 39): (200, 200, 200)},
               "MiniMage": {(96, 152, 232): (235, 235, 235), (64, 88, 192): (165, 165, 165)},
               "MiniArchMage": {(96, 152, 232): (235, 235, 235), (64, 88, 192): (165, 165, 165)},
               "MiniLightningWarrior": {(240, 213, 10): (235, 235, 235), (224, 145, 40): (165, 165, 165)},   # yellow vest + hair
               "MiniHalberdMan": {(96, 152, 232): (235, 235, 235), (64, 88, 192): (165, 165, 165)},
               "MiniShieldMan": {(96, 152, 232): (235, 235, 235), (64, 88, 192): (165, 165, 165)}}   # robe + hat: the tint multiplies source grey, so the dark blues showed palette colours at ~half brightness
# Robe classes: the robe is one garment. Its cloth never routes to the weapon (a swinging sleeve or the cuff at the
# staff hand reaches past the body, and the weapon sheet never recolours), and the hem below the hips stays on the
# shirt body instead of pants (pants roll their own palette, so a split robe came out two colours)
ROBE = {"MiniMage": {(64, 88, 192), (96, 152, 232)}, "MiniArchMage": {(64, 88, 192), (96, 152, 232)}}
# Extra fixed-colour tones per pack (→ armour / helmet like steel): the arch mage's gold trim
FIXED = {"MiniArchMage": {(247, 247, 87), (223, 199, 111)}, "MiniLightningWarrior": {(167, 71, 63), (111, 47, 39)}}   # + the lightning warrior's red scarf
CRYSTAL = {(152, 216, 248), (216, 248, 248), (223, 249, 249)}   # staff orb core: robe-blue touching it is the orb's rim (stays weapon)
# Beard → its own face sheet, recoloured in-game with the def's FaceColourPalette (natural hair colours). The pack's
# beard greys are dark, and the Outfit shader takes source lightness, so they're lifted onto our hair sheet's tones.
# Opt-in per pack: the thief's cloak + hood share these exact greys
BEARD = {"MiniMage": {(78, 75, 73): (199, 199, 199), (62, 59, 57): (131, 131, 131)},
         "MiniArchMage": {(229, 233, 222): (199, 199, 199), (174, 182, 161): (131, 131, 131)}}   # long hair + beard, same tones as the mage's beard → its colour roll carries over
# Hair packs (the hair-top layer is real hair, recoloured by the hair palette): hair-tone cloth touching the head's hat
# region joins it, so a lock hanging past the face line doesn't land on the shirt
HAIR = {"MiniLightningWarrior": {(240, 213, 10), (224, 145, 40)}}
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
        m = re.match(r".* \((.+)\) (\d+)\.aseprite$", fr["filename"]); layer, idx = m.group(1).lower(), int(m.group(2))
        if layer == "weapon": layer = "hands"
        r = fr["frame"]; s = fr["spriteSourceSize"]
        canvas = Image.new("RGBA", (C, C)); canvas.paste(sheet.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"])), (s["x"], s["y"]))
        frames[(layer, idx)] = np.array(canvas)
    tags = {t["name"].lower(): (t["from"], t["to"]) for t in d["meta"]["frameTags"]}
    return frames, tags


def frame_map(tags, anims=ANIMS, jump_picks=None):
    """our (col,row) → pack frame index, picking each tag's frames evenly when it has more than we need."""
    m = {}
    for row, (anim, n) in enumerate(anims):
        if anim == "Jump" and jump_picks:
            for f, off in enumerate(jump_picks[1]): m[(f, row)] = tags[jump_picks[0]][0] + off
            continue
        tag = next(t for t in TAG_FOR[anim] if t in tags); a, b = tags[tag]; count = b - a + 1
        picks = [a + int(round(i * (count - 1) / max(n - 1, 1))) for i in range(n)] if count >= n else [a + min(i, count - 1) for i in range(n)]
        for f, idx in enumerate(picks): m[(f, row)] = idx
    return m


def composite(frames, idx):
    """main + hands for one frame (hands on top), plus the hands mask."""
    empty = np.zeros((C, C, 4), np.uint8)
    T = frames.get(("main", idx), empty).copy(); H = frames.get(("hands", idx), empty)
    hm = H[..., 3] > 0; T[hm] = H[hm]
    return T, hm


def chop(name, out, jump=False):
    anims = ANIMS + [JUMP] if jump else ANIMS; R = len(anims)
    frames, tags = load_ase(name); jump_picks = JUMP_PICKS.get(os.path.basename(name).removesuffix(".aseprite")); fmap = frame_map(tags, anims, jump_picks)
    tones = CLOTH_TONES.get(os.path.basename(name).removesuffix(".aseprite"), {})
    robe_tones = ROBE.get(os.path.basename(name).removesuffix(".aseprite"), set())
    beard_tones = BEARD.get(os.path.basename(name).removesuffix(".aseprite"), {})
    hair_tones = HAIR.get(os.path.basename(name).removesuffix(".aseprite"), set())
    fixed = STEEL | FIXED.get(os.path.basename(name).removesuffix(".aseprite"), set())
    L = lambda p: Image.open(ADV + p).convert("RGBA")
    ours = dict(af=L("Base/Arm Front/base arm front.png"), ab=L("Base/Arm Back/base arm back.png"), pants=L("Equipment/Pants/pants 1.png"))
    S = {k: Image.new("RGBA", (7 * C, R * C)) for k in ("body", "armf", "armb", "pants", "hat", "helmet", "weapon") + (("face",) if beard_tones else ())}
    S["shirt"] = Image.new("RGBA", (7 * C, 3 * R * C)); S["armour"] = Image.new("RGBA", (7 * C, 3 * R * C))
    preview = Image.new("RGBA", (7 * C * 4, R * C * 4), (40, 40, 48, 255))
    ys, xs = np.mgrid[0:C, 0:C]
    for row, (anim, n) in enumerate(anims):
        for f in range(n):
            ref = (f, 1) if anim == "Jump" else (f, row)   # our arm / pants masks have no jump row: borrow the walk pose
            oc = (f, row); T, hands = composite(frames, fmap[oc]); alpha = T[..., 3] > 0
            if anim == "Jump" and jump_picks:   # a picked spell frame (lightning teleport) is one effect: the whole frame, fixed colour, on the weapon sheet
                for layer in [l for (l, i) in frames if i == fmap[oc] and l not in ("outline", "bg")]:
                    S["weapon"].alpha_composite(Image.fromarray(frames[(layer, fmap[oc])]), (f * C, row * C))
                continue
            key = [[tuple(T[y, x, :3]) for x in range(C)] for y in range(C)]
            skin = np.array([[alpha[y, x] and key[y][x] in SKIN for x in range(C)] for y in range(C)])
            steel = np.array([[alpha[y, x] and key[y][x] in fixed for x in range(C)] for y in range(C)])
            drop = np.array([[alpha[y, x] and key[y][x] in DROP for x in range(C)] for y in range(C)])
            ink = np.array([[alpha[y, x] and key[y][x] == INK for x in range(C)] for y in range(C)])
            robe = np.array([[alpha[y, x] and key[y][x] in robe_tones for x in range(C)] for y in range(C)])
            beard = np.array([[alpha[y, x] and key[y][x] in beard_tones for x in range(C)] for y in range(C)])
            crystal = np.array([[alpha[y, x] and key[y][x] in CRYSTAL for x in range(C)] for y in range(C)])
            keep = alpha & ~drop
            # face = the topmost skin cluster that isn't a hand; its box anchors head / torso / legs and the body box
            fskin = skin & ~hands
            sy, sx = np.nonzero(fskin)
            if len(sy) > 0:
                face = fskin & (ys <= sy.min() + 7)
                fy, fx = np.nonzero(face); face_bot = fy.max(); face_top = fy.min(); face_l, face_r = fx.min(), fx.max()
            else:                                                               # face-down death frames: no skin showing → no head region, body box from the silhouette
                face = np.zeros((C, C), bool); ky, kx = np.nonzero(keep | ~keep.any())   # (a frame that's all effect: whole cell)
                face_bot = -1; face_top = ky.min(); face_l, face_r = (kx.min() + kx.max()) // 2 - 6, (kx.min() + kx.max()) // 2 + 6
            shaft = alpha & (T[..., 0] == 98) & (T[..., 1] == 63) & (T[..., 2] == 42)   # pole / staff wood: only the halberd and the mage use it
            pan = cell(ours["pants"], *ref)[..., 3] > 0; pan_top = ys[pan].min() if pan.any() else face_bot + 8
            af = dilate(cell(ours["af"], *ref)[..., 3] > 0, 2); ab = dilate(cell(ours["ab"], *ref)[..., 3] > 0, 2)
            head = keep & (ys <= face_bot) & ~skin
            eyes = ink & dilate(face, 1) & (ys <= face_bot)
            # weapon = what sticks out past the body and the arms (shield, bow, staff, blade), plus pole wood anywhere
            outside = (xs < face_l - 2) | (xs > face_r + 2) | (ys < face_top - 6)
            # the pack's hands layer is hands + whatever they hold (the mage's staff sits entirely in it); a hands-layer
            # blob that reaches past the body box or is big is a held thing, a small one is just a hand + cuff (stays cloth)
            held = np.zeros((C, C), bool)
            for comp in components(hands):
                if (comp & outside).any() or comp.sum() >= 12: held |= comp
            weapon_all = (keep & ~skin & ~af & ~ab & ~hands & outside) | (shaft & keep) | (held & keep & ~skin)
            fx = np.zeros((C, C), bool)                                  # effect layers (orb glow, spell burst) draw over the cloth in the pack
            for (layer, i), px in frames.items():
                if i == fmap[oc] and layer not in NOT_FX: fx |= px[..., 3] > 0
            robe_held = np.zeros((C, C), bool)                           # robe cloth the weapon rule caught = the sleeve at the held hand / a swinging sleeve…
            for comp in components(weapon_all & robe):
                if not (dilate(comp, 1) & (crystal | fx)).any(): robe_held |= comp   # …unless it rims the orb (touches its crystal / glow)
            weapon = weapon_all & ~robe_held
            robe_held &= ~fx                                             # …so robe under an effect stays behind the weapon, not on the front sleeve over it
            cloth = keep & ~skin & ~steel & ~weapon & ~eyes   # eyes belong to the body only (were also landing in the hat, tinted by its palette)
            face_px = keep & beard & ~weapon                             # beard (opt-in packs) → face sheet, out of helmet / armour
            metal = keep & steel & ~weapon & ~face_px
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
            c_f = cloth & (af | (robe_held & ~ab)); c_b = cloth & ab & ~c_f; rest = cloth & ~c_f & ~c_b   # robe_held → a sleeve, never the hat
            hat = rest & head
            hairish = rest & np.array([[key[y][x] in hair_tones for x in range(C)] for y in range(C)])
            while hair_tones and (grow := dilate(hat, 1) & hairish & ~hat).any(): hat |= grow
            legs = rest & ~hat & (ys >= pan_top) & ~robe; c_body = rest & ~hat & ~legs
            m_f = metal & af; m_b = metal & ab & ~m_f; mrest = metal & ~m_f & ~m_b
            helmet = mrest & head; m_body = mrest & ~helmet
            routed = skin_all | weapon | c_f | c_b | hat | legs | c_body | m_f | m_b | helmet | m_body | face_px
            lost = keep & ~routed
            if lost.any(): print(f"  {name} frame {oc}: {int(lost.sum())} kept pixels routed nowhere")
            Tc = T.copy()
            for (r, g, b), nc in tones.items(): Tc[(T[..., 0] == r) & (T[..., 1] == g) & (T[..., 2] == b), :3] = nc
            put(S["hat"], hat, f, row, Tc); put(S["helmet"], helmet, f, row); put(S["pants"], legs, f, row); put(S["weapon"], weapon, f, row)
            if beard_tones:
                Tb = T.copy()
                for (r, g, b), nc in beard_tones.items(): Tb[(T[..., 0] == r) & (T[..., 1] == g) & (T[..., 2] == b), :3] = nc
                put(S["face"], face_px, f, row, Tb)
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
                 weapon="Weapon/Basic Sword.png.meta", shirt="Equipment/Shirt/Tunic.png.meta", armour="Equipment/Shirt/Tunic.png.meta",
                 face="Base/Body/base body.png.meta")
    for k, im in S.items():
        if jump:   # move the jump row(s) from the bottom to the top; the 5 original rows keep their bottom-up rects
            per = 3 if k in ("shirt", "armour") else 1; W = im.width; base = (R - 1) * per * C
            moved = Image.new("RGBA", im.size); moved.paste(im.crop((0, base, W, im.height)), (0, 0)); moved.paste(im.crop((0, 0, W, base)), (0, per * C))
            im = moved; px = im.load()
            for r in range(per):
                for f in range(JUMP[1]):
                    if not np.array(im.crop((f * C, r * C, f * C + C, r * C + C)))[..., 3].any(): px[f * C, r * C + C - 1] = (0, 0, 0, 1)
        pad(im, ADV + metas[k]); im.save(os.path.join(out, k + ".png"))
    preview.save(os.path.join(out, "preview.png"))
    print(f"{name}: tags {dict((a, tags[a]) for a in tags)} → frames {sorted(set(fmap.values()))}")


if __name__ == "__main__":
    chop(sys.argv[1], sys.argv[2], "--jump" in sys.argv[3:])
