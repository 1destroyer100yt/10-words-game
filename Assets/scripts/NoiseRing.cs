using UnityEngine;

/// <summary>Expanding ring drawn on the minimap where a noise happened; destroys itself when done.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NoiseRing : MonoBehaviour, IRunResettable
{
    public float radius = 12f;
    public float seconds = 0.6f;

    float started;
    float spriteDiameter = 1f;

    void Start()
    {
        started = Time.time;
        Sprite sprite = GetComponent<SpriteRenderer>().sprite;
        if (sprite != null) spriteDiameter = Mathf.Max(0.001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        float t = (Time.time - started) / Mathf.Max(0.01f, seconds);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        float scale = Mathf.Lerp(0.5f, radius * 2f, t) / spriteDiameter;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void ResetRun()
    {
        Destroy(gameObject);
    }
}
