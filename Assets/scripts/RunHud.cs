using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pixel-digit readout of the run time (grey) and the best time (red), right-aligned under the
/// minimap. Glyphs are 5x5 cells holding a 3x5 digit, drawn with point filtering, so the display
/// stays inside the palette with no anti-aliased text. The rows are built at runtime from UI Images.
/// </summary>
public class RunHud : MonoBehaviour
{
    [Tooltip("12 glyph sprites in this order: 0-9, ':' and '.'.")]
    public Sprite[] glyphs;

    [Tooltip("Minimap camera; the rows sit just under its viewport.")]
    public Camera minimapCamera;

    public int cellPixels = 5;

    [Tooltip("Screen pixels per glyph pixel at a 720-pixel-tall window.")]
    public int scale = 5;

    [Tooltip("Grow and shrink with the window, so the clock is the same share of the screen at any size.")]
    public bool scaleWithWindow = true;

    int PixelScale => scaleWithWindow
        ? Mathf.Clamp(Mathf.RoundToInt(scale * Screen.height / 720f), 2, scale * 2)
        : scale;

    [Tooltip("Gap in screen pixels below the minimap and between the rows.")]
    public float gap = 6f;

    public Color timeColor = new Color32(70, 70, 70, 255);
    public Color bestColor = new Color32(237, 28, 36, 255);
    public bool showBest = true;

    [Tooltip("Set by the title screen, which covers the clock while it is up.")]
    public bool hidden;

    readonly List<Image> timeImages = new List<Image>();
    readonly List<Image> bestImages = new List<Image>();
    RectTransform timeRow;
    RectTransform bestRow;
    PixelCutscene cutscene;

    void Awake()
    {
        cutscene = GetComponentInChildren<PixelCutscene>(true);
        timeRow = CreateRow("Time");
        bestRow = CreateRow("Best");
    }

    void LateUpdate()
    {
        GameRun run = GameRun.Instance;
        if (run == null) return;

        if (hidden || (cutscene != null && cutscene.IsPlaying))
        {
            foreach (Image image in timeImages) image.enabled = false;
            foreach (Image image in bestImages) image.enabled = false;
            return;
        }

        float cell = cellPixels * PixelScale;
        Vector2 anchor = TopRightAnchor();

        string time = Format(run.RunTime);
        SetText(timeRow, timeImages, time, timeColor);
        timeRow.anchoredPosition = new Vector2(anchor.x - time.Length * cell, anchor.y - cell);

        bool best = showBest && run.BestTime > 0f;
        if (bestRow.gameObject.activeSelf != best) bestRow.gameObject.SetActive(best);
        if (!best) return;

        string bestText = Format(run.BestTime);
        SetText(bestRow, bestImages, bestText, bestColor);
        bestRow.anchoredPosition = new Vector2(anchor.x - bestText.Length * cell, anchor.y - 2f * cell - gap);
    }

    /// <summary>Screen pixel position (bottom-left origin) of the top-right corner of the readout.</summary>
    Vector2 TopRightAnchor()
    {
        if (minimapCamera != null)
        {
            Rect rect = minimapCamera.pixelRect;
            return new Vector2(rect.xMax, rect.yMin - gap);
        }
        return new Vector2(Screen.width - gap, Screen.height - gap);
    }

    RectTransform CreateRow(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        return rect;
    }

    void SetText(RectTransform row, List<Image> images, string text, Color color)
    {
        float cell = cellPixels * PixelScale;
        while (images.Count < text.Length)
        {
            var go = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(row, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            images.Add(image);
        }

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            bool used = i < text.Length;
            if (image.enabled != used) image.enabled = used;
            if (!used) continue;
            image.sprite = Glyph(text[i]);
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(cell, cell);
            image.rectTransform.anchoredPosition = new Vector2(i * cell, 0f);
        }
        row.sizeDelta = new Vector2(text.Length * cell, cell);
    }

    Sprite Glyph(char c)
    {
        if (glyphs == null || glyphs.Length == 0) return null;
        int index = c >= '0' && c <= '9' ? c - '0' : c == ':' ? 10 : 11;
        return index < glyphs.Length ? glyphs[index] : null;
    }

    /// <summary>m:ss, for example 1:05.</summary>
    public static string Format(float seconds)
    {
        int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return $"{total / 60}:{total % 60:00}";
    }
}
