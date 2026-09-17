using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The whole story, in ten words: "The Debt". You borrowed light from the devil and never paid it
/// back, the light around you is the loan, and the thing hunting you is collections.
///
/// Each card shows once and never again, so the story is told across your first few runs and then
/// the game goes quiet and lets you play. Words are drawn with the generated pixel alphabet, red
/// on black, so nothing here leaves the game's three colours.
/// </summary>
public class StoryCards : MonoBehaviour
{
    [Header("Words (ten in total, and that is the budget)")]
    [Tooltip("Shown once, at the start of the first run.")]
    public string openingWords = "YOU BORROWED THE LIGHT";

    [Tooltip("Shown once, the first time an enemy starts chasing you.")]
    public string spottedWords = "HE WANTS IT BACK";

    [Tooltip("Shown once, the first time you are caught. After that the skull icon does the job.")]
    public string caughtWords = "PAID";

    [Tooltip("Shown once, the first time a run beats your best.")]
    public string bestWords = "LONGER";

    [Tooltip("On when a StoryDirector is present: it lands the opening card on the theft frame.")]
    public bool deferOpening;

    [Header("Look")]
    [Tooltip("A to Z, in order, as 7x7 pixel glyph sprites.")]
    public Sprite[] letters;

    [Tooltip("Centred container on the HUD canvas the letters are built inside.")]
    public RectTransform root;

    public Color wordColor = new Color32(237, 28, 36, 255);

    [Tooltip("Seconds a card stays up, in real time so it still reads while the game is frozen.")]
    public float holdSeconds = 2f;

    [Tooltip("Characters before a line wraps. Keeps the long cards to two lines.")]
    public int maxLineCharacters = 12;

    [Tooltip("Pixels per glyph cell in the source sprites.")]
    public int glyphPixels = 7;

    [Tooltip("Largest screen pixels per glyph pixel.")]
    public int maxScale = 9;

    [Tooltip("Screen pixels kept clear at each side.")]
    public float sideMargin = 90f;

    [Header("Sound")]
    public bool playTones = true;
    [Range(0f, 1f)] public float volume = 0.8f;

    /// <summary>True while a card is on screen or still waiting its turn.</summary>
    public bool IsShowing => showing != null || pending.Count > 0;

    struct Card
    {
        public string words;
        public AudioClip tone;
    }

    AudioSource source;
    AudioClip thunk, sting, flatLow, flatHigh;
    readonly List<Image> glyphPool = new List<Image>();
    readonly Queue<Card> pending = new Queue<Card>();
    int glyphsInUse;
    Coroutine showing;
    bool shownOpening, shownSpotted, shownCaught, shownBest;
    GameRun subscribedTo;

    void Awake()
    {
        if (root == null) root = transform as RectTransform;

        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        thunk = ProceduralTones.Thunk();
        sting = ProceduralTones.Sting();
        flatLow = ProceduralTones.Flat("Tone_Paid", 174.61f, 0.20f);
        flatHigh = ProceduralTones.Flat("Tone_Longer", 261.63f, 0.13f);
    }

    void OnEnable()
    {
        Subscribe();
    }

    void Start()
    {
        Subscribe();
    }

    /// <summary>The opening card waits for the menu to close, so it is not read behind a title screen.</summary>
    void OnStarted()
    {
        if (deferOpening) return; // the director shows it, over the picture of the theft
        ShowOpeningCard();
    }

    /// <summary>
    /// Spends the opening four words. Returns true when it took them, so a caller that wanted to
    /// time the card itself knows whether anything is on screen to wait for.
    /// </summary>
    public bool ShowOpeningCard()
    {
        if (shownOpening) return false;
        shownOpening = Show(openingWords, thunk);
        return shownOpening;
    }

    void OnDisable()
    {
        Unsubscribe();

        // Disabling mid-card stops the coroutine where it stands. Without this, showing stays
        // non-null and pending keeps its cards, so IsShowing never goes false again and anything
        // waiting on it (the caught sequence) waits forever.
        if (showing != null) StopCoroutine(showing);
        showing = null;
        pending.Clear();
        glyphsInUse = 0;
        SetVisible(false);
    }

    void Subscribe()
    {
        GameRun run = GameRun.Instance;
        if (run == null || run == subscribedTo) return;
        Unsubscribe();
        subscribedTo = run;
        run.Spotted += OnSpotted;
        run.NewBest += OnNewBest;
        run.Started += OnStarted;

        // Script execution order is not guaranteed; if the run already began, catch up now.
        if (run.HasBegun) OnStarted();
    }

    void Unsubscribe()
    {
        if (subscribedTo == null) return;
        subscribedTo.Spotted -= OnSpotted;
        subscribedTo.NewBest -= OnNewBest;
        subscribedTo.Started -= OnStarted;
        subscribedTo = null;
    }

    void OnSpotted()
    {
        if (shownSpotted) return;
        shownSpotted = Show(spottedWords, sting);
    }

    void OnNewBest()
    {
        if (shownBest) return;
        shownBest = Show(bestWords, flatHigh);
    }

    /// <summary>
    /// Called by GameRun during the caught sequence. Returns true when it took the moment, so the
    /// skull icon stands down; every later death gets the icon instead. Returns false when it has
    /// nothing to draw, so the icon still appears rather than leaving the screen empty.
    /// </summary>
    public bool ShowCaughtCard()
    {
        if (shownCaught) return false;
        shownCaught = Show(caughtWords, flatLow);
        return shownCaught;
    }

    /// <summary>
    /// Queues a card. Cards never cut one another off: a word the player only half read would be
    /// gone for good, since each is shown exactly once. Returns false when there is nothing
    /// drawable, so the caller can leave the card unspent.
    /// </summary>
    bool Show(string words, AudioClip tone)
    {
        if (string.IsNullOrEmpty(words) || letters == null || letters.Length == 0) return false;

        pending.Enqueue(new Card { words = words.ToUpperInvariant(), tone = tone });
        if (showing == null) showing = StartCoroutine(ShowPending());
        return true;
    }

    IEnumerator ShowPending()
    {
        while (pending.Count > 0)
        {
            Card card = pending.Dequeue();
            Build(card.words);
            if (playTones && card.tone != null && source != null) source.PlayOneShot(card.tone, volume);

            // Two frames of flicker, the way a card lands in a pixel game, then it simply sits there.
            yield return Hold(0.05f);
            SetVisible(false);
            yield return Hold(0.05f);
            SetVisible(true);
            yield return Hold(holdSeconds);

            SetVisible(false);
            glyphsInUse = 0;
        }
        showing = null;
    }

    IEnumerator Hold(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (GameRun.Instance == null || !GameRun.Instance.IsPaused)
                elapsed += Time.unscaledDeltaTime;
        }
    }

    /// <summary>
    /// Toggles the letters themselves rather than the container, because the container may be this
    /// component's own object and switching that off would stop the coroutine mid-card.
    /// </summary>
    void SetVisible(bool visible)
    {
        for (int i = 0; i < glyphPool.Count; i++)
        {
            glyphPool[i].enabled = visible && i < glyphsInUse;
        }
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Lays the words out as centred lines of pixel glyphs, scaled to fit the screen.</summary>
    void Build(string words)
    {
        List<string> lines = Wrap(words, maxLineCharacters);

        int longest = 1;
        foreach (string line in lines) longest = Mathf.Max(longest, line.Length);

        float available = Mathf.Max(80f, Screen.width - 2f * sideMargin);
        int scale = Mathf.Clamp(Mathf.FloorToInt(available / (longest * glyphPixels)), 2, maxScale);
        float offsetY = 0f;
        if (GameRun.Instance != null && GameRun.Instance.StoryPlaying && words == openingWords)
        {
            // The theft reserves rows 16..25: ten pixels, centred one pixel below the frame.
            // Fit the complete two-line card inside that band even in a short Game view.
            int frameScale = Mathf.Max(1, Mathf.Min(Screen.width / PixelFrames.Width, Screen.height / PixelFrames.Height));
            float rows = 1f + (lines.Count - 1) * 1.5f;
            int bandScale = Mathf.Max(1, Mathf.FloorToInt(10f * frameScale / (glyphPixels * rows)));
            scale = Mathf.Min(scale, bandScale);
            offsetY = -frameScale;
        }
        float cell = glyphPixels * scale;
        float lineHeight = cell * 1.5f;

        int used = 0;
        float top = (lines.Count - 1) * lineHeight * 0.5f + offsetY;

        for (int l = 0; l < lines.Count; l++)
        {
            string line = lines[l];
            float y = top - l * lineHeight;
            float left = (-line.Length * cell + cell) * 0.5f;

            for (int c = 0; c < line.Length; c++)
            {
                Sprite glyph = GlyphSprite(line[c]);
                if (glyph == null) continue; // a space still advances the cursor

                Image image = GlyphImage(used++);
                image.sprite = glyph;
                image.color = wordColor;
                image.rectTransform.sizeDelta = new Vector2(cell, cell);
                image.rectTransform.anchoredPosition = new Vector2(left + c * cell, y);
            }
        }

        glyphsInUse = used;
        SetVisible(true);
    }

    Image GlyphImage(int index)
    {
        while (glyphPool.Count <= index)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            glyphPool.Add(image);
        }
        return glyphPool[index];
    }

    Sprite GlyphSprite(char c)
    {
        if (c < 'A' || c > 'Z') return null;
        int index = c - 'A';
        return index < letters.Length ? letters[index] : null;
    }

    /// <summary>Greedy word wrap that never splits a word, so a break always lands between words.</summary>
    static List<string> Wrap(string words, int limit)
    {
        var lines = new List<string>();
        string line = "";

        foreach (string word in words.Split(' '))
        {
            if (word.Length == 0) continue;
            if (line.Length == 0) line = word;
            else if (line.Length + 1 + word.Length <= limit) line += " " + word;
            else { lines.Add(line); line = word; }
        }

        if (line.Length > 0) lines.Add(line);
        if (lines.Count == 0) lines.Add(words);
        return lines;
    }
}
