"""SUPERSEDED by Brent's art (make_game_button.py); running this overwrites it. Placeholder skip button for the story scenes: two arrows and a bar (skip to the end).

Brent: replace Assets/Resources/UI/skip-button.png (normal) and skip-button-hover.png (under the
mouse) with your own art. Keep both the same size; any size works, and it is drawn at a whole-number
scale so pixels stay square. Only black, grey (70,70,70) and red (237,28,36), with transparency.
"""
import os, struct, zlib

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Resources', 'UI')
GREY, RED, CLEAR = (70, 70, 70, 255), (237, 28, 36, 255), (0, 0, 0, 0)
W, H = 13, 9


def icon(colour):
    img = [[CLEAR] * W for _ in range(H)]
    for left in (0, 5):                 # two right-pointing triangles
        for c in range(5):
            for r in range(c, H - c):
                img[r][left + c] = colour
    for r in range(H):                  # the end bar
        img[r][11] = img[r][12] = colour
    return img


def png(path, img):
    def chunk(k, d):
        return struct.pack('>I', len(d)) + k + d + struct.pack('>I', zlib.crc32(k + d) & 0xffffffff)
    raw = b''.join(b'\x00' + bytes(v for p in row for v in p) for row in img)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


png(os.path.join(OUT, 'skip-button.png'), icon(GREY))
png(os.path.join(OUT, 'skip-button-hover.png'), icon(RED))
print('wrote skip-button.png and skip-button-hover.png')
