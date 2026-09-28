"""Security camera art, two options, drawn pixel by pixel in the game's three colours.

Seen from above, facing up (out into the room); the wall is below. The floor in the game is the same
grey as the palette grey, so every shape is outlined in black or it vanishes into the floor. The red
lens is a separate sprite in the game (it blinks), so here it is drawn on top only in the preview.
Writes camera-preview.png: each option on the floor with the lens on and off, then in the dark
as it is now (lens only) and with a faint body.
"""
import os, struct, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
T = (0, 0, 0, 0)
K = (0, 0, 0, 255)
G = (70, 70, 70, 255)
R = (237, 28, 36, 255)
CODES = {'.': T, 'k': K, 'g': G, 'r': R}

# Option A: a box camera. A hood at the front, a seam, a tapered back, on a short arm.
BOX = [
    "..kkkkkkkk..",
    ".kggggggggk.",
    ".kgkkkkkkgk.",
    ".kgkkkkkkgk.",
    ".kggggggggk.",
    ".kggggggggk.",
    ".kkkkkkkkkk.",
    ".kggggggggk.",
    ".kggggggggk.",
    ".kggggggggk.",
    "..kggggggk..",
    "...kkkkkk...",
]
BOX_LENS = (5.5, 2.5)          # where the red lens sits, in art pixels (centre)

# Option B: a dome. The eye sits forward in the dome, so you can see which way it looks.
DOME = [
    "....kkkk....",
    "..kkggggkk..",
    ".kggkkkkggk.",
    ".kgkkkkkkgk.",
    "kggkkkkkkggk",
    "kgggkkkkgggk",
    "kggggggggggk",
    "kggggggggggk",
    ".kggggggggk.",
    ".kggggggggk.",
    "..kkggggkk..",
    "....kkkk....",
]
DOME_LENS = (5.5, 3.5)

# The wall mount, which does not turn with the sweep.
MOUNT = [
    "....kggk....",
    "....kggk....",
    "....kggk....",
    "..kkkkkkkk..",
    "..kggggggk..",
    "..kkkkkkkk..",
]


def grid(rows):
    return [[CODES[c] for c in row] for row in rows]


def png(path, w, h, pixels):
    def chunk(k, d):
        return struct.pack('>I', len(d)) + k + d + struct.pack('>I', zlib.crc32(k + d) & 0xffffffff)
    raw = b''.join(b'\x00' + bytes(v for p in row for v in p) for row in pixels)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def scene(body, lens, lens_on, dark, faint=False):
    """One 20x24 tile: floor (or darkness), the wall along the bottom, the mount, the camera."""
    w, h = 20, 24
    floor = K if dark else G
    img = [[floor] * w for _ in range(h)]
    for y in range(h - 3, h):
        for x in range(w):
            img[y][x] = K  # the wall
    ox, oy = 4, 3

    def paste(art, px, py):
        for y, row in enumerate(art):
            for x, c in enumerate(row):
                if c[3] and not dark:
                    img[py + y][px + x] = c
                elif c[3] and faint and c == G:
                    img[py + y][px + x] = (25, 25, 25, 255)  # palette grey at 35% over black

    paste(grid(body), ox, oy)
    paste(grid(MOUNT), ox, oy + len(body))
    if lens_on:
        lx, ly = int(ox + lens[0]), int(oy + lens[1])
        for dy in (0, 1):
            for dx in (0, 1):
                img[ly + dy][lx + dx] = R
    return img


def main():
    scale = 10
    tiles = []
    for body, lens in ((BOX, BOX_LENS), (DOME, DOME_LENS)):
        tiles.append([scene(body, lens, True, False), scene(body, lens, False, False),
                      scene(body, lens, True, True), scene(body, lens, True, True, True)])
    th, tw = 24, 20
    gap = 2
    w = (tw * 4 + gap * 3)
    h = (th * 2 + gap)
    sheet = [[(20, 20, 20, 255)] * w for _ in range(h)]
    for r, row in enumerate(tiles):
        for c, tile in enumerate(row):
            for y in range(th):
                for x in range(tw):
                    sheet[r * (th + gap) + y][c * (tw + gap) + x] = tile[y][x]
    big = [[p for p in row for _ in range(scale)] for row in sheet for _ in range(scale)]
    png(os.path.join(HERE, 'camera-preview.png'), w * scale, h * scale, big)
    for name, art in (('camera-box', BOX), ('camera-dome', DOME), ('camera-mount', MOUNT)):
        png(os.path.join(HERE, name + '.png'), len(art[0]), len(art), grid(art))


if __name__ == '__main__':
    main()
