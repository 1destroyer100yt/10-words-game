using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// On-screen controls for phones and tablets: a stick on the left, and run, hide and throw on the
/// right, with pause in the top left corner. Each one presses the matching part of a virtual gamepad,
/// so the game reads them through the gamepad bindings it already has. They are built on a phone or
/// tablet, or the first time anyone touches the screen: an iPad's browser says it is a Mac, so asking
/// the browser alone misses it. On a computer nobody touches, nothing is built and this only watches.
/// They show only while a run is being played. The art is a placeholder drawn here in the three colours.
/// </summary>
public class TouchControls : MonoBehaviour
{
    static readonly Color Grey = new Color32(70, 70, 70, 255);
    static readonly Color Red = new Color32(237, 28, 36, 255);
    static readonly Color Faint = new Color(0.27f, 0.27f, 0.27f, 0.45f);

    /// <summary>True on phones and tablets, and on any screen that has touch and no mouse.</summary>
    public static bool IsTouchDevice =>
        Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);

    /// <summary>True once the controls are built; the rest of the game can then ignore taps as clicks.</summary>
    public static bool Active { get; private set; }

    static TouchControls instance;
    GameObject playSet, pauseButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("Touch Controls");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TouchControls>();
    }

    static bool TouchedNow()
    {
        Touchscreen screen = Touchscreen.current;
        return screen != null && screen.primaryTouch.press.wasPressedThisFrame;
    }

    // Reload Domain is off: forget the last play session's controls.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        Active = false;
        icons.Clear();
    }

    void Build()
    {
        Active = true;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();

        playSet = new GameObject("Play Controls", typeof(RectTransform));
        playSet.transform.SetParent(transform, false);
        Stretch((RectTransform)playSet.transform);

        Stick(new Vector2(190, 180));
        Button("Throw", "<Gamepad>/buttonWest", new Vector2(-150, 150), 150, Icon.Coin, Red);
        Button("Hide", "<Gamepad>/buttonNorth", new Vector2(-330, 110), 120, Icon.Locker, Grey);
        Button("Run", "<Gamepad>/leftStickPress", new Vector2(-170, 330), 120, Icon.Run, Grey);
        pauseButton = Button("Pause", "<Gamepad>/start", new Vector2(80, -80), 90, Icon.Pause, Grey, topLeft: true);
        pauseButton.transform.SetParent(transform, false);

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureEventSystem();
        Show(false);
    }

    void OnDestroy()
    {
        if (!Active) { if (instance == this) instance = null; return; }
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) { instance = null; Active = false; }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureEventSystem();

    /// <summary>The on-screen controls take their touches from an EventSystem; the game has none of its own.</summary>
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("Touch Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (!Active)
        {
            if (!IsTouchDevice && !TouchedNow()) return;
            Build();
        }

        GameRun run = GameRun.Instance;
        bool playing = run != null && run.AcceptsGameplayInput;
        Show(playing);
        bool paused = run != null && run.IsPaused;
        if (pauseButton.activeSelf != (playing && !paused)) pauseButton.SetActive(playing && !paused);
    }

    void Show(bool visible)
    {
        // Switching a control off releases it, so a thumb left on the stick cannot keep walking.
        if (playSet.activeSelf != visible) playSet.SetActive(visible);
    }

    // ------------------------------------------------------------------ building

    void Stick(Vector2 centre)
    {
        var area = Rect("Stick", playSet.transform, new Vector2(0, 0), centre, new Vector2(240, 240));
        var ring = area.gameObject.AddComponent<Image>();
        ring.sprite = IconSprite(Icon.Ring);
        ring.color = Faint;
        ring.raycastTarget = false;

        var knobRect = Rect("Knob", area, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
        knobRect.gameObject.SetActive(false);
        var knob = knobRect.gameObject.AddComponent<Image>();
        knob.sprite = IconSprite(Icon.Disc);
        knob.color = Grey;
        var stick = knobRect.gameObject.AddComponent<OnScreenStick>();
        stick.controlPath = "<Gamepad>/leftStick";
        stick.movementRange = 90;
        // The thumb does not have to land on the knob: anywhere near it starts the stick there.
        stick.behaviour = OnScreenStick.Behaviour.ExactPositionWithDynamicOrigin;
        stick.dynamicOriginRange = 170;
        knobRect.gameObject.SetActive(true);
    }

    GameObject Button(string name, string path, Vector2 offset, float size, Icon art, Color iconColor, bool topLeft = false)
    {
        Vector2 anchor = topLeft ? new Vector2(0, 1) : new Vector2(1, 0);
        var rect = Rect(name, playSet.transform, anchor, offset, new Vector2(size, size));
        rect.gameObject.SetActive(false);
        var back = rect.gameObject.AddComponent<Image>();
        back.sprite = IconSprite(Icon.Ring);
        back.color = Faint;

        var icon = Rect("Icon", rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));
        var iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = IconSprite(art);
        iconImage.color = iconColor;
        iconImage.raycastTarget = false;

        var button = rect.gameObject.AddComponent<OnScreenButton>();
        button.controlPath = path;
        rect.gameObject.SetActive(true);
        return rect.gameObject;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ placeholder art

    public enum Icon { Ring, Disc, Coin, Locker, Run, Pause }

    static readonly System.Collections.Generic.Dictionary<Icon, Sprite> icons =
        new System.Collections.Generic.Dictionary<Icon, Sprite>();

    /// <summary>The pictures on the controls, shared so the intro can teach them on a phone.</summary>
    public static Sprite IconSprite(Icon icon)
    {
        if (icons.TryGetValue(icon, out Sprite sprite) && sprite != null) return sprite;
        string[] art = icon switch
        {
            Icon.Ring => RingArt,
            Icon.Disc => DiscArt,
            Icon.Coin => CoinArt,
            Icon.Locker => LockerArt,
            Icon.Run => RunArt,
            _ => PauseArt,
        };
        sprite = Art(art);
        icons[icon] = sprite;
        return sprite;
    }

    /// <summary>White pixel art from rows of '#' and '.', tinted by the Image colour.</summary>
    static Sprite Art(string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                texture.SetPixel(x, h - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
    }

    static readonly string[] RingArt =
    {
        "....######....",
        "..##......##..",
        ".#..........#.",
        ".#..........#.",
        "#............#",
        "#............#",
        "#............#",
        "#............#",
        "#............#",
        "#............#",
        ".#..........#.",
        ".#..........#.",
        "..##......##..",
        "....######....",
    };

    static readonly string[] DiscArt =
    {
        "....######....",
        "..##########..",
        ".############.",
        ".############.",
        "##############",
        "##############",
        "##############",
        "##############",
        "##############",
        "##############",
        ".############.",
        ".############.",
        "..##########..",
        "....######....",
    };

    static readonly string[] CoinArt =
    {
        "..####..",
        ".######.",
        "###..###",
        "##.##.##",
        "##.##.##",
        "###..###",
        ".######.",
        "..####..",
    };

    static readonly string[] LockerArt =
    {
        "########",
        "#......#",
        "#.####.#",
        "#......#",
        "#.....##",
        "#......#",
        "#......#",
        "########",
    };

    static readonly string[] RunArt =
    {
        "#...#...",
        "##..##..",
        ".##..##.",
        "..##..##",
        "..##..##",
        ".##..##.",
        "##..##..",
        "#...#...",
    };

    static readonly string[] PauseArt =
    {
        "........",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        "........",
    };
}
