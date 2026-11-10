# Vyrobí jednoduché zvuky pro hru (bez stahování z internetu) a uloží je jako WAV do Assets/Audio.
# Spuštění: python3 Tools/generate_audio.py   (potřebuje jen Python 3, žádné knihovny navíc)
import math, random, struct, wave, os

RATE = 44100                                    # vzorků za sekundu (kvalita CD)
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio")

def save(name, samples):
    """Uloží seznam čísel -1..1 jako 16bitový mono WAV."""
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))

def square(phase):                              # "8bitový" tón
    return 1.0 if (phase % 1.0) < 0.5 else -1.0

def saw(phase):                                 # pilový tón (typický pro synthwave)
    return 2.0 * (phase % 1.0) - 1.0

# 1) Snězení: krátké "blip" – tón rychle stoupá nahoru
def eat():
    n = int(RATE * 0.12); out = []; ph = 0
    for i in range(n):
        t = i / n
        freq = 600 + 900 * t                    # stoupající výška
        ph += freq / RATE
        env = (1 - t) ** 2                      # hlasitost postupně klesá
        out.append(0.5 * square(ph) * env)
    return out

# 2) Turbo: "vžum" – šum, který se zesílí a zeslabí, plus stoupající tón
def whoosh():
    n = int(RATE * 0.6); out = []; ph = 0; lp = 0
    for i in range(n):
        t = i / n
        env = math.sin(math.pi * t) ** 2
        noise = random.uniform(-1, 1)
        lp += (noise - lp) * (0.05 + 0.3 * t)   # jednoduchý filtr – šum zní měkčeji
        ph += (200 + 800 * t) / RATE
        out.append(0.6 * env * (0.7 * lp + 0.3 * math.sin(2 * math.pi * ph)))
    return out

# 3) Náraz: hluboké "bum" – klesající nízký tón + šum
def boom():
    n = int(RATE * 0.9); out = []; ph = 0; lp = 0
    for i in range(n):
        t = i / n
        ph += (120 * (1 - t) + 35) / RATE       # tón klesá
        lp += (random.uniform(-1, 1) - lp) * 0.08
        env = math.exp(-4 * t)
        out.append(0.9 * env * (0.7 * math.sin(2 * math.pi * ph) + 0.6 * lp))
    return out

# 4) Hudba: smyčka 8 taktů – basa + arpeggio (rozložený akord), tempo 110 BPM
def music():
    bpm = 110; step = 60 / bpm / 4               # délka šestnáctinové noty
    chords = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]]  # Am F C G (MIDI čísla)
    steps_per_chord = 16 * 2                     # každý akord 2 takty
    total = steps_per_chord * len(chords)
    n = int(RATE * step * total); out = [0.0] * n
    def midi(m): return 440 * 2 ** ((m - 69) / 12)
    for s in range(total):
        chord = chords[s // steps_per_chord]
        start = int(s * step * RATE); length = int(step * RATE)
        arp = midi(chord[s % 3] + 12)            # arpeggio o oktávu výš
        bass = midi(chord[0] - 12)
        for i in range(length):
            t = i / RATE; k = start + i
            if k >= n: break
            env = math.exp(-t * 18)              # krátké "pípnutí"
            v = 0.18 * saw(arp * t) * env
            if s % 2 == 0:                       # basa na každou osminu
                v += 0.25 * saw(bass * t) * math.exp(-t * 6)
            if s % 4 == 0:                       # "kopák" (buben) na každou dobu
                v += 0.5 * math.sin(2 * math.pi * (50 + 80 * math.exp(-t * 30)) * t) * math.exp(-t * 12)
            out[k] += v
    return out

if __name__ == "__main__":
    save("eat.wav", eat()); save("turbo.wav", whoosh()); save("crash.wav", boom()); save("music.wav", music())
    print("Hotovo – zvuky jsou v", os.path.abspath(OUT))
