using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Five wordless animated lessons before the title. The ten story words remain in-game.</summary>
public class IntroSequence : MonoBehaviour
{
    public Sprite pixel, gem, ring, demon, mouse, keyCap, arrow;
    public Sprite clue, activator;
    public Sprite[] letters;
    public string nextScene = "main";
    public float lessonSeconds = 6f;
    public int Lesson { get; private set; }
    public bool Transitioning { get; private set; }
    const int LessonCount = 5;

    readonly Color red = new Color32(237, 28, 36, 255);
    readonly Color grey = new Color32(70, 70, 70, 255);
    readonly List<Image> keys = new List<Image>();
    RectTransform stage, actor;
    Image jewel, enemy, exitRing, noise, coin, progress, shutter;
    RectTransform skipTarget, nextTarget, backTarget;
    Image nextMark, skipMark;
    Image leftMouseButton;
    readonly List<Image> shiftKey = new List<Image>();
    readonly List<Image> clueMarks = new List<Image>();
    readonly List<GameObject> crossedWings = new List<GameObject>();
    GameObject searchWing;
    Image portalKey, jewelSocket, keySocket;
    float elapsed;
    int width, height;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SoundSettings.Apply();
        Build();
    }

    void Update()
    {
        if (Transitioning) return;
        if (width != Screen.width || height != Screen.height) Fit();
        elapsed += Time.unscaledDeltaTime;
        Animate(Mathf.Clamp01(elapsed / lessonSeconds));
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        // Esc and Start skip the lot. Enter, Space and A are what people press to mean "next", so they
        // turn one page; skipping all five on them hid the hiding, coin and clue lessons from anyone
        // who just wanted to move on a little faster.
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Finish(); return; }
        if (pad != null && pad.startButton.wasPressedThisFrame) { Finish(); return; }
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) { Next(); return; }
        if (pad != null && pad.buttonSouth.wasPressedThisFrame) { Next(); return; }
        if (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) { Previous(); return; }
        if (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) { Next(); return; }
        if (pad != null && pad.dpad.left.wasPressedThisFrame) { Previous(); return; }
        if (pad != null && pad.dpad.right.wasPressedThisFrame) { Next(); return; }
        var pointer = Mouse.current;
        if (pointer != null)
        {
            Vector2 at = pointer.position.ReadValue();
            nextMark.color = Over(nextTarget, at) ? red : grey;
            skipMark.color = Over(skipTarget, at) ? red : grey;
            if (pointer.leftButton.wasPressedThisFrame)
            {
                if (Over(skipTarget, at)) { Finish(); return; }
                if (Over(nextTarget, at)) { Next(); return; }
                if (Over(backTarget, at)) { Previous(); return; }
            }
        }
        if (elapsed >= lessonSeconds) Next();
    }

    static bool Over(RectTransform rect, Vector2 point) => RectTransformUtility.RectangleContainsScreenPoint(rect, point, null);

    public void Next()
    {
        if (Transitioning) return;
        if (Lesson >= LessonCount - 1) { Finish(); return; }
        Lesson++;
        elapsed = 0f;
        Build();
    }

    public void Previous()
    {
        if (Transitioning) return;
        Lesson = Mathf.Max(0, Lesson - 1);
        elapsed = 0f;
        Build();
    }

    public void Finish()
    {
        if (Transitioning) return;
        Transitioning = true;
        StartCoroutine(GoToTitle());
    }

    IEnumerator GoToTitle()
    {
        // Close a black shutter over the lesson before loading the actual level and title.
        shutter = Graphic((RectTransform)transform, pixel, Color.black, Vector2.zero, new Vector2(Screen.width, 0f));
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            shutter.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height * Mathf.Clamp01(t / 0.35f));
            yield return null;
        }
        shutter.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
        yield return SceneManager.LoadSceneAsync(nextScene);
    }

    void Fit()
    {
        width = Screen.width;
        height = Screen.height;
        if (stage != null) stage.localScale = Vector3.one * Mathf.Min(width / 960f, height / 600f);
    }

    void Build()
    {
        if (stage != null) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
        keys.Clear();
        leftMouseButton = null;
        shiftKey.Clear();
        clueMarks.Clear();
        crossedWings.Clear();
        stage = Rect((RectTransform)transform, "Lesson " + (Lesson + 1), Vector2.zero, new Vector2(960, 600));
        Fit();

        // Diamonds show progression without spending any words.
        for (int i = 0; i < LessonCount; i++)
            Graphic(stage, gem, i == Lesson ? red : grey, new Vector2((i - (LessonCount - 1) * 0.5f) * 36f, 250f), new Vector2(14, 18));
        Graphic(stage, pixel, grey, new Vector2(0, -246), new Vector2(560, 3));
        progress = Graphic(stage, pixel, red, new Vector2(-280, -246), new Vector2(1, 3));
        progress.rectTransform.pivot = new Vector2(0, 0.5f);
        nextMark = Graphic(stage, arrow, grey, new Vector2(335, -246), new Vector2(18, 28));
        nextTarget = Rect(stage, "Next", new Vector2(335, -246), new Vector2(56, 56));
        Graphic(stage, arrow, grey, new Vector2(-335, -246), new Vector2(18, 28)).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        backTarget = Rect(stage, "Previous", new Vector2(-335, -246), new Vector2(56, 56));
        skipMark = Graphic(stage, arrow, grey, new Vector2(388, 250), new Vector2(15, 22));
        Graphic(stage, arrow, grey, new Vector2(402, 250), new Vector2(15, 22));
        skipTarget = Rect(stage, "Skip to menu", new Vector2(395, 250), new Vector2(64, 54));

        // A little architectural stage gives every lesson the same visual grammar as the level.
        Line(-320, 86, 320, 86, grey, 4);
        Line(-320, -86, 320, -86, grey, 4);
        Line(-320, -86, -320, 86, grey, 4);
        Line(320, -86, 320, 86, grey, 4);
        actor = MakeActor();
        jewel = Graphic(stage, gem, red, new Vector2(230, 0), new Vector2(28, 34));
        enemy = Graphic(stage, demon, red, new Vector2(240, 0), new Vector2(66, 66));
        exitRing = Graphic(stage, ring, grey, new Vector2(-250, 0), new Vector2(86, 86));
        noise = Graphic(stage, ring, red, Vector2.zero, new Vector2(20, 20));
        coin = Graphic(stage, pixel, red, Vector2.zero, new Vector2(10, 10)); // the thrown coin is red in the game too
        jewel.enabled = Lesson >= 3;
        enemy.enabled = Lesson == 1 || Lesson == 2;
        exitRing.enabled = Lesson == 4;
        noise.enabled = false;
        coin.enabled = false;

        if (Lesson == 0)
        {
            Graphic(stage, arrow, red, new Vector2(0, 160), new Vector2(30, 45));
            Key('W', new Vector2(0, -131));
            Key('A', new Vector2(-48, -179));
            Key('S', new Vector2(0, -179));
            Key('D', new Vector2(48, -179));
            ShiftKey(new Vector2(-136, -179));
        }
        else if (Lesson == 1)
        {
            Locker(new Vector2(-90, 0), 78);
            Locker(new Vector2(0, 160), 52);
            Key('E', new Vector2(0, -156));
        }
        else if (Lesson == 2)
        {
            Graphic(stage, ring, grey, new Vector2(0, 160), new Vector2(66, 66));
            Graphic(stage, pixel, red, new Vector2(0, 160), new Vector2(12, 12));
            var mouseHint = Graphic(stage, mouse, grey, new Vector2(0, -156), new Vector2(32, 48));
            // Fill only the left 2x2 button recess in the 10x10 mouse sprite.
            leftMouseButton = Graphic(mouseHint.rectTransform, pixel, red, new Vector2(-6.4f, 9.6f), new Vector2(6.4f, 9.6f));
            leftMouseButton.name = "Left Mouse Button";
        }
        else if (Lesson == 3)
        {
            BuildClueLesson();
        }
        else
        {
            Graphic(stage, gem, red, new Vector2(-96, 160), new Vector2(30, 36));
            Graphic(stage, activator, red, new Vector2(-36, 160), new Vector2(45, 39));
            Graphic(stage, arrow, grey, new Vector2(24, 160), new Vector2(16, 26));
            Graphic(stage, ring, red, new Vector2(90, 160), new Vector2(60, 60));
            portalKey = Graphic(stage, activator, red, new Vector2(225, 0), new Vector2(45, 39));
            jewelSocket = Graphic(stage, ring, grey, new Vector2(-250, 62), new Vector2(20, 20));
            keySocket = Graphic(stage, ring, grey, new Vector2(-250, -62), new Vector2(20, 20));
        }
        Animate(0f);
    }

    void Animate(float t)
    {
        progress.rectTransform.sizeDelta = new Vector2(560f * t, 3f);
        float walk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.72f));
        foreach (var key in keys) key.color = Mathf.Repeat(elapsed, 0.7f) < 0.35f ? red : grey;
        if (leftMouseButton != null)
            leftMouseButton.color = Mathf.Repeat(elapsed, 0.7f) < 0.35f ? red : Color.black;
        if (Lesson == 0)
        {
            // Walk the first part, then Shift lights and the figure runs, loud: red rings trail it.
            bool running = t > 0.42f && t < 0.8f;
            float x = t < 0.42f ? Mathf.Lerp(-250f, -70f, Mathf.SmoothStep(0f, 1f, t / 0.42f))
                    : Mathf.Lerp(-70f, 230f, Mathf.Clamp01((t - 0.42f) / 0.3f));
            actor.anchoredPosition = new Vector2(x, 0);
            foreach (Image part in shiftKey) part.color = running ? red : grey;
            noise.enabled = running;
            if (running)
            {
                float pulse = Mathf.Repeat(elapsed, 0.5f) / 0.5f;
                noise.rectTransform.anchoredPosition = actor.anchoredPosition;
                noise.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(30f, 130f, pulse);
            }
        }
        else if (Lesson == 1)
        {
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.34f));
            actor.anchoredPosition = new Vector2(Mathf.Lerp(-260, -90, approach), 0);
            actor.gameObject.SetActive(t < 0.35f || t > 0.88f);
            enemy.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(250, -25, Mathf.PingPong(t * 1.65f, 1f)), 0);
            if (t > 0.88f) actor.anchoredPosition = new Vector2(-90 - (t - 0.88f) * 500f, 0);
        }
        else if (Lesson == 2)
        {
            actor.anchoredPosition = new Vector2(-230 + Mathf.Max(0, t - 0.58f) * 800, -35);
            float flight = Mathf.Clamp01((t - 0.12f) / 0.28f);
            coin.enabled = t > 0.12f;
            coin.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-230, 0), new Vector2(210, 52), flight);
            noise.enabled = t > 0.4f && t < 0.9f;
            noise.rectTransform.anchoredPosition = new Vector2(210, 52);
            noise.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(20, 160, Mathf.Clamp01((t - 0.4f) / 0.5f));
            enemy.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(110, 0), new Vector2(210, 52), Mathf.Clamp01((t - 0.45f) / 0.35f));
        }
        else if (Lesson == 3)
        {
            float journey = Mathf.Clamp01(t / 0.82f) * 5f;
            int solved = Mathf.Min(5, Mathf.FloorToInt(journey));
            int index = Mathf.Min(4, solved);
            float part = journey - solved;
            float from = index == 0 ? -285f : -200f + (index - 1) * 90f;
            float to = -200f + index * 90f;
            actor.anchoredPosition = new Vector2(solved == 5 ? Mathf.Lerp(160, 245, (t - 0.82f) / 0.18f) : Mathf.Lerp(from, to, Mathf.Clamp01(part / 0.4f)), 0);
            for (int i = 0; i < clueMarks.Count; i++)
            {
                clueMarks[i].color = i < solved ? grey : red;
                clueMarks[i].enabled = i != index || solved == 5 || part < 0.4f || Mathf.Repeat(elapsed, 0.2f) < 0.1f;
                crossedWings[i].SetActive(i < solved);
            }
            searchWing.SetActive(solved == 5);
            jewel.enabled = solved == 5;
            jewel.rectTransform.anchoredPosition = new Vector2(265, 0);
        }
        else
        {
            bool hasJewel = t >= 0.2f;
            bool hasKey = t >= 0.5f;
            float x = t < 0.2f ? Mathf.Lerp(-160, 0, t / 0.2f) :
                t < 0.5f ? Mathf.Lerp(0, 225, (t - 0.2f) / 0.3f) :
                Mathf.Lerp(225, -250, Mathf.Clamp01((t - 0.5f) / 0.38f));
            actor.anchoredPosition = new Vector2(x, 0);
            jewel.rectTransform.anchoredPosition = hasJewel ? actor.anchoredPosition + new Vector2(-18, 63) : Vector2.zero;
            portalKey.rectTransform.anchoredPosition = hasKey ? actor.anchoredPosition + new Vector2(24, 63) : new Vector2(225, 0);
            portalKey.enabled = hasJewel && t < 0.9f;
            jewel.enabled = t < 0.9f;
            jewelSocket.color = hasJewel ? red : grey;
            keySocket.color = hasKey ? red : grey;
            exitRing.color = hasKey ? red : grey;
            exitRing.rectTransform.localScale = Vector3.one * (hasKey ? 1f + 0.08f * Mathf.Sin(elapsed * 5) : 1f);
            actor.gameObject.SetActive(t < 0.9f);
        }
    }

    void BuildClueLesson()
    {
        // Match the actual six-wing minimap; five solved scratches eliminate five wings.
        for (int i = 0; i < 6; i++)
        {
            Vector2 centre = new Vector2((i % 3 - 1) * 74, 185 - (i / 3) * 48);
            var cell = Rect(stage, "Wing", centre, new Vector2(66, 40));
            Outline(cell, grey);
            if (i < 5)
            {
                var cross = Rect(cell, "Eliminated", Vector2.zero, Vector2.zero);
                for (int slope = -1; slope <= 1; slope += 2)
                {
                    var bar = Graphic(cross, pixel, grey, Vector2.zero, new Vector2(59, 3));
                    bar.rectTransform.localRotation = Quaternion.Euler(0, 0, slope * 29);
                }
                crossedWings.Add(cross.gameObject);
                clueMarks.Add(Graphic(stage, clue, red, new Vector2(-200 + i * 90, 0), new Vector2(36, 36)));
            }
            else
            {
                var box = Rect(cell, "Search here", Vector2.zero, cell.sizeDelta);
                Outline(box, red);
                searchWing = box.gameObject;
            }
        }
        Graphic(stage, clue, red, new Vector2(-34, -156), new Vector2(36, 36));
        Graphic(stage, arrow, grey, new Vector2(8, -156), new Vector2(12, 22));
        Graphic(stage, gem, red, new Vector2(48, -156), new Vector2(24, 30));
    }

    void Outline(RectTransform parent, Color color)
    {
        Vector2 half = parent.sizeDelta * 0.5f;
        Graphic(parent, pixel, color, new Vector2(0, half.y), new Vector2(half.x * 2, 3));
        Graphic(parent, pixel, color, new Vector2(0, -half.y), new Vector2(half.x * 2, 3));
        Graphic(parent, pixel, color, new Vector2(-half.x, 0), new Vector2(3, half.y * 2));
        Graphic(parent, pixel, color, new Vector2(half.x, 0), new Vector2(3, half.y * 2));
    }

    RectTransform MakeActor()
    {
        var person = Rect(stage, "Player", new Vector2(-250, 0), new Vector2(40, 64));
        Graphic(person, pixel, grey, new Vector2(0, 24), new Vector2(16, 14));
        Graphic(person, pixel, grey, new Vector2(0, 3), new Vector2(12, 22));
        Graphic(person, pixel, grey, new Vector2(-13, 2), new Vector2(6, 23));
        Graphic(person, pixel, grey, new Vector2(13, 2), new Vector2(6, 23));
        Graphic(person, pixel, grey, new Vector2(-6, -18), new Vector2(7, 22));
        Graphic(person, pixel, grey, new Vector2(6, -18), new Vector2(7, 22));
        return person;
    }

    void Locker(Vector2 p, float height)
    {
        float w = height * 0.58f;
        Line(p.x-w/2, p.y-height/2, p.x-w/2, p.y+height/2, grey, 4);
        Line(p.x+w/2, p.y-height/2, p.x+w/2, p.y+height/2, grey, 4);
        Line(p.x-w/2, p.y-height/2, p.x+w/2, p.y-height/2, grey, 4);
        Line(p.x-w/2, p.y+height/2, p.x+w/2, p.y+height/2, grey, 4);
        Line(p.x-w*.28f, p.y+height*.23f, p.x+w*.28f, p.y+height*.23f, grey, 3);
        Line(p.x-w*.28f, p.y+height*.1f, p.x+w*.28f, p.y+height*.1f, grey, 3);
        Line(p.x+w*.23f, p.y-height*.1f, p.x+w*.23f, p.y-height*.27f, red, 3);
    }

    /// <summary>
    /// A wide key with an up arrow on it: the Shift symbol, drawn rather than spelled, because the
    /// game's only words are the story's ten.
    /// </summary>
    void ShiftKey(Vector2 at)
    {
        const float w = 84f, h = 40f, edge = 3f;
        shiftKey.Add(Graphic(stage, pixel, grey, at + new Vector2(0, h / 2 - edge / 2), new Vector2(w, edge)));
        shiftKey.Add(Graphic(stage, pixel, grey, at - new Vector2(0, h / 2 - edge / 2), new Vector2(w, edge)));
        shiftKey.Add(Graphic(stage, pixel, grey, at - new Vector2(w / 2 - edge / 2, 0), new Vector2(edge, h)));
        shiftKey.Add(Graphic(stage, pixel, grey, at + new Vector2(w / 2 - edge / 2, 0), new Vector2(edge, h)));
        Image head = Graphic(stage, arrow, grey, at + new Vector2(0, 4), new Vector2(14, 20));
        head.rectTransform.localRotation = Quaternion.Euler(0, 0, 90); // the arrow art points right
        shiftKey.Add(head);
        shiftKey.Add(Graphic(stage, pixel, grey, at + new Vector2(0, -8), new Vector2(5, 10))); // its stem
    }

    void Key(char c, Vector2 position)
    {
        keys.Add(Graphic(stage, keyCap, grey, position, new Vector2(40, 40)));
        int index = c - 'A';
        if (letters != null && index >= 0 && index < letters.Length)
            Graphic(stage, letters[index], red, position, new Vector2(22, 28));
    }

    void Line(float x1, float y1, float x2, float y2, Color color, float thickness)
    {
        Vector2 delta = new Vector2(x2-x1, y2-y1);
        var line = Graphic(stage, pixel, color, new Vector2((x1+x2)/2, (y1+y2)/2), new Vector2(delta.magnitude, thickness));
        line.rectTransform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
    }

    static RectTransform Rect(RectTransform parent, string name, Vector2 at, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f,0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
        return rect;
    }

    static Image Graphic(RectTransform parent, Sprite sprite, Color color, Vector2 at, Vector2 size)
    {
        var image = Rect(parent, "Illustration", at, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
