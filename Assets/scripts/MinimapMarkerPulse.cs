using UnityEngine;

/// <summary>
/// Pulses the player's own minimap dot in size. It is black on a map drawn in black lines, and a
/// shape that breathes is found at a glance without spending a fourth colour.
/// </summary>
public class MinimapMarkerPulse : MonoBehaviour
{
    public float period = 0.8f;
    [Range(0f, 2f)] public float grow = 0.6f;

    Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float t = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.05f, period)) * 0.5f + 0.5f;
        transform.localScale = baseScale * (1f + grow * t);
    }
}
