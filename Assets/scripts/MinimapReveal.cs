using UnityEngine;

/// <summary>
/// Radar rule for minimap markers: shown only while the owner is alerted (suspicious, chasing,
/// searching or alarmed) or inside the player's own line of sight. With rememberOnceSeen the
/// marker stays once the player has seen the owner, which suits fixed cameras.
///
/// For a demon it also governs the main view: its own sprites fade out wherever the player's light
/// does not reach, behind a wall or past the edge of the glow, so the dark genuinely hides it.
/// </summary>
public class MinimapReveal : MonoBehaviour
{
    [Tooltip("Minimap renderers of this owner: its dot, its vision cone.")]
    public Renderer[] renderers;

    public Transform player;

    [Tooltip("How far the player can see; matches the player's light radius.")]
    public float sightRadius = 16f;

    public LayerMask wallMask;
    public bool rememberOnceSeen;
    public NpcChaser chaser;
    public SecurityCamera securityCamera;

    [Header("Main view")]
    [Tooltip("Demons only: fade the owner's own sprites out where the player's light does not reach.")]
    public bool hideInDark = true;

    [Tooltip("Fraction of sightRadius at which a demon starts fading; it is gone at sightRadius.")]
    [Range(0f, 1f)] public float fadeStart = 0.5f;

    [Tooltip("How quickly a demon fades in or out, in full fades per second.")]
    public float fadeSpeed = 5f;

    public bool Visible { get; private set; }

    /// <summary>1 when the player's light shows this demon fully, 0 when the dark hides it.</summary>
    public float Brightness { get; private set; }

    /// <summary>True for a demon whose sprites this component fades.</summary>
    public bool HidesInDark => body != null;

    bool seenBefore;
    SpriteRenderer[] body;
    float[] bodyAlpha;

    void Awake()
    {
        if (chaser == null || !hideInDark) return;

        // Everything the main camera draws for this demon, but not its minimap dot or cone.
        Camera view = Camera.main;
        int drawn = view != null ? view.cullingMask : ~0;
        var found = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderers != null && System.Array.IndexOf(renderers, sprite) >= 0) continue;
            if ((drawn & (1 << sprite.gameObject.layer)) == 0) continue;
            found.Add(sprite);
        }
        body = found.ToArray();
        bodyAlpha = new float[body.Length];
        for (int i = 0; i < body.Length; i++) bodyAlpha[i] = body[i].color.a;
    }

    void OnEnable()
    {
        // Switched on by the difficulty menu somewhere in the dark: start hidden, not popping out.
        Brightness = 0f;
        ApplyBrightness();
    }

    void LateUpdate()
    {
        bool alerted = (chaser != null && chaser.IsAlerted) || (securityCamera != null && securityCamera.Alarmed);
        bool inSight = InSight();
        if (inSight) seenBefore = true;

        Visible = alerted || inSight || (rememberOnceSeen && seenBefore);
        UpdateBrightness(inSight);
        if (renderers == null) return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled != Visible) renderer.enabled = Visible;
        }
    }

    void UpdateBrightness(bool inSight)
    {
        if (body == null) return;

        float target = 0f;
        if (inSight && player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            target = 1f - Mathf.InverseLerp(sightRadius * fadeStart, sightRadius, distance);
        }
        Brightness = Mathf.MoveTowards(Brightness, target, fadeSpeed * Time.unscaledDeltaTime);
        ApplyBrightness();
    }

    void ApplyBrightness()
    {
        if (body == null) return;
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == null) continue;
            Color color = body[i].color;
            color.a = bodyAlpha[i] * Brightness;
            body[i].color = color;
        }
    }

    bool InSight()
    {
        if (player == null) return false;
        Vector2 from = transform.position;
        Vector2 to = player.position;
        if (Vector2.Distance(from, to) > sightRadius) return false;
        return Physics2D.Linecast(from, to, wallMask).collider == null;
    }
}
