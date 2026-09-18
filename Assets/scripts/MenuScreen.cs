using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The title screen, which carries no words at all: the game's ten-word budget is spent entirely on
/// the story, so an emblem, a row of pictograms and a handful of marks do the whole job. It holds
/// the game frozen until the player starts it, which also gives the browser the gesture it needs
/// before any sound will play.
///
/// Difficulty arrows, demon marks and the settings in the top right (fullscreen, volume slider and
/// sound switch) can be used without starting the run.
/// Enter, Space, the play mark or gamepad confirm starts or resumes.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MenuScreen : MonoBehaviour
{
    [Header("Pieces")]
    public Image backdrop;
    public RectTransform root;
    public GameRun run;

    [Header("Art")]
    [Tooltip("The emblem: the jewel between two horns. The largest thing on the screen.")]
    public Sprite title;
    public Sprite keyCap;
    public Sprite mouse;
    public Sprite playMark;
    public Sprite coin;
    public Sprite arrowLeft;
    public Sprite arrowRight;
    public Sprite speakerOn;
    public Sprite speakerOff;
    [Tooltip("L-shaped bracket drawn for the top-left corner and rotated for the other three.")]
    public Sprite frameCorner;
    [Tooltip("A single white pixel, stretched into the rule under the difficulty row.")]
    public Sprite pixel;
    [Tooltip("Small demon mark used for the difficulty pips.")]
    public Sprite demonMark;
    [Tooltip("A to Z, for the letters printed on the key caps.")]
    public Sprite[] letters;
    [Tooltip("0-9 then ':' and '.', for the best time under the prompt.")]
    public Sprite[] digits;

    [Header("Look")]
    public Color markColor = new Color32(237, 28, 36, 255);
    public Color hintColor = new Color32(70, 70, 70, 255);
    public float blinkSeconds = 0.75f;

    [Tooltip("Clock and best time, hidden while the title screen is up.")]
    public RunHud hud;

    [Header("Difficulty")]
    [Tooltip("Demons in the building. Chosen here, spawned by DemonCount.")]
    public int demons = 1;
    public int maxDemons = 3;
    public DemonCount demonCount;

    public bool IsOpen { get; private set; }

    /// <summary>Something on the screen the pointer can act on without starting the run.</summary>
    struct Hotspot
    {
        public RectTransform rect;
        public Image image;
        public System.Action press;
    }

    const string DifficultyKey = "Floorplan.Demons";

    readonly List<Image> parts = new List<Image>();
    readonly List<Hotspot> hotspots = new List<Hotspot>();
    Image[] pips;
    Image playImage;
    Image titleImage;
    Image soundImage;
    float openedAt;
    int builtForHeight;
    int builtForWidth;
    float builtForBest = -1f;
    float builtForWin = -1f;
    Vector2 builtForSize;
    Sprite gemMark;
    Sprite clueMark, activatorMark;
    Sprite exitMark;
    Image volumeTrack, volumeFill, volumeKnob;
    float trackLeft, trackWidth, trackY;
    bool draggingVolume;
    Image[] fullscreenParts;
    bool builtForFullscreen;
    Sprite menuArt;
    Image quitImage;
    RectTransform startTarget, quitTarget;
    Image[] startEdges, quitEdges;
    int selectedAction;

    void Awake()
    {
        if (root == null) root = transform as RectTransform;
        menuArt = Resources.Load<Sprite>("Menu/menu-art");
        AudioListener.pause = false;
        if (run == null) run = FindAnyObjectByType<GameRun>();
        demons = Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, demons), 1, Mathf.Max(1, maxDemons));
        if (run != null && run.wonIcon != null) gemMark = run.wonIcon.sprite;
        var objective = FindAnyObjectByType<Objective>();
        if (objective != null && objective.exitSprite != null) exitMark = objective.exitSprite.sprite;
        var storyDirector = FindAnyObjectByType<StoryDirector>();
        if (storyDirector != null)
        {
            if (storyDirector.clues.Length > 0 && storyDirector.clues[0] != null)
                clueMark = storyDirector.clues[0].body.sprite;
            if (storyDirector.activatorBody != null) activatorMark = storyDirector.activatorBody.sprite;
        }
        SoundSettings.Apply();
        Open();
    }

    void Update()
    {
        if (!IsOpen)
        {
            // Let a caught/won sequence finish before another screen takes ownership of time.
            if (EscapePressed() && (run == null || !run.IsCaught)) Open();
            return;
        }

        // The whole layout is sized off the window, so a resize rebuilds it. The best time joins the
        // test because GameRun may well load it after this screen first drew itself.
        if (Screen.height != builtForHeight || Screen.width != builtForWidth ||
            root.rect.size != builtForSize || BestSeconds() != builtForBest ||
            Screen.fullScreen != builtForFullscreen ||
            (run != null && run.BestWin != builtForWin))
        {
            Rebuild();
            SetVisible(true);
        }

        Breathe();
        Hover();

        // A short grace period stops the click that opened the page from skipping the menu instantly.
        if (Time.unscaledTime - openedAt < 0.25f) return;

        if (EscapePressed() && run != null && run.HasBegun) { Close(); return; }
        if (DragVolume()) return;
        if (Clicked()) return;
        if (Keyed()) return;
        if (Pressed()) { if (selectedAction == 1 && menuArt != null) GameExit.Quit(); else Close(); }
    }

    // ------------------------------------------------------------------ input

    /// <summary>A press on one of the three controls; true when it was handled here.</summary>
    bool Clicked()
    {
        Mouse pointer = Mouse.current;
        if (pointer == null || !pointer.leftButton.wasPressedThisFrame) return false;

        Vector2 point = pointer.position.ReadValue();
        foreach (Hotspot spot in hotspots)
        {
            if (spot.rect == null) continue;
            if (!RectTransformUtility.RectangleContainsScreenPoint(spot.rect, point, null)) continue;
            spot.press();
            return true;
        }
        return false;
    }

    /// <summary>A key that changes a setting rather than starting the run.</summary>
    bool Keyed()
    {
        Keyboard navigation = Keyboard.current;
        Gamepad controller = Gamepad.current;
        if ((navigation != null && (navigation.upArrowKey.wasPressedThisFrame || navigation.downArrowKey.wasPressedThisFrame || navigation.tabKey.wasPressedThisFrame)) ||
            (controller != null && (controller.dpad.up.wasPressedThisFrame || controller.dpad.down.wasPressedThisFrame)))
        {
            selectedAction = 1 - selectedAction;
            return true;
        }
        int moved = Stepped();
        if (moved != 0)
        {
            SetDemons(demons + moved);
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            ToggleSound();
            return true;
        }
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
        {
            ToggleFullscreen();
            return true;
        }

        int louder = 0;
        if (keyboard != null)
        {
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) louder = -1;
            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) louder = 1;
        }
        if (controller != null)
        {
            if (controller.leftShoulder.wasPressedThisFrame) louder = -1;
            if (controller.rightShoulder.wasPressedThisFrame) louder = 1;
        }
        if (louder != 0)
        {
            // Volume, not Heard: Heard reads 0 while muted, so stepping up from muted would drop a
            // saved 0.8 to 0.1, and stepping down would do nothing at all.
            SetVolume(SoundSettings.Volume + 0.1f * louder);
            return true;
        }
        return false;
    }

    /// <summary>Follows the pointer while the volume knob is held; true while a drag is under way.</summary>
    bool DragVolume()
    {
        if (!draggingVolume) return false;
        Mouse pointer = Mouse.current;
        if (pointer == null || !pointer.leftButton.isPressed)
        {
            draggingVolume = false;
            return true;
        }
        SetVolumeFromPointer(pointer.position.ReadValue());
        return true;
    }

    /// <summary>-1 or +1 when the player changes the demon count, otherwise 0.</summary>
    static int Stepped()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) return -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) return 1;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) return -1;
            if (pad.dpad.right.wasPressedThisFrame) return 1;
        }
        return 0;
    }

    static bool EscapePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;

        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    static bool Pressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) return true;

        return false;
    }

    // ------------------------------------------------------------------ state

    void SetDemons(int count)
    {
        demons = Mathf.Clamp(count, 1, maxDemons);
        PlayerPrefs.SetInt(DifficultyKey, demons);
        PlayerPrefs.Save();
        PaintPips();
    }

    /// <summary>Lit pips show the chosen count; the rest sit dark so the scale is still readable.</summary>
    void PaintPips()
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] != null) pips[i].color = i < demons ? markColor : hintColor;
        }
    }

    void ToggleSound()
    {
        SoundSettings.Muted = !SoundSettings.Muted;
        // Unmuting a slider left at zero would still be silent, which reads as a broken switch.
        if (!SoundSettings.Muted && SoundSettings.Volume < 0.05f) SoundSettings.Volume = 0.5f;
        PaintSound();
        PaintVolume();
    }

    void SetVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        SoundSettings.Volume = volume;
        if (volume > 0.01f && SoundSettings.Muted) SoundSettings.Muted = false;
        PaintSound();
        PaintVolume();
    }

    void SetVolumeFromPointer(Vector2 screenPoint)
    {
        if (volumeTrack == null || trackWidth <= 0f) return;
        // The root's pivot is its centre, which is also where every part is anchored.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out Vector2 local);
        SetVolume(Mathf.InverseLerp(trackLeft, trackLeft + trackWidth, local.x));
    }

    void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
    }

    void PaintSound()
    {
        if (soundImage == null) return;
        bool silent = SoundSettings.Heard <= 0.001f;
        soundImage.sprite = silent ? speakerOff : speakerOn;
        soundImage.color = silent ? hintColor : markColor;
    }

    /// <summary>The lit part of the track is how loud the game is; muted shows it empty.</summary>
    void PaintVolume()
    {
        if (volumeTrack == null || volumeFill == null || volumeKnob == null) return;
        float filled = trackWidth * SoundSettings.Heard;
        float stroke = volumeTrack.rectTransform.sizeDelta.y;
        volumeFill.rectTransform.sizeDelta = new Vector2(filled, stroke);
        volumeFill.rectTransform.anchoredPosition = new Vector2(trackLeft + filled * 0.5f, trackY);
        volumeKnob.rectTransform.anchoredPosition = new Vector2(trackLeft + filled, trackY);
        volumeKnob.color = filled > 0.5f ? markColor : hintColor;
    }

    public void Open()
    {
        if (IsOpen || (run != null && (run.IsCaught || run.StoryPlaying))) return;
        IsOpen = true;
        selectedAction = 0;
        openedAt = Time.unscaledTime;
        Rebuild();
        SetVisible(true);
        if (hud != null) hud.hidden = true;
        root.SetAsLastSibling();
        if (run != null) run.SetPaused(true);
        else Time.timeScale = 0f;
    }

    /// <summary>Starts the game: unfreezes, hides the menu and lets the first run (and its card) begin.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        SetVisible(false);
        if (hud != null) hud.hidden = false;
        bool difficultyChanged = demonCount != null && demonCount.Active != demons;
        if (run != null) run.SetPaused(false);
        else Time.timeScale = 1f;
        if (demonCount != null) demonCount.SetActiveDemons(demons);

        // Ordinary pause/resume preserves progress. A different difficulty starts a fresh run.
        if (run != null)
        {
            if (!run.HasBegun) run.Begin();
            else if (difficultyChanged) run.RestartFresh();
        }
    }

    void SetVisible(bool visible)
    {
        if (backdrop != null) backdrop.enabled = visible;
        foreach (Image part in parts)
        {
            if (part != null) part.enabled = visible;
        }
        if (!visible && playImage != null) playImage.enabled = false;
    }

    // ------------------------------------------------------------------ life

    /// <summary>The prompt stays visible and gently scales, so its click target never disappears.</summary>
    void Breathe()
    {
        float t = Time.unscaledTime - openedAt;

        if (playImage != null)
        {
            float pulse = 1f + 0.08f * Mathf.Sin(t * Mathf.PI / Mathf.Max(0.1f, blinkSeconds));
            playImage.rectTransform.localScale = Vector3.one * pulse;
        }

        if (titleImage != null)
        {
            float pulse = 1f + 0.02f * Mathf.Sin(t * 1.6f);
            titleImage.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    /// <summary>The three controls light up under the pointer, so they read as controls.</summary>
    void Hover()
    {
        Mouse pointer = Mouse.current;
        Vector2 point = pointer != null ? pointer.position.ReadValue() : new Vector2(-10000,-10000);
        foreach (Hotspot spot in hotspots)
        {
            if (spot.rect == null || spot.image == null) continue;
            if (spot.image == soundImage) continue; // the sound switch shows its state, not the pointer
            if (spot.image == volumeTrack) continue; // so does the volume slider
            if (fullscreenParts != null && spot.image == fullscreenParts[0])
            {
                bool overFullscreen = RectTransformUtility.RectangleContainsScreenPoint(spot.rect, point, null);
                foreach (Image part in fullscreenParts) part.color = overFullscreen ? markColor : hintColor;
                continue;
            }
            if (spot.image == quitImage || (menuArt != null && spot.image == playImage)) continue;
            if (pips != null && System.Array.IndexOf(pips, spot.image) >= 0) continue;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(spot.rect, point, null);
            spot.image.color = over || spot.image == playImage ? markColor : hintColor;
        }
        PaintAction(startEdges, selectedAction == 0 || (startTarget != null && RectTransformUtility.RectangleContainsScreenPoint(startTarget, point, null)));
        PaintAction(quitEdges, selectedAction == 1 || (quitTarget != null && RectTransformUtility.RectangleContainsScreenPoint(quitTarget, point, null)));
        if (quitImage != null) quitImage.color = selectedAction == 1 ? markColor : hintColor;
    }

    // ------------------------------------------------------------------ layout

    /// <summary>
    /// Lays the screen out from the window size, so it reads the same in a short browser canvas as it
    /// does full screen. Everything is a fraction of the height; nothing is a fixed pixel count.
    /// </summary>
    void Rebuild()
    {
        foreach (Image part in parts)
        {
            if (part != null) Destroy(part.gameObject);
        }
        parts.Clear();

        // The targets are not Images, so they are not in parts and have to be cleared separately.
        foreach (Hotspot spot in hotspots)
        {
            if (spot.rect != null) Destroy(spot.rect.gameObject);
        }
        hotspots.Clear();
        pips = null;
        playImage = null;
        titleImage = null;
        soundImage = null;
        volumeTrack = volumeFill = volumeKnob = null;
        fullscreenParts = null;
        draggingVolume = false;
        quitImage = null;
        startTarget = quitTarget = null;
        startEdges = quitEdges = null;

        builtForHeight = Screen.height;
        builtForWidth = Screen.width;
        builtForBest = BestSeconds();
        builtForWin = run != null ? run.BestWin : 0f;
        builtForSize = root.rect.size;
        builtForFullscreen = Screen.fullScreen;

        float h = Mathf.Max(1f, root.rect.height);
        float w = Mathf.Max(1f, root.rect.width);
        float half = h * 0.5f;
        float halfWidth = w * 0.5f;

        if (menuArt != null)
        {
            BuildArtMenu(w, h);
            return;
        }

        BuildFrame(w, h);

        // The emblem: the jewel the building is named for, between its horns.
        float titleWidth = Mathf.Clamp(w * 0.44f, 180f, 520f);
        const float titleAspect = 19f / 40f; // the art is 40 wide by 19 tall
        float titleHeight = Mathf.Min(titleWidth * titleAspect, h * 0.26f);
        titleWidth = titleHeight / titleAspect;
        titleImage = Add(title, markColor, titleWidth, titleHeight, new Vector2(0f, h * 0.22f));

        BuildDifficulty(w, h);

        // A rule separating what you choose from what you are told.
        float ruleWidth = Mathf.Min(w * 0.52f, 440f);
        Add(pixel, hintColor, ruleWidth, 2f, new Vector2(0f, -h * 0.045f));

        BuildControls(w, h);
        BuildObjective(w, h);

        // The blinking prompt.
        float mark = Mathf.Clamp(h * 0.05f, 16f, 34f);
        playImage = Add(playMark, markColor, mark, mark, new Vector2(0f, -h * 0.35f));
        AddHotspot(playImage, Mathf.Max(44f, mark * 2f), Mathf.Max(36f, mark * 1.5f), Close);

        BuildBestTime(h);
        BuildSound(halfWidth, half, h);
    }

    /// <summary>Four brackets, so the screen reads as a framed panel rather than floating marks.</summary>
    void BuildFrame(float w, float h)
    {
        if (frameCorner == null) return;

        float size = Mathf.Clamp(h * 0.055f, 16f, 44f);
        float inset = Mathf.Clamp(h * 0.035f, 10f, 34f);
        float x = w * 0.5f - inset - size * 0.5f;
        float y = h * 0.5f - inset - size * 0.5f;

        Add(frameCorner, hintColor, size, size, new Vector2(-x, y), 0f);    // top left, as drawn
        Add(frameCorner, hintColor, size, size, new Vector2(x, y), -90f);   // top right
        Add(frameCorner, hintColor, size, size, new Vector2(x, -y), 180f);  // bottom right
        Add(frameCorner, hintColor, size, size, new Vector2(-x, -y), 90f);  // bottom left
    }

    /// <summary>How many demons: marks lit up to the chosen count, with an arrow on each side.</summary>
    void BuildDifficulty(float w, float h)
    {
        float pip = Mathf.Min(Mathf.Clamp(h * 0.065f, 18f, 42f), w * 0.085f);
        float pipGap = pip * 0.45f;
        float y = menuArt != null ? -h * 0.10f : h * 0.04f;
        float span = (maxDemons - 1) * (pip + pipGap);

        pips = new Image[maxDemons];
        for (int i = 0; i < maxDemons; i++)
        {
            float x = -span * 0.5f + i * (pip + pipGap);
            pips[i] = Add(demonMark, hintColor, pip, pip, new Vector2(x, y));
            int count = i + 1;
            AddHotspot(pips[i], pip * 1.25f, pip * 1.4f, () => SetDemons(count));
        }
        PaintPips();

        float arrowHeight = pip * 1.15f;
        float arrowWidth = arrowHeight * 9f / 14f; // the art is 9 x 14
        float reach = span * 0.5f + pip * 0.5f + arrowWidth * 0.5f + pip * 0.55f;

        Image left = Add(arrowLeft, hintColor, arrowWidth, arrowHeight, new Vector2(-reach, y));
        Image right = Add(arrowRight, hintColor, arrowWidth, arrowHeight, new Vector2(reach, y));

        // A generous pad, because the arrows themselves are a small target in a browser window.
        float padX = arrowWidth * 1.6f;
        float padY = arrowHeight * 1.4f;
        AddHotspot(left, padX, padY, () => SetDemons(demons - 1));
        AddHotspot(right, padX, padY, () => SetDemons(demons + 1));
    }

    /// <summary>Move, hide, distract: three groups sharing one band under the rule.</summary>
    void BuildControls(float w, float h)
    {
        float key = Mathf.Min(Mathf.Clamp(h * 0.068f, 16f, 42f), w * 0.052f);
        float gap = key * 0.28f;
        float icon = key;
        float y = -h * 0.135f;
        float column = Mathf.Min(w * 0.26f, 250f);

        // Move: W above, A S D below.
        float wasd = -column;
        Key('W', key, new Vector2(wasd, y + key * 0.5f + gap * 0.5f));
        Key('A', key, new Vector2(wasd - (key + gap), y - key * 0.5f - gap * 0.5f));
        Key('S', key, new Vector2(wasd, y - key * 0.5f - gap * 0.5f));
        Key('D', key, new Vector2(wasd + (key + gap), y - key * 0.5f - gap * 0.5f));

        // Hide: E, then the locker it puts you in.
        Key('E', key, new Vector2(-key * 0.8f, y));
        AddLocker(icon, new Vector2(key * 0.8f, y));

        // Distract: the mouse, then the coin it throws.
        Add(mouse, hintColor, icon * 0.66f, icon, new Vector2(column - key * 0.7f, y));
        Add(coin, hintColor, icon * 0.45f, icon * 0.45f, new Vector2(column + key * 0.7f, y));
    }

    // The original locker sprite has dark baked pixels; draw its outline in the menu palette.
    void AddLocker(float size, Vector2 at)
    {
        float width = size * 0.65f;
        float stroke = Mathf.Max(1f, size * 0.07f);
        Add(pixel, hintColor, width, stroke, at + Vector2.up * size * 0.5f);
        Add(pixel, hintColor, width, stroke, at - Vector2.up * size * 0.5f);
        Add(pixel, hintColor, stroke, size, at + Vector2.left * width * 0.5f);
        Add(pixel, hintColor, stroke, size, at + Vector2.right * width * 0.5f);
        Add(pixel, hintColor, width * 0.5f, stroke, at + Vector2.up * size * 0.24f);
        Add(pixel, hintColor, width * 0.5f, stroke, at + Vector2.up * size * 0.10f);
        Add(pixel, hintColor, stroke, size * 0.17f, at + Vector2.right * width * 0.24f - Vector2.up * size * 0.14f);
    }

    void BuildObjective(float w, float h, float verticalFraction = -0.255f)
    {
        if (gemMark == null || exitMark == null) return;
        float size = Mathf.Min(h * 0.035f, w * 0.045f);
        float y = h * verticalFraction;
        if (clueMark != null && activatorMark != null)
        {
            Add(clueMark, markColor, size, size, new Vector2(-size * 4f, y));
            Add(arrowRight, hintColor, size * 0.42f, size * 0.65f, new Vector2(-size * 2.5f, y));
            Add(gemMark, markColor, size, size, new Vector2(-size, y));
            Add(activatorMark, markColor, size * 1.15f, size, new Vector2(size * 0.6f, y));
            Add(arrowRight, hintColor, size * 0.42f, size * 0.65f, new Vector2(size * 2.2f, y));
            Add(exitMark, markColor, size * 1.25f, size * 1.25f, new Vector2(size * 3.8f, y));
            return;
        }
        Add(gemMark, markColor, size, size, new Vector2(-size * 2f, y));
        Add(arrowRight, hintColor, size * 0.42f, size * 0.65f, new Vector2(0f, y));
        Add(exitMark, markColor, size * 1.25f, size * 1.25f, new Vector2(size * 2f, y));
    }

    /// <summary>The best run so far, in digits, which cost none of the ten words.</summary>
    void BuildBestTime(float h)
    {
        if (digits == null || digits.Length < 12 || run == null) return;
        float cell = Mathf.Min(Mathf.Clamp(h * 0.025f, 8f, 20f), root.rect.width * 0.027f);
        bool both = run.BestTime > 0f && run.BestWin > 0f;
        float spacing = Mathf.Min(root.rect.width * 0.19f, cell * 7f);
        if (run.BestTime > 0f)
            BuildRecord(run.BestTime, run.caughtIcon != null ? run.caughtIcon.sprite : demonMark,
                hintColor, both ? -spacing : 0f, -h * 0.43f, cell);
        if (run.BestWin > 0f)
            BuildRecord(run.BestWin, gemMark, markColor, both ? spacing : 0f, -h * 0.43f, cell);
    }

    void BuildRecord(float seconds, Sprite symbol, Color color, float x, float y, float cell)
    {
        string text = RunHud.Format(seconds);
        float start = x - (text.Length - 1) * cell * 0.5f + cell * 0.7f;
        Add(symbol, color, cell, cell, new Vector2(start - cell * 1.5f, y));

        for (int i = 0; i < text.Length; i++)
        {
            Sprite glyph = Digit(text[i]);
            if (glyph == null) continue;
            Add(glyph, color, cell, cell, new Vector2(start + i * cell, y));
        }
    }

    /// <summary>
    /// The settings row, parked in the top right where it is out of the way. Reading right to left:
    /// the sound switch, a volume slider, and the fullscreen toggle.
    /// </summary>
    void BuildSound(float halfWidth, float half, float h)
    {
        if (speakerOn == null) return;

        float height = Mathf.Clamp(h * 0.045f, 16f, 34f);
        float width = height * 16f / 14f; // the art is 16 x 14
        float inset = Mathf.Clamp(h * 0.035f, 10f, 34f) + Mathf.Clamp(h * 0.055f, 16f, 44f) + height * 0.4f;
        Vector2 speakerAt = new Vector2(halfWidth - inset, half - inset);

        // Lay the row out first, so a black panel can go down behind it. The menu art is busy up
        // here, and grey marks drawn straight onto grey horns disappear.
        float gap = height * 0.7f;
        trackWidth = Mathf.Clamp(halfWidth * 0.28f, 60f, 170f);
        trackLeft = speakerAt.x - width * 0.5f - gap - trackWidth;
        trackY = speakerAt.y;
        float mark = height * 1.05f;
        bool hasFullscreen = frameCorner != null;
        float rowLeft = hasFullscreen ? trackLeft - gap - mark : trackLeft;
        float rowRight = speakerAt.x + width * 0.5f;
        float pad = height * 0.45f;
        ButtonFrame(new Vector2((rowLeft + rowRight) * 0.5f, trackY),
            rowRight - rowLeft + pad * 2f, height + pad * 1.4f);

        soundImage = Add(speakerOn, markColor, width, height, speakerAt);
        PaintSound();
        AddHotspot(soundImage, width * 1.5f, height * 1.6f, ToggleSound);

        // Volume: a track running left from the speaker, lit up to the current level.
        float stroke = Mathf.Max(2f, Mathf.Round(height * 0.14f));
        float knob = height * 0.6f;
        volumeTrack = Add(pixel, hintColor, trackWidth, stroke, new Vector2(trackLeft + trackWidth * 0.5f, trackY));
        volumeFill = Add(pixel, markColor, trackWidth, stroke, new Vector2(trackLeft + trackWidth * 0.5f, trackY));
        volumeKnob = Add(pixel, markColor, Mathf.Max(4f, knob * 0.45f), knob, new Vector2(trackLeft, trackY));
        PaintVolume();
        AddHotspot(volumeTrack, trackWidth + knob * 2f, height * 1.6f, BeginVolumeDrag);

        // Fullscreen: four corner brackets pointing out, or pointing in once already fullscreen.
        if (!hasFullscreen) return;
        Vector2 markAt = new Vector2(trackLeft - gap - mark * 0.5f, trackY);
        fullscreenParts = FullscreenMark(markAt, mark, Screen.fullScreen);
        AddHotspot(fullscreenParts[0], mark * 1.7f, height * 1.6f, ToggleFullscreen);
        hotspots[hotspots.Count - 1].rect.anchoredPosition = markAt;
    }

    void BeginVolumeDrag()
    {
        draggingVolume = true;
        Mouse pointer = Mouse.current;
        if (pointer != null) SetVolumeFromPointer(pointer.position.ReadValue());
    }

    Image[] FullscreenMark(Vector2 at, float size, bool inward)
    {
        float corner = size * 0.36f; // short arms, so the four corners read as corners, not a box
        float reach = (size - corner) * 0.5f;
        float turn = inward ? 180f : 0f;
        return new[]
        {
            Add(frameCorner, hintColor, corner, corner, at + new Vector2(-reach, reach), 0f + turn),
            Add(frameCorner, hintColor, corner, corner, at + new Vector2(reach, reach), -90f + turn),
            Add(frameCorner, hintColor, corner, corner, at + new Vector2(reach, -reach), 180f + turn),
            Add(frameCorner, hintColor, corner, corner, at + new Vector2(-reach, -reach), 90f + turn),
        };
    }

    float BestSeconds()
    {
        return run != null ? run.BestTime : 0f;
    }

    void BuildArtMenu(float w, float h)
    {
        float aspect = menuArt.rect.width / menuArt.rect.height;
        float artWidth = Mathf.Max(w, h * aspect);
        Add(menuArt, Color.white, artWidth, artWidth / aspect, Vector2.zero);
        BuildFrame(w, h);
        BuildDifficulty(w, h);
        float buttonWidth = Mathf.Min(180f, w * 0.31f);
        float buttonHeight = Mathf.Clamp(h * 0.10f, 44f, 68f);
        float offset = buttonWidth * 0.61f;
        float y = -h * 0.26f;
        startEdges = ButtonFrame(new Vector2(-offset, y), buttonWidth, buttonHeight);
        quitEdges = ButtonFrame(new Vector2(offset, y), buttonWidth, buttonHeight);
        float icon = buttonHeight * 0.4f;
        playImage = Add(playMark, markColor, icon, icon, new Vector2(-offset, y));
        AddHotspot(playImage, buttonWidth, buttonHeight, Close);
        startTarget = hotspots[hotspots.Count - 1].rect;
        // Door plus outgoing arrow: a recognizable quit control without spending story words.
        quitImage = Add(arrowRight, hintColor, icon * 0.5f, icon * 0.65f, new Vector2(offset + icon * 0.18f, y));
        Add(pixel, hintColor, 2f, icon, new Vector2(offset - icon * 0.5f, y));
        Add(pixel, hintColor, icon * 0.6f, 2f, new Vector2(offset - icon * 0.2f, y + icon * 0.5f));
        Add(pixel, hintColor, icon * 0.6f, 2f, new Vector2(offset - icon * 0.2f, y - icon * 0.5f));
        AddHotspot(quitImage, buttonWidth, buttonHeight, GameExit.Quit);
        quitTarget = hotspots[hotspots.Count - 1].rect;
        quitTarget.anchoredPosition = new Vector2(offset, y);
        if (h >= 360f) BuildObjective(w, h, -0.355f);
        BuildBestTime(h);
        BuildSound(w * 0.5f, h * 0.5f, h);
        float inset = Mathf.Clamp(h * 0.09f, 40f, 76f);
        Vector2 replayAt = new Vector2(-w * 0.5f + inset, h * 0.5f - inset);
        ButtonFrame(replayAt, 36f, 30f);
        Image replay = Add(playMark, hintColor, 14f, 14f, replayAt);
        AddHotspot(replay, 48f, 44f, ReplayIntro);
    }

    public void ReplayIntro()
    {
        SceneManager.LoadScene("Intro");
    }

    Image[] ButtonFrame(Vector2 at, float width, float height)
    {
        Add(pixel, Color.black, width, height, at);
        return new[] {
            Add(pixel, hintColor, width, 2f, at + Vector2.up * height * 0.5f),
            Add(pixel, hintColor, width, 2f, at - Vector2.up * height * 0.5f),
            Add(pixel, hintColor, 2f, height, at + Vector2.left * width * 0.5f),
            Add(pixel, hintColor, 2f, height, at + Vector2.right * width * 0.5f)
        };
    }

    void PaintAction(Image[] edges, bool active)
    {
        if (edges == null) return;
        foreach (var edge in edges) if (edge != null) edge.color = active ? markColor : hintColor;
    }

    void Key(char letter, float size, Vector2 position)
    {
        Add(keyCap, hintColor, size, size, position);
        Sprite glyph = Letter(letter);
        if (glyph != null) Add(glyph, hintColor, size * 0.40f, size * 0.58f, position);
    }

    Sprite Letter(char c)
    {
        if (letters == null || c < 'A' || c > 'Z') return null;
        int index = c - 'A';
        return index < letters.Length ? letters[index] : null;
    }

    Sprite Digit(char c)
    {
        if (digits == null) return null;
        int index = c >= '0' && c <= '9' ? c - '0' : c == ':' ? 10 : 11;
        return index < digits.Length ? digits[index] : null;
    }

    /// <summary>
    /// An invisible target centred on a mark, sized larger than it, so the pointer does not have to
    /// be pixel accurate. It carries no Image of its own, only a rect to test against.
    /// </summary>
    void AddHotspot(Image mark, float width, float height, System.Action press)
    {
        if (mark == null) return;

        var go = new GameObject("Hotspot", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = mark.rectTransform.anchoredPosition;

        hotspots.Add(new Hotspot { rect = rect, image = mark, press = press });
    }

    Image Add(Sprite sprite, Color color, float width, float height, Vector2 position)
    {
        return Add(sprite, color, width, height, position, 0f);
    }

    Image Add(Sprite sprite, Color color, float width, float height, Vector2 position, float rotation)
    {
        var go = new GameObject("Part", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
        if (!Mathf.Approximately(rotation, 0f)) rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        parts.Add(image);
        return image;
    }
}
