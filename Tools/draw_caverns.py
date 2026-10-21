"""Deep Caverns parallax backgrounds, 320x192, horizontally tileable, same structure as the Mines (SSW SET4):
far = opaque wall with glow pockets (magma fissures instead of daylight), mid + near = dark see-through
silhouettes. Flat 3-4 tone shapes, no outlines. Run: uv run --with pillow --with numpy python3 draw_caverns.py <out>"""
import sys, os, math, random
import numpy as np
from PIL import Image

W, H = 320, 192


def pnoise1(x, freqs, seed):
    """Periodic 1-D noise over W: sum of sines with integer frequencies (wraps seamlessly)."""
    r = random.Random(seed)
    v = 0.0
    for f, a in freqs:
        ph = r.random() * 2 * math.pi
        v += a * math.sin(2 * math.pi * f * x / W + ph)
    return v


def value_noise(seed, cell=8):
    """2-D value noise, periodic in x, as an HxW array in 0..1."""
    rng = np.random.default_rng(seed)
    gx, gy = W // cell, H // cell + 2
    g = rng.random((gy, gx))
    ys, xs = np.mgrid[0:H, 0:W]
    fx, fy = xs / cell, ys / cell
    x0 = np.floor(fx).astype(int); y0 = np.floor(fy).astype(int)
    tx, ty = fx - x0, fy - y0
    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty)
    a = g[y0, x0 % gx]; b = g[y0, (x0 + 1) % gx]; c = g[y0 + 1, x0 % gx]; d = g[y0 + 1, (x0 + 1) % gx]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def wrapdx(x, cx):
    d = (x - cx) % W
    return np.where(d > W / 2, d - W, d)


def to_img(rgb, alpha=None):
    a = np.full((H, W), 255, np.uint8) if alpha is None else alpha.astype(np.uint8)
    return Image.fromarray(np.dstack([rgb.astype(np.uint8), a]), "RGBA").copy()


# ───────────────────────── FAR: umber wall + magma fissure pockets ─────────────────────────
def far():
    BASE = np.array((58, 44, 40)); R1, R2, R3 = np.array((66, 50, 45)), np.array((78, 60, 52)), np.array((94, 73, 62))
    BANDS = [np.array(c) for c in ((96, 52, 36), (156, 78, 38), (214, 128, 56), (244, 194, 120))]
    img = np.zeros((H, W, 3)); img[:] = BASE
    ys, xs = np.mgrid[0:H, 0:W]
    coarse = value_noise(3, 16); fine = value_noise(4, 4)
    # broad warm-dark mottling so the wall has depth
    img[coarse > 0.62] = R1
    # fissure pockets: openings in the wall; the glow lives low in each, rock teeth hang from the top edge
    pockets = [(84, 62, 22, 11), (148, 112, 20, 10), (240, 46, 26, 12), (300, 120, 18, 9), (16, 104, 16, 8), (208, 150, 16, 7), (150, 28, 14, 6)]
    for i, (cx, cy, rx, ry) in enumerate(pockets):
        dx = wrapdx(xs, cx).astype(float); dy = (ys - cy).astype(float)
        teeth = (2.5 + 2.5 * np.abs(np.sin(dx * 0.45 + i * 1.7))) * (dy < 0)
        d = (dx / rx) ** 2 + ((dy + teeth + 3 * (fine - 0.5)) / ry) ** 2
        img[d < 1.3] = BANDS[0]
        img[d < 1.0] = BANDS[1]
        img[(d < 0.62) & (dy > -ry * 0.35)] = BANDS[2]
        img[(d < 0.28) & (dy > -ry * 0.05)] = BANDS[3]
    glow = np.zeros((H, W), bool)
    for b in BANDS[1:]: glow |= (img == b).all(axis=2)
    # rubble: organic lumps from thresholded fine noise, denser around the pockets' lower rims
    near_pk = np.zeros((H, W))
    for (cx, cy, rx, ry) in pockets:
        dx = wrapdx(xs, cx).astype(float); dy = (ys - cy - ry).astype(float)
        near_pk = np.maximum(near_pk, np.exp(-(dx / (rx * 1.6)) ** 2 - (dy / 10.0) ** 2))
    lump = fine + 0.35 * near_pk
    body = (lump > 0.78) & ~glow
    img[body] = R2
    top = body & ~np.roll(body, 1, 0)
    img[top] = R3
    img[body & ~np.roll(body, -1, 0)] = R1
    # a few broken magma threads, dim
    for k, (y0, amp, f) in enumerate(((84, 5, 2), (150, 4, 3))):
        for x in range(W):
            if (x // 11 + k) % 4 == 0: continue
            y = int(y0 + amp * math.sin(2 * math.pi * f * x / W + k) + 1.5 * math.sin(x * 0.27 + k))
            if not glow[y, x]: img[y, x] = BANDS[0] if (x + k) % 4 else BANDS[1]
    return to_img(img)


# ───────────────────────── silhouette helper ─────────────────────────
def silhouette(seed, columns, stalactites, stalagmites, ledges, jag=5.0):
    ys, xs = np.mgrid[0:H, 0:W]
    n = value_noise(seed, 3); nf = value_noise(seed + 7, 2); nc = value_noise(seed + 1, 12)
    rough = (n - 0.5) * jag + (nf - 0.5) * jag * 1.2 + (nc - 0.5) * jag * 1.6
    m = np.zeros((H, W), bool)
    for (cx, w, waist) in columns:
        dx = np.abs(wrapdx(xs, cx)).astype(float)
        wob = 0.25 * w * np.sin(ys / 13.0 + cx) + 0.2 * w * np.sin(ys / 5.0 + cx * 0.3)
        prof = w * (1 - waist * np.exp(-((ys - H * 0.5) / (H * 0.3)) ** 2)) + wob
        m |= dx < prof + rough
    for (cx, w, ln) in stalactites:
        dx = np.abs(wrapdx(xs, cx)).astype(float)
        taper = np.clip(1 - dx / w, 0, 1) ** 0.7
        m |= ys < ln * taper + rough * (dx < w)
    for (cx, w, ln) in stalagmites:
        dx = np.abs(wrapdx(xs, cx)).astype(float)
        taper = np.clip(1 - dx / w, 0, 1) ** 0.7
        m |= (H - 1 - ys) < ln * taper + rough * (dx < w)
    for (cx, cy, w, h) in ledges:
        dx = np.abs(wrapdx(xs, cx)).astype(float)
        under = (ys >= cy) & (ys < cy + h * np.clip(1 - (dx / w) ** 2, 0, 1) + np.abs(rough) * 0.6)
        m |= under & (dx < w + rough * 0.5)
    # remove isolated speckles from the noise
    nb = sum(np.roll(np.roll(m, a, 0), b, 1).astype(int) for a in (-1, 0, 1) for b in (-1, 0, 1))
    m = (m & (nb >= 4)) | (~m & (nb >= 7))
    return m, n


def shade(m, n, base, tex, rim=None, rim_dir=(-1, -1), crack=None):
    img = np.zeros((H, W, 3)); img[m] = base
    img[m & (n > 0.62)] = tex
    a = np.where(m, 255, 0)
    if rim is not None:                                            # 1px light edge on the glow-facing side
        sx, sy = rim_dir
        edge = m & ~np.roll(np.roll(m, -sy, 0), -sx, 1)
        img[edge] = rim
    if crack is not None:
        for (pts, hot, warm) in crack:
            for (x, y) in pts:
                X = x % W
                if 0 <= y < H and m[y, X]:
                    img[y, X] = hot
                    if 0 <= y - 1 < H and m[y - 1, X]: img[y - 1, X] = warm
    return to_img(img, a)


def crack_path(x0, y0, steps, seed):
    r = random.Random(seed); pts = []; x, y = x0, y0
    for _ in range(steps):
        pts.append((x, y)); y += 1
        x += r.choice((-1, 0, 0, 1))
        if r.random() < 0.15: pts.append((x + r.choice((-1, 1)), y))
    return pts


# ───────────────────────── MID: rust-brown columns, stalactites, magma cracks, ore glints ─────────────────────────
def mid():
    cols = [(50, 24, 0.4), (178, 30, 0.45), (270, 20, 0.35)]
    stal = [(104, 22, 70), (130, 12, 40), (226, 24, 84), (304, 14, 44), (14, 16, 50)]
    gmit = [(112, 24, 70), (236, 18, 56), (8, 14, 40), (150, 10, 30)]
    m, n = silhouette(21, cols, stal, gmit, [])
    cracks = [(crack_path(48, 40, 50, 1), (210, 100, 40), (120, 54, 30)),
              (crack_path(180, 80, 60, 2), (226, 128, 52), (130, 58, 32))]
    im = shade(m, n, base=(36, 27, 27), tex=(42, 31, 30), rim=(70, 46, 38), rim_dir=(-1, -1), crack=cracks)
    px = im.load(); rng = random.Random(8)
    for _ in range(40):                                            # iron ore glints in the rock
        x, y = rng.randrange(W), rng.randrange(H)
        if px[x, y][3] and px[x, y][:3] != (226, 110, 44):
            px[x, y] = (150, 140, 132, 255)
            if x + 1 < W and px[x + 1, y][3]: px[x + 1, y] = (110, 100, 96, 255)
    return im


# ───────────────────────── NEAR: darkest framing — ledges, big stalagmites, warm rim ─────────────────────────
def near():
    cols = [(148, 16, 0.3)]
    stal = [(30, 30, 56), (76, 16, 32), (248, 34, 66), (292, 14, 30)]
    gmit = [(40, 30, 76), (206, 34, 84), (256, 16, 40), (318, 14, 34)]
    ledges = [(96, 120, 40, 12), (300, 86, 28, 10), (178, 40, 24, 8)]
    m, n = silhouette(33, cols, stal, gmit, ledges, jag=6.0)
    im = shade(m, n, base=(20, 16, 18), tex=(25, 20, 21), rim=(104, 54, 32), rim_dir=(0, -1))
    # ledge tops get a lighter strip, like the Mines' near-layer shelves
    px = im.load()
    for (cx, cy, w, h) in ledges:
        for x in range(cx - w + 2, cx + w - 1):
            X = x % W
            if px[X, cy][3]: px[X, cy] = (92, 62, 44, 255)
            if px[X, cy + 1][3]: px[X, cy + 1] = (62, 44, 36, 255)
    return im


def main(out):
    os.makedirs(out, exist_ok=True)
    layers = {"background1-sheet": far(), "background2-sheet": mid(), "background3-sheet": near()}
    for k, im in layers.items(): im.save(os.path.join(out, k + ".png"))
    # composite, tiled twice to prove the seam, at 3x
    comp = Image.new("RGBA", (W * 2, H))
    for im in layers.values():
        for i in range(2): comp.alpha_composite(im, (i * W, 0))
    comp.resize((W * 2 * 3, H * 3), Image.NEAREST).save(os.path.join(out, "caverns_composite.png"))
    # side-by-side with the Mines (same composite)
    mines_dir = "/Users/tuan/Documents/Game/Idle Merchant Guild Project/Idle Merchant Guild/Assets/Sprites/Dungeon/Level 1/Background"
    mc = Image.new("RGBA", (W, H))
    for k in ("background1-sheet", "background2-sheet", "background3-sheet"):
        mc.alpha_composite(Image.open(os.path.join(mines_dir, k + ".png")).convert("RGBA"))
    cc = Image.new("RGBA", (W, H))
    for im in layers.values(): cc.alpha_composite(im)
    pair = Image.new("RGBA", (W * 2 + 8, H), (0, 0, 0, 255)); pair.paste(mc, (0, 0)); pair.paste(cc, (W + 8, 0))
    pair.resize((pair.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "mines_vs_caverns.png"))
    # the three layers separately on magenta for inspection
    sep = Image.new("RGBA", (W * 3 + 20, H), (255, 0, 255, 255))
    for i, im in enumerate(layers.values()): sep.alpha_composite(im, (i * (W + 10), 0))
    sep.resize((sep.width * 2, H * 2), Image.NEAREST).save(os.path.join(out, "caverns_layers.png"))


if __name__ == "__main__":
    main(sys.argv[1])
