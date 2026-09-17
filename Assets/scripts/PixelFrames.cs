using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The story's pictures, drawn at run time as 64x40 frames in the game's three colours: the four
/// wordless opening beats and the two ending beats. There is no fourth colour to fade with, so
/// light and shadow are dithered, exactly as the game's own sprites are. Frames are rasterised into
/// point-filtered textures once per variant and cached, so playing them costs nothing.
/// </summary>
public static class PixelFrames
{
    public const int Width = 64;
    public const int Height = 40;

    public enum Frame
    {
        WorldGoingOut,  // a grey planet cracking beside a staff with an empty socket
        WayDown,        // the crowned figure walks toward a red ring
        Theft,          // the jewel lifted from under a horned head; its eyes open
        FallsInSchool,  // the school floorplan dissolving into black, the jewel dropped
        BurnsOpen,      // the portal lit, the player running for it, devils behind
        Filled,         // the socket filled, the planet veined red, one eye opening
    }

    const byte K = 0, G = 1, R = 2;

    static readonly Color32[] Palette =
    {
        new Color32(0, 0, 0, 255),
        new Color32(70, 70, 70, 255),
        new Color32(237, 28, 36, 255),
    };

    static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 },
    };

    static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

    /// <summary>Which drawing a frame shows t seconds in. Most blink at 2 Hz; the last one opens its eye.</summary>
    public static int Variant(Frame frame, float t)
    {
        if (frame == Frame.Filled) return t >= 1.6f ? 1 : 0;
        return Mathf.FloorToInt(t / 0.5f) % 2;
    }

    /// <summary>A sprite of the frame at pixelsPerUnit 1, so 64x40 units; size the Image yourself.</summary>
    public static Sprite Get(Frame frame, int variant)
    {
        int key = (int)frame * 8 + variant;
        if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

        Texture2D texture = Render(frame, variant);
        sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), 1f, 0,
                               SpriteMeshType.FullRect);
        sprite.name = texture.name;
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Draws one frame into a fresh texture. Pass keepReadable when the pixels have to be read back
    /// afterwards (the frame dump tool does); the game itself never reads them, so the default
    /// drops the CPU copy.
    /// </summary>
    public static Texture2D Render(Frame frame, int variant, bool keepReadable = false)
    {
        var d = new byte[Width * Height];
        bool b = variant == 1;
        switch (frame)
        {
            case Frame.WorldGoingOut: WorldGoingOut(d, b); break;
            case Frame.WayDown: WayDown(d, b); break;
            case Frame.Theft: Theft(d, b); break;
            case Frame.FallsInSchool: FallsInSchool(d, b); break;
            case Frame.BurnsOpen: BurnsOpen(d, b); break;
            case Frame.Filled: Filled(d, b); break;
        }

        var pixels = new Color32[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            // Row 0 of the drawing is the top; texture row 0 is the bottom.
            int ty = Height - 1 - y;
            for (int x = 0; x < Width; x++) pixels[ty * Width + x] = Palette[d[y * Width + x]];
        }

        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            name = $"Frame_{frame}_{variant}",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, !keepReadable);
        return texture;
    }

    // ------------------------------------------------------------------ the frames

    static void WorldGoingOut(byte[] d, bool b)
    {
        Planet(d);
        // Broad, branching faults break the silhouette as well as the surface.
        Veins(d, K);
        Line(d, 24, 8, 21, 15, K);
        Line(d, 21, 15, 23, 22, K);
        Rect(d, 29, 11, 5, 3, K);
        Rect(d, 31, 25, 4, 3, K);
        Rect(d, 25, 32, 5, 2, K);
        int drift = b ? 1 : 0;
        Rect(d, 34 + drift, 11 - drift, 2, 2, G);
        Rect(d, 36 + drift, 26 + drift, 2, 2, G);
        Px(d, 40 + drift, 30 + drift, G);
        Px(d, 38 + drift, 7 - drift, G);
        Px(d, 29 + drift, 36 + drift, G);
        Staff(d, false);
    }

    static void WayDown(byte[] d, bool b)
    {
        // Broken flagstones vanish at the threshold. Nothing answers from inside.
        Line(d, 3, 35, 36, 29, G);
        Line(d, 20, 39, 39, 30, G);
        Line(d, 9, 34, 14, 37, G);
        Line(d, 23, 31, 26, 34, G);
        FadeGrey(d, (x, y) => y > 34 ? 5f : 10f);
        Ring(d, 46, 18, 13, R, 2);
        // Two heavy stones interrupt the rim, recalling the portal's sockets.
        Rect(d, 33, 21, 4, 4, G); Rect(d, 55, 21, 4, 4, G);
        Rect(d, 34, 22, 2, 2, K); Rect(d, 56, 22, 2, 2, K);
        if (b) { Px(d, 41, 6, K); Px(d, 57, 12, K); }
        else { Px(d, 50, 6, K); Px(d, 35, 12, K); }
        Sp(d, b ? 20 : 19, 20, b ? PlayerStride : Player);
    }

    static void Theft(byte[] d, bool b)
    {
        Sp(d, 20, 0, Demon, 2);
        // Rows 16..25 stay entirely black for the existing centred opening card.
        // The reaching hand and lifted jewel act out the theft below the words.
        Sp(d, 18, 27, Player);
        Line(d, 24, 32, 29, b ? 29 : 31, G);
        Line(d, 24, 33, 29, b ? 30 : 32, G);
        Sp(d, b ? 28 : 31, b ? 26 : 28, Jewel);
        Rect(d, 30, 35, 10, 2, G);
        Rect(d, 32, 37, 6, 3, G);
        // A rough plinth; no line or ornament enters the text band.
        Px(d, 31, 36, K); Px(d, 38, 36, K);
    }

    static void FallsInSchool(byte[] d, bool b)
    {
        // Paired corridor walls and actual door gaps make this a building, not a grid.
        Box(d, 5, 5, 54, 30, G); Box(d, 6, 6, 52, 28, G);
        Rect(d, 7, 16, 50, 2, G); Rect(d, 7, 22, 50, 2, G);
        Rect(d, 20, 7, 2, 9, G); Rect(d, 35, 7, 2, 9, G);
        Rect(d, 47, 7, 2, 9, G);
        Rect(d, 24, 24, 2, 9, G); Rect(d, 42, 24, 2, 9, G);
        Rect(d, 12, 16, 4, 2, K); Rect(d, 27, 16, 4, 2, K);
        Rect(d, 40, 16, 4, 2, K); Rect(d, 17, 22, 4, 2, K);
        Rect(d, 32, 22, 4, 2, K); Rect(d, 8, 33, 5, 2, K);
        // The ragged darkness advances and retreats by two pixels at each flicker.
        FadeGrey(d, (x, y) => x < 28 ? 16f :
            16f - (x - 28 + (b ? 2 : 0)) * 0.65f - (y % 7) * 0.5f);
        Rect(d, 57, 0, 7, Height, K);
        Sp(d, 32, 9, Scar, 1, b ? '\0' : 'r', 'g');
        Sp(d, 43, 24, Scar);
        Sp(d, 9, 25, Jewel, 1, b ? '\0' : 'r', 'g');
    }

    static void BurnsOpen(byte[] d, bool b)
    {
        Ring(d, 47, 20, 13, R, b ? 3 : 2);
        // Unequal tongues cling to the rim; the centre remains unknowable black.
        Sp(d, 38, b ? 2 : 3, Flame);
        Sp(d, 49, b ? 1 : 2, Flame);
        Sp(d, 58, b ? 13 : 15, Flame);
        Line(d, 38, 29, b ? 34 : 36, 35, R);
        Line(d, 53, 31, b ? 56 : 54, 37, R);
        Rect(d, 34, 23, 4, 4, G); Rect(d, 56, 23, 4, 4, G);
        Rect(d, 35, 24, 2, 2, R); Rect(d, 57, 24, 2, 2, R);
        // The jewel sits in the outstretched hand, never floats above the runner.
        int step = b ? 1 : 0;
        Sp(d, 21 + step, 20, b ? PlayerStride : Player);
        Rect(d, 27 + step, 25, 3, 2, G);
        Sp(d, 26, 18, Jewel);
        Sp(d, 1, 9, Demon);
        Sp(d, 8, 27, Demon);
        // Broad shoulders dissolve into the dark; a hooked arm reaches after the thief.
        for (int y = 17; y <= 23; y++)
            Rect(d, 4 - (y - 17) / 2, y, 6 + y - 17, 1, G);
        for (int y = 35; y < Height; y++)
            Rect(d, 11 - (y - 35) / 2, y, 6 + y - 35, 1, G);
        FadeGrey(d, (x, y) => x > 21 ? 16f : y > 34 ? 14f - (y - 34) * 3f :
            y > 16 && y < 25 ? 16f - (y - 16) * 2f : 16f);
        Line(d, 13, 21, b ? 20 : 18, 23, G);
        Line(d, b ? 20 : 18, 23, b ? 19 : 17, 25, G);
    }

    static void Filled(byte[] d, bool eyeOpen)
    {
        Planet(d);
        Veins(d, R);
        Staff(d, true);
        // A single crooked current links the returned jewel to the repaired world.
        Line(d, 48, 10, 41, 14, R);
        Line(d, 41, 14, 39, 19, R);
        Line(d, 39, 19, 32, 23, R);
        Sp(d, 53, 33, eyeOpen ? Eye : EyeShut);
    }

    // Shared silhouettes make the last shot answer the first without another word.
    static void Planet(byte[] d)
    {
        Disc(d, 19, 21, 13, G);
        FadeGrey(d, (x, y) => x < 12 ? 4f : x < 16 ? 8f : y > 29 ? 11f : 16f);
        // Two broken land masses, drawn with black cutouts rather than another shade.
        Rect(d, 17, 11, 5, 2, K); Rect(d, 15, 13, 4, 3, K);
        Rect(d, 16, 16, 2, 2, K);
        Rect(d, 24, 26, 4, 3, K); Rect(d, 23, 28, 3, 3, K);
    }

    static void Veins(byte[] d, byte c)
    {
        Line(d, 24, 9, 22, 15, c); Line(d, 22, 15, 24, 21, c);
        Line(d, 24, 21, 21, 26, c); Line(d, 21, 26, 22, 33, c);
        Line(d, 24, 21, 29, 23, c); Line(d, 29, 23, 32, 23, c);
        Line(d, 22, 18, 17, 20, c); Line(d, 17, 20, 7, 18, c);
        Line(d, 21, 26, 15, 28, c); Line(d, 15, 28, 12, 32, c);
    }

    static void Staff(byte[] d, bool full)
    {
        Rect(d, 50, 13, 2, 14, G);
        Rect(d, 49, 26, 2, 7, G);
        Rect(d, 48, 32, 2, 3, G);
        Sp(d, 46, 3, full ? SocketFull : Socket);
    }

    // ------------------------------------------------------------------ sprites, at the scale the game draws its icons

    static readonly string[] Player =
    {
        ".g.g.g.", ".ggggg.", "..ggg..", "..ggg..", ".gggg..", ".ggggg.",
        "gggggg.", "gggggg.", ".gggg..", ".gg.gg.", "gg..gg.",
    };

    static readonly string[] PlayerStride =
    {
        ".g.g.g.", ".ggggg.", "..ggg..", "..ggg..", ".gggg..", ".ggggg.",
        "gggggg.", "gggggg.", ".gggg..", "gg..gg.", ".gg..gg",
    };

    static readonly string[] Demon =
    {
        "gg........gg", "ggg......ggg", ".gggggggggg.", "gggggggggggg",
        "gg.rrggrr.gg", ".gggggggggg.", "..ggg..ggg..", "...gggggg...",
    };

    static readonly string[] Jewel =
    {
        "..rrr..", ".rrrrr.", "rr.rrrr", "rrrrrrr", ".rrrrr.", "..rrr..", "...r...",
    };
    static readonly string[] Scar =
    {
        ".rr....", "rr..rr.", "r..r.rr", "..rr..r", ".r...r.", "..rrr..", "...r...",
    };
    static readonly string[] Flame =
    {
        "...r.", "..rr.", "..r..", ".rr.r", "rrrrr", ".rrr.", "..rr.",
    };
    static readonly string[] Eye =
    {
        "...rrrrr...", ".rr.....rr.", "r....r....r", ".rr..r..rr.", "...rrrrr...",
    };
    static readonly string[] EyeShut =
    {
        "...........", "...........", "gg.......gg", "..gg...gg..", "....ggg....",
    };
    static readonly string[] Socket =
    {
        "gg.....gg", ".gg...gg.", ".ggggggg.", "gg.....gg", "gg.....gg",
        "gg.....gg", ".gg...gg.", "..ggggg..", "...ggg...", "....gg...",
    };
    static readonly string[] SocketFull =
    {
        "gg.....gg", ".gg...gg.", ".ggggggg.", "gg.rrr.gg", "ggrr.rrgg",
        "ggrrrrrgg", ".ggrrrgg.", "..ggrgg..", "...ggg...", "....gg...",
    };

    // ------------------------------------------------------------------ rasteriser

    static void Px(byte[] d, int x, int y, byte c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        d[y * Width + x] = c;
    }

    static void Rect(byte[] d, int x, int y, int w, int h, byte c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Px(d, i, j, c);
    }

    static void Box(byte[] d, int x, int y, int w, int h, byte c)
    {
        Rect(d, x, y, w, 1, c); Rect(d, x, y + h - 1, w, 1, c);
        Rect(d, x, y, 1, h, c); Rect(d, x + w - 1, y, 1, h, c);
    }

    static void Line(byte[] d, int x0, int y0, int x1, int y1, byte c)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int e = dx + dy;
        for (;;)
        {
            Px(d, x0, y0, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * e;
            if (e2 >= dy) { e += dy; x0 += sx; }
            if (e2 <= dx) { e += dx; y0 += sy; }
        }
    }

    static void Disc(byte[] d, int cx, int cy, int r, byte c)
    {
        for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
                if (i * i + j * j <= r * r + r * 0.5f) Px(d, cx + i, cy + j, c);
    }

    static void Ring(byte[] d, int cx, int cy, int r, byte c, int t)
    {
        int ri = r - t;
        for (int j = -r; j <= r; j++)
            for (int i = -r; i <= r; i++)
            {
                int q = i * i + j * j;
                if (q <= r * r + r * 0.5f && q > ri * ri + ri * 0.5f) Px(d, cx + i, cy + j, c);
            }
    }

    static void Sp(byte[] d, int x, int y, string[] rows, int s = 1, char swapFrom = '\0', char swapTo = '\0')
    {
        for (int j = 0; j < rows.Length; j++)
            for (int i = 0; i < rows[j].Length; i++)
            {
                char ch = rows[j][i];
                if (ch == '.') continue;
                if (ch == swapFrom) ch = swapTo;
                Rect(d, x + i * s, y + j * s, s, s, ch == 'r' ? R : ch == 'g' ? G : K);
            }
    }

    /// <summary>Grey pixels fade to black by dither wherever the level function drops below 16.</summary>
    static void FadeGrey(byte[] d, System.Func<int, int, float> level)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (d[y * Width + x] == G && Bayer[y & 3, x & 3] >= level(x, y)) d[y * Width + x] = K;
    }
}
