"""Deep Caverns v2 — detail + shading pass on top of draw_caverns.py's shapes.
Shading: erosion-depth volume bands, light-facing rim (warm near a glow, cool otherwise), a warm band inside
light-facing edges, ambient occlusion toward the floor, warm bounce light from the far wall's fissures.
Details: plank bridge, chain + miner's lantern, molten drip, ore crystal clusters, mine cart on a rail,
chipped / broken cone tips. Run: uv run --with pillow --with numpy python3 draw_caverns2.py <out>"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from draw_caverns import W, H, value_noise, wrapdx, to_img, silhouette, crack_path

POCKETS = [(84, 62, 22, 11), (148, 112, 20, 10), (240, 46, 26, 12), (300, 120, 18, 9), (16, 104, 16, 8),
           (208, 150, 16, 7), (150, 28, 14, 6)]
YS, XS = np.mgrid[0:H, 0:W]


def light_field(scale=1.0):
    """Warm light strength 0..1 from the far wall's fissures, plus the signed x-direction to the strongest one."""
    L = np.zeros((H, W)); dirx = np.zeros((H, W)); diry = np.zeros((H, W))
    for (cx, cy, rx, ry) in POCKETS:
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        f = np.exp(-(dx / (rx * 3.2 * scale)) ** 2 - (dy / (ry * 4.5 * scale)) ** 2)
        upd = f > L
        L = np.where(upd, f, L); dirx = np.where(upd, -np.sign(dx), dirx); diry = np.where(upd, -np.sign(dy), diry)
    return L, dirx, diry


def depth(m, maxd=5):
    d = np.zeros((H, W), int); cur = m.copy()
    for k in range(1, maxd + 1):
        cur = cur & np.roll(cur, 1, 0) & np.roll(cur, -1, 0) & np.roll(cur, 1, 1) & np.roll(cur, -1, 1)
        d[cur] = k
    return d


def volume_shade(m, n, P, ao_rows=30, lscale=1.0):
    """P: dict core, mid, tex, edge, rim_warm, band_warm, ao."""
    img = np.zeros((H, W, 3)); dpt = depth(m)
    img[m] = P["mid"]
    img[m & (dpt >= 3)] = P["core"]
    img[m & (n > 0.64) & (dpt >= 1)] = P["tex"]
    # ambient occlusion: the lower rows sink into darkness
    img[m & (YS > H - ao_rows) & (dpt >= 2)] = P["ao"]
    L, dx, dy = light_field(lscale)
    # boundary pixels and which side they face
    left = m & ~np.roll(m, 1, 1); right = m & ~np.roll(m, -1, 1); top = m & ~np.roll(m, 1, 0)
    faces = (left & (dx < 0)) | (right & (dx > 0)) | (top & (dy < 0))
    img[left | right | top] = P["edge"]
    img[faces & (L > 0.45)] = P["rim_warm"]          # only edges really close to a fissure catch the glow
    # a 2px warm band just inside light-facing edges where the glow is strong
    band = np.zeros((H, W), bool)
    for s in (1, 2):
        band |= np.roll(faces & (L > 0.35), int(-dx.mean() > 0) * 0 + s * (1 if True else 0), 1) & m & (dpt <= 2)
    near_face = m & (dpt == 1) & (L > 0.6)
    img[near_face & ~(left | right | top)] = P["band_warm"]
    return img, np.where(m, 255, 0)


def far_v2():
    BASE = np.array((58, 44, 40)); R1, R2, R3 = np.array((66, 50, 45)), np.array((78, 60, 52)), np.array((94, 73, 62))
    WARM = [np.array(c) for c in ((70, 50, 42), (84, 58, 44), (102, 68, 46))]
    BANDS = [np.array(c) for c in ((96, 52, 36), (156, 78, 38), (214, 128, 56), (244, 194, 120))]
    img = np.zeros((H, W, 3)); img[:] = BASE
    coarse = value_noise(3, 16); fine = value_noise(4, 4); grain = value_noise(14, 2)
    img[coarse > 0.62] = R1
    # vertical mineral staining (runs down the wall)
    stain = (value_noise(17, 6) > 0.7) & (np.sin(XS * 0.9 + value_noise(18, 10) * 6) > 0.6)
    img[stain] = R1
    # banded glow falloff on the wall around each fissure (stepped, not smooth)
    glowd = np.full((H, W), 99.0)
    for (cx, cy, rx, ry) in POCKETS:
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        glowd = np.minimum(glowd, (dx / (rx * 1.0)) ** 2 + (dy / (ry * 1.25)) ** 2)
    jitter = (grain - 0.5) * 0.8
    img[glowd + jitter < 4.6] = WARM[0]
    img[glowd + jitter < 2.8] = WARM[1]
    img[glowd + jitter < 1.75] = WARM[2]
    for i, (cx, cy, rx, ry) in enumerate(POCKETS):
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy).astype(float)
        teeth = (2.5 + 2.5 * np.abs(np.sin(dx * 0.45 + i * 1.7))) * (dy < 0)
        d = (dx / rx) ** 2 + ((dy + teeth + 3 * (fine - 0.5)) / ry) ** 2
        img[d < 1.3] = BANDS[0]
        img[d < 1.0] = BANDS[1]
        img[(d < 0.62) & (dy > -ry * 0.35)] = BANDS[2]
        img[(d < 0.28) & (dy > -ry * 0.05)] = BANDS[3]
    glow = np.zeros((H, W), bool)
    for b in BANDS[1:]: glow |= (img == b).all(axis=2)
    # rubble lumps, lit warm on top when close to a fissure
    near_pk = np.zeros((H, W))
    for (cx, cy, rx, ry) in POCKETS:
        dx = wrapdx(XS, cx).astype(float); dy = (YS - cy - ry).astype(float)
        near_pk = np.maximum(near_pk, np.exp(-(dx / (rx * 1.6)) ** 2 - (dy / 10.0) ** 2))
    body = ((fine + 0.35 * near_pk) > 0.78) & ~glow
    img[body] = R2
    top = body & ~np.roll(body, 1, 0)
    img[top] = R3
    img[top & (glowd < 4.6)] = np.array((132, 88, 60))
    img[body & ~np.roll(body, -1, 0)] = R1
    # broken magma threads
    for k, (y0, amp, f) in enumerate(((84, 5, 2), (150, 4, 3))):
        for x in range(W):
            if (x // 11 + k) % 4 == 0: continue
            y = int(y0 + amp * math.sin(2 * math.pi * f * x / W + k) + 1.5 * math.sin(x * 0.27 + k))
            if not glow[y, x]: img[y, x] = BANDS[0] if (x + k) % 4 else BANDS[1]
    return to_img(img)


def chip(m, seed, count, size=(1, 3)):
    """Bite small notches out of the silhouette boundary so cones read as broken rock, not geometry."""
    r = random.Random(seed)
    edge = np.argwhere(m & ~(np.roll(m, 1, 1) & np.roll(m, -1, 1) & np.roll(m, 1, 0) & np.roll(m, -1, 0)))
    for _ in range(count):
        y, x = edge[r.randrange(len(edge))]
        s = r.randint(*size)
        m[max(0, y - s):y + s, max(0, x - s):x + s] = False if r.random() < 0.7 else m[max(0, y - s):y + s, max(0, x - s):x + s]
    return m


def mid_v2():
    cols = [(50, 24, 0.4), (178, 30, 0.45), (270, 20, 0.35)]
    stal = [(104, 22, 70), (130, 12, 40), (226, 24, 84), (304, 14, 44), (14, 16, 50), (116, 8, 54), (238, 9, 60)]
    gmit = [(112, 24, 70), (236, 18, 56), (8, 14, 40), (150, 10, 30), (124, 9, 48)]
    m, n = silhouette(21, cols, stal, gmit, [])
    m = chip(m, 4, 90)
    m[0:2, :] |= True                                              # ceiling band so stalactites hang from something
    P = dict(core=(30, 22, 22), mid=(36, 27, 27), tex=(42, 31, 30), edge=(48, 36, 33), rim_warm=(116, 64, 40),
             band_warm=(62, 42, 34), ao=(24, 18, 19))
    img, a = volume_shade(m, n, P)
    im = to_img(img, a); d = ImageDraw.Draw(im); px = im.load()
    # magma cracks down two columns
    for pts, hot, warm in ((crack_path(48, 40, 50, 1), (226, 116, 46), (120, 54, 30)), (crack_path(180, 80, 60, 2), (240, 140, 58), (130, 58, 32))):
        for (x, y) in pts:
            X = x % W
            if 0 <= y < H and m[y, X]:
                px[X, y] = hot + (255,)
                if y - 1 >= 0 and m[y - 1, X]: px[X, y - 1] = warm + (255,)
    # plank bridge slung between the middle and right columns
    x0, x1, yb = 204, 254, 98
    for x in range(x0, x1 + 1):
        sag = int(4 * math.sin(math.pi * (x - x0) / (x1 - x0)))
        y = yb + sag
        px[x, y - 4] = (40, 30, 28, 255)                            # rope
        if not m[y, x]:
            px[x, y] = (88, 62, 44, 255) if (x - x0) % 4 else (50, 36, 30, 255)
            px[x, y + 1] = (58, 40, 32, 255)
            if (x - x0) % 8 == 0:
                for yy in range(y - 4, y): px[x, yy] = (40, 30, 28, 255)   # hanger
    # chain + miner's lantern from the long stalactite at 226
    lx, ly0 = 228, 82
    for y in range(ly0, ly0 + 22): px[lx, y] = ((70, 66, 64, 255) if y % 2 else (46, 42, 42, 255))
    ly = ly0 + 22
    d.rectangle([lx - 2, ly, lx + 2, ly + 5], fill=(44, 38, 36)); d.rectangle([lx - 1, ly + 1, lx + 1, ly + 4], fill=(244, 196, 110))
    px[lx, ly + 2] = (255, 236, 180, 255); d.line([(lx - 3, ly + 6), (lx + 3, ly + 6)], fill=(44, 38, 36))
    for (gx, gy) in ((lx - 3, ly + 2), (lx + 3, ly + 2), (lx, ly - 1), (lx - 2, ly + 7), (lx + 2, ly + 7)):
        if 0 <= gy < H and px[gx, gy][3] == 0: px[gx, gy] = (150, 84, 44, 255)
    # molten drip from the stalactite tip at 104 (static mock; can animate later)
    tipx = 104
    tipy = max(y for y in range(H) if m[y, tipx] and y < 90)
    for (yy, c) in ((tipy + 1, (244, 170, 70)), (tipy + 2, (226, 116, 46)), (tipy + 7, (244, 170, 70)), (tipy + 8, (200, 92, 40)), (tipy + 15, (226, 116, 46))):
        if px[tipx, yy][3] == 0 or True: px[tipx, yy] = c + (255,)
    # ore crystal cluster on the right column's flank
    for (cx, cy, h, c1, c2) in ((282, 132, 6, (180, 92, 52), (232, 150, 92)), (285, 134, 4, (150, 150, 150), (210, 210, 206)), (279, 135, 3, (180, 92, 52), (232, 150, 92))):
        for k in range(h):
            px[cx, cy - k] = c1 + (255,); px[cx + 1, cy - k] = (c1[0] - 50, c1[1] - 40, c1[2] - 30, 255)
        px[cx, cy - h] = c2 + (255,)
    return im


def near_v2():
    cols = [(148, 16, 0.3)]
    stal = [(30, 30, 56), (76, 16, 32), (248, 34, 66), (292, 14, 30), (46, 10, 40)]
    gmit = [(40, 30, 76), (206, 34, 84), (256, 16, 40), (318, 14, 34), (26, 12, 48), (222, 12, 56)]
    ledges = [(96, 120, 42, 16), (300, 86, 30, 13), (178, 40, 26, 11)]
    # top surface depth of each oval (seen from slightly above, like the Mines' shelves)
    ovals = [(cx, cy, w, max(4, int(w * 0.2))) for (cx, cy, w, h) in ledges]
    under = [(cx, cy, int(w * 0.92), h) for (cx, cy, w, h) in ledges]
    m, n = silhouette(33, cols, stal, gmit, under, jag=6.0)
    for (cx, cy, rx, ry) in ovals:                                  # oval footprint is solid too
        m |= ((wrapdx(XS, cx) / rx) ** 2 + ((YS - cy) / ry) ** 2) <= 1.0
    # broken tips on the two big stalagmites
    for (cx, cut) in ((40, 132), (206, 116)):
        m[:cut, cx - 6:cx + 7] &= False
    m = chip(m, 9, 110, (1, 4))
    P = dict(core=(16, 12, 14), mid=(20, 16, 18), tex=(25, 20, 21), edge=(30, 23, 24), rim_warm=(96, 50, 32),
             band_warm=(44, 30, 28), ao=(13, 10, 12))
    img, a = volume_shade(m, n, P, ao_rows=26, lscale=1.25)
    im = to_img(img, a); d = ImageDraw.Draw(im); px = im.load()
    # ledge tops: oval top plane with streaky texture, lit back rim, dark front lip
    TOP_HI, TOP, TOP_LO, STREAK, LIP = (112, 80, 56), (88, 63, 46), (72, 51, 39), (60, 43, 34), (40, 29, 27)
    tex = value_noise(41, 2)
    for (cx, cy, rx, ry) in ovals:
        for y in range(cy - ry, cy + ry + 1):
            for x in range(cx - rx - 1, cx + rx + 2):
                X = x % W
                e = (wrapdx(np.array(X), cx) / rx) ** 2 + ((y - cy) / ry) ** 2
                edge_noise = (tex[y, X] - 0.5) * 0.25
                if e > 1.0 + edge_noise or not (0 <= y < H): continue
                c = TOP if y <= cy else TOP_LO                      # back half lit, front half falls off
                if e > 0.72 + edge_noise and y < cy: c = TOP_HI        # back rim catches the light
                if (tex[y, X] > 0.66) and ((x + 2 * y) % 5 < 3): c = STREAK   # horizontal grain streaks
                if e > 0.8 + edge_noise and y > cy: c = LIP             # front lip
                px[X, y] = c + (255,)
        # 1px dark seam under the front lip, then the rock mass continues below
        for x in range(cx - rx + 2, cx + rx - 1):
            X = x % W
            yb = cy + int(ry * math.sqrt(max(0.0, 1 - ((x - cx) / rx) ** 2))) + 1
            if 0 <= yb < H and px[X, yb][3]: px[X, yb] = (22, 17, 19, 255)
    # rail + mine cart on the big ledge, both in the same slight top-down perspective as the oval
    lcx, lcy, lrx, lry = ovals[0]
    def inside(x, y):
        return ((x - lcx) / lrx) ** 2 + ((y - lcy) / lry) ** 2 <= 0.86
    RAIL_BACK, RAIL_FRONT, RAIL_HI, TIE, TIE_HI = (70, 66, 64), (112, 108, 104), (160, 156, 150), (40, 28, 24), (66, 46, 36)
    yb, yf = lcy - 5, lcy - 1                                       # back rail sits higher on the plane than the front one
    for x in range(lcx - lrx, lcx + lrx + 1):
        if x % 6 == 0 and inside(x, yb) and inside(x + 1, yf):         # ties: short slanted planks joining the rails
            for k, y in enumerate(range(yb - 1, yf + 2)):
                xx = x + (1 if k >= 3 else 0)
                px[xx, y] = (TIE_HI if y == yb else TIE) + (255,)
                px[xx + 1, y] = TIE + (255,)
    for x in range(lcx - lrx, lcx + lrx + 1):
        if inside(x, yb): px[x, yb] = RAIL_BACK + (255,)
        if inside(x, yf):
            px[x, yf] = RAIL_FRONT + (255,)
            if x % 7 == 3: px[x, yf] = RAIL_HI + (255,)              # a glint on the near rail
    # cart: front face (trapezoid), receding right side face, open top showing the ore load
    CF, CF_HI, CF_LO, CS, RIM, INNER = (78, 72, 70), (150, 120, 96), (52, 48, 48), (50, 46, 46), (118, 108, 102), (22, 17, 18)
    fx0, fx1, fy0, fy1 = 85, 101, yf - 9, yf - 2                   # front face box
    dx, dy = 3, -3                                                  # depth offset toward the back-right
    for y in range(fy0, fy1 + 1):                                   # front face, slightly wider at the top
        inset = (y - fy0) // 4
        for x in range(fx0 + inset, fx1 - inset + 1): px[x, y] = CF + (255,)
    for x in range(fx0, fx1 + 1): px[x, fy0] = CF_HI + (255,)        # lit top edge of the front face
    for k in range(1, 4): px[fx0 + 3, fy0 + 2 + k] = CF_LO + (255,); px[fx1 - 3, fy0 + 2 + k] = CF_LO + (255,)   # rivet straps
    for y in range(fy0 + dy, fy1 + dy + 1):                          # side face, parallelogram receding up-right
        for j in range(1, dx + 1):
            if fy0 + dy + (j - 1) <= y <= fy1 + dy + j - 1: px[fx1 + j, y] = CS + (255,)
    for j in range(0, dx + 1):                                       # top rim: back edge + right edge
        px[fx0 + j, fy0 - j] = RIM + (255,); px[fx1 + j, fy0 - j] = RIM + (255,)
    for x in range(fx0 + dx, fx1 + dx + 1): px[x, fy0 + dy] = RIM + (255,)
    for y in range(fy0 + dy + 1, fy0):                               # interior + ore heap
        for x in range(fx0 + 1 + (fy0 - y), fx1 + (fy0 - y)):
            px[x, y] = INNER + (255,)
    for (x, y, c) in ((89, fy0 - 2, (128, 116, 108)), (92, fy0 - 3, (150, 140, 132)), (95, fy0 - 4, (236, 150, 84)), (97, fy0 - 2, (128, 116, 108)),
                      (99, fy0 - 3, (200, 200, 196)), (91, fy0 - 1, (96, 88, 84)), (94, fy0 - 2, (110, 100, 94)), (96, fy0 - 5, (150, 140, 132))):
        px[x, y] = c + (255,)
    # wheels: front pair on the front rail, back pair peeking on the back rail
    for wx in (fx0 + 3, fx1 - 3):
        for (ox, oy, c) in ((-1, 0, (40, 36, 36)), (1, 0, (40, 36, 36)), (0, -1, (40, 36, 36)), (0, 1, (40, 36, 36)), (0, 0, (100, 96, 92))):
            px[wx + ox, yf - 1 + oy] = c + (255,)
        px[wx + dx, yb - 1] = (40, 36, 36, 255)
    # ore crystals on the broken stalagmite at 206
    base_y = max(y for y in range(H) if not m[y, 206]) + 1 if m[H - 1, 206] else 116
    for (cx, h, c1, c2) in ((202, 7, (176, 90, 50), (236, 156, 96)), (205, 10, (176, 90, 50), (244, 176, 110)), (209, 6, (140, 140, 138), (214, 214, 210)), (212, 4, (176, 90, 50), (236, 156, 96))):
        for k in range(h):
            px[cx, base_y - 1 - k] = c1 + (255,)
            px[cx + 1, base_y - 1 - k] = (c1[0] - 60, c1[1] - 46, c1[2] - 30, 255)
        px[cx, base_y - 1 - h] = c2 + (255,)
    return im


def main(out):
    os.makedirs(out, exist_ok=True)
    layers = {"background1-sheet": far_v2(), "background2-sheet": mid_v2(), "background3-sheet": near_v2()}
    for k, im in layers.items(): im.save(os.path.join(out, k + ".png"))
    comp = Image.new("RGBA", (W * 2, H))
    for im in layers.values():
        for i in range(2): comp.alpha_composite(im, (i * W, 0))
    comp.resize((W * 2 * 3, H * 3), Image.NEAREST).save(os.path.join(out, "caverns_composite.png"))
    mines_dir = "/Users/tuan/Documents/Game/Idle Merchant Guild Project/Idle Merchant Guild/Assets/Sprites/Dungeon/Level 1/Background"
    mc = Image.new("RGBA", (W, H))
    for k in ("background1-sheet", "background2-sheet", "background3-sheet"):
        mc.alpha_composite(Image.open(os.path.join(mines_dir, k + ".png")).convert("RGBA"))
    cc = Image.new("RGBA", (W, H))
    for im in layers.values(): cc.alpha_composite(im)
    pair = Image.new("RGBA", (W * 2 + 8, H), (0, 0, 0, 255)); pair.paste(mc, (0, 0)); pair.paste(cc, (W + 8, 0))
    pair.resize((pair.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "mines_vs_caverns.png"))
    cc.resize((W * 4, H * 4), Image.NEAREST).save(os.path.join(out, "caverns_4x.png"))
    sep = Image.new("RGBA", (W * 3 + 20, H), (255, 0, 255, 255))
    for i, im in enumerate(layers.values()): sep.alpha_composite(im, (i * (W + 10), 0))
    sep.resize((sep.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "caverns_layers.png"))


if __name__ == "__main__":
    main(sys.argv[1])
