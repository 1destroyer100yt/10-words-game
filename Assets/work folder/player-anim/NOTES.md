# Jewel of the Devil - player animations

Run `python -B make_player.py` to rebuild; `python -B make_player.py --verify-only` to validate existing files.
Python 3 standard library only. No image libraries, external tools, randomness, or generated character art.

## Layout and playback

- RGBA strips: idle 800x100, sprint 600x100, caught 800x100. Slice into 100x100 cells, left to right; retain the existing pivot.
- All animations run at 10 fps. Idle loops in 0.8 s; sprint loops in 0.6 s; caught plays once in 0.8 s and holds frame 8.
- APNGs: full 100x100 cells enlarged to 600x600 by exact 6x pixel replication, black background. acTL plays=0 for loops, plays=1 for caught; final disposal=NONE.
- preview.png: top idle, middle sprint (two empty spaces), bottom caught. Every frame is enlarged exactly 8x, with the SAME crop x37..62 / y12..97 to remove empty margins. Dark background (16,16,16); black gutters. This review background is not part of the exported sprite palette.

## Idle, frame by frame

Copies each corresponding original idle frame exactly. Flames start two rows below the lowest body pixel, preserving the moving strip's one empty row below the pods. Flame width is 3 pixels at the root, then 2, then 1. The two fixed flicker sequences are offset from each other and the bob.

| Frame | Body y offset from original frame 1 | Left / right flame height |
|---|---:|---:|
| 1 | +0 | 5 / 4 px |
| 2 | -1 | 6 / 5 px |
| 3 | -2 | 4 / 6 px |
| 4 | -3 | 5 / 4 px |
| 5 | -2 | 6 / 5 px |
| 6 | -1 | 4 / 6 px |
| 7 | +0 | 5 / 4 px |
| 8 | +1 | 4 / 5 px |

The source actually spans y14..47 across its bob cycle: offsets 0,-1,-2,-3,-2,-1,0,+1. Each transition is one pixel. This sequence is retained verbatim, including the wrap.

## Sprint, frame by frame

Original idle frame 1 is fixed in place, exactly matching the moving strip body. Both roots copy six rows of red pixels from the corresponding moving flame. Long serrated red plumes taper into separated two-pixel streaks and single sparks; patterned transparent holes supply fire texture. Right flame phase leads left by two frames.

| Frame | Left / right plume length | Lowest spark y (left / right) |
|---|---:|---:|
| 1 | 34 / 35 px | 90 / 91 |
| 2 | 38 / 39 px | 94 / 95 |
| 3 | 35 / 36 px | 91 / 92 |
| 4 | 39 / 37 px | 95 / 93 |
| 5 | 36 / 34 px | 92 / 90 |
| 6 | 37 / 38 px | 93 / 94 |

Moving flames end at y59..72. Sprint flames and sparks reach y90..95 in every frame; idle flames are only 4..6 pixels tall.

## Caught, frame by frame

All frames copy original idle frame 1, with no displacement. The silhouette and all unedited body pixels are retained. Only explicit red crack paths, their adjoining black banks, and eye socket pixels are changed.

| Frame | Action |
|---|---|
| 1 | Intact body; small 6 / 5 px hover jets. |
| 2 | Left jet surges to 9 px while right shrinks to 3 px; first red fissure opens in left pod. |
| 3 | Left jet breaks into two embers; right coughs once at 6 px. Cracks climb both pods; first eye rim pixel darkens. |
| 4 | Last two right-side embers. Cracks spread upward with black banks; left eye socket collapses. |
| 5 | Jets fully gone. Cracks reach shoulders and branch outward; both eye sockets darken. |
| 6 | Cracks cross the neck into the helmet. Black fissure banks deepen; eye rims continue to disappear. |
| 7 | Cracks climb through the helmet; both eye rims are extinguished. |
| 8 | Final branching red cracks reach the helmet crown. Dark, recognizable figure; no jets. Hold this frame. |

The original eye slits are already black, with no red light to switch off. Their surrounding grey pixels darken into black sockets (8 pixels total); no new glowing eyes are introduced. The darker final body comes from black banks directly beside cracks, not a recolour of the whole character.

## Self-check against hard rules

1. Both original PNGs are read with a CRC-checking RGBA PNG decoder using struct and zlib, supporting filters 0..4. All bodies are copied from original idle cells. Moving flame roots are sampled from bost player.png. Caught changes are restricted to the documented crack/eye mask.
2. Cells are exactly 100x100. Idle retains the original bob; sprint/caught use original idle frame 1 at its unchanged position. No scaling, rotation, or added displacement in any strip.
3. Every strip contains exactly transparent (0,0,0,0), opaque black (0,0,0,255), grey (70,70,70,255), and red (237,28,36,255). Alpha is binary. Previews composite transparency onto the requested backgrounds without smoothing.
4. All patterns, phase offsets, paths, and timing are explicit and deterministic. Fixed PNG/APNG chunk order and zlib level 9; no timestamps or random values. Repeated execution in the same Python/zlib environment produces identical bytes.
5. The only write targets are the eight requested artifacts next to this script. Source PNGs are opened read-only. Run with -B to suppress Python import caches.

## Automated verification

Every build decodes the written strips, checks PNG CRCs and dimensions, checks the exact RGBA palette, and compares every original opaque pixel to its corresponding source frame (or the explicitly allowed caught edits). It checks that all additional pixels are red jets confined below the pods, that hover is short, that sprint reaches beyond moving, and that caught has no jets from frame 5 onward.
It also decodes every APNG frame, compares it byte for byte to the strip composited at 6x, and verifies sequence numbers, 100 ms delays, frame counts, looping, and hold disposal. Source SHA-256 digests are checked before/after each build.
