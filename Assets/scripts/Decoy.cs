using UnityEngine;

/// <summary>
/// A thrown coin: flies in a straight line to its landing point, then "clatters" by raising
/// NoiseEmitted so enemies within the radius come to investigate. Destroyed after its lifetime
/// or when the run restarts.
/// </summary>
public class Decoy : MonoBehaviour, IRunResettable
{
    /// <summary>World position and radius of a noise. Enemies subscribe to this.</summary>
    public static event System.Action<Vector3, float> NoiseEmitted;

    /// <summary>Raises NoiseEmitted from outside this class; a solved clue is loud too.</summary>
    public static void Emit(Vector3 at, float radius)
    {
        NoiseEmitted?.Invoke(at, radius);
    }

    public float noiseRadius = 12f;
    public float lifetime = 8f;

    [Tooltip("How much the coin grows mid-flight, to suggest an arc.")]
    public float hop = 0.6f;

    Vector3 start;
    Vector3 landing;
    Vector3 baseScale;
    float flightSeconds;
    float launched = -1f;
    float landedAt;
    bool landed;
    System.Action<Vector3> onLanded;

    public void Launch(Vector3 from, Vector3 to, float speed, System.Action<Vector3> landedCallback)
    {
        start = from;
        landing = to;
        onLanded = landedCallback;
        flightSeconds = Mathf.Max(0.05f, Vector3.Distance(from, to) / Mathf.Max(0.01f, speed));
        launched = Time.time;
        baseScale = transform.localScale;
        transform.position = from;
    }

    void Update()
    {
        if (launched < 0f) return;

        if (!landed)
        {
            float t = Mathf.Clamp01((Time.time - launched) / flightSeconds);
            transform.position = Vector3.Lerp(start, landing, t);
            transform.localScale = baseScale * (1f + hop * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f) Land();
            return;
        }

        if (Time.time - landedAt > lifetime) Destroy(gameObject);
    }

    void Land()
    {
        landed = true;
        landedAt = Time.time;
        transform.position = landing;
        transform.localScale = baseScale;
        NoiseEmitted?.Invoke(landing, noiseRadius);
        onLanded?.Invoke(landing);
    }

    public void ResetRun()
    {
        Destroy(gameObject);
    }

    // Enter Play Mode keeps the loaded domain (Reload Domain is off in this project), so statics
    // keep whatever the last play session left in them. This wipes them at the start of each one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        NoiseEmitted = null;
    }
}
