"""sfxr-style retro SFX synth -> 44.1kHz 16-bit mono WAVs.
Regenerate: uv run --with numpy python3 Tools/gen_sfx.py Assets/Audio/SFX"""
import sys, os, wave
import numpy as np

SR = 44100
rng = np.random.default_rng(7)  # deterministic: regenerating gives identical files


def t(dur): return np.arange(int(SR * dur)) / SR


def phase(freq):  # per-sample freq array -> running phase (cycles), so sweeps stay click-free
    return np.cumsum(np.asarray(freq, float)) / SR


def sine(freq, n): return np.sin(2 * np.pi * phase(np.full(n, freq) if np.isscalar(freq) else freq))
def square(freq, n, duty=0.5):
    p = phase(np.full(n, freq) if np.isscalar(freq) else freq) % 1.0
    return np.where(p < duty, 1.0, -1.0)
def triangle(freq, n):
    p = phase(np.full(n, freq) if np.isscalar(freq) else freq) % 1.0
    return 4 * np.abs(p - 0.5) - 1
def noise(n): return rng.uniform(-1, 1, n)


def sh_noise(freq):  # sfxr noise: sample-and-hold random at a (sweepable) rate -> "pitched" noise
    p = phase(freq)
    idx = np.floor(p * 2).astype(int)
    vals = rng.uniform(-1, 1, idx[-1] + 2)
    return vals[idx]


def lowpass(x, cut):  # one-pole, cut scalar or per-sample
    cut = np.broadcast_to(np.asarray(cut, float), x.shape)
    a = 1 - np.exp(-2 * np.pi * cut / SR)
    y = np.empty_like(x); s = 0.0
    for i in range(len(x)):
        s += a[i] * (x[i] - s); y[i] = s
    return y


def highpass(x, cut): return x - lowpass(x, cut)
def bandpass(x, lo, hi): return lowpass(lowpass(highpass(highpass(x, lo), lo), hi), hi)  # 2-pole each side
def decay(n, tau): return np.exp(-np.arange(n) / (SR * tau))


def pad(*parts):  # (offset_seconds, signal) -> mixed
    n = max(int(o * SR) + len(s) for o, s in parts)
    out = np.zeros(n)
    for o, s in parts:
        i = int(o * SR); out[i:i + len(s)] += s
    return out


def finish(x, peak_db=-6.0):
    x = x - np.mean(x)                       # no DC
    fi, fo = int(SR * 0.001), int(SR * 0.005)
    x[:fi] *= np.linspace(0, 1, fi)          # 1 ms fade-in
    x[-fo:] *= np.linspace(1, 0, fo)         # 5 ms fade-out: no end click
    return x / np.max(np.abs(x)) * 10 ** (peak_db / 20)


# ---- recipes ----

def coin():
    a = t(0.045); b = t(0.11)
    blip1 = sine(1318.5, len(a)) * decay(len(a), 0.025)           # E6
    blip2 = sine(1975.5, len(b)) * decay(len(b), 0.04)            # B6
    blip2 += 0.25 * sine(3951.0, len(b)) * decay(len(b), 0.02)    # tiny sparkle partial
    return pad((0, blip1), (0.04, blip2))


def click_hit():
    n = len(t(0.08))
    thump = sine(np.linspace(160, 60, n), n) * decay(n, 0.025)
    m = len(t(0.03))
    burst = lowpass(lowpass(noise(m), 1800), 1800) * decay(m, 0.008) * 2.5  # dull 'thock', not hiss
    return pad((0, thump), (0, burst))


def kill():
    m = len(t(0.035))
    crack = highpass(noise(m), 1200) * decay(m, 0.01)              # sharp attack
    n = len(t(0.16))
    body = sh_noise(np.geomspace(4000, 250, n)) * decay(n, 0.06)   # sfxr explosion: pitch sweeps down
    thud = 0.6 * sine(np.linspace(110, 45, n), n) * decay(n, 0.05)
    return pad((0, crack), (0.055, body + thud))                    # 20 ms gap after the crack


def kaching():
    c = coin()
    c2 = c * 0.8
    m = len(t(0.3))
    env = np.minimum(1, np.arange(m) / (SR * 0.01)) * decay(m, 0.09)
    shimmer = 0.18 * highpass(noise(m), 6000) * env
    return pad((0, c), (0.075, c2), (0.02, shimmer))


def hire():
    n = len(t(0.14))
    thud = square(np.linspace(110, 55, n), n, 0.5) * decay(n, 0.035)
    thud = lowpass(thud, 900)
    m = len(t(0.09))
    paper = bandpass(noise(m), 1500, 5000) * decay(m, 0.025) * 0.9
    return pad((0, thud), (0.005, paper))


def milestone_ding():
    n = len(t(0.75))
    f = 1046.5                                                      # C6
    bell = triangle(f, n) * decay(n, 0.22)
    bell += 0.35 * sine(f * 2.76, n) * decay(n, 0.08)               # inharmonic bell partial, dies fast
    bell += 0.15 * sine(f * 5.4, n) * decay(n, 0.03)
    return bell


def star_fanfare():
    notes = [(523.25, 0.12), (659.25, 0.12), (783.99, 0.32)]        # C5 E5 G5
    parts, o = [], 0.0
    for f, d in notes:
        n = len(t(d))
        tone = square(f, n, 0.25) * 0.7 + square(f * 2, n, 0.5) * 0.15  # 25% duty chiptune + octave shimmer
        env = np.minimum(1, np.arange(n) / (SR * 0.004)) * (0.65 + 0.35 * decay(n, 0.05))
        env *= np.minimum(1, (n - np.arange(n)) / (SR * 0.01))      # per-note release, no clicks between notes
        if d > 0.2:
            env *= decay(n, 0.25)
            tone *= 1 + 0.08 * np.sin(2 * np.pi * 6 * np.arange(n) / SR)  # vibrato-ish amp wobble on the held note
        parts.append((o, lowpass(tone * env, 5000))); o += d
    return pad(*parts)


def vacuum():
    n = len(t(0.25))
    x = np.arange(n) / n
    env = np.sin(np.pi * x) ** 1.5                                  # rise then fall
    sweep = np.geomspace(600, 3500, n)                              # pitch rises: sucking in
    return bandpass(noise(n), sweep * 0.6, sweep * 1.6) * env


def ui_tick():
    n = len(t(0.015))
    return square(1800, n, 0.5) * decay(n, 0.006)


def page_flip():
    n = len(t(0.18))
    x = np.arange(n) / n
    env = (x / 0.25).clip(0, 1) * np.exp(-np.maximum(0, x - 0.25) * 6)
    sweep = np.geomspace(1200, 4500, n)
    swish = bandpass(noise(n), sweep * 0.5, sweep * 1.5) * env
    crinkle = (rng.uniform(0, 1, n) > 0.985) * rng.uniform(-1, 1, n) * env * 0.6  # paper grain
    return swish + lowpass(crinkle, 5000)


RECIPES = {
    "sfx_click_hit": click_hit, "sfx_kill": kill, "sfx_coin": coin, "sfx_kaching": kaching,
    "sfx_hire": hire, "sfx_milestone_ding": milestone_ding, "sfx_star_fanfare": star_fanfare,
    "sfx_vacuum": vacuum, "sfx_ui_tick": ui_tick, "sfx_page_flip": page_flip,
}

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in RECIPES.items():
        x = finish(fn())
        pcm = np.round(x * 32767).astype("<i2")
        with wave.open(os.path.join(out, name + ".wav"), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
        # self-check: format, level, clean edges
        peak = 20 * np.log10(np.max(np.abs(x)))
        rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)))
        assert abs(peak + 6) < 0.1, name
        assert abs(pcm[-1]) <= 1 and abs(pcm[0]) <= 1, name
        spec = np.abs(np.fft.rfft(x)); fr = np.fft.rfftfreq(len(x), 1 / SR)
        print(f"{name:20s} {len(x)/SR*1000:6.0f} ms  peak {peak:5.1f} dBFS  rms {rms:6.1f} dBFS  "
              f"centroid {np.sum(fr*spec)/np.sum(spec):6.0f} Hz  dc {np.mean(x):+.4f}")
