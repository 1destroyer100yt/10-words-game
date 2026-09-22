"""Hand-authored pixel art. Python 3, standard library only.

Run: python draw_title.py
Coordinates name actual source pixels; every primitive writes palette indices.
The two scenes share the devil, jewel, stone wall, window, and belfry drawings.
No fonts, external assets, randomness, image libraries, or image models.
"""

from pathlib import Path
import struct
import zlib


OUT = Path(__file__).resolve().parent
BLACK, GREY, RED = range(3)
PALETTE = ((0, 0, 0), (70, 70, 70), (237, 28, 36))
BAYER = ((0, 8, 2, 10), (12, 4, 14, 6),
         (3, 11, 1, 9), (15, 7, 13, 5))


class Canvas:
    def __init__(self, width, height):
        self.w, self.h = width, height
        self.pixels = bytearray(width * height)

    def px(self, x, y, colour):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.pixels[y * self.w + x] = colour

    def rect(self, x, y, w, h, colour):
        for j in range(max(0, y), min(self.h, y + h)):
            for i in range(max(0, x), min(self.w, x + w)):
                self.px(i, j, colour)

    def line(self, x, y, x1, y1, colour):
        dx, dy = abs(x1 - x), -abs(y1 - y)
        sx, sy = (1 if x < x1 else -1), (1 if y < y1 else -1)
        err = dx + dy
        while True:
            self.px(x, y, colour)
            if (x, y) == (x1, y1):
                break
            twice = 2 * err
            if twice >= dy:
                err += dy
                x += sx
            if twice <= dx:
                err += dx
                y += sy

    def path(self, points, colour, closed=False):
        pairs = list(zip(points, points[1:]))
        if closed:
            pairs.append((points[-1], points[0]))
        for (x, y), (xx, yy) in pairs:
            self.line(x, y, xx, yy, colour)

    def poly(self, points, colour):
        # Vertices denote pixel centres. Integer sampling and the boundary path
        # use the same lattice, preventing pinholes in thin diagonal polygons.
        edges = list(zip(points, points[1:] + points[:1]))
        for y in range(max(0, min(p[1] for p in points)),
                       min(self.h, max(p[1] for p in points) + 1)):
            for x in range(max(0, min(p[0] for p in points)),
                           min(self.w, max(p[0] for p in points) + 1)):
                inside = False
                for (ax, ay), (bx, by) in edges:
                    if (ay > y) != (by > y):
                        lhs = (x - ax) * (by - ay)
                        rhs = (y - ay) * (bx - ax)
                        if (lhs < rhs) if by > ay else (lhs > rhs):
                            inside = not inside
                if inside:
                    self.px(x, y, colour)
        self.path(points, colour, closed=True)

    def shade(self, x, y, w, h, colour, level):
        """Dither a bounded surface, without touching its black cutouts."""
        for j in range(max(0, y), min(self.h, y + h)):
            for i in range(max(0, x), min(self.w, x + w)):
                n = j * self.w + i
                value = level(i, j) if callable(level) else level
                if self.pixels[n] == colour and BAYER[j & 3][i & 3] >= value:
                    self.pixels[n] = BLACK

    def paste(self, source, x, y, width=None, height=None, transparent=False):
        width, height = width or source.w, height or source.h
        for j in range(height):
            for i in range(width):
                value = source.pixels[(j * source.h // height) * source.w
                                      + i * source.w // width]
                if not transparent or value != BLACK:
                    self.px(x + i, y + j, value)

    def scaled(self, scale):
        result = Canvas(self.w * scale, self.h * scale)
        result.paste(self, 0, 0, result.w, result.h)
        return result

    def png(self, path):
        # Opaque RGB PNG; filter 0, no timestamps or other variable metadata.
        def chunk(kind, payload):
            return (struct.pack('>I', len(payload)) + kind + payload
                    + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff))
        raw = bytearray()
        for y in range(self.h):
            raw.append(0)
            for value in self.pixels[y * self.w:(y + 1) * self.w]:
                raw.extend(PALETTE[value])
        path.write_bytes(b'\x89PNG\r\n\x1a\n'
                         + chunk(b'IHDR', struct.pack('>IIBBBBB', self.w, self.h,
                                                      8, 2, 0, 0, 0))
                         + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
                         + chunk(b'IEND', b''))


def devil():
    """144 x 96 master: one horn pair, head, neck, cloak, chain, gem."""
    c = Canvas(144, 96)
    # One solid garment grows from the neck, over rounded shoulders, then
    # falls mostly downward. Only its hem and outer margins dissolve.
    c.poly([(57, 49), (86, 49), (90, 57), (104, 60), (112, 65),
            (117, 77), (124, 95), (19, 95), (26, 77), (31, 65),
            (39, 60), (53, 57)], GREY)
    c.shade(0, 60, 144, 36, GREY,
            lambda x, y: min(16, max(0, (95 - y) * 2),
                             max(0, (min(x, 143 - x) - 19) * 2)))
    # A narrow opening leaves substantial cloth on both sides of the chest.
    c.poly([(57, 63), (71, 67), (86, 63), (88, 79), (94, 95),
            (49, 95), (55, 79)], BLACK)
    # Raised collar is attached to the shoulders, with inset triangular seams.
    c.poly([(51, 57), (57, 58), (65, 65), (56, 64)], BLACK)
    c.poly([(92, 57), (86, 58), (78, 65), (87, 64)], BLACK)
    # Tapered folds run down the cloth, rather than radiating like feathers.
    c.poly([(40, 68), (39, 82), (35, 94), (37, 80)], BLACK)
    c.poly([(103, 68), (104, 82), (108, 94), (106, 80)], BLACK)
    # Each curved horn is a single connected taper returning into the temple.
    left_horn = [(49, 34), (39, 30), (32, 23), (28, 15), (28, 7),
                 (31, 1), (32, 13), (37, 20), (44, 23), (55, 23),
                 (59, 29), (56, 36)]
    c.poly(left_horn, GREY)
    c.poly([(143 - x, y) for x, y in left_horn], GREY)
    c.path([(31, 13), (34, 23), (41, 29), (48, 30)], BLACK)
    c.path([(112, 13), (109, 23), (102, 29), (95, 30)], BLACK)
    # Broad cranium and angular jaw. No additional spikes or horns.
    c.poly([(58, 19), (85, 19), (94, 26), (96, 37), (90, 47),
            (80, 56), (63, 56), (53, 47), (47, 37), (49, 26)], GREY)
    c.poly([(51, 30), (56, 35), (55, 42), (65, 52), (59, 48),
            (51, 41)], BLACK)
    c.poly([(92, 30), (87, 35), (88, 42), (78, 52), (84, 48),
            (92, 41)], BLACK)
    # Heavy diagonal brows above two clean red eye slits.
    c.poly([(55, 29), (65, 31), (69, 35), (66, 39), (57, 36)], BLACK)
    c.poly([(88, 29), (78, 31), (74, 35), (77, 39), (86, 36)], BLACK)
    c.poly([(57, 33), (65, 35), (65, 37), (59, 35)], RED)
    c.poly([(86, 33), (78, 35), (78, 37), (84, 35)], RED)
    # Nose and closed mouth are cutouts, not teeth or glyphs.
    c.poly([(71, 34), (69, 43), (74, 43)], BLACK)
    c.path([(63, 49), (66, 47), (77, 47), (80, 49)], BLACK)
    # Two sides of a chain reach the metal bail at the chest.
    c.path([(54, 58), (59, 65), (65, 70), (70, 73)], GREY)
    c.path([(89, 58), (84, 65), (78, 70), (73, 73)], GREY)
    c.rect(70, 72, 4, 4, GREY)
    c.rect(71, 73, 2, 2, BLACK)
    jewel(c, 72, 83)
    return c


def jewel(c, x, y):
    # A tight stepped halo: Bayer-ordered red, only within three pixels of stone.
    for dy in range(-11, 13):
        for dx in range(-13, 14):
            distance = abs(dx) + abs(dy)
            level = 4 if distance <= 12 else 2 if distance <= 15 else 0
            if BAYER[(y + dy) & 3][(x + dx) & 3] < level:
                c.px(x + dx, y + dy, RED)
    def p(points, colour):
        c.poly([(x + xx, y + yy) for xx, yy in points], colour)
    # Black one-pixel setting separates the facets from the glow.
    p([(-5, -8), (5, -8), (9, -3), (7, 4), (0, 11), (-7, 4), (-9, -3)], BLACK)
    p([(-4, -7), (4, -7), (8, -3), (6, 3), (0, 9), (-6, 3), (-8, -3)], RED)
    p([(-8, -3), (-3, -2), (0, 9), (-6, 3)], BLACK)
    # The dark triangular facet has a continuous red inner edge, not stipple.
    c.line(x - 7, y - 2, x - 1, y + 7, RED)
    c.path([(x - 4, y - 7), (x - 3, y - 2), (x + 3, y - 2),
            (x + 4, y - 7)], GREY)
    c.line(x + 3, y - 2, x, y + 9, BLACK)
    c.line(x + 3, y - 2, x + 8, y - 3, BLACK)


def arch(c, x, y, w, h, lit=False):
    """Front-on pointed opening, black recess, solid stone frame and sill."""
    mid = x + w // 2
    spring = y + w // 2
    outline = [(x, y + h - 1), (x, spring), (x + 2, y + 3),
               (mid, y), (x + w - 3, y + 3), (x + w - 1, spring),
               (x + w - 1, y + h - 1)]
    c.poly(outline, GREY)
    inside = [(x + 2, y + h - 3), (x + 2, spring + 1),
              (mid, y + 3), (x + w - 3, spring + 1),
              (x + w - 3, y + h - 3)]
    c.poly(inside, RED if lit else BLACK)
    if lit:
        c.shade(x + 2, y + 3, w - 4, h - 5, RED,
                lambda xx, yy: 4 if yy < y + h - 9 else 2)
    c.rect(x - 1, y + h, w + 2, 2, GREY)


def wall(c, x, y, w, h, level=8):
    # Broad stone surfaces with ordered light, no isolated masonry strokes.
    c.rect(x, y, w, h, GREY)
    c.shade(x, y, w, h, GREY, level)


def school_left():
    c = Canvas(90, 160)
    # Connected teaching wing and taller belfry: common cornices and foundation.
    c.poly([(0, 39), (31, 39), (31, 25), (67, 25), (67, 44),
            (86, 44), (86, 153), (0, 153)], GREY)
    wall(c, 0, 40, 30, 113, 8)
    wall(c, 33, 29, 32, 124, 12)
    wall(c, 68, 45, 18, 108, 8)
    c.path([(0, 39), (31, 39), (31, 25), (67, 25), (67, 44), (85, 44),
            (85, 152)], GREY)
    c.rect(0, 43, 30, 3, GREY)
    c.rect(69, 47, 17, 3, GREY)
    c.rect(30, 30, 3, 124, GREY)
    c.rect(65, 30, 3, 124, GREY)
    c.rect(82, 50, 3, 103, GREY)
    # Pitched roof, supported belfry, visible bell suspended from a crossbeam.
    c.poly([(27, 25), (49, 3), (71, 25)], GREY)
    c.rect(29, 26, 41, 3, GREY)
    arch(c, 38, 32, 23, 30)
    c.rect(42, 42, 15, 2, GREY)
    c.rect(48, 44, 2, 4, GREY)
    c.poly([(46, 47), (51, 47), (53, 54), (55, 56), (42, 56),
            (44, 54)], GREY)
    c.rect(48, 57, 2, 3, GREY)
    c.rect(28, 67, 42, 3, GREY)
    c.rect(0, 104, 85, 2, GREY)
    c.rect(0, 150, 86, 3, GREY)
    arch(c, 10, 58, 13, 36)
    arch(c, 42, 78, 14, 22, lit=True)
    arch(c, 10, 115, 13, 29)
    # Recessed double entrance reaches the landing. Every tread has a riser.
    arch(c, 39, 121, 22, 30)
    c.line(50, 132, 50, 149, GREY)
    c.rect(37, 152, 27, 2, GREY)
    c.rect(34, 155, 33, 2, GREY)
    c.rect(31, 158, 39, 2, GREY)
    # Deep shadow at the outer edge of the stone facade.
    c.rect(0, 46, 2, 104, BLACK)
    return c


def school_right():
    c = Canvas(86, 130)
    # A broader gabled assembly hall, joined to a classroom bay on the right.
    wall(c, 8, 25, 48, 95, 12)
    wall(c, 56, 33, 30, 87, 8)
    c.poly([(4, 24), (31, 3), (59, 24)], GREY)
    c.rect(5, 25, 55, 3, GREY)
    c.rect(60, 32, 26, 3, GREY)
    c.rect(8, 28, 3, 92, GREY)
    c.rect(53, 28, 3, 92, GREY)
    c.rect(81, 36, 3, 84, GREY)
    c.rect(5, 72, 81, 3, GREY)
    c.rect(6, 119, 80, 3, GREY)
    arch(c, 16, 41, 13, 26)
    arch(c, 36, 41, 13, 26)
    arch(c, 67, 42, 12, 24)
    arch(c, 67, 84, 12, 28)
    arch(c, 23, 84, 17, 35, lit=True)
    c.line(31, 96, 31, 117, BLACK)
    c.rect(20, 120, 24, 2, GREY)
    c.rect(17, 123, 30, 2, GREY)
    c.rect(14, 126, 36, 2, GREY)
    c.rect(11, 129, 42, 1, GREY)
    return c


def title():
    c = Canvas(320, 180)
    c.paste(school_left(), 0, 20)
    c.paste(school_right(), 234, 50)
    c.paste(devil(), 88, 0)
    return c


def cover():
    c = Canvas(315, 250)
    # Recompose rather than crop: a larger upper apparition and lower campus.
    c.paste(devil(), 49, 12, 216, 144)
    c.paste(school_left(), 0, 90)
    c.paste(school_right(), 229, 120)
    # The shared rear corridor joins both wings below the larger apparition.
    wall(c, 86, 185, 147, 40, 8)
    c.rect(86, 181, 147, 5, GREY)
    c.rect(86, 186, 147, 1, GREY)
    c.rect(86, 224, 147, 2, GREY)
    for x in (97, 121, 182, 206):
        arch(c, x, 193, 11, 26)
    # Central entrance: an unlit arch and short, physically supported stairs.
    arch(c, 146, 190, 25, 36)
    c.line(158, 205, 158, 224, GREY)
    for y, x, w in ((227, 142, 33), (231, 138, 41), (235, 134, 49)):
        c.rect(x, y, w, 2, GREY)
    # Quiet paving joints, all attached to the entrance approach.
    c.line(134, 241, 182, 241, GREY)
    c.line(143, 238, 139, 249, GREY)
    c.line(173, 238, 177, 249, GREY)
    return c


def main():
    menu = title()
    menu.png(OUT / 'menu-art.png')
    menu.scaled(4).png(OUT / 'menu-art-preview.png')
    cover().scaled(2).png(OUT / 'cover.png')
    print('Wrote menu-art.png (320x180), menu-art-preview.png (1280x720), '
          'cover.png (630x500).')


if __name__ == '__main__':
    main()
