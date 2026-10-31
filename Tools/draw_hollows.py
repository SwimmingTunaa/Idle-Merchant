"""Crystal Hollows (dungeon level 3) parallax backgrounds, 320x192 / 8 PPU, horizontally tileable, same rig as
the Mines and Caverns: far = opaque slate wall with glowing geode mouths and crystal veins, mid = hexagonal crystals
(three body faces + a three-face pyramid tip, a lit facet seam; pillars are chunky prisms with
crystals sprouting off them, stalactites / stalagmites are clusters; translucent toward the geode behind), near = darkest shard silhouettes with a cyan rim (no ledges). Translucency: glow bleed + quantised subsurface
scatter from the lit edge and thin tips.
Run: uv run --with pillow --with numpy python3 draw_hollows.py <out>"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from draw_caverns import W, H, value_noise, wrapdx, to_img

YS, XS = np.mgrid[0:H, 0:W]
MID_OPACITY = float(os.environ.get('HOLLOWS_MID_OPACITY', '0.72'))     # 1.0 = opaque silhouettes like the other depths
NEAR_OPACITY = float(os.environ.get('HOLLOWS_NEAR_OPACITY', '0.88'))
# geode mouths in the far wall: (cx, cy, rx, ry, violet?)
POCKETS = [(70, 58, 24, 12, False), (150, 118, 20, 10, True), (236, 44, 26, 13, False), (304, 124, 18, 9, False),
           (14, 110, 15, 8, True), (204, 152, 17, 8, False), (120, 24, 13, 6, True)]
# crystal palettes (dark / mid / light / spec / edge / rim); a cluster picks one by seed
MID_PALETTES = [
    dict(dark=(16, 28, 50), mid=(24, 48, 80), light=(42, 86, 122), spec=(120, 190, 206), edge=(12, 20, 38), rim=(110, 190, 206)),       # cyan
    dict(dark=(30, 18, 54), mid=(54, 32, 92), light=(92, 62, 140), spec=(190, 150, 236), edge=(20, 12, 40), rim=(176, 136, 226)),       # violet
    dict(dark=(14, 36, 36), mid=(24, 62, 58), light=(46, 108, 92), spec=(150, 222, 190), edge=(10, 26, 26), rim=(130, 208, 172)),       # pale green
]
NEAR_PALETTES = [
    dict(dark=(10, 12, 24), mid=(14, 17, 32), light=(20, 26, 46), spec=(52, 88, 118), edge=(8, 10, 20), rim=(70, 138, 160)),
    dict(dark=(14, 9, 26), mid=(20, 13, 36), light=(30, 20, 52), spec=(96, 66, 140), edge=(10, 7, 20), rim=(130, 96, 180)),
    dict(dark=(8, 14, 16), mid=(12, 20, 22), light=(18, 30, 30), spec=(60, 110, 96), edge=(6, 10, 12), rim=(80, 150, 130)),
]
CYAN = [np.array(c) for c in ((34, 80, 104), (56, 150, 168), (130, 226, 230), (224, 255, 250))]
VIOLET = [np.array(c) for c in ((66, 40, 108), (120, 78, 186), (190, 150, 240), (244, 228, 255))]


def light_field(scale=1.0):
    """Glow strength 0..1 from the geodes plus the signed x / y direction toward the strongest one."""
    L = np.zeros((H, W)); dirx = np.zeros((H, W)); diry = np.zeros((H, W)); vio = np.zeros((H, W), bool)
    for (cx, cy, rx, ry, v) in POCKETS:
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        f = np.exp(-(dx / (rx * 3.0 * scale)) ** 2 - (dy / (ry * 4.0 * scale)) ** 2)
        upd = f > L
        L = np.where(upd, f, L); dirx = np.where(upd, -np.sign(dx), dirx); diry = np.where(upd, -np.sign(dy), diry); vio = np.where(upd, v, vio)
    return L, dirx, diry, vio


# ───────────────────────── FAR: slate wall, crystal veins, geode mouths ─────────────────────────
def far():
    BASE = np.array((30, 32, 52)); R1, R2, R3 = np.array((36, 38, 61)), np.array((46, 50, 78)), np.array((64, 72, 104))
    img = np.zeros((H, W, 3)); img[:] = BASE
    coarse = value_noise(31, 16); fine = value_noise(32, 4); grain = value_noise(33, 2)
    img[coarse > 0.6] = R1
    # stepped glow falloff on the wall around each geode
    glowd = np.full((H, W), 99.0); vmap = np.zeros((H, W), bool)
    for (cx, cy, rx, ry, v) in POCKETS:
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        d = (dx / rx) ** 2 + (dy / (ry * 1.25)) ** 2
        vmap = np.where(d < glowd, v, vmap); glowd = np.minimum(glowd, d)
    jit = (grain - 0.5) * 0.8
    for thr, cc, cv in ((4.6, (38, 52, 72), (46, 40, 76)), (2.8, (44, 70, 92), (60, 48, 98)), (1.75, (52, 92, 112), (76, 58, 122))):
        sel = glowd + jit < thr
        img[sel & ~vmap] = cc; img[sel & vmap] = cv
    # crystal veins: thin bright threads wandering down the wall
    r = random.Random(5)
    for k in range(9):
        x, y = r.randrange(W), r.randrange(0, 20)
        for _ in range(r.randint(60, 150)):
            if 0 <= y < H and glowd[y, x % W] > 1.4:
                img[y, x % W] = R3 if (x + y) % 3 else R2
            y += 1; x += r.choice((-1, 0, 0, 0, 1))
            if r.random() < 0.08: y += 1
    # geode mouths: crystal teeth all round the rim, bands brightening toward the centre
    for i, (cx, cy, rx, ry, v) in enumerate(POCKETS):
        B = VIOLET if v else CYAN
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        teeth = 2.0 + 2.5 * np.abs(np.sin(dx * 0.55 + i * 1.3)) + 1.5 * np.abs(np.sin(dy * 0.7 + i))
        d = (dx / rx) ** 2 + (dy / ry) ** 2 + teeth * 0.04 + 0.25 * (fine - 0.5)
        img[d < 1.35] = B[0]
        img[d < 1.0] = B[1]
        img[d < 0.6] = B[2]
        img[d < 0.25] = B[3]
        # a few spike crystals pointing into the mouth
        rr = random.Random(i)
        for _ in range(5):
            ang = rr.random() * 2 * math.pi; ln = rr.randint(3, 6)
            for t in range(ln):
                px_ = int(cx + math.cos(ang) * (rx * 0.9 - t)) % W; py_ = int(cy + math.sin(ang) * (ry * 0.9 - t))
                if 0 <= py_ < H: img[py_, px_] = B[2] if t < ln - 1 else B[3]
    glow = np.zeros((H, W), bool)
    for B in (CYAN, VIOLET):
        for b in B[1:]: glow |= (img == b).all(axis=2)
    # crystal chip rubble: lumps with a lit top, brighter near a geode
    body = (fine + 0.3 * np.exp(-np.clip(glowd, 0, 9) / 2.5) > 0.8) & ~glow
    img[body] = R2
    top = body & ~np.roll(body, 1, 0)
    img[top] = R3
    img[top & (glowd < 3.0) & ~vmap] = CYAN[2]; img[top & (glowd < 3.0) & vmap] = VIOLET[2]
    img[body & ~np.roll(body, -1, 0)] = R1
    return to_img(img)


# ───────────────────────── crystal prisms ─────────────────────────
def crystal(img, alpha, bx, by, ang, length, w, P, L, dirx, glowc, flat=False, bleed=0.4, seed=0, opacity=1.0):
    """A hexagonal crystal seen side-on, twisted a little about its axis so the back edges land at a different
    x than the front ones. The geodes sit behind the sheet, so the crystal is backlit: front faces are toned by
    their normal against the light, the back faces' edges show through the body and converge in the pyramid
    tip, light wraps the far edge, and the whole rim is lit (Fresnel), brighter on the lit side.
    Base at (bx, by), pointing along angle `ang` from straight up (radians, +ve leans right)."""
    r = random.Random(seed)
    sa, ca = math.sin(ang), math.cos(ang)
    dx = wrapdx(XS, bx).astype(float); dy = (YS - by).astype(float)
    u = dx * sa - dy * ca                                             # along the axis, 0 at the base
    v = dx * ca + dy * sa                                             # across the axis
    tip = 0 if flat else w * 1.6
    taper = np.where(u > length - tip, np.clip((length - u) / max(tip, 1e-6), 0, 1), 1.0)
    half = w * taper
    m = (u >= 0) & (u <= length) & (np.abs(v) <= half)
    if not m.any(): return
    cx, cy = int(bx + sa * length * 0.5) % W, int(by - ca * length * 0.5)
    lit_right = dirx[min(max(cy, 0), H - 1), cx] >= 0
    vv = v if lit_right else -v                                       # vv > 0 is the lit side
    in_tip = u > length - tip
    # hexagon cross-section: vertices at twist + k*60°, viewer along -y; project to the across axis
    twist = r.uniform(0.2, 0.85)
    verts = [(twist + k * math.pi / 3) for k in range(6)]
    xs_ = [math.cos(t) for t in verts]; sc = w / max(abs(x) for x in xs_)
    front = sorted([(math.cos(t) * sc, t) for t in verts if math.sin(t) < 0])   # 3 toward the viewer
    back = [math.cos(t) * sc for t in verts if math.sin(t) > 0 and abs(math.cos(t) * sc) < w * 0.98]
    if not lit_right:                                                 # mirror so +x is always the lit side
        front = sorted([(-x, math.pi - t) for (x, t) in front]); back = [-x for x in back]
    bounds = [x for (x, t) in front if abs(x) < w * 0.98]           # interior front edges split the faces
    # face normal angles → lit-ness against light from behind-right (geode side)
    light = math.atan2(1.0, 1.0)
    edges_x = [-w] + sorted(bounds) + [w]
    tones_ = [np.array(P["dark"]), np.array(P["mid"]), np.array(P["light"])]
    tip_tones = [np.array(P["mid"]), np.array(P["light"]), np.array(P["spec"]) * 0.55 + np.array(P["light"]) * 0.45]
    col = np.zeros((H, W, 3)); face = np.zeros((H, W), int)
    fverts = sorted(front, key=lambda ft: ft[0])
    for i in range(len(edges_x) - 1):
        lo, hi = edges_x[i] * taper, edges_x[i + 1] * taper            # edges converge toward the tip
        sel = m & (vv >= lo) & (vv <= hi)
        # the face between two front vertices: normal = mean angle, lit-ness vs the light direction
        ta, tb = fverts[min(i, len(fverts) - 1)][1], fverts[min(i + 1, len(fverts) - 1)][1]
        nrm = (ta + tb) / 2 if abs(ta - tb) < math.pi else (ta + tb) / 2 + math.pi
        lit = math.cos(nrm - light)
        f = 2 if lit > 0.25 else (1 if lit > -0.35 else 0)
        face[sel] = f
        col[sel & ~in_tip] = tones_[f]; col[sel & in_tip] = tip_tones[f]
    # translucency: the glow behind drifts the lit faces toward the geode colour
    b = np.clip(L * bleed, 0, 0.5)[..., None]
    col = np.where(m[..., None] & (face[..., None] > 0), col * (1 - b) + glowc * b, col)
    # subsurface scattering in three quantised steps from the lit edge, strongest through the thin tip
    across = np.clip((half - vv) / max(w, 1), 0, 3)
    thin = np.clip(1 - taper, 0, 1)
    amb = 0.7 + 0.3 * L
    sss = amb * np.clip(1.0 * np.exp(-across * 0.85) + 1.0 * thin ** 1.2, 0, 1) * bleed / 0.4
    band = np.floor(np.clip(sss, 0, 0.999) * 4) / 4
    C = glowc * 0.8 + np.array(P["spec"])[None, None, :] * 0.2
    k = (band * np.where(face == 0, 0.7, 0.9))[..., None]
    col = np.where(m[..., None], col * (1 - k) + C * k, col)
    # back edges seen through the body: faint bright lines at the back vertices' x, converging in the tip
    for xb in back:
        line = m & (np.abs(vv - xb * taper) < 0.6) & (np.abs(vv) < half - 1)
        col[line] = col[line] * 0.55 + C[line] * 0.45
    # internal bounce: a 1px lighter line just inside the shadow edge
    wrap = m & (np.abs(vv + half) < 1.5) & (np.abs(vv) > 1) & (face == 0)
    if wrap.any(): col[wrap] = np.array(P["mid"]) * 0.5 + C[wrap] * 0.5
    hot = m & (band >= 0.74)
    # lit front seam catches a specular line
    fshift = np.roll(face, 1, 1) if lit_right else np.roll(face, -1, 1)
    seam = m & (face != fshift) & np.roll(m, 1 if lit_right else -1, 1)
    col[seam & (face == 2)] = P["spec"]
    # rim: Fresnel lights the whole outline, brighter toward the lit side and on the pyramid edges
    edge = m & ~(np.roll(m, 1, 1) & np.roll(m, -1, 1) & np.roll(m, 1, 0) & np.roll(m, -1, 0))
    col[edge] = np.array(P["mid"]) * 0.6 + np.array(P["edge"]) * 0.4
    col[edge & (vv > 0) & (L > 0.12)] = P["rim"]
    col[edge & in_tip & (face == 2)] = P["spec"]
    col[edge & hot] = C[edge & hot]
    img[m] = col[m]
    av = np.full((H, W), 255.0)
    if opacity < 1.0:                                                 # bodies let the far wall through, tips more so
        av = 255.0 * (opacity - 0.3 * opacity * thin)
        av = np.where(edge | (seam & (face == 2)), 255.0, av)
    alpha[m] = av[m]


def cluster(img, alpha, bx, by, up, main_len, main_w, P, L, dirx, glowc, seed, side=None, bleed=0.4, flat=False, opacity=1.0):
    """A main crystal plus 2-4 smaller ones leaning off the same base. up=True grows from a floor, else hangs."""
    r = random.Random(seed); base = 0 if up else math.pi
    subs = []
    for k in range(r.randint(1, 3)):
        lean = r.choice((-1, 1)) * r.uniform(0.3, 0.75)
        if side is not None: lean = abs(lean) * side
        subs.append((lean, r.uniform(0.35, 0.65), r.uniform(0.45, 0.7), r.uniform(-0.2, 0.2) * main_w))
    for (lean, lf, wf, off) in subs:                                  # leaning ones first, the main one covers their butts
        crystal(img, alpha, bx + off, by, base + lean, main_len * lf, max(3, main_w * wf), P, L, dirx, glowc, bleed=bleed, seed=seed * 7 + int(lean * 10), opacity=opacity)
    # a small crystal sprouting sideways part-way up, its base buried in the main one
    lean = r.choice((-1, 1)) * r.uniform(0.9, 1.3)
    if side is not None: lean = abs(lean) * side
    sy = by - main_len * 0.3 if up else by + main_len * 0.3
    crystal(img, alpha, bx, sy, base + lean, main_len * 0.3 + main_w * 0.5, max(3, main_w * 0.4), P, L, dirx, glowc, bleed=bleed, seed=seed + 99, opacity=opacity)
    crystal(img, alpha, bx, by, base + r.uniform(-0.08, 0.08), main_len, main_w, P, L, dirx, glowc, flat=flat, bleed=bleed, seed=seed, opacity=opacity)


def new_canvas():
    L, dirx, diry, vio = light_field(1.1)
    glowc = np.where(vio[..., None], np.array(VIOLET[2])[None, None, :], np.array(CYAN[2])[None, None, :])
    return np.zeros((H, W, 3)), np.zeros((H, W)), L, dirx, glowc


# ───────────────────────── MID: crystal pillars, hanging and standing shards ─────────────────────────
def mid():
    img, a, L, dirx, glowc = new_canvas(); OP = MID_OPACITY
    PAL = lambda i: MID_PALETTES[i % len(MID_PALETTES)]
    # crystal pillars: a hanging crystal with a floor crystal rising into it, a couple of sprouts each
    for i, (cx, w, lean, pi) in enumerate(((60, 12, -0.03, 0), (222, 13, 0.02, 1))):
        P = PAL(pi); r = random.Random(40 + i)
        for k in range(2):                                            # sprouts first so the pillar buries their butts
            y = r.randint(40, H - 40); side = r.choice((-1, 1))
            crystal(img, a, cx, y, side * r.uniform(1.0, 1.4), r.randint(16, 26) + w * 0.6, r.randint(3, 5), P, L, dirx, glowc, seed=50 + i * 10 + k, opacity=OP)
        crystal(img, a, cx + 2, 0, math.pi - lean, H * 0.58, w * 0.9, P, L, dirx, glowc, seed=20 + i, opacity=OP)          # hanging
        crystal(img, a, cx, H, lean, H * 0.62, w, P, L, dirx, glowc, seed=10 + i, opacity=OP)                               # rising into it
    # hanging clusters from the ceiling band
    for i, (cx, ln, w, pi) in enumerate(((120, 58, 8, 2), (170, 36, 6, 0), (290, 66, 9, 1), (14, 44, 6, 0))):
        cluster(img, a, cx, 0, False, ln, w, PAL(pi), L, dirx, glowc, seed=100 + i, opacity=OP)
    # standing clusters on the floor
    for i, (cx, ln, w, pi) in enumerate(((130, 56, 9, 1), (280, 44, 7, 2), (8, 34, 6, 0), (178, 24, 4, 0))):
        cluster(img, a, cx, H, True, ln, w, PAL(pi), L, dirx, glowc, seed=200 + i, opacity=OP)
    img[0:2, :] = MID_PALETTES[0]["edge"]; a[0:2, :] = 255           # ceiling band the shards hang from
    im = to_img(img, a); px = im.load()
    # a prospector's lantern on a chain from the long stalactite at 290, lit by a crystal instead of a flame
    lx, ly0 = 293, 70
    for y in range(ly0, ly0 + 20): px[lx, y] = ((76, 84, 96, 255) if y % 2 else (46, 50, 60, 255))
    ly = ly0 + 20; d = ImageDraw.Draw(im)
    d.rectangle([lx - 2, ly, lx + 2, ly + 6], fill=(40, 44, 54)); d.rectangle([lx - 1, ly + 1, lx + 1, ly + 5], fill=(130, 226, 230))
    px[lx, ly + 3] = (224, 255, 250, 255); d.rectangle([lx - 3, ly - 1, lx + 3, ly - 1], fill=(70, 76, 90))
    return im


# ───────────────────────── NEAR: darkest framing — big shards, crystal-slab ledges, cyan rim ─────────────────────────
def near():
    img, a, L, dirx, glowc = new_canvas()
    PAL = lambda i: NEAR_PALETTES[i % len(NEAR_PALETTES)]
    for i, (cx, ln, w, pi) in enumerate(((30, 54, 13, 0), (248, 62, 14, 1), (160, 32, 8, 2))):
        cluster(img, a, cx, 0, False, ln, w, PAL(pi), L, dirx, glowc, seed=300 + i, bleed=0.2, opacity=NEAR_OPACITY)
    for i, (cx, ln, w, pi) in enumerate(((40, 68, 14, 1), (206, 78, 16, 0), (318, 32, 8, 2))):
        cluster(img, a, cx, H, True, ln, w, PAL(pi), L, dirx, glowc, seed=400 + i, bleed=0.2, opacity=NEAR_OPACITY)
    im = to_img(img, a); px = im.load()
    # a pickaxe left stuck in the big crystal at 206
    hx, hy = 196, 116
    for t in range(14): px[hx + t, hy + 10 - t // 2] = (96, 70, 48, 255)                        # haft
    for t in range(-6, 7): px[hx + 13 + t // 2, hy + 3 + abs(t)] = (150, 156, 162, 255) if t % 3 else (110, 114, 120, 255)   # head
    return im


def quantize(im, colors):
    """Collapse the blended tones to a small palette (alpha kept as-is, no dither) so it stays pixel art."""
    a = im.getchannel("A")
    q = im.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    q.putalpha(a)
    return q


def main(out):
    os.makedirs(out, exist_ok=True)
    layers = {"background1-sheet": quantize(far(), 40), "background2-sheet": quantize(mid(), 36), "background3-sheet": quantize(near(), 24)}
    for k, im in layers.items(): im.save(os.path.join(out, k + ".png"))
    comp = Image.new("RGBA", (W * 2, H))
    for im in layers.values():
        for i in range(2): comp.alpha_composite(im, (i * W, 0))
    comp.resize((W * 2 * 3, H * 3), Image.NEAREST).save(os.path.join(out, "hollows_composite.png"))
    root = "/Users/tuan/Documents/Game/Idle Merchant Guild Project/Idle Merchant Guild/Assets/Sprites/Dungeon"
    trio = Image.new("RGBA", (W * 3 + 16, H), (0, 0, 0, 255))
    for i, lvl in enumerate(("Level 1", "Level 2")):
        c = Image.new("RGBA", (W, H))
        for k in layers: c.alpha_composite(Image.open(os.path.join(root, lvl, "Background", k + ".png")).convert("RGBA"))
        trio.paste(c, (i * (W + 8), 0))
    cc = Image.new("RGBA", (W, H))
    for im in layers.values(): cc.alpha_composite(im)
    trio.paste(cc, (2 * (W + 8), 0))
    trio.resize((trio.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "mines_caverns_hollows.png"))
    cc.resize((W * 4, H * 4), Image.NEAREST).save(os.path.join(out, "hollows_4x.png"))
    sep = Image.new("RGBA", (W * 3 + 20, H), (255, 0, 255, 255))
    for i, im in enumerate(layers.values()): sep.alpha_composite(im, (i * (W + 10), 0))
    sep.resize((sep.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "hollows_layers.png"))


if __name__ == "__main__":
    main(sys.argv[1])
