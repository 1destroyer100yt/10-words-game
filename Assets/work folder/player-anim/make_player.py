"""Deterministic player strips. Python 3 standard library only.

Run: python -B make_player.py
Verify existing deliverables without rewriting: python -B make_player.py --verify-only
Source files are read only; every output is resolved next to this script.
"""
import hashlib
import os
from pathlib import Path
import struct
import sys
import zlib

ROOT = Path(__file__).resolve().parent
# The ORIGINAL strips this was built from: the jet-less 'idle player.png' (in git history before
# 2026-09-22) and 'bost player.png'. Keep them outside Assets/, or the level builder may pick the
# copy up as the player's idle strip. Point PLAYER_SOURCE at that folder.
SOURCE = Path(os.environ.get('PLAYER_SOURCE', str(ROOT / 'source')))
T = (0, 0, 0, 0)
K = (0, 0, 0, 255)
G = (70, 70, 70, 255)
R = (237, 28, 36, 255)
PALETTE = {T, K, G, R}
SIG = b'\x89PNG\r\n\x1a\n'
BOB = (0, -1, -2, -3, -2, -1, 0, 1)
HOVER_L = (5, 6, 4, 5, 6, 4, 5, 4)
HOVER_R = (4, 5, 6, 4, 5, 6, 4, 5)
POWER = (34, 38, 35, 39, 36, 37)


def chunk(kind, data):
    return (struct.pack('>I', len(data)) + kind + data
            + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff))


def chunks(data):
    assert data[:8] == SIG, 'Invalid PNG signature'
    pos = 8
    while pos < len(data):
        assert pos + 12 <= len(data), 'Truncated chunk'
        size = struct.unpack_from('>I', data, pos)[0]
        kind = data[pos + 4:pos + 8]
        end = pos + 8 + size
        body = data[pos + 8:end]
        assert end + 4 <= len(data), 'Truncated payload'
        assert zlib.crc32(kind + body) & 0xffffffff == struct.unpack_from('>I', data, end)[0], 'CRC mismatch'
        yield kind, body
        pos = end + 4
        if kind == b'IEND':
            assert pos == len(data), 'Data after IEND'
            return
    raise AssertionError('Missing IEND')


def decode_rows(w, h, packed):
    raw = zlib.decompress(packed)
    stride = w * 4
    assert len(raw) == h * (stride + 1), 'Incorrect pixel data length'
    rows = []
    prev = bytes(stride)
    for y in range(h):
        offset = y * (stride + 1)
        kind = raw[offset]
        assert 0 <= kind <= 4, 'Unknown PNG filter'
        row = bytearray(raw[offset + 1:offset + 1 + stride])
        if kind:
            for x in range(stride):
                a = row[x - 4] if x >= 4 else 0
                b = prev[x]
                c = prev[x - 4] if x >= 4 else 0
                if kind == 1:
                    predictor = a
                elif kind == 2:
                    predictor = b
                elif kind == 3:
                    predictor = (a + b) // 2
                else:
                    p = a + b - c
                    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                    predictor = a if pa <= pb and pa <= pc else b if pb <= pc else c
                row[x] = (row[x] + predictor) & 255
        rows.append(bytes(row))
        prev = row
    return rows


def read_png(path):
    parts = list(chunks(path.read_bytes()))
    assert parts[0][0] == b'IHDR'
    w, h, depth, ctype, compression, filtering, interlace = struct.unpack('>IIBBBBB', parts[0][1])
    assert (depth, ctype, compression, filtering, interlace) == (8, 6, 0, 0, 0), 'Expected noninterlaced RGBA8'
    rows = decode_rows(w, h, b''.join(body for kind, body in parts if kind == b'IDAT'))
    return w, h, rows


def cells(path):
    w, h, rows = read_png(path)
    assert h == 100 and w % 100 == 0
    return [[tuple(rows[y][x * 4:x * 4 + 4])
             for y in range(100) for x in range(i * 100, (i + 1) * 100)]
            for i in range(w // 100)]


def header(w, h):
    return chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))


def packed_rows(rows):
    return zlib.compress(b''.join(b'\0' + row for row in rows), 9)


def write_png(name, w, h, rows):
    assert len(rows) == h and all(len(row) == w * 4 for row in rows)
    (ROOT / name).write_bytes(SIG + header(w, h) + chunk(b'IDAT', packed_rows(rows)) + chunk(b'IEND', b''))


def put(frame, x, y, colour=R):
    assert 0 <= x < 100 and 0 <= y < 100
    assert frame[y * 100 + x] == T, 'Jet overwrote body'
    frame[y * 100 + x] = colour


def hover(frame, cx, y0, length, phase):
    for j in range(length):
        offsets = (-1, 0, 1) if j < 2 else (0, 1) if j < length - 1 else (phase % 2,)
        for dx in offsets:
            put(frame, cx + dx, y0 + j)


def sprint_jet(frame, moving, side, phase):
    # Keep the moving art's actual flame roots, then extend with ordered serrations.
    xs = range(41, 49) if side == 0 else range(51, 59)
    for y in range(48, 54):
        for x in xs:
            if moving[phase][y * 100 + x] == R:
                put(frame, x, y)
    cx = 44 if side == 0 else 55
    length = POWER[phase]
    for j in range(6, length):
        left, right = (-2, 3) if j < 15 else (-2, 2) if j < 24 else (-1, 2) if j < 30 else (-1, 1)
        if j >= length - 3:
            left, right = (0, 0)
        sway = (0, 0, 1, 0, -1, 0)[(j // 4 + phase + side) % 6] if j >= 18 else 0
        for dx in range(left, right + 1):
            # Serrated silhouette and sparse alternating voids, never a soft edge.
            if dx in (left, right) and j % 4 == phase % 4 and right > left:
                continue
            if j >= 20 and (j + phase) % 5 == 0 and dx == (phase % 2):
                continue
            put(frame, cx + dx + sway, 48 + j)
    # Separate short streaks and single sparks beyond the flame, ending at y 91..95.
    for offset, dx in ((2, 0), (3, 0), (6, -1), (8, 1)):
        y = min(95, 48 + length + offset)
        x = cx + dx + (1 if side == 1 else -1) * (phase % 2)
        if frame[y * 100 + x] == T:
            put(frame, x, y)


# Deliberate one-pixel paths in original frame-0 coordinates: pods -> shoulders -> helmet.
CRACK_PATHS = (
    ((45, 43), (44, 42), (44, 41), (45, 40), (46, 39), (46, 38),
     (45, 37), (45, 36), (46, 35), (47, 34), (47, 33), (48, 32),
     (48, 31), (49, 30), (49, 29), (50, 28)),
    ((54, 43), (55, 42), (55, 41), (54, 40), (53, 39), (53, 38),
     (54, 37), (54, 36), (53, 35), (52, 34), (52, 33), (51, 32),
     (51, 31), (50, 30), (50, 29)),
    ((50, 27), (50, 26), (49, 25), (49, 24), (50, 23), (50, 22),
     (49, 21), (49, 20), (50, 19)),
    ((47, 34), (46, 33), (45, 33), (44, 32), (43, 33)),
    ((52, 34), (53, 33), (54, 33), (55, 32), (56, 33)),
)
CRACK_LENGTHS = ((0, 0, 0, 0, 0), (3, 0, 0, 0, 0), (6, 4, 0, 0, 0),
                 (9, 8, 0, 0, 0), (12, 11, 0, 2, 2), (16, 15, 3, 4, 4),
                 (16, 15, 6, 5, 5), (16, 15, 9, 5, 5))


def caught_edits(original, index):
    cracks = {p for path, n in zip(CRACK_PATHS, CRACK_LENGTHS[index]) for p in path[:n]}
    assert all(original[y * 100 + x][3] == 255 for x, y in cracks)
    edits = {}
    # Black crack banks darken the figure without changing its shape or redrawing it.
    if index >= 3:
        for x, y in cracks:
            directions = ((-1, 0),) if index < 6 else ((-1, 0), (0, 1))
            for dx, dy in directions:
                q = (x + dx, y + dy)
                if original[q[1] * 100 + q[0]] == G:
                    edits[q] = K
    # Existing eyes are BLACK, not emissive. Collapse the grey rims into dark sockets.
    sockets = ((47, 21), (47, 22), (52, 21), (52, 22),
               (48, 20), (48, 23), (51, 20), (51, 23))
    eye_count = (0, 0, 1, 2, 4, 6, 8, 8)[index]
    for q in sockets[:eye_count]:
        assert original[q[1] * 100 + q[0]] == G
        edits[q] = K
    edits.update({q: R for q in cracks})
    return edits, cracks


def build(original, moving):
    idle = []
    for i, body in enumerate(original):
        frame = body.copy()
        hover(frame, 45, 48 + BOB[i], HOVER_L[i], i)
        hover(frame, 54, 48 + BOB[i], HOVER_R[i], i + 3)
        idle.append(frame)
    sprint = []
    for i in range(6):
        frame = original[0].copy()
        sprint_jet(frame, moving, 0, i)
        sprint_jet(frame, moving, 1, (i + 2) % 6)
        sprint.append(frame)
    caught = []
    for i in range(8):
        frame = original[0].copy()
        if i == 0:
            hover(frame, 45, 48, 6, 0)
            hover(frame, 54, 48, 5, 1)
        elif i == 1:
            hover(frame, 45, 48, 9, 1)
            hover(frame, 54, 48, 3, 0)
        elif i == 2:
            hover(frame, 54, 48, 6, 1)
            put(frame, 45, 51)
            put(frame, 44, 54)
        elif i == 3:
            put(frame, 54, 50)
            put(frame, 55, 54)
        edits, _ = caught_edits(original[0], i)
        for (x, y), colour in edits.items():
            frame[y * 100 + x] = colour
        caught.append(frame)
    return {'idle': idle, 'sprint': sprint, 'caught': caught}


def strip_rows(frames):
    return [bytes(c for frame in frames for p in frame[y * 100:(y + 1) * 100] for c in p)
            for y in range(100)]


def enlarged(frame, scale, background=K, box=(0, 0, 100, 100)):
    x0, y0, x1, y1 = box
    rows = []
    for y in range(y0, y1):
        row = b''.join(bytes(frame[y * 100 + x] if frame[y * 100 + x][3] else background) * scale
                       for x in range(x0, x1))
        rows.extend([row] * scale)
    return rows


def write_apng(name, frames):
    data = bytearray(SIG + header(600, 600))
    data += chunk(b'acTL', struct.pack('>II', len(frames), 1 if name == 'caught' else 0))
    seq = 0
    for i, frame in enumerate(frames):
        # Full opaque frame, SOURCE blend, NONE disposal. Final caught frame stays displayed.
        data += chunk(b'fcTL', struct.pack('>IIIIIHHBB', seq, 600, 600, 0, 0, 1, 10, 0, 0))
        seq += 1
        payload = packed_rows(enlarged(frame, 6))
        if i == 0:
            data += chunk(b'IDAT', payload)
        else:
            data += chunk(b'fdAT', struct.pack('>I', seq) + payload)
            seq += 1
    data += chunk(b'IEND', b'')
    (ROOT / (name + '.apng')).write_bytes(data)


def preview(animations):
    # Common crop removes only empty cell margins; coordinates and scale stay identical.
    box = (37, 12, 63, 98)
    bg = (16, 16, 16, 255)
    cell_w, cell_h = 26 * 8, 86 * 8
    gutter = 8
    w = 8 * (cell_w + gutter) + gutter
    rows = [bytes(K) * w] * gutter
    for frames in animations.values():
        tiles = [enlarged(f, 8, bg, box) for f in frames]
        for y in range(cell_h):
            row = bytes(K) * gutter
            for i in range(8):
                row += tiles[i][y] if i < len(tiles) else bytes(K) * cell_w
                row += bytes(K) * gutter
            rows.append(row)
        rows.extend([bytes(K) * w] * gutter)
    write_png('preview.png', w, len(rows), rows)


def verify_apng(name, frames):
    parts = list(chunks((ROOT / (name + '.apng')).read_bytes()))
    assert parts[0][1] == struct.pack('>IIBBBBB', 600, 600, 8, 6, 0, 0, 0)
    assert [b for k, b in parts if k == b'acTL'] == [struct.pack('>II', len(frames), 1 if name == 'caught' else 0)]
    seq = 0
    payloads = []
    for kind, body in parts:
        if kind == b'fcTL':
            assert struct.unpack('>IIIIIHHBB', body) == (seq, 600, 600, 0, 0, 1, 10, 0, 0)
            seq += 1
            payloads.append(bytearray())
        elif kind == b'IDAT':
            assert len(payloads) == 1
            payloads[-1].extend(body)
        elif kind == b'fdAT':
            assert struct.unpack('>I', body[:4])[0] == seq
            seq += 1
            payloads[-1].extend(body[4:])
    assert len(payloads) == len(frames)
    for frame, payload in zip(frames, payloads):
        assert decode_rows(600, 600, payload) == enlarged(frame, 6), 'APNG pixels differ'


def verify(original, moving):
    assert len(original) == 8 and len(moving) == 6
    base = original[0]
    for i, frame in enumerate(original):
        for y in range(100):
            for x in range(100):
                sy = y - BOB[i]
                expected = base[sy * 100 + x] if 0 <= sy < 100 else T
                assert frame[y * 100 + x] == expected, 'Unexpected original bob'
    for frame in moving:
        assert [T if p == R else p for p in frame] == base, 'Moving body differs'
    counts = {'idle': 8, 'sprint': 6, 'caught': 8}
    previous_cracks = set()
    for name, count in counts.items():
        frames = cells(ROOT / (name + ' player.png'))
        assert len(frames) == count
        assert {p for frame in frames for p in frame} == PALETTE, 'Palette mismatch'
        for i, frame in enumerate(frames):
            ref = original[i] if name == 'idle' else base
            edits, cracks = caught_edits(base, i) if name == 'caught' else ({}, set())
            if name == 'caught':
                assert previous_cracks <= cracks, 'Cracks must spread monotonically'
                previous_cracks = cracks
            for n, source in enumerate(ref):
                x, y = n % 100, n // 100
                if source[3]:
                    assert frame[n] == edits.get((x, y), source), ('Body changed', name, i, x, y)
                elif frame[n] != T:
                    assert frame[n] == R and 41 <= x <= 58
                    assert 48 + (BOB[i] if name == 'idle' else 0) <= y <= 95, 'Jet outside allowed region'
            jets = [(n % 100, n // 100) for n, p in enumerate(frame) if p == R and ref[n] == T]
            if name == 'idle':
                assert all(y <= 53 + BOB[i] for x, y in jets)
                assert any(x < 50 for x, y in jets) and any(x > 50 for x, y in jets)
            elif name == 'sprint':
                assert max(y for x, y in jets if x < 50) >= 90
                assert max(y for x, y in jets if x > 50) >= 90
            elif i >= 4:
                assert not jets, 'Caught jets did not extinguish'
        verify_apng(name, frames)
        print(f'PASS {name}: {count * 100}x100, {count} cells, exact palette, original body, APNG 600x600 / 10 fps')
    w, h, rows = read_png(ROOT / 'preview.png')
    assert (w, h) == (1736, 2096)
    print('PASS preview: 1736x2096, 8x nearest-neighbour, rows idle / sprint / caught')


def notes(animations):
    lines = ['# Jewel of the Devil - player animations', '',
             'Run `python -B make_player.py` to rebuild; `python -B make_player.py --verify-only` to validate existing files.',
             'Python 3 standard library only. No image libraries, external tools, randomness, or generated character art.', '',
             '## Layout and playback', '',
             '- RGBA strips: idle 800x100, sprint 600x100, caught 800x100. Slice into 100x100 cells, left to right; retain the existing pivot.',
             '- All animations run at 10 fps. Idle loops in 0.8 s; sprint loops in 0.6 s; caught plays once in 0.8 s and holds frame 8.',
             '- APNGs: full 100x100 cells enlarged to 600x600 by exact 6x pixel replication, black background. acTL plays=0 for loops, plays=1 for caught; final disposal=NONE.',
             '- preview.png: top idle, middle sprint (two empty spaces), bottom caught. Every frame is enlarged exactly 8x, with the SAME crop x37..62 / y12..97 to remove empty margins. Dark background (16,16,16); black gutters. This review background is not part of the exported sprite palette.', '',
             '## Idle, frame by frame', '',
             'Copies each corresponding original idle frame exactly. Flames start two rows below the lowest body pixel, preserving the moving strip\'s one empty row below the pods. Flame width is 3 pixels at the root, then 2, then 1. The two fixed flicker sequences are offset from each other and the bob.', '',
             '| Frame | Body y offset from original frame 1 | Left / right flame height |',
             '|---|---:|---:|']
    for i in range(8):
        lines.append(f'| {i+1} | {BOB[i]:+d} | {HOVER_L[i]} / {HOVER_R[i]} px |')
    lines += ['', 'The source actually spans y14..47 across its bob cycle: offsets 0,-1,-2,-3,-2,-1,0,+1. Each transition is one pixel. This sequence is retained verbatim, including the wrap.', '',
              '## Sprint, frame by frame', '',
              'Original idle frame 1 is fixed in place, exactly matching the moving strip body. Both roots copy six rows of red pixels from the corresponding moving flame. Long serrated red plumes taper into separated two-pixel streaks and single sparks; patterned transparent holes supply fire texture. Right flame phase leads left by two frames.', '',
              '| Frame | Left / right plume length | Lowest spark y (left / right) |', '|---|---:|---:|']
    for i, frame in enumerate(animations['sprint']):
        tails = [max(n // 100 for n, p in enumerate(frame) if p == R and (n % 100 < 50) == (side == 0)) for side in (0, 1)]
        lines.append(f'| {i+1} | {POWER[i]} / {POWER[(i+2)%6]} px | {tails[0]} / {tails[1]} |')
    lines += ['', 'Moving flames end at y59..72. Sprint flames and sparks reach y90..95 in every frame; idle flames are only 4..6 pixels tall.', '',
              '## Caught, frame by frame', '',
              'All frames copy original idle frame 1, with no displacement. The silhouette and all unedited body pixels are retained. Only explicit red crack paths, their adjoining black banks, and eye socket pixels are changed.', '',
              '| Frame | Action |', '|---|---|',
              '| 1 | Intact body; small 6 / 5 px hover jets. |',
              '| 2 | Left jet surges to 9 px while right shrinks to 3 px; first red fissure opens in left pod. |',
              '| 3 | Left jet breaks into two embers; right coughs once at 6 px. Cracks climb both pods; first eye rim pixel darkens. |',
              '| 4 | Last two right-side embers. Cracks spread upward with black banks; left eye socket collapses. |',
              '| 5 | Jets fully gone. Cracks reach shoulders and branch outward; both eye sockets darken. |',
              '| 6 | Cracks cross the neck into the helmet. Black fissure banks deepen; eye rims continue to disappear. |',
              '| 7 | Cracks climb through the helmet; both eye rims are extinguished. |',
              '| 8 | Final branching red cracks reach the helmet crown. Dark, recognizable figure; no jets. Hold this frame. |', '',
              'The original eye slits are already black, with no red light to switch off. Their surrounding grey pixels darken into black sockets (8 pixels total); no new glowing eyes are introduced. The darker final body comes from black banks directly beside cracks, not a recolour of the whole character.', '',
              '## Self-check against hard rules', '',
              '1. Both original PNGs are read with a CRC-checking RGBA PNG decoder using struct and zlib, supporting filters 0..4. All bodies are copied from original idle cells. Moving flame roots are sampled from bost player.png. Caught changes are restricted to the documented crack/eye mask.',
              '2. Cells are exactly 100x100. Idle retains the original bob; sprint/caught use original idle frame 1 at its unchanged position. No scaling, rotation, or added displacement in any strip.',
              '3. Every strip contains exactly transparent (0,0,0,0), opaque black (0,0,0,255), grey (70,70,70,255), and red (237,28,36,255). Alpha is binary. Previews composite transparency onto the requested backgrounds without smoothing.',
              '4. All patterns, phase offsets, paths, and timing are explicit and deterministic. Fixed PNG/APNG chunk order and zlib level 9; no timestamps or random values. Repeated execution in the same Python/zlib environment produces identical bytes.',
              '5. The only write targets are the eight requested artifacts next to this script. Source PNGs are opened read-only. Run with -B to suppress Python import caches.', '',
              '## Automated verification', '',
              'Every build decodes the written strips, checks PNG CRCs and dimensions, checks the exact RGBA palette, and compares every original opaque pixel to its corresponding source frame (or the explicitly allowed caught edits). It checks that all additional pixels are red jets confined below the pods, that hover is short, that sprint reaches beyond moving, and that caught has no jets from frame 5 onward.',
              'It also decodes every APNG frame, compares it byte for byte to the strip composited at 6x, and verifies sequence numbers, 100 ms delays, frame counts, looping, and hold disposal. Source SHA-256 digests are checked before/after each build.']
    (ROOT / 'NOTES.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')


def main():
    assert sys.argv[1:] in ([], ['--verify-only']), 'Usage: python -B make_player.py [--verify-only]'
    sources = [SOURCE / 'idle player.png', SOURCE / 'bost player.png']
    before = [hashlib.sha256(p.read_bytes()).digest() for p in sources]
    original, moving = (cells(p) for p in sources)
    if '--verify-only' not in sys.argv:
        animations = build(original, moving)
        for name, frames in animations.items():
            write_png(name + ' player.png', len(frames) * 100, 100, strip_rows(frames))
            write_apng(name, frames)
        preview(animations)
        notes(animations)
    verify(original, moving)
    assert before == [hashlib.sha256(p.read_bytes()).digest() for p in sources], 'Sources changed'
    print('PASS source files unchanged')


if __name__ == '__main__':
    main()
