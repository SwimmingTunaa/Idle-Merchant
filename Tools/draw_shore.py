"""Lava Shore (dungeon level 5, "Infernal Depths") parallax backgrounds, 320x192 / 8 PPU, tileable.
The first depth that isn't a wall: the cavern opens onto a lava sea. far = dark sky over a stepped orange haze,
a lava surface with floating crust plates and hot cracks (plates shrink toward the horizon), distant basalt
columns on the horizon, a lavafall pouring from the ceiling into the sea, embers in the air; mid = basalt column clusters on the shore, lit orange on the lava
side, near = black shore rocks with an orange rim, a cooled lava plate.
Human scale (adventurer ≈ 24 px). Only the lower ~126 px of a sheet shows in the band.
Run: uv run --with pillow --with numpy python3 draw_shore.py <out>"""
import sys, os, math, random
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from draw_caverns import W, H, value_noise, wrapdx, to_img

YS, XS = np.mgrid[0:H, 0:W]
FLOOR, HIDDEN = 192, 66
HORIZON = 166
SKY = [(18, 8, 10), (30, 10, 12), (48, 16, 14), (74, 26, 16), (110, 42, 18)]   # top → horizon haze
LAVA = dict(hot=(250, 200, 100), bright=(222, 124, 44), mid=(166, 72, 26), dim=(118, 42, 18), cool=(86, 30, 16), crust=(46, 16, 14), crust_d=(30, 10, 9), crust_l=(70, 26, 20))
BASALT = dict(light=(56, 36, 36), mid=(34, 22, 24), dark=(22, 14, 16), edge=(10, 6, 8), rim=(210, 96, 40), rim2=(120, 50, 26))
NEAR = dict(light=(30, 18, 20), mid=(18, 11, 13), dark=(10, 6, 8), edge=(5, 3, 4), rim=(190, 86, 36), rim2=(100, 40, 20))


def canvas(): return Image.new("L", (W, H), 0)


def column_cluster(mask, cx, base_y, heights, widths, step=3):
    """Hexagonal basalt columns side by side: flat stepped tops. Returns the column rects (x0, w, h)."""
    d = ImageDraw.Draw(mask); x = cx - sum(widths) // 2; rects = []
    for h, w in zip(heights, widths):
        for off in (-W, 0, W):
            d.rectangle([x + off, base_y - h, x + off + w - 1, base_y], fill=255)
            d.polygon([(x + off, base_y - h), (x + off + w // 2, base_y - h - step), (x + off + w - 1, base_y - h)], fill=255)   # bevelled cap
        rects.append((x, w, h)); x += w
    return rects


def shade_columns(rects, P, img, alpha, base_y=FLOOR, seed=81, depth_order=None):
    """Each column is a prism: dark left face, mid centre, lit right face (the lava side), a 1px bright rim on the
    right edge, a bevelled cap with a lit top, horizontal fracture lines, and the lower part warming toward the
    lava's bounce light. Columns flagged as 'back' sit a tone darker so the cluster reads in depth."""
    r = random.Random(seed); g = value_noise(seed, 2)
    WARM = [(74, 36, 28), (100, 46, 30), (140, 64, 34)]
    for i, (x0, w, h) in enumerate(rects):
        back = depth_order[i] if depth_order else (i % 2 == 1)
        light, mid, dark = (P["mid"], P["dark"], P["edge"]) if back else (P["light"], P["mid"], P["dark"])
        top = base_y - h
        for x in range(x0, x0 + w):
            X = x % W; f = (x - x0) / max(w - 1, 1)
            tone = dark if f < 0.3 else (mid if f < 0.68 else light)
            col = np.array(tone, float)
            for y in range(top, min(base_y + 1, H)):
                c = col.copy()
                if g[y, X] > 0.78: c = c * 0.85                                 # pitting
                warm = (y - (base_y - 26)) / 26.0                               # bounce light from the lava below
                if warm > 0.66: c = c * 0.5 + np.array(WARM[2 if f > 0.68 else 1]) * 0.5
                elif warm > 0.33: c = c * 0.7 + np.array(WARM[1 if f > 0.68 else 0]) * 0.3
                elif warm > 0: c = c * 0.85 + np.array(WARM[0]) * 0.15
                img[y, X] = c; alpha[y, X] = 255
            img[top, X] = light if not back else mid                             # lit cap edge
        img[top + 1, (x0 + 1) % W:(x0 + w - 1) % W if x0 + w - 1 <= W else W] = mid
        img[top:min(base_y + 1, H), x0 % W] = P["edge"]                         # left outline
        img[top:min(base_y + 1, H), (x0 + w - 1) % W] = P["rim"] if not back else P["rim2"]   # lava-side rim
        img[base_y - 8:min(base_y + 1, H), (x0 + w - 1) % W] = P["rim"]
        y = top + r.randint(6, 14)
        while y < base_y - 3:                                                   # fracture lines
            xs = [(x % W) for x in range(x0 + 1, x0 + w - 1)]
            img[y, xs] = P["edge"]; img[y + 1, xs] = np.array(img[y + 1, xs]) * 0.75 + np.array(light, float) * 0.25
            y += r.randint(9, 18)
    # cap bevels drawn by the mask but not coloured yet: fill any mask pixel still black with the light tone
    return


# ───────────────────────── FAR: sky, haze, lava sea, distant columns, embers ─────────────────────────
def far(frame=0, nframes=6):
    """frame/nframes: the fall's streaks scroll down, the spray shifts and the embers drift up and flicker."""
    img = np.zeros((H, W, 3))
    # the far cavern wall: rock texture (mottling + strata) stepping brighter toward the lava that lights it
    n = value_noise(91, 10); mot = value_noise(101, 9); fine = value_noise(102, 2)
    lvl = np.zeros((H, W), int)
    for i in range(len(SKY)):
        y_top = HORIZON - 22 * (len(SKY) - i)
        lvl[YS >= (y_top + (n - 0.5) * 10)] = i
    pal = np.array(SKY, float)
    img[:] = pal[lvl]
    up = np.clip(lvl - 1, 0, len(SKY) - 1); dn = np.clip(lvl + 1, 0, len(SKY) - 1)
    img[mot > 0.74] = pal[dn][mot > 0.74] * 0.35 + pal[lvl][mot > 0.74] * 0.65    # a few broad lighter patches
    img[mot < 0.24] = pal[up][mot < 0.24] * 0.4 + pal[lvl][mot < 0.24] * 0.6      # a few broad darker patches
    img[fine > 0.965] = pal[up][fine > 0.965]                                   # sparse pocks
    for k, (y0, amp, f) in enumerate(((HIDDEN + 18, 3, 2), (HIDDEN + 40, 4, 3), (HIDDEN + 62, 3, 1), (HIDDEN + 84, 4, 2))):   # strata
        for x in range(W):
            y = int(y0 + amp * math.sin(2 * math.pi * f * x / W + k) + (n[y0, x] - 0.5) * 6)
            if 0 <= y < HORIZON: img[y, x] = pal[max(lvl[y, x] - 1, 0)]
            if 0 <= y + 1 < HORIZON: img[y + 1, x] = pal[min(lvl[y, x] + 1, len(SKY) - 1)] * 0.7 + pal[lvl[y, x]] * 0.3
    img[YS >= HORIZON] = LAVA["mid"]
    # distant basalt columns on the horizon, two ranks, the farther rank darker
    far_rank = canvas(); near_rank = canvas()
    r = random.Random(92)
    for cx in (30, 110, 200, 275):
        column_cluster(far_rank, cx, HORIZON + 2, [r.randint(10, 30) for _ in range(4)], [r.randint(4, 7) for _ in range(4)], step=2)
    for cx in (70, 160, 245, 318):
        column_cluster(near_rank, cx, HORIZON + 4, [r.randint(22, 52) for _ in range(5)], [r.randint(5, 9) for _ in range(5)], step=3)
    fr = np.array(far_rank) > 0; nr = np.array(near_rank) > 0
    img[fr] = (52, 18, 14); img[fr & ~np.roll(fr, 1, 0)] = (96, 40, 24)
    img[fr & ~np.roll(fr, -1, 1)] = (80, 32, 20)
    img[nr] = (30, 12, 12); img[nr & ~np.roll(nr, 1, 0)] = (74, 30, 20)
    img[nr & ~np.roll(nr, -1, 1)] = (120, 50, 26)                              # lava-side edges lit
    img[nr & np.roll(nr, 1, 1) & ~np.roll(nr, 2, 1)] = (22, 8, 9)               # shadow face on the other side
    for y in range(HORIZON - 44, HORIZON, 9):                                   # fracture lines on the nearer rank
        img[y, nr[y]] = (18, 7, 8); img[y + 1, nr[y + 1] & nr[y]] = (48, 20, 16)
    # a far shoreline: low rocks on the horizon between the columns
    for (cx, w) in ((50, 9), (135, 6), (178, 11), (215, 5), (258, 8), (296, 6)):
        for x in range(cx - w, cx + w + 1):
            h = int(3 * (1 - ((x - cx) / w) ** 2)) + 1
            X = x % W
            img[HORIZON - h:HORIZON + 1, X] = (36, 14, 12); img[HORIZON - h, X] = (90, 36, 22)
    # a lavafall: pours from a lip of rock at the hidden ceiling down into the sea, in front of the horizon ranks
    fx, fw = 130, 6
    fl = value_noise(104, 3)
    for y in range(HIDDEN - 8, HORIZON + 1):
        wob = 1 if (y // 26) % 2 else 0                                          # a single gentle one-pixel step, not a wiggle
        for x in range(fx - fw + wob, fx + fw + 1 + wob):
            X = x % W; f = abs(x - fx - wob) / float(fw)
            c = LAVA["hot"] if f < 0.4 else (LAVA["bright"] if f < 0.8 else LAVA["mid"])
            ya = (y - frame * (H // nframes) // 4) % H                             # streaks scroll down with the frame
            if fl[ya, X] > 0.7 and f > 0.3: c = LAVA["bright"] if c == LAVA["hot"] else LAVA["dim"]   # flow streaks
            if ((y - frame * 2 + x * 3) % 11 == 0) and f < 0.5: c = LAVA["hot"]
            img[y, X] = c
        img[y, (fx - fw - 1 + wob) % W] = LAVA["crust_l"]; img[y, (fx + fw + 1 + wob) % W] = LAVA["crust"]   # crusted edges
    # glow on the wall either side, stepped by distance
    for dx, k in ((26, 0.2), (16, 0.45), (10, 0.8)):
        sel = (np.abs(wrapdx(XS, fx)) < dx) & (np.abs(wrapdx(XS, fx)) > fw + 1) & (YS > HIDDEN - 8) & (YS < HORIZON) & ~(fr | nr)
        img[sel] = np.clip(img[sel] * (1 + k) * np.array((1.0, 0.86, 0.7)), 0, 255)
    # the rock lip it pours over, dark, with a lit underside
    lip = canvas(); ld = ImageDraw.Draw(lip)
    ld.polygon([(fx - 18, HIDDEN - 12), (fx + 18, HIDDEN - 12), (fx + 14, HIDDEN - 2), (fx + fw + 2, HIDDEN + 2), (fx - fw - 2, HIDDEN + 2), (fx - 14, HIDDEN - 1)], fill=255)
    lm = np.array(lip) > 0
    img[lm] = (14, 6, 8); img[lm & ~np.roll(lm, -1, 0)] = (110, 44, 24)
    # splash where it hits: a bright pool and spray
    for y in range(HORIZON, HORIZON + 7):
        hw = 12 - (y - HORIZON)
        for x in range(fx - hw, fx + hw + 1): img[y, x % W] = LAVA["hot"] if abs(x - fx) < hw * 0.6 else LAVA["bright"]
    spray = [(-14, -3), (-10, -6), (-6, -9), (7, -8), (11, -5), (15, -2), (-17, -1), (18, -4), (0, -12), (4, -11), (-3, -14), (9, -11)]
    for i, (dx, dy) in enumerate(spray):                                        # spray rises and fades with the frame
        ph = (i + frame) % 4
        if ph == 3: continue
        img[HORIZON + dy - ph, (fx + dx + (ph if dx > 0 else -ph)) % W] = LAVA["hot"] if ph < 2 else LAVA["bright"]
    # the lava surface: liquid first — row-banded flow with thin bright streaks — then sparse rounded crust
    # islands with a glowing rim where they meet the melt, cooling to dark against the shore
    rows = YS >= HORIZON
    depth = np.clip((YS - HORIZON) / float(FLOOR - HORIZON), 0, 1)              # 0 horizon → 1 shore
    rowband = value_noise(99, 4)[:, 0]                                          # one value per row → flow banding
    liquid = np.zeros((H, W, 3)); liquid[:] = LAVA["mid"]
    for y in range(HORIZON, H):
        v = rowband[y] + 0.3 * math.sin(y * 0.9)
        liquid[y] = LAVA["bright"] if v > 0.55 else (LAVA["dim"] if v < 0.35 else LAVA["mid"])
    flow = value_noise(100, 5)
    liquid[(flow > 0.72) & rows] = LAVA["bright"]
    for _ in range(90):                                                         # streaks: thin yellow lines along the flow
        x, y, ln = r.randrange(W), r.randrange(HORIZON + 1, H - 2), r.randint(4, 16)
        for k in range(ln): liquid[y, (x + k) % W] = LAVA["hot"] if depth[y, 0] < 0.6 else LAVA["bright"]
    splash = rows & (YS < HORIZON + 7) & (np.abs(wrapdx(XS, fx)) <= 12 - (YS - HORIZON))
    img[rows & ~(fr | nr) & ~splash] = liquid[rows & ~(fr | nr) & ~splash]
    coarse = value_noise(93, 9)
    island = rows & (coarse > 0.6 + 0.08 * (1 - depth)) & ~(fr | nr) & (YS > HORIZON + 2) & ~splash
    for _ in range(2):                                                          # round the islands: erode then dilate
        island &= np.roll(island, 1, 1) & np.roll(island, -1, 1) & np.roll(island, 1, 0) & np.roll(island, -1, 0)
    for _ in range(2):
        island |= np.roll(island, 1, 1) | np.roll(island, -1, 1) | np.roll(island, 1, 0) | np.roll(island, -1, 0)
    island &= rows & ~(fr | nr) & (YS > HORIZON + 1)
    img[island] = LAVA["crust"]
    img[island & ~np.roll(island, 1, 0)] = LAVA["crust_l"]
    rim = ~island & rows & (np.roll(island, 1, 1) | np.roll(island, -1, 1) | np.roll(island, 1, 0) | np.roll(island, -1, 0)) & ~(fr | nr)
    img[rim] = LAVA["hot"]                                                      # the melt glows hottest against the crust
    img[rows & (YS < HORIZON + 1) & ~(fr | nr) & ~splash] = LAVA["hot"]        # horizon line
    for _ in range(14):                                                         # bubbles: tiny rings on the melt
        x, y = r.randrange(W), r.randrange(HORIZON + 4, FLOOR - 6)
        if island[y, x] or (fr | nr)[y, x]: continue
        for (dx, dy) in ((-1, 0), (1, 0), (0, -1), (0, 1)): img[y + dy, (x + dx) % W] = LAVA["hot"]
        img[y, x] = LAVA["dim"]
    inner = island & np.roll(island, 1, 1) & np.roll(island, -1, 1) & np.roll(island, 1, 0) & np.roll(island, -1, 0)
    img[inner & (fine > 0.74)] = LAVA["crust_d"]                                 # pitted crust
    img[inner & (fine < 0.14)] = LAVA["dim"]                                     # cracks in the crust glowing through
    shore = rows & (YS >= FLOOR - 4) & ~(fr | nr)
    img[shore] = LAVA["cool"]; img[shore & (YS == FLOOR - 4)] = LAVA["bright"]  # cooled rind at the shore, lit where it meets the melt
    img[shore & (YS >= FLOOR - 2)] = LAVA["crust"]
    # embers: single bright pixels drifting up with the frame, each flickering on its own phase
    er = random.Random(105)
    embers = [(er.randrange(W), er.randrange(HIDDEN, HORIZON), er.randrange(3), er.random() < 0.4) for _ in range(70)]
    for i, (x, y0, ph, hot) in enumerate(embers):
        y = HIDDEN + (y0 - HIDDEN - frame * 2 + ((i % 3) - 1) * 0) % (HORIZON - HIDDEN)
        x = (x + (1 if (frame + i) % 4 == 0 else 0)) % W
        if (fr | nr)[y, x]: continue
        if (ph + frame) % 3 == 2: img[y, x] = LAVA["dim"]; continue             # dim phase
        img[y, x] = LAVA["hot"] if hot else LAVA["bright"]
        if (ph + frame) % 3 == 0 and y + 1 < H and not (fr | nr)[y + 1, x]: img[y + 1, x] = LAVA["dim"]
    # heat shimmer: a few single pixels lifted a step in the haze bands
    sh = value_noise(95, 2)
    hz = (YS > HORIZON - 30) & (YS < HORIZON) & (sh > 0.86) & ~(fr | nr)
    img[hz] = SKY[4]
    return to_img(img)


# ───────────────────────── MID: shore columns ─────────────────────────
def mid():
    img = np.zeros((H, W, 3)); a = np.zeros((H, W))
    r = random.Random(96)
    m = canvas(); rects = []; order = []
    for (cx, n, hr, wr, st, depth) in ((60, 4, (40, 96), (7, 10), 3, (True, False, True, False)), (230, 3, (30, 70), (6, 9), 3, (False, True, False)), (300, 3, (12, 26), (5, 8), 2, (True, False, False))):
        rects += column_cluster(m, cx, FLOOR, [r.randint(*hr) for _ in range(n)], [r.randint(*wr) for _ in range(n)], step=st); order += list(depth)
    shade_columns(rects, BASALT, img, a, seed=97, depth_order=order)
    mm = np.array(m) > 0
    caps = mm & (a == 0)                                                        # the bevel triangles above each column's top row
    img[caps] = BASALT["light"]; a[caps] = 255
    for (x0, w, h) in rects:                                                    # glowing cracks in the lower part of the taller columns
        if h < 30: continue
        x, y = x0 + r.randint(1, max(1, w - 2)), FLOOR - r.randint(4, 12)
        for _ in range(r.randint(6, 14)):
            X = x % W
            if mm[y, X] and y < FLOOR: img[y, X] = LAVA["bright"] if y > FLOOR - 10 else LAVA["dim"]
            y -= 1; x += r.choice((-1, 0, 0, 1))
    rub = canvas(); rd = ImageDraw.Draw(rub)
    for (x0, w, h) in rects:
        for k in range(r.randint(1, 3)):
            cx = x0 + r.randint(-6, w + 6); rw = r.randint(2, 4)
            rd.ellipse([cx - rw, FLOOR - rw, cx + rw, FLOOR + 1], fill=255)
    rm = (np.array(rub) > 0) & ~mm
    img[rm] = BASALT["dark"]; img[rm & ~np.roll(rm, 1, 0)] = BASALT["rim2"]; a[rm] = 255
    im = to_img(img, a); px = im.load(); d = ImageDraw.Draw(im)
    return im


# ───────────────────────── NEAR: shore rocks, cooled plate ─────────────────────────
def near():
    img = np.zeros((H, W, 3)); a = np.zeros((H, W))
    m = canvas(); d = ImageDraw.Draw(m)
    d.polygon([(100, FLOOR), (104, FLOOR - 10), (112, FLOOR - 16), (122, FLOOR - 12), (128, FLOOR - 15), (136, FLOOR - 6), (140, FLOOR)], fill=255)
    d.polygon([(170, FLOOR), (174, FLOOR - 6), (182, FLOOR - 9), (190, FLOOR - 4), (194, FLOOR)], fill=255)
    d.polygon([(250, FLOOR), (254, FLOOR - 14), (262, FLOOR - 22), (268, FLOOR - 18), (276, FLOOR - 20), (284, FLOOR - 8), (290, FLOOR)], fill=255)
    d.polygon([(8, FLOOR), (12, FLOOR - 5), (22, FLOOR - 7), (30, FLOOR - 3), (34, FLOOR)], fill=255)
    d.ellipse([200, FLOOR - 5, 236, FLOOR + 3], fill=255)                       # a cooled lava plate on the shore
    mm = np.array(m) > 0
    col = np.zeros((H, W, 3)); col[mm] = NEAR["mid"]
    g = value_noise(98, 3); col[mm & (g > 0.7)] = NEAR["dark"]; col[mm & (g < 0.25)] = NEAR["light"]
    edge = mm & ~(np.roll(mm, 1, 1) & np.roll(mm, -1, 1) & np.roll(mm, 1, 0) & np.roll(mm, -1, 0))
    col[edge] = NEAR["edge"]
    col[edge & ~np.roll(mm, 1, 0)] = NEAR["rim2"]                               # top edges catch the glow
    col[edge & ~np.roll(mm, -1, 1)] = NEAR["rim"]                               # lava-facing (right) edges burn brighter
    facet = value_noise(103, 4)
    inner = mm & ~edge
    col[inner & (facet > 0.62)] = NEAR["light"]; col[inner & (facet < 0.3)] = NEAR["dark"]   # faceted faces
    seam = inner & ((facet > 0.6) != np.roll(facet > 0.6, 1, 1))
    col[seam] = NEAR["edge"]
    col[inner & (np.roll(facet > 0.62, 1, 0) & ~(facet > 0.62))] = NEAR["rim2"]  # facet tops catch the glow
    plate = mm & (YS >= FLOOR - 5) & (XS >= 200) & (XS <= 236)
    col[plate] = NEAR["dark"]; col[plate & (g > 0.6)] = NEAR["rim2"]; col[plate & (g > 0.78)] = NEAR["rim"]   # cracks still glowing
    img[mm] = col[mm]; a[mm] = 255
    return to_img(img, a)


def main(out):
    os.makedirs(out, exist_ok=True)
    NF = 6
    frames = [far(f, NF) for f in range(NF)]
    layers = {"background1-sheet": frames[0], "background2-sheet": mid(), "background3-sheet": near()}
    for k, im in layers.items(): im.save(os.path.join(out, k + ".png"))
    for f in range(1, NF): frames[f].save(os.path.join(out, f"background1-sheet_f{f}.png"))
    strip = Image.new("RGBA", (W * NF, H - HIDDEN))
    for f in range(NF): strip.alpha_composite(frames[f].crop((0, HIDDEN, W, H)), (f * W, 0))
    strip.resize((strip.width * 2, strip.height * 2), Image.NEAREST).save(os.path.join(out, "shore_frames.png"))
    gif = [fr_.crop((100, HIDDEN, 220, H)).resize((480, (H - HIDDEN) * 4), Image.NEAREST).convert("P") for fr_ in frames]
    gif[0].save(os.path.join(out, "shore_fall.gif"), save_all=True, append_images=gif[1:], duration=160, loop=0)
    comp = Image.new("RGBA", (W * 2, H))
    for im in layers.values():
        for i in range(2): comp.alpha_composite(im, (i * W, 0))
    comp.save(os.path.join(out, "shore_full.png"))
    band = comp.crop((0, HIDDEN, W * 2, H)); band.resize((band.width * 3, band.height * 3), Image.NEAREST).save(os.path.join(out, "shore_band.png"))
    comp.crop((0, HIDDEN, W, H)).resize((W * 4, (H - HIDDEN) * 4), Image.NEAREST).save(os.path.join(out, "shore_4x.png"))
    chk = comp.crop((0, HIDDEN, W, H)).copy(); dd = ImageDraw.Draw(chk)
    dd.rectangle([150, FLOOR - HIDDEN - 24, 160, FLOOR - HIDDEN - 1], outline=(80, 200, 255, 255))
    chk.resize((W * 4, (H - HIDDEN) * 4), Image.NEAREST).save(os.path.join(out, "shore_scale.png"))
    sep = Image.new("RGBA", (W * 3 + 20, H - HIDDEN), (255, 0, 255, 255))
    for i, im in enumerate(layers.values()): sep.alpha_composite(im.crop((0, HIDDEN, W, H)), (i * (W + 10), 0))
    sep.resize((sep.width * 2, sep.height * 2), Image.NEAREST).save(os.path.join(out, "shore_layers.png"))


if __name__ == "__main__":
    main(sys.argv[1])
