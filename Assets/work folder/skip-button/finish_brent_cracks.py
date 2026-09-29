"""Finishes Brent's skip button: his thin pale sketch lines become full lava cracks like the ones he
finished, with the same five bands from the edge in (dark orange-red, orange, amber, gold, pale core)
and the same width, about 26 pixels. The bands and width were measured from his finished cracks.
Where a new crack meets one of his, the hotter colour wins, so the lava runs together.
Nothing else in the picture changes.

Run: python finish_brent_cracks.py
"""
import os
from pngio import read, write

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, 'brent not finished skip button.png')
OUT = os.path.join(HERE, 'skip button finished.png')

# Brent's crack colours, coolest (outer edge) to hottest (core).
BANDS = [(242, 43, 0, 255), (255, 90, 0, 255), (255, 159, 28, 255), (255, 212, 71, 255), (255, 244, 163, 255)]
HEAT = {c: i + 1 for i, c in enumerate(BANDS)}
CORE, GOLD = BANDS[4], BANDS[3]
# Distance from the nearest sketch pixel at which each band stops, hottest first (measured).
LIMITS = [(0.5, 5), (3.2, 4), (6.2, 3), (9.2, 2), (11.8, 1)]
REACH = 12


def main():
    w, h, px = read(SOURCE)

    # A sketch pixel is pale core with no gold anywhere near it: in a finished crack the core
    # always has gold within a few pixels.
    def near_gold(x, y):
        for j in range(max(0, y - 4), min(h, y + 5)):
            for i in range(max(0, x - 4), min(w, x + 5)):
                if px[j][i] == GOLD:
                    return True
        return False

    sketch = [(x, y) for y in range(h) for x in range(w) if px[y][x] == CORE and not near_gold(x, y)]

    # Keep off the picture's own black frame and whatever sits outside it.
    def paintable(x, y):
        c = px[y][x]
        if c[3] == 0 or c in ((255, 255, 255, 255), (217, 217, 217, 255)):
            return False
        if c == (0, 0, 0, 255) and (x < 24 or y < 24 or x >= w - 24 or y >= h - 24):
            return False
        return True

    best = {}
    offsets = [(dx, dy, (dx * dx + dy * dy) ** 0.5) for dy in range(-REACH, REACH + 1)
               for dx in range(-REACH, REACH + 1) if dx * dx + dy * dy <= REACH * REACH]
    for sx, sy in sketch:
        for dx, dy, d in offsets:
            x, y = sx + dx, sy + dy
            if 0 <= x < w and 0 <= y < h and d < best.get((x, y), 99):
                best[(x, y)] = d

    changed = 0
    for (x, y), d in best.items():
        if not paintable(x, y):
            continue
        heat = next((level for limit, level in LIMITS if d <= limit), 0)
        if heat and heat > HEAT.get(px[y][x], 0):
            px[y][x] = BANDS[heat - 1]
            changed += 1
    write(OUT, px)
    print(f'{len(sketch)} sketch pixels, {changed} pixels painted -> {os.path.basename(OUT)}')


if __name__ == '__main__':
    main()
