"""Создает встроенный сигнал Match Finder. Только стандартная библиотека Python."""
import math
import struct
import wave
from pathlib import Path

rate = 44100
duration = 0.72
samples = []
for index in range(round(rate * duration)):
    time = index / rate
    value = 0.0
    for start, frequency in ((0.0, 659.255), (0.22, 880.0)):
        local = time - start
        if 0 <= local < 0.5:
            attack = min(local / 0.012, 1.0)
            release = min((0.5 - local) / 0.05, 1.0)
            envelope = attack * release * math.exp(-7.0 * local)
            value += 0.22 * envelope * (
                math.sin(2 * math.pi * frequency * local)
                + 0.18 * math.sin(2 * math.pi * frequency * 2 * local)
            )
    samples.append(round(max(-1.0, min(1.0, value)) * 32767))

target = Path(__file__).resolve().parents[1] / "source" / "Assets" / "match-found.wav"
target.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(target), "wb") as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes(struct.pack(f"<{len(samples)}h", *samples))
print(f"Создан сигнал: {duration} с, {rate} Гц, PCM 16 бит, моно.")
