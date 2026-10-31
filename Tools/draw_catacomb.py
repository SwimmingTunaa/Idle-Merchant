"""Catacomb Arcade (dungeon level 4, "Cursed Crypts") parallax backgrounds, 320x192 / 8 PPU, tileable.
Built to human scale: an adventurer is ~24 px tall, so alcoves are ~36 px (1.5 people), sarcophagi ~26 px long,
pillars 10 px wide, four arches per tile with a pillar between each. Dark and cool so it sits behind the action.
far = running-bond masonry, arched alcoves (voussoir rings, dark recess, sarcophagus on a plinth, a candle lighting
the recess stone by stone), cobwebs; mid = fluted pillars with stepped bases, a chain lantern, an upright coffin;
near = darkest framing — leaning tombstone, iron gate, fallen drum, bone pile. Only the lower ~126 px of a sheet
shows in the band. Run: uv run --with pillow --with numpy python3 draw_catacomb.py <out>"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from draw_caverns import W, H, value_noise, wrapdx, to_img

YS, XS = np.mgrid[0:H, 0:W]
FLOOR, HIDDEN = 192, 66
# cool, dark stone: a background, not a set piece
STONE = dict(light=(74, 72, 68), mid=(52, 50, 48), dark=(36, 34, 36), mortar=(20, 18, 24), deep=(12, 11, 15), hi=(96, 94, 86))
RECESS = dict(light=(44, 42, 42), mid=(30, 29, 30), dark=(20, 19, 22), mortar=(11, 10, 14), deep=(8, 7, 10), hi=(56, 54, 50))
MIDSTONE = dict(light=(70, 68, 64), mid=(48, 46, 44), dark=(32, 30, 32), edge=(14, 12, 14), hi=(92, 90, 82))
NEAR = dict(light=(26, 25, 28), mid=(16, 15, 18), dark=(9, 9, 11), edge=(5, 5, 7), rim=(34, 78, 56))
WOOD = dict(light=(92, 70, 56), mid=(64, 48, 40), dark=(42, 31, 27), edge=(20, 15, 13), grain=(54, 40, 34))
IRON, IRON_L, NAIL = (38, 38, 44), (72, 72, 80), (150, 140, 120)
FLAME, GLOW1 = (180, 240, 180), (84, 190, 118)
ARCH_X = (40, 120, 200, 280); ARCH_HW = 14; ARCH_STRAIGHT = 22
PILLAR_X = (80, 160, 240, 320)


def masonry(img, region, P, seed, row_h=7, bw=(10, 18)):
    """Running-bond blocks inside a boolean region: per-block tone from noise, mortar lines, chipped corners."""
    r = random.Random(seed); g = value_noise(seed, 5)
    tone = np.zeros((H, W, 3)); tone[:] = P["mid"]
    mortar = np.zeros((H, W), bool)
    y = 0; row = 0
    while y < H:
        h = row_h + r.choice((-1, 0, 0, 1))
        x = -r.randrange(0, bw[1]) if row % 2 else -r.randrange(0, bw[0])
        while x < W:
            w = r.randint(*bw)
            bx0, bx1 = x, x + w
            t = g[min(y + h // 2, H - 1), (bx0 + w // 2) % W]
            c = P["light"] if t > 0.7 else (P["dark"] if t < 0.3 else P["mid"])
            x0c, x1c = max(bx0, 0), min(bx1 - 1, W); y1c = min(y + h - 1, H)
            tone[y:y1c, x0c:x1c] = c
            if x1c - x0c > 3 and y1c - y > 3:                                   # bevel: light top/left, dark bottom/right
                lit = P["hi"] if c == P["light"] else P["light"]
                tone[y, x0c:x1c] = lit; tone[y:y1c, x0c] = lit
                tone[y1c - 1, x0c:x1c] = P["dark"]; tone[y:y1c, x1c - 1] = P["dark"]
                if r.random() < 0.6: tone[y + 1, x0c + 1:x0c + 1 + r.randint(1, 3)] = P["hi"]   # corner highlight
                if r.random() < 0.3: tone[y + r.randint(2, max(2, y1c - y - 2)), x0c + r.randint(2, max(2, x1c - x0c - 2))] = P["dark"]   # pock
            if r.random() < 0.1 and w > 8:                                      # chipped corner
                cs = r.randint(2, 4); cy0 = y if r.random() < 0.5 else y + h - cs
                for k in range(cs):
                    xx = (bx1 - 2 - k) if r.random() < 0.5 else (bx0 + k)
                    for yy in range(cy0, cy0 + cs - k):
                        if 0 <= xx < W and 0 <= yy < H: tone[yy, xx] = P["dark"]
            if bx1 < W: mortar[y:y + h, bx1 - 1:bx1] = True
            x += w
        mortar[y + h - 1:y + h, :] = True
        y += h; row += 1
    tone[mortar] = P["mortar"]
    img[region] = tone[region]


def arch_mask(cx, hw, straight):
    m = Image.new("L", (W, H), 0); d = ImageDraw.Draw(m)
    for off in (-W, 0, W):
        x = cx + off
        d.rectangle([x - hw, FLOOR - straight, x + hw, FLOOR], fill=255)
        d.ellipse([x - hw, FLOOR - straight - hw, x + hw, FLOOR - straight + hw], fill=255)
    return np.array(m) > 0


def light_blocks(img, region, glow, mortar_cols, steps=((0.6, 0.8), (0.3, 0.5), (0.12, 0.25)), tint=(0.86, 1.0, 0.88)):
    """Light each mortar-bounded stone in `region` by one step from the mean glow over it."""
    mortar_here = np.zeros((H, W), bool)
    for c in mortar_cols: mortar_here |= (img == np.array(c)).all(axis=2)
    seen = np.zeros((H, W), bool)
    for (y0, x0) in np.argwhere(region & ~mortar_here):
        if seen[y0, x0]: continue
        stack = [(y0, x0)]; cells = []
        while stack:
            y, x = stack.pop()
            if seen[y, x] or not region[y, x] or mortar_here[y, x]: continue
            seen[y, x] = True; cells.append((y, x))
            for (ny, nx) in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < H and 0 <= nx < W: stack.append((ny, nx))
        if not cells: continue
        ys_, xs_ = zip(*cells); gm = glow[ys_, xs_].mean()
        k = next((kk for thr, kk in steps if gm > thr), 0.0)
        if k: img[ys_, xs_] = np.clip(img[ys_, xs_] * (1 + k) * np.array(tint), 0, 255)


# ───────────────────────── FAR: masonry wall, arched alcoves, sarcophagi, candles ─────────────────────────
def far():
    img = np.zeros((H, W, 3)); img[:] = STONE["mid"]
    masonry(img, np.ones((H, W), bool), STONE, 51)
    grain = value_noise(52, 2)
    img[(grain > 0.9)] = STONE["dark"]; img[(grain < 0.07)] = STONE["light"]
    for i, cx in enumerate(ARCH_X):
        hw, straight = ARCH_HW, ARCH_STRAIGHT
        opening = arch_mask(cx, hw, straight)
        ring = arch_mask(cx, hw + 5, straight) & ~opening
        ringc = np.zeros((H, W, 3)); ringc[:] = STONE["light"]
        ang = np.arctan2(FLOOR - straight - YS, wrapdx(XS, cx))
        wedge = ((ang / (math.pi / 7)).astype(int) % 2 == 0)
        above = YS < FLOOR - straight
        ringc[above & wedge] = STONE["mid"]
        jamb = (((YS - (FLOOR - straight)) // 7) % 2 == 0)
        ringc[~above & jamb] = STONE["mid"]
        img[ring] = ringc[ring]
        img[ring & ~np.roll(ring, 1, 0)] = STONE["dark"]
        img[ring & ~np.roll(ring, -1, 1) & ~opening] = STONE["dark"]
        # recess: darker, finer masonry stepped back, and a smaller arch deeper in
        masonry(img, opening, RECESS, 53 + i, row_h=5, bw=(7, 12))
        inner = arch_mask(cx, hw - 7, straight - 7)
        deep = np.zeros((H, W, 3))
        masonry(deep, inner, dict(RECESS, light=RECESS["mid"], mid=RECESS["dark"], dark=STONE["deep"], mortar=STONE["deep"], hi=RECESS["light"]), 57 + i, row_h=4, bw=(5, 9))
        img[inner] = deep[inner]
        # sarcophagus on a plinth, one person long, lid slid open
        sx, sy = cx - 13, FLOOR - 14
        img[FLOOR - 3:FLOOR, sx - 3:sx + 29] = STONE["dark"]; img[FLOOR - 3:FLOOR - 2, sx - 3:sx + 29] = STONE["mid"]       # plinth
        img[sy + 3:FLOOR - 3, sx:sx + 26] = STONE["mid"]                       # body
        img[sy + 3:FLOOR - 3, sx:sx + 1] = STONE["deep"]; img[sy + 3:FLOOR - 3, sx + 25:sx + 26] = STONE["deep"]
        img[sy + 6:sy + 9, sx + 4:sx + 9] = STONE["dark"]; img[sy + 6:sy + 9, sx + 11:sx + 16] = STONE["dark"]; img[sy + 6:sy + 9, sx + 18:sx + 23] = STONE["dark"]   # panels
        img[sy + 7:sy + 8, sx + 13:sx + 14] = STONE["light"]                   # carved skull
        img[sy + 1:sy + 3, sx + 1:sx + 27] = STONE["light"]; img[sy:sy + 1, sx + 2:sx + 26] = STONE["hi"]              # lid, lit top
        img[sy + 1:sy + 3, sx + 26:sx + 27] = STONE["dark"]; img[sy + 4:sy + 5, sx + 1:sx + 25] = STONE["light"]      # lid's shadow side, body's lit top
        img[sy + 4:FLOOR - 3, sx + 1:sx + 2] = STONE["light"]; img[sy + 4:FLOOR - 3, sx + 24:sx + 25] = STONE["dark"]
        img[sy + 3:sy + 4, sx:sx + 26] = STONE["deep"]                         # shadow under the lid
        img[sy + 1:sy + 3, sx + 22:sx + 28] = STONE["deep"]                    # the open gap
        # candle on the lid: stub + flame, then light the recess blocks from it
        fx, fy = sx + 7, sy
        img[fy - 3:fy + 1, fx:fx + 2] = (150, 144, 124)
        img[fy - 4:fy - 3, fx:fx + 2] = FLAME; img[fy - 5:fy - 4, fx:fx + 1] = GLOW1
        dxs = wrapdx(XS, fx) / 24.0; dys = (YS - (fy - 4)) / 20.0
        glow = np.exp(-(dxs ** 2 + dys ** 2))
        lit = opening & ~((YS >= sy) & (np.abs(wrapdx(XS, cx)) < 15))
        light_blocks(img, lit, glow, (RECESS["mortar"], STONE["deep"]), steps=((0.55, 1.5), (0.3, 1.1), (0.08, 0.7)), tint=(0.62, 1.0, 0.7))
        img[lit & (glow > 0.3) & ((img == np.array(RECESS["mortar"])).all(axis=2))] = (14, 26, 18)        # joints catch a little too
        # cobweb threads in the arch's upper corners
        for side in (-1, 1):
            ox, oy = cx + side * (hw - 1), FLOOR - straight - 1
            for k in range(3):
                for t in range(0, 8):
                    x, y = int(ox - side * t) % W, int(oy + t * (0.4 + k * 0.5))
                    if 0 <= y < H and opening[y, x] and (t + k) % 2 == 0: img[y, x] = (52, 54, 64)
    # loculi: stacked burial niches cut into the wall above the arches, staggered by row
    r = random.Random(59); BONE, BONE_D = (150, 142, 124), (96, 90, 80)
    for row, (ny, nh) in enumerate(((HIDDEN + 20, 9), (HIDDEN + 38, 9), (HIDDEN + 56, 9))):
        for k in range(10):
            nx = (k * 32 + (16 if row % 2 else 0) + 6) % W; nw = 14
            kind = r.random()
            xs = [(x % W) for x in range(nx, nx + nw)]
            img[ny:ny + nh, xs] = RECESS["dark"]                                # the dark of the niche
            img[ny:ny + 1, xs] = STONE["deep"]; img[ny + nh - 1:ny + nh, xs] = STONE["light"]   # lintel shadow, sill
            img[ny:ny + nh, nx % W] = STONE["deep"]; img[ny:ny + nh, (nx + nw - 1) % W] = STONE["dark"]
            if kind < 0.45:                                                     # a skull resting in the niche
                sx_ = (nx + 5 + r.randint(0, 3)) % W; sy_ = ny + nh - 5
                for (dx, dy, c) in ((0, 0, BONE), (1, 0, BONE), (2, 0, BONE), (0, 1, BONE), (1, 1, BONE_D), (2, 1, BONE), (0, 2, BONE_D), (2, 2, BONE_D), (1, 2, BONE_D), (0, 3, BONE), (1, 3, BONE), (2, 3, BONE)):
                    img[sy_ + dy, (sx_ + dx) % W] = c
                img[sy_ + 1, sx_ % W] = STONE["deep"]; img[sy_ + 1, (sx_ + 2) % W] = STONE["deep"]   # sockets
                if r.random() < 0.5:                                            # a long bone beside it
                    img[ny + nh - 2, [(x % W) for x in range(nx + 2, nx + 5)]] = BONE_D
            elif kind < 0.7:                                                    # bricked up
                img[ny + 1:ny + nh - 1, xs] = STONE["dark"]
                img[ny + 4:ny + 5, xs] = STONE["mortar"]; img[ny + 1:ny + nh - 1, (nx + 6) % W] = STONE["mortar"]
            elif kind < 0.82:                                                   # a candle left burning
                cx_ = (nx + 7) % W
                img[ny + nh - 4:ny + nh - 1, cx_] = (150, 144, 124); img[ny + nh - 5, cx_] = FLAME
                dxs = wrapdx(XS, cx_) / 9.0; dys = (YS - (ny + nh - 5)) / 6.0
                glow = np.exp(-(dxs ** 2 + dys ** 2))
                around = (np.abs(wrapdx(XS, cx_)) < 13) & (YS > ny - 7) & (YS < ny + nh + 5)
                light_blocks(img, around, glow, (STONE["mortar"], STONE["deep"], RECESS["mortar"]), steps=((0.5, 0.5), (0.22, 0.28)))
    # ambient: darker overall so it sits behind the action, the top of the band sinking into the ceiling shadow
    fade = np.clip((YS - HIDDEN) / 22.0, 0, 1)[..., None]
    keep = (img == np.array(FLAME)).all(axis=2) | (img == np.array(GLOW1)).all(axis=2)
    dark = img * 0.7 * (0.6 + 0.4 * fade)
    img = np.where(keep[..., None], img, dark)
    return to_img(img)


# ───────────────────────── MID: pillars, lantern, coffin ─────────────────────────
def pillar(img, a, cx, P, seed):
    hw = 5; r = random.Random(seed)
    x0, x1 = cx - hw, cx + hw
    xs = [(x % W) for x in range(x0, x1)]
    m = np.zeros((H, W), bool); m[:, xs] = True
    col = np.zeros((H, W, 3)); col[:] = P["mid"]
    col[:, x0 % W] = P["light"]; col[:, (x0 + 1) % W] = P["light"]; col[:, (x1 - 1) % W] = P["dark"]
    for gx in (x0 + 4, x0 + 7): col[:, gx % W] = P["dark"]; col[:, (gx + 1) % W] = P["light"]   # flutes
    g = value_noise(seed, 3)
    col[g > 0.82] = np.array(P["mid"]) * 0.85 + np.array(P["dark"]) * 0.15
    for y in range(HIDDEN + 6 + r.randint(0, 4), FLOOR - 4, 12):               # drum joints: dark seam, lit lip below it
        col[y, xs] = P["edge"]; col[y + 1, xs[1:-1]] = P["light"]
        if r.random() < 0.5: col[y + 2, xs[1]:xs[1] + 2] = P["hi"]
    # edge light from the alcove candles either side: a green rim on both edges, strongest low down, fading up
    RIM = [(118, 196, 140), (72, 132, 92), (46, 84, 62)]
    for (y0, y1, c) in ((FLOOR - 18, FLOOR - 4, RIM[0]), (FLOOR - 36, FLOOR - 18, RIM[1]), (FLOOR - 58, FLOOR - 36, RIM[2])):
        col[y0:y1, x0 % W] = c; col[y0:y1, (x1 - 1) % W] = c
        if c is RIM[0]: col[y0:y1, (x0 + 1) % W] = RIM[1]; col[y0:y1, (x1 - 2) % W] = RIM[2]
    img[m] = col[m]; a[m] = 255
    def band(y0, y1, ex, top=True):
        xs2 = [(x % W) for x in range(x0 - ex, x1 + ex)]
        img[y0:y1, xs2] = P["mid"]; a[y0:y1, xs2] = 255
        img[y0:y0 + 1, xs2] = P["light"]
        if not top: img[y1 - 1:y1, xs2] = P["dark"]
    band(FLOOR - 2, FLOOR, 4); band(FLOOR - 4, FLOOR - 2, 2)                  # stepped base
    for (y, ex) in ((FLOOR - 4, 2), (FLOOR - 2, 4)):                            # the base steps' top edges catch the candlelight
        img[y, (x0 - ex) % W:(x0 - ex + 3) % W] = (118, 196, 140); img[y, (x1 + ex - 3) % W:(x1 + ex) % W] = (118, 196, 140)
    band(HIDDEN + 1, HIDDEN + 4, 3, top=False); band(HIDDEN + 4, HIDDEN + 6, 1, top=False)   # capital under the floor above
    band(126, 128, 1)                                                           # carved ring mid-shaft
    x, y = cx + r.randint(-3, 3), r.randint(90, 150)                           # crack
    for _ in range(r.randint(6, 16)):
        if x0 < x < x1 - 1: img[y, x % W] = P["edge"]
        y += 1; x += r.choice((-1, 0, 0, 1))


def mid():
    img = np.zeros((H, W, 3)); a = np.zeros((H, W))
    for i, cx in enumerate(PILLAR_X): pillar(img, a, cx, MIDSTONE, 71 + i)
    im = to_img(img, a); d = ImageDraw.Draw(im); px = im.load()
    lx = 96                                                                     # iron lantern on a chain by the first pillar
    for y in range(HIDDEN - 2, 100): px[lx, y] = ((56, 56, 64, 255) if y % 2 else (34, 34, 40, 255))
    d.rectangle([lx - 2, 100, lx + 2, 107], fill=(30, 30, 36)); d.rectangle([lx - 1, 102, lx + 1, 106], fill=(18, 18, 22))
    px[lx, 103] = FLAME + (255,); px[lx, 104] = GLOW1 + (255,)
    d.rectangle([lx - 3, 99, lx + 3, 99], fill=(56, 56, 64))
    for (ox, oy) in ((-3, 104), (3, 104), (0, 109), (-2, 108), (2, 108)): px[lx + ox, oy] = GLOW1 + (90,)
    cx0, cy0 = 251, FLOOR - 28                                                  # upright coffin leaning on the third pillar
    pts = [(cx0 + 3, cy0), (cx0 + 9, cy0), (cx0 + 13, cy0 + 8), (cx0 + 11, FLOOR), (cx0 + 1, FLOOR), (cx0 - 1, cy0 + 8)]
    d.polygon(pts, fill=WOOD["mid"])
    for x in (cx0 + 4, cx0 + 8):                                                # plank seams with grain beside them
        for y in range(cy0 + 1, FLOOR):
            if px[x, y][3]: px[x, y] = WOOD["grain"] + (255,)
            if y % 5 == 2 and px[x - 1, y][3]: px[x - 1, y] = WOOD["dark"] + (255,)
    d.polygon(pts, outline=WOOD["edge"])
    for (x, y) in ((cx0 + 2, cy0 + 1), (cx0 + 1, cy0 + 4), (cx0, cy0 + 8)):     # bevel: lit upper-left edge
        if px[x, y][3]: px[x, y] = WOOD["light"] + (255,)
    for y in range(cy0 + 1, cy0 + 8): px[cx0 + 3 + (y - cy0) // 2, y] = WOOD["light"] + (255,)
    for y in range(cy0 + 9, FLOOR - 1): px[cx0 + 12 - (y - cy0 - 8) // 10, y] = WOOD["dark"] + (255,)   # shadow edge on the right
    d.rectangle([cx0 + 3, cy0 - 1, cx0 + 9, cy0 - 1], fill=WOOD["light"])      # lid's top rim catching light
    for by in (cy0 + 10, cy0 + 20):                                             # iron bands with nail heads
        for x in range(cx0, cx0 + 13):
            if px[x, by][3]: px[x, by] = IRON + (255,)
            if px[x, by + 1][3]: px[x, by + 1] = IRON_L + (255,) if x in (cx0 + 2, cx0 + 10) else IRON + (255,)
        px[cx0 + 2, by] = NAIL + (255,); px[cx0 + 10, by] = NAIL + (255,)
    px[cx0 + 6, cy0 + 4] = WOOD["dark"] + (255,); px[cx0 + 6, cy0 + 5] = WOOD["dark"] + (255,)   # a split in the lid
    return im


# ───────────────────────── NEAR: tombstone, iron gate, drum, bone pile ─────────────────────────
def near():
    img = np.zeros((H, W, 3)); a = np.zeros((H, W))
    m = Image.new("L", (W, H), 0); d = ImageDraw.Draw(m)
    tx, ty = 64, FLOOR - 16                                                     # leaning tombstone (gap after arch 1)
    d.polygon([(tx - 5, FLOOR), (tx - 6, ty + 5), (tx - 2, ty), (tx + 4, ty - 1), (tx + 7, ty + 4), (tx + 6, FLOOR)], fill=255)
    fx0, fx1, fy = 186, 214, FLOOR - 13                                         # iron gate across arch 3
    for x in range(fx0, fx1 + 1, 4):
        d.rectangle([x, fy, x, FLOOR], fill=255); px_ = (x, fy - 2); d.polygon([(x - 1, fy), (x + 1, fy), (x, fy - 3)], fill=255)
    d.rectangle([fx0 - 1, fy + 3, fx1 + 1, fy + 3], fill=255); d.rectangle([fx0 - 1, FLOOR - 4, fx1 + 1, FLOOR - 4], fill=255)
    d.rectangle([fx0 - 2, fy - 1, fx0 - 1, FLOOR], fill=255); d.rectangle([fx1 + 1, fy - 1, fx1 + 2, FLOOR], fill=255)
    # a toppled column lying across the floor, broken into two drums, and the stump it snapped from
    d.rounded_rectangle([132, FLOOR - 9, 170, FLOOR - 1], radius=3, fill=255)
    d.polygon([(170, FLOOR - 9), (176, FLOOR - 7), (173, FLOOR - 4), (177, FLOOR - 1), (170, FLOOR - 1)], fill=255)   # broken end
    d.rounded_rectangle([178, FLOOR - 7, 198, FLOOR - 1], radius=2, fill=255)   # the drum that rolled on
    d.polygon([(296, FLOOR), (296, FLOOR - 12), (299, FLOOR - 15), (303, FLOOR - 11), (306, FLOOR - 16), (309, FLOOR - 12), (309, FLOOR)], fill=255)   # jagged stump
    d.rectangle([294, FLOOR - 3, 311, FLOOR], fill=255)                        # its base
    mm = np.array(m) > 0
    col = np.zeros((H, W, 3)); col[mm] = NEAR["mid"]
    col[mm & ~np.roll(mm, 1, 0)] = NEAR["light"]
    col[mm & ~np.roll(mm, -1, 0)] = NEAR["dark"]
    edge = mm & ~(np.roll(mm, 1, 1) & np.roll(mm, -1, 1) & np.roll(mm, 1, 0) & np.roll(mm, -1, 0))
    col[edge] = NEAR["edge"]
    col[edge & ~np.roll(mm, 1, 1) & (YS < FLOOR - 2)] = NEAR["rim"]
    img[mm] = col[mm]; a[mm] = 255
    im = to_img(img, a); px = im.load()
    px[tx, ty + 6] = (28, 28, 36, 255); px[tx + 2, ty + 6] = (28, 28, 36, 255); px[tx - 1, ty + 9] = (28, 28, 36, 255); px[tx + 3, ty + 9] = (28, 28, 36, 255)   # worn lettering
    for x in (142, 152, 162, 188):                                              # drum seams on the fallen column
        for y in range(FLOOR - 8, FLOOR - 1):
            if px[x, y][3]: px[x, y] = NEAR["edge"] + (255,)
            if px[x + 1, y][3] and y == FLOOR - 8: px[x + 1, y] = NEAR["light"] + (255,)
    for y in range(FLOOR - 11, FLOOR - 3):                                      # a flute line on the stump
        px[302, y] = NEAR["edge"] + (255,)
    return im


def main(out):
    os.makedirs(out, exist_ok=True)
    layers = {"background1-sheet": far(), "background2-sheet": mid(), "background3-sheet": near()}
    for k, im in layers.items(): im.save(os.path.join(out, k + ".png"))
    comp = Image.new("RGBA", (W * 2, H))
    for im in layers.values():
        for i in range(2): comp.alpha_composite(im, (i * W, 0))
    comp.save(os.path.join(out, "catacomb_full.png"))
    band = comp.crop((0, HIDDEN, W * 2, H)); band.resize((band.width * 3, band.height * 3), Image.NEAREST).save(os.path.join(out, "catacomb_band.png"))
    comp.crop((0, HIDDEN, W, H)).resize((W * 4, (H - HIDDEN) * 4), Image.NEAREST).save(os.path.join(out, "catacomb_4x.png"))
    # scale check: a 24px-tall figure box standing on the floor
    chk = comp.crop((0, HIDDEN, W, H)).copy(); dd = ImageDraw.Draw(chk)
    dd.rectangle([150, FLOOR - HIDDEN - 24, 160, FLOOR - HIDDEN - 1], outline=(255, 80, 80, 255))
    chk.resize((W * 4, (H - HIDDEN) * 4), Image.NEAREST).save(os.path.join(out, "catacomb_scale.png"))


if __name__ == "__main__":
    main(sys.argv[1])
