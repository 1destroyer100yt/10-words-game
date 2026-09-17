using UnityEngine;

/// <summary>
/// One clue: a red mark somewhere in the building. Stand on it and its blink hurries until it is
/// solved, then it goes grey and the StoryDirector crosses its wing off the minimap. The director
/// does the counting and the timing; this only draws.
/// </summary>
public class Clue : MonoBehaviour
{
    [Tooltip("Which wing of the building this clue crosses off: an index into the director's crosses.")]
    public int zone;

    public SpriteRenderer body;

    [Tooltip("Its dot on the minimap; gone once solved.")]
    public SpriteRenderer marker;

    public Color armedColor = new Color32(237, 28, 36, 255);
    public Color dormantColor = new Color32(70, 70, 70, 255);

    [Tooltip("Blink period while nobody is standing on it.")]
    public float blinkSeconds = 0.9f;

    public bool Solved { get; private set; }

    /// <summary>0..1, how long the player has been standing on it. The director drives this.</summary>
    public float Progress { get; set; }

    void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
    }

    public void SetSolved(bool solved)
    {
        Solved = solved;
        Progress = 0f;
        if (marker != null) marker.enabled = !solved;
        if (body != null) body.color = solved ? dormantColor : armedColor;
    }

    void Update()
    {
        if (Solved || body == null) return;
        float period = Mathf.Lerp(blinkSeconds, 0.12f, Progress);
        bool on = Mathf.Repeat(Time.time, period) < period * 0.6f;
        body.color = on ? armedColor : dormantColor;
    }
}
