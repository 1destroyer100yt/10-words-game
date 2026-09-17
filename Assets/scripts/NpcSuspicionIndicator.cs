using UnityEngine;

/// <summary>
/// A "?" with a fill bar while an enemy grows suspicious and a "!" the moment it starts chasing.
/// Kept upright above the enemy whichever way it turns.
/// </summary>
public class NpcSuspicionIndicator : MonoBehaviour
{
    public NpcChaser chaser;
    public SpriteRenderer icon;
    public Sprite questionSprite;
    public Sprite exclaimSprite;

    [Tooltip("Bar under the icon, scaled by suspicion; its sprite is 1 unit square at scale 1.")]
    public Transform bar;

    public float barWidth = 1.2f;
    public float barHeight = 0.15f;
    public float barOffset = -0.85f;
    public float height = 1.5f;
    public float exclaimSeconds = 1f;

    [Tooltip("The demon's darkness rule. When set, the warning fades with the demon, so a floating ? " +
             "cannot give away a demon the dark is hiding.")]
    public MinimapReveal reveal;

    float exclaimUntil = -1f;
    NpcChaser.Mode lastMode;

    void Awake()
    {
        if (chaser == null) chaser = GetComponentInParent<NpcChaser>();
        if (reveal == null && chaser != null) reveal = chaser.GetComponent<MinimapReveal>();
    }

    void LateUpdate()
    {
        if (chaser == null || icon == null) return;

        transform.SetPositionAndRotation(chaser.transform.position + Vector3.up * height, Quaternion.identity);

        NpcChaser.Mode mode = chaser.CurrentMode;
        if (mode == NpcChaser.Mode.Chase && lastMode != NpcChaser.Mode.Chase) exclaimUntil = Time.time + exclaimSeconds;
        lastMode = mode;

        bool exclaim = Time.time < exclaimUntil;
        bool question = !exclaim && mode != NpcChaser.Mode.Chase && chaser.Suspicion > 0.01f;
        float brightness = reveal != null && reveal.HidesInDark ? reveal.Brightness : 1f;
        bool lit = brightness > 0.01f;
        icon.enabled = (exclaim || question) && lit;
        SetAlpha(icon, brightness);
        if (exclaim) icon.sprite = exclaimSprite;
        else if (question) icon.sprite = questionSprite;

        if (bar == null) return;
        bool showBar = question && lit;
        if (bar.gameObject.activeSelf != showBar) bar.gameObject.SetActive(showBar);
        if (!showBar) return;
        SetAlpha(bar.GetComponent<SpriteRenderer>(), brightness);
        float width = barWidth * Mathf.Clamp01(chaser.Suspicion);
        bar.localScale = new Vector3(width, barHeight, 1f);
        bar.localPosition = new Vector3((width - barWidth) * 0.5f, barOffset, 0f); // fills from the left
    }

    static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null) return;
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
