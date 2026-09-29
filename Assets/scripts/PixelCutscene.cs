using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Plays PixelFrames full screen over the HUD: a black backdrop and one picture scaled to a whole
/// number of screen pixels per game pixel. Runs on unscaled time, so it works while the game is
/// frozen, and any key, click or button after a short grace period skips to the next frame. The
/// skip button in the top right corner, Esc or Start skips the whole scene.
/// </summary>
public class PixelCutscene : MonoBehaviour
{
    [Tooltip("Seconds a frame must stay up before a key press can skip it.")]
    public float minSkipSeconds = 1.2f;

    [Tooltip("Seconds before the skip button, Esc or Start will skip the whole scene, so the click that started the run does not.")]
    public float minSkipAllSeconds = 0.3f;

    // Brent's button art under Resources/UI, made by "work folder/skip-button/make_game_button.py".
    const string SkipArt = "UI/skip-button";
    const string SkipHoverArt = "UI/skip-button-hover";

    public bool IsPlaying { get; private set; }

    RectTransform root;
    Image backdrop, picture, skipButton;
    Sprite skipSprite, skipHoverSprite;

    void Awake()
    {
        root = transform as RectTransform;

        backdrop = Make("Backdrop");
        backdrop.color = Color.black;
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;

        picture = Make("Picture");
        picture.color = Color.white; // the frames carry their own three colours
        picture.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        picture.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        skipSprite = LoadSprite(SkipArt);
        skipHoverSprite = LoadSprite(SkipHoverArt);
        skipButton = Make("Skip");
        skipButton.sprite = skipSprite;
        skipButton.rectTransform.anchorMin = Vector2.one;
        skipButton.rectTransform.anchorMax = Vector2.one;
        skipButton.rectTransform.pivot = Vector2.one;

        SetVisible(false);
    }

    static Sprite LoadSprite(string path)
    {
        var texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
    }

    Image Make(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    void SetVisible(bool visible)
    {
        backdrop.enabled = visible;
        picture.enabled = visible;
        skipButton.enabled = visible && skipSprite != null;
    }

    /// <summary>Shows each frame for its number of seconds. Yield on it from a coroutine.</summary>
    public IEnumerator Play(PixelFrames.Frame[] frames, float[] seconds, System.Action<int> onFrame)
    {
        if (IsPlaying || frames == null || frames.Length == 0) yield break;
        IsPlaying = true;
        // No SetAsLastSibling here: the builder parks this just under the word cards, so the four
        // opening words land on top of the picture of the theft rather than behind it.
        SetVisible(true);

        float played = 0f;
        bool skipAll = false;
        for (int i = 0; i < frames.Length && !skipAll; i++)
        {
            float hold = seconds != null && i < seconds.Length ? seconds[i] : 3f;
            onFrame?.Invoke(i);
            float t = 0f;
            int variant = -1;
            while (t < hold)
            {
                int wanted = PixelFrames.Variant(frames[i], t);
                if (wanted != variant)
                {
                    variant = wanted;
                    picture.sprite = PixelFrames.Get(frames[i], variant);
                }
                Fit();
                if (skipButton.enabled)
                    skipButton.sprite = OverSkip() && skipHoverSprite != null ? skipHoverSprite : skipSprite;
                yield return null;
                t += Time.unscaledDeltaTime;
                played += Time.unscaledDeltaTime;
                if (played >= minSkipAllSeconds && SkipAllPressed(OverSkip())) { skipAll = true; break; }
                if (t >= minSkipSeconds && SkipPressed()) break;
            }
        }

        SetVisible(false);
        IsPlaying = false;
    }

    /// <summary>Whole pixels only, so the picture never blurs or shimmers.</summary>
    void Fit()
    {
        int scale = Mathf.Max(1, Mathf.Min(Screen.width / PixelFrames.Width, Screen.height / PixelFrames.Height));
        picture.rectTransform.sizeDelta = new Vector2(PixelFrames.Width * scale, PixelFrames.Height * scale);

        if (skipSprite == null) return;
        // A small corner button, 7% of the screen tall. Brent's art is soft-edged lava, so it is
        // smoothed rather than kept to whole pixels.
        Rect art = skipSprite.rect;
        float height = Mathf.Round(Screen.height * 0.07f);
        skipButton.rectTransform.sizeDelta = new Vector2(Mathf.Round(height * art.width / art.height), height);
        float margin = Mathf.Round(Screen.height * 0.025f);
        skipButton.rectTransform.anchoredPosition = new Vector2(-margin, -margin);
    }

    /// <summary>True while the mouse is over the skip button.</summary>
    bool OverSkip()
    {
        Pointer mouse = Pointer.current;
        if (mouse == null || !skipButton.enabled) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(skipButton.rectTransform, mouse.position.ReadValue(), null);
    }

    /// <summary>The skip button clicked, or Esc or Start: the whole scene, not just this frame.</summary>
    static bool SkipAllPressed(bool overButton)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
        Pointer mouse = Pointer.current;
        if (overButton && mouse != null && mouse.press.wasPressedThisFrame) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
        Pointer mouse = Pointer.current;
        if (mouse != null && mouse.press.wasPressedThisFrame) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonSouth.wasPressedThisFrame;
    }
}
