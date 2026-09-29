"""Turns Brent's finished skip button into the two in-game files under Assets/Resources/UI.

Crops to his black frame, dropping the editor's checker border around it, and shrinks it six times
by averaging each 6 x 6 block, so the lava keeps its glow instead of breaking into noise. The hover
version is the same picture with the outer black frame turned the game's red.

Run after finish_brent_cracks.py: python make_game_button.py
"""
import os
from pngio import read, write

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, 'skip button finished.png')
OUT = os.path.join(HERE, '..', '..', 'Resources', 'UI')
FACTOR = 6
BLACK, RED = (0, 0, 0, 255), (237, 28, 36, 255)


def frame_bounds(w, h, px):
    """The black frame's outer edges, found along the middle row and column."""
    row = [x for x in range(w) if px[h // 2][x] == BLACK]
    col = [y for y in range(h) if px[y][w // 2] == BLACK]
    return row[0], col[0], row[-1] + 1, col[-1] + 1


def shrink(px, left, top, right, bottom):
    w, h = (right - left) // FACTOR, (bottom - top) // FACTOR
    out = []
    for j in range(h):
        line = []
        for i in range(w):
            total = [0, 0, 0, 0]
            for y in range(top + j * FACTOR, top + (j + 1) * FACTOR):
                for x in range(left + i * FACTOR, left + (i + 1) * FACTOR):
                    for k in range(4):
                        total[k] += px[y][x][k]
            line.append(tuple(v // (FACTOR * FACTOR) for v in total))
        out.append(line)
    return out


def main():
    w, h, px = read(SOURCE)
    left, top, right, bottom = frame_bounds(w, h, px)
    small = shrink(px, left, top, right, bottom)
    write(os.path.join(OUT, 'skip-button.png'), small)

    # The frame is the outermost couple of pixels once shrunk; any near-black pixel there turns red.
    hover = [list(r) for r in small]
    sh, sw = len(hover), len(hover[0])
    for y in range(sh):
        for x in range(sw):
            edge = min(x, y, sw - 1 - x, sh - 1 - y) < 2
            if edge and sum(hover[y][x][:3]) < 60:
                hover[y][x] = RED
    write(os.path.join(OUT, 'skip-button-hover.png'), hover)
    print(f'cropped to {right - left} x {bottom - top}, in-game art {sw} x {sh}')


if __name__ == '__main__':
    main()
