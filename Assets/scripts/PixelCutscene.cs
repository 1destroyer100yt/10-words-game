using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Plays PixelFrames full screen over the HUD: a black backdrop and one picture scaled to a whole
/// number of screen pixels per game pixel. Runs on unscaled time, so it works while the game is
/// frozen, and any key, click or button after a short grace period skips to the next frame.
/// </summary>
public class PixelCutscene : MonoBehaviour
{
    [Tooltip("Seconds a frame must stay up before a key press can skip it.")]
    public float minSkipSeconds = 1.2f;

    public bool IsPlaying { get; private set; }

    RectTransform root;
    Image backdrop, picture;

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

        SetVisible(false);
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
    }

    /// <summary>Shows each frame for its number of seconds. Yield on it from a coroutine.</summary>
    public IEnumerator Play(PixelFrames.Frame[] frames, float[] seconds, System.Action<int> onFrame)
    {
        if (IsPlaying || frames == null || frames.Length == 0) yield break;
        IsPlaying = true;
        // No SetAsLastSibling here: the builder parks this just under the word cards, so the four
        // opening words land on top of the picture of the theft rather than behind it.
        SetVisible(true);

        for (int i = 0; i < frames.Length; i++)
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
                yield return null;
                t += Time.unscaledDeltaTime;
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
    }

    static bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
    }
}
