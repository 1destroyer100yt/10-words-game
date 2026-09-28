"""The itch.io cover, 630 x 500: the title-screen scene with the game's name above it.

The scene comes from ../title-art/draw_title.py (the hand-placed devil and school). The letters are
the game's own 5 x 7 pixel alphabet from Assets/Floorplan/Icons/Letters.png, scaled by whole numbers
and ringed in black so they stay readable over the art. Three colours only. Standard library only.

Run: python draw_cover.py
"""
import struct
import sys
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / 'title-art'))
from draw_title import Canvas, BLACK, GREY, RED, devil, school_left, school_right, wall, arch  # noqa: E402

LETTERS = HERE.parents[1] / 'Floorplan' / 'Icons' / 'Letters.png'
CELL, GLYPH = 7, 5


def load_letters():
    """26 glyphs, A to Z, as sets of lit (x, y) pixels, from the game's letter strip."""
    data = LETTERS.read_bytes()
    pos, idat = 8, b''
    while pos < len(data):
        n = struct.unpack('>I', data[pos:pos + 4])[0]
        kind, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if kind == b'IHDR':
            w, h, depth, colour = struct.unpack('>IIBB', body[:10])
        elif kind == b'IDAT':
            idat += body
    assert depth == 8 and colour == 6, 'expected an 8-bit RGBA strip'
    raw, bpp = zlib.decompress(idat), 4
    stride, rows, prev, i = w * bpp, [], bytearray(w * bpp), 0
    for _ in range(h):
        kind, line = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if kind == 1:
                line[x] = (line[x] + a) & 255
            elif kind == 2:
                line[x] = (line[x] + b) & 255
            elif kind == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif kind == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    glyphs = []
    for g in range(26):
        lit = set()
        for y in range(CELL):
            for x in range(CELL):
                if rows[y][(g * CELL + x) * bpp + 3] > 127:
                    lit.add((x - (CELL - GLYPH) // 2, y))
        glyphs.append(lit)
    return glyphs


def word_width(text, scale, gap, bold=0):
    return sum(GLYPH * scale + bold + gap for ch in text) - gap


def write(c, glyphs, text, top, scale, gap, colour, bold=0):
    """Centred text; a one-pixel black ring around every letter keeps it off the art behind.
    bold widens every stroke sideways by that many pixels, so the thin alphabet reads at size."""
    x = (c.w - word_width(text, scale, gap, bold)) // 2
    cells = []
    for ch in text:
        if ch != ' ':
            for gx, gy in glyphs[ord(ch) - ord('A')]:
                cells.append((x + gx * scale, top + gy * scale))
        x += GLYPH * scale + bold + gap
    for cx, cy in cells:
        c.rect(cx - 1, cy - 1, scale + bold + 2, scale + 2, BLACK)
    for cx, cy in cells:
        c.rect(cx, cy, scale + bold, scale, colour)


def cover():
    glyphs = load_letters()
    c = Canvas(315, 250)
    # The scene from the title-art cover, with the devil set lower to leave room for the name.
    c.paste(devil(), 67, 66, 180, 120)
    c.paste(school_left(), 0, 90)
    c.paste(school_right(), 229, 120)
    wall(c, 86, 185, 147, 40, 8)
    c.rect(86, 181, 147, 5, GREY)
    c.rect(86, 186, 147, 1, GREY)
    c.rect(86, 224, 147, 2, GREY)
    for x in (97, 121, 182, 206):
        arch(c, x, 193, 11, 26)
    arch(c, 146, 190, 25, 36)
    c.line(158, 205, 158, 224, GREY)
    for y, x, w in ((227, 142, 33), (231, 138, 41), (235, 134, 49)):
        c.rect(x, y, w, 2, GREY)
    c.line(134, 241, 182, 241, GREY)
    c.line(143, 238, 139, 249, GREY)
    c.line(173, 238, 177, 249, GREY)

    write(c, glyphs, 'JEWEL OF THE', 7, 3, 3, GREY, bold=1)
    write(c, glyphs, 'DEVIL', 33, 6, 6, RED, bold=2)
    return c


def main():
    cover().scaled(2).png(HERE / 'cover-630x500.png')
    print('Wrote cover-630x500.png')


if __name__ == '__main__':
    main()
