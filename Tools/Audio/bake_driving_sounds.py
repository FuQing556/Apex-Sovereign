"""Bake original PCM effects. Requires numpy. Loops use periodic synthesis."""
from pathlib import Path
import wave

import numpy as np

OUT = Path(__file__).resolve().parents[2] / "Assets/_Project/Audio/Clips"
RATE = 44100
TAU = 2 * np.pi
rng = np.random.default_rng(31)


def time(seconds):
    return np.arange(round(seconds * RATE)) / RATE


def colour(white, bands):
    """Circular, coloured noise. Each tuple is centre Hz, bandwidth Hz, gain."""
    hz = np.fft.rfftfreq(len(white), 1 / RATE)
    shape = sum(gain * np.exp(-0.5 * ((hz - centre) / width) ** 2)
                for centre, width, gain in bands)
    shape[0] = 0
    signal = np.fft.irfft(np.fft.rfft(white) * shape, n=len(white))
    return signal / np.std(signal)


def noise(t, bands):
    return colour(rng.normal(size=len(t)), bands)


def write(name, signal):
    signal = signal - np.mean(signal, axis=0)
    pcm = np.rint(signal * (0.87 * 32767 / np.max(np.abs(signal)))).astype("<i2")
    with wave.open(str(OUT / (name + ".wav")), "wb") as wav:
        wav.setparams((1 if pcm.ndim == 1 else pcm.shape[1], 2, RATE, 0, "NONE", ""))
        wav.writeframes(pcm.tobytes())


def transient(name, t, signal, attack=.004, release=.035):
    envelope = np.clip(t / attack, 0, 1) * np.clip((t[-1] - t) / release, 0, 1)
    write(name, signal * envelope)


def exhaust(name, rpm, white, loaded):
    # Build individual exhaust events over complete four-stroke cycles. Alternating banks
    # travel different acoustic distances. Pipe resonances stay in Hz across the recordings.
    local = np.random.default_rng(rpm)
    n = len(white)
    n_fires = round(n / RATE * rpm / 20)
    spacing = n / n_fires
    firing = np.arange(n_fires)
    bank = firing % 2
    times = firing * spacing + bank * (.00085 * RATE) + local.normal(0, .025*spacing, n_fires)
    arrivals = np.rint(times).astype(int) % n
    cylinder = np.array([1.10, .68, 1.00, .73, .94, .85])
    weights = cylinder[firing % 6] * local.uniform(.70, 1.30, n_fires)
    pulses = np.zeros(n)
    np.add.at(pulses, arrivals, weights)
    age = np.arange(round(.07 * RATE)) / RATE
    response = (1-np.exp(-age/.0008)) * (np.cos(TAU*100*age)*np.exp(-age/.0058)
                + .38*np.cos(TAU*246*age)*np.exp(-age/.0028))
    pressure = np.fft.irfft(np.fft.rfft(pulses) * np.fft.rfft(response, n), n=n)
    pressure -= pressure.mean()
    pressure /= pressure.std()
    body = colour(white, [(115, 68, 1), (280, 115, .48), (680, 260, .16)])
    breath = colour(local.normal(size=n), [(410, 180, 1), (990, 350, .50), (1710, 530, .17)])
    fluctuation = colour(local.normal(size=n), [(9, 5, 1)])
    air = (.62 + .25*np.tanh(pressure)) * breath
    signal = (.55*pressure + (.60 if loaded else .42)*body + (.46 if loaded else .28)*air)
    write(name, np.tanh(.68*signal) * (1+.10*np.tanh(fluctuation)))


def wind():
    local = np.random.default_rng(702)
    n = len(time(6))
    shared = colour(local.normal(size=n), [(160, 95, .85), (450, 220, 1), (1350, 690, .42)])
    gust = 1 + .13*np.tanh(colour(local.normal(size=n), [(1.1, .75, 1)]))
    channels = []
    for _ in range(2):
        eddies = colour(local.normal(size=n), [(320, 175, .7), (1050, 540, 1), (2850, 900, .32)])
        channels.append(gust * (.75*shared + .52*eddies))
    write("Wind", np.column_stack(channels))


def bake():
    OUT.mkdir(parents=True, exist_ok=True)
    exhaust("EngineLow", 1200, rng.normal(size=len(time(2))), False)
    exhaust("EngineHigh", 4200, rng.normal(size=len(time(2))), True)
    exhaust("EngineRedline", 7200, np.random.default_rng(72).normal(size=len(time(3))), True)
    wind()
    t = time(2)
    road = noise(t, [(120, 70, .95), (550, 250, .50), (2200, 680, .13)])
    write("Asphalt", road * (.9 + .1 * np.cos(TAU * 1.5 * t)))
    rasp = noise(t, [(110, 65, 1), (570, 280, .55), (1150, 650, .24)])
    # A repeating rumble-strip strike with some variation from ridge to ridge.
    comb = np.exp(-5.2 * ((t * 20 + .035 * np.sin(TAU * 2 * t)) % 1))
    write("Kerb", comb * (rasp + .85 * np.sin(TAU * 70 * t)))
    grit = noise(t, [(210, 130, .6), (980, 470, .9), (3560, 1200, .45)])
    clumps = noise(t, [(14, 8, 1), (60, 20, .35)])
    write("Grass", grit * (.42 + .75 * np.maximum(0, clumps)))
    bend = .8 * np.sin(TAU * 2 * t) + 1.2 * np.sin(TAU * 4.5 * t)
    slide = (.52 * np.sin(TAU * 884 * t + bend) + .21 * np.sin(TAU * 1361 * t + 2 * bend)
             + .42 * noise(t, [(850, 145, 1), (1950, 350, .55), (3800, 600, .20)]))
    write("TyreScrub", slide * (.86 + .14 * np.sin(TAU * 3.5 * t)))
    t = time(.18)
    transient("Shift", t, np.exp(-38*t) * (np.sin(TAU*(136*t-110*t*t))
              + .6 * noise(t, [(950, 490, 1), (2200, 350, .4)])))
    t = time(.52)
    transient("Impact", t, 1.6*np.sin(TAU*(90*t-47*t*t))*np.exp(-12*t)
              + noise(t, [(230, 105, 1), (1120, 580, .75)])*np.exp(-19*t)
              + .32*np.sin(TAU*381*t)*np.exp(-13*t))
    t = time(.68)
    transient("Boost", t, noise(t, [(410, 200, 1), (1670, 680, .8), (4100, 880, .33)])
              * np.exp(-4.5*t) + .23*np.sin(TAU*(270*t+330*t*t))*np.exp(-5*t), attack=.032)
    for name, hz, duration in [("Ready", 621, .13), ("Go", 984, .38)]:
        t = time(duration)
        transient(name, t, np.sin(TAU*hz*t) + .12*np.sin(TAU*hz*2*t), attack=.012)
    t = time(.64)
    chime = np.zeros_like(t)
    for start, hz in [(0, 659), (.13, 831), (.26, 988)]:
        age = np.maximum(0, t-start)
        chime += np.sin(TAU*hz*age) * np.minimum(1, age/.012) * np.exp(-8*age)
    transient("Finish", t, chime)


if __name__ == "__main__":
    bake()
