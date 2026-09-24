"""The Listener: a blind demon that hunts by sound. Drawn pixel by pixel, three colours only.

Seen from above like the other demon, facing up (the top of the cell). Same 100x100 cells, and the
drawing sits in the same box as the other demon, so the two are the same size in the game. The head
and ears are hand placed below; the smoke it floats on is dithered by rule. Deterministic.
"""
import math, os, struct, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
CELL = 100
OX, OY = 37, 18          # top-left of the 26 x 44 drawing area inside the cell

T = (0, 0, 0, 0)
K = (0, 0, 0, 255)
G = (70, 70, 70, 255)
R = (237, 28, 36, 255)
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
COLOURS = {'k': K, 'r': R}

# Left half of the head, 13 columns (0-12); the right half is its mirror (13-25).
# '.' empty, 'k' black, 'r' red. No eyes: it is blind. The zigzag is its mouth.
HEAD = [
    ".........rrrr",
    "........rkkkk",
    ".......rkkkkk",
    ".......rkkkkk",
    ".......rkkkkk",
    ".......rkkkkk",
    ".......rkkkkk",
    ".......rkrkrk",
    ".......rkkrkr",
    "........rkkkk",
    ".........rrrr",
]
MOUTH_OPEN = [   # rows 7-9 of the head when it screams: the mouth tears open
    ".......rkrrrr",
    ".......rkrkkk",
    "........rkrrr",
]

# The left ear: a tall fan joined to the side of the head.
EAR = [
    "r......",
    "rr.....",
    "rkr....",
    "rkkr...",
    ".rkkr..",
    ".rkkkr.",
    "..rkrkr",
    "..rkkkr",
    "...rkkr",
    "....rrr",
]


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[T] * w for _ in range(h)]

    def set(self, x, y, c):
        x, y = int(math.floor(x + 0.5)), int(math.floor(y + 0.5))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def line(self, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = [int(math.floor(v + 0.5)) for v in (x0, y0, x1, y1)]
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy; x0 += sx
            if e2 <= dx:
                err += dx; y0 += sy

    def poly(self, pts, c):
        ys = [p[1] for p in pts]
        for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            xs = []
            for k in range(len(pts)):
                (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % len(pts)]
                if (y0 <= y + 0.5 < y1) or (y1 <= y + 0.5 < y0):
                    xs.append(x0 + (y + 0.5 - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for a, b in zip(xs[::2], xs[1::2]):
                for x in range(int(math.ceil(a - 0.5)), int(math.floor(b - 0.5)) + 1):
                    self.set(x, y, c)


def stamp(c, rows, x0, y0, flip=False, mirror=False):
    h = len(rows)
    for y, row in enumerate(rows):
        yy = y0 + (h - 1 - y if flip else y)
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            if mirror:
                c.set(25 - (x0 + x), yy, COLOURS[ch])
            else:
                c.set(x0 + x, yy, COLOURS[ch])


BOB = {'idle': [0, 0, 1, 1, 1, 0, 0, -1], 'walk': [0, 0, 1, 1, 0, 0, 1, 1],
       'search': [0, 0, 1, 1, 1, 0, 0, -1], 'chase': [0, 1, 1, 0, 0, 1], 'alert': [0, 1, 1, 0]}


def frame(state, i, n):
    c = Canvas(26, 44)
    t = i / n
    top = 4 + BOB[state][i]            # the head's first row

    # ears: offset of each side, and flip = folded back behind it
    flip = False
    if state == 'idle':
        dl, dr = (1 if i in (3, 4) else 0), 0             # the left ear twitches
    elif state == 'walk':
        dl = dr = 1
    elif state == 'search':
        tilt = [0, -1, -2, -1, 0, 1, 2, 1][i]            # cocked one way, then the other: listening
        dl, dr = tilt, -tilt
    elif state == 'alert':
        dl = dr = [-1, -3, -3, -2][i]                       # snapped straight up
    else:                                                   # chase: flat back along the body
        dl = dr = 0
        flip = True
    ear_y = top + 3 if flip else top - 3
    stamp(c, EAR, 0, ear_y + dl, flip=flip)
    stamp(c, EAR, 0, ear_y + dr, flip=flip, mirror=True)

    # body: black, ribbed, tapering into the smoke
    hx = 12.5
    by = top + 10
    c.poly([(hx - 5, by), (hx + 5, by), (hx + 4, by + 11), (hx + 1, by + 16), (hx - 1, by + 16), (hx - 4, by + 11)], K)
    for r in range(by + 2, by + 13, 3):
        half = 4 if r < by + 9 else 3
        c.set(hx - half - 0.5, r, R)
        c.set(hx + half + 0.5, r, R)
    reach = 3 if state == 'chase' else 2 if state == 'alert' else 0
    for side in (-1, 1):
        ex, ey = hx + side * (7.5 + reach / 2), by + 5 - reach
        c.line(hx + side * 5.5, by + 1, ex, ey, K)
        c.set(ex, ey + 1, R)                                 # a claw

    # the head goes on last, so nothing ever crosses the face
    stamp(c, HEAD, 0, top)
    stamp(c, HEAD, 0, top, mirror=True)
    if state in ('chase', 'alert'):
        stamp(c, MOUTH_OPEN, 0, top + 7)
        stamp(c, MOUTH_OPEN, 0, top + 7, mirror=True)

    # the smoke it floats on: grey, dithered, never red
    length = {'idle': 12, 'walk': 15, 'chase': 18, 'search': 13, 'alert': 14}[state]
    start = by + 15
    for y in range(start, min(44, start + length)):
        depth = (y - start) / max(1, length - 1)
        width = 3.2 * (1 - depth) + 0.8
        sway = math.sin(y * 0.55 + t * 2 * math.pi) * 1.6 * depth
        for x in range(26):
            dist = abs(x - (hx + sway))
            if dist > width:
                continue
            density = 1 - depth * 0.85 - dist / (width + 0.01) * 0.35
            if density * 16 > BAYER[(y + i) % 4][x % 4]:
                c.set(x, y, G)
    return c


STATES = [('idle', 8), ('walk', 8), ('chase', 6), ('search', 8), ('alert', 4)]


def chunk(k, d):
    return struct.pack('>I', len(d)) + k + d + struct.pack('>I', zlib.crc32(k + d) & 0xffffffff)


def png(path, w, h, rows):
    raw = b''.join(b'\x00' + bytes(v for p in r for v in p) for r in rows)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def enlarge(c, s, bg):
    return [[c.px[y // s][x // s] if c.px[y // s][x // s][3] else bg for x in range(c.w * s)] for y in range(c.h * s)]


def apng(path, frames, s, fps):
    w, h = frames[0].w * s, frames[0].h * s
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    data += chunk(b'acTL', struct.pack('>II', len(frames), 0))
    seq = 0
    for n, c in enumerate(frames):
        raw = zlib.compress(b''.join(b'\x00' + bytes(v for p in r for v in p) for r in enlarge(c, s, K)), 9)
        data += chunk(b'fcTL', struct.pack('>IIIIIHHBB', seq, w, h, 0, 0, 1, fps, 0, 0)); seq += 1
        if n == 0:
            data += chunk(b'IDAT', raw)
        else:
            data += chunk(b'fdAT', struct.pack('>I', seq) + raw); seq += 1
    open(path, 'wb').write(data + chunk(b'IEND', b''))


def main():
    rows_all = []
    for state, n in STATES:
        frames = [frame(state, i, n) for i in range(n)]
        rows_all.append(frames)
        strip = [[T] * (CELL * n) for _ in range(CELL)]
        for k, c in enumerate(frames):
            for y in range(c.h):
                for x in range(c.w):
                    if c.px[y][x][3]:
                        strip[OY + y][k * CELL + OX + x] = c.px[y][x]
        png(os.path.join(HERE, 'listener %s.png' % state), CELL * n, CELL, strip)
        apng(os.path.join(HERE, 'listener-%s.apng' % state), frames, 8, 8)

    s, cw, ch = 6, 28, 46
    width = 8 * cw * s
    sheet = [[(16, 16, 16, 255)] * width for _ in range(len(STATES) * ch * s)]
    for row, frames in enumerate(rows_all):
        for k, c in enumerate(frames):
            for y in range(c.h):
                for x in range(c.w):
                    p = c.px[y][x]
                    if p[3]:
                        for yy in range(s):
                            for xx in range(s):
                                sheet[(row * ch + 1 + y) * s + yy][(k * cw + 1 + x) * s + xx] = p
    png(os.path.join(HERE, 'listener-preview.png'), width, len(sheet), sheet)
    print('written')


if __name__ == '__main__':
    main()
