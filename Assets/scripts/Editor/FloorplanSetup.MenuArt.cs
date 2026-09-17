/// <summary>
/// Pixel art for the title screen. Kept apart from the gameplay icons so the menu can be
/// redrawn without touching the scene builder.
/// </summary>
static partial class FloorplanSetup
{
    static readonly string[] TitleArt =
    {
        ".w....................................w.",
        "ww....................................ww",
        "ww....................................ww",
        "ww....................................ww",
        "ww...............wwwwww...............ww",
        "ww..............w.wwww.w..............ww",
        "www............w.wwwwww.w............www",
        "www...........wwwwwwwwwwww...........www",
        ".www..........wwwwwwwwwwww..........www.",
        ".www..........wwwwwwwwwwww..........www.",
        "..www..........wwwwwwwwww..........www..",
        "..wwww.........wwwwwwwwww.........wwww..",
        "...wwww.........wwwwwwww.........wwww...",
        "....wwww........wwwwwwww........wwww....",
        ".....wwww........wwwwww........wwww.....",
        ".....wwwww.......wwwwww.......wwwww.....",
        "......wwwww.......wwww.......wwwww......",
        ".......wwwww......wwww......wwwww.......",
        ".......wwwww.......ww.......wwwww.......",
    };

    static readonly string[] ArrowLeftArt =
    {
        ".......ww",
        "......www",
        ".....wwww",
        "....wwwww",
        "...wwwwww",
        "..wwwwwww",
        "wwwwwwwww",
        "wwwwwwwww",
        "..wwwwwww",
        "...wwwwww",
        "....wwwww",
        ".....wwww",
        "......www",
        ".......ww",
    };

    static readonly string[] ArrowRightArt =
    {
        "ww.......",
        "www......",
        "wwww.....",
        "wwwww....",
        "wwwwww...",
        "wwwwwww..",
        "wwwwwwwww",
        "wwwwwwwww",
        "wwwwwww..",
        "wwwwww...",
        "wwwww....",
        "wwww.....",
        "www......",
        "ww.......",
    };

    static readonly string[] SpeakerOnArt =
    {
        "...........ww...",
        "...........ww...",
        "......ww.....ww.",
        "......ww.....ww.",
        "....wwww..ww..ww",
        "wwwwwwww...ww.ww",
        "wwwwwwww...ww.ww",
        "wwwwwwww...ww.ww",
        "wwwwwwww...ww.ww",
        "....wwww..ww..ww",
        "......ww.....ww.",
        "......ww.....ww.",
        "...........ww...",
        "...........ww...",
    };

    static readonly string[] SpeakerOffArt =
    {
        "................",
        "................",
        "......ww........",
        "......ww.ww...ww",
        "....wwww.www.www",
        "wwwwwwww..wwwww.",
        "wwwwwwww...www..",
        "wwwwwwww...www..",
        "wwwwwwww..wwwww.",
        "....wwww.www.www",
        "......ww.ww...ww",
        "......ww........",
        "................",
        "................",
    };

    static readonly string[] FrameCornerArt =
    {
        "wwwwwwwwww",
        "wwwwwwwwww",
        "ww........",
        "ww........",
        "ww........",
        "ww........",
        "ww........",
        "ww........",
        "ww........",
        "ww........",
    };

    static readonly string[] DemonHeadArt =
    {
        "ww........ww",
        "ww........ww",
        "www......www",
        ".www....www.",
        ".wwwwwwwwww.",
        "wwwwwwwwwwww",
        "ww..wwww..ww",
        "ww..wwww..ww",
        "wwwwwwwwwwww",
        ".wwwwwwwwww.",
        "..wwwwwwww..",
        "...wwwwww...",
    };
}
