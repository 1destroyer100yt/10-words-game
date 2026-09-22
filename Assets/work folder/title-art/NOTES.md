# Title and cover art - second pass

Kept the composition, curved horn pair, angular face, two red eyes, pendant and chain, red tower window and red hall doorway. Rebuilt the cloak with a solid neck, collar and shoulders, downward cloth folds, and ordered fading only at the outer margins and hem. Replaced all brick strokes with broad Bayer-shaded stone surfaces; roofs are solid grey, and black window recesses have grey frames and sills. The cover's rear corridor now also has a filled wall and roof.

Visual review covered both complete compositions: removed the E/reversed-E masonry marks, window crossbars and hollow triangular roofs that could suggest letters. Simplified a cross-like dither repeat to regular checker/grid shading. No text, letters, numbers or runes were identified in the final art; the bell, arches, chain and gem read as objects in context.

Self-check, rules 1-6: (1) Hand-authored Python 3 pixels, standard library only, PNGs written with struct/zlib; no image generation. (2) Exactly three opaque colours: #000000, #464646, #ED1C24. (3) No text or glyphs. (4) No randomness: fixed polygons and 4x4 Bayer shading. (5) One horn pair and one connected cloaked body, no hands; pendant hangs from a chain; front-on walls support roofs, inset openings and a suspended bell; entrance stairs meet their landings. (6) All writes stay in this folder; v1 is unchanged.

Verification: `python verify_art.py` passes PNG decoding and CRCs, dimensions, exact palette, nearest-neighbour scaling and byte-for-byte reproduction. Title is 320x180; preview is exact 4x at 1280x720; cover is composed at 315x250 and exported at exact 2x to 630x500. Menu block and both control corners remain 100% black (zero non-black pixels), matching v1.

Rebuild: `python draw_title.py`. Verify: `python verify_art.py`.
