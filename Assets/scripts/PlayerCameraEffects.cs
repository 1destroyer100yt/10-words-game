using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives the player camera's vignette. The edges close in as the nearest demon gets closer, seen
/// or not, and squeeze on every heartbeat in time with the sound; when a demon is right on top of
/// you each beat also knocks the camera. Being spotted flashes the edges red and shakes the view.
/// Works on the volume's runtime profile copy, so the asset is never modified.
/// </summary>
[RequireComponent(typeof(Volume))]
public class PlayerCameraEffects : MonoBehaviour
{
    public Transform player;
    public CameraFollow2D cameraFollow;

    [Header("Vignette")]
    public float baseIntensity = 0.58f;
    [Range(0.01f, 1f)] public float baseSmoothness = 0.62f;
    public Color baseColor = Color.black;

    [Tooltip("How much further the edges close in with a demon at point-blank range, between beats.")]
    public float closeDarkening = 0.14f;

    [Header("Heartbeat")]
    [Tooltip("Extra vignette intensity at the peak of a beat when the demon is close.")]
    public float maxPulse = 0.35f;

    [Tooltip("Extra vignette smoothness at the peak of a beat, so the squeeze spreads inward.")]
    public float pulseSmoothness = 0.2f;

    [Tooltip("Seconds one lub-dub takes to play out on screen; matches the heartbeat sound.")]
    public float beatSeconds = 0.55f;

    [Tooltip("Closeness above which each beat also knocks the camera.")]
    [Range(0f, 1f)] public float beatShakeFrom = 0.5f;
    public float beatShakeAmplitude = 0.14f;
    public float beatShakeSeconds = 0.12f;

    [Header("Spotted")]
    public Color spottedColor = new Color32(237, 28, 36, 255);
    public float spottedIntensity = 0.95f;
    public float spottedSeconds = 0.6f;
    public float shakeAmplitude = 0.35f;
    public float shakeSeconds = 0.35f;

    Volume volume;
    Vignette vignette;
    GameRun subscribedTo;
    float beatAt = -10f;
    float beatStrength;
    float spottedUntil = -1f;

    void Awake()
    {
        volume = GetComponent<Volume>();
        // 'profile' (not sharedProfile) is an instance created for this volume at runtime.
        if (volume.profile != null) volume.profile.TryGet(out vignette);
    }

    void OnEnable()
    {
        GameAudio.HeartBeat += OnHeartBeat;
    }

    void OnDisable()
    {
        GameAudio.HeartBeat -= OnHeartBeat;
        if (subscribedTo == null) return;
        subscribedTo.Spotted -= OnSpotted;
        subscribedTo = null;
    }

    void Subscribe()
    {
        GameRun run = GameRun.Instance;
        if (run == null || run == subscribedTo) return;
        if (subscribedTo != null) subscribedTo.Spotted -= OnSpotted;
        subscribedTo = run;
        run.Spotted += OnSpotted;
    }

    void Update()
    {
        if (vignette == null) return;
        Subscribe();

        float closeness = GameAudio.DemonCloseness;
        float elapsed = (Time.unscaledTime - beatAt) / Mathf.Max(0.05f, beatSeconds);
        float beat = elapsed < 1f ? Heartbeat(elapsed) * beatStrength : 0f;

        float spotted = spottedUntil > Time.unscaledTime
            ? Mathf.Clamp01((spottedUntil - Time.unscaledTime) / Mathf.Max(0.01f, spottedSeconds))
            : 0f;

        float calm = Mathf.Clamp01(baseIntensity + closeDarkening * closeness + maxPulse * beat);
        vignette.intensity.value = Mathf.Lerp(calm, spottedIntensity, spotted);
        vignette.smoothness.value = Mathf.Clamp(baseSmoothness + pulseSmoothness * beat, 0.01f, 1f);
        vignette.color.value = Color.Lerp(baseColor, spottedColor, spotted);
    }

    void OnHeartBeat(float closeness)
    {
        beatAt = Time.unscaledTime;
        // Squared, so a distant demon barely stirs the edges and a close one slams them.
        beatStrength = Mathf.Lerp(0.25f, 1f, closeness * closeness);

        bool spottedShakeRunning = spottedUntil > Time.unscaledTime;
        if (cameraFollow != null && closeness >= beatShakeFrom && !spottedShakeRunning)
        {
            float t = Mathf.InverseLerp(beatShakeFrom, 1f, closeness);
            cameraFollow.Shake(beatShakeAmplitude * t, beatShakeSeconds);
        }
    }

    /// <summary>Two bumps per beat (lub-dub), 0..1, with the dub landing where the sound's does.</summary>
    static float Heartbeat(float t)
    {
        return Mathf.Max(Bump(t, 0.06f, 0.07f), 0.7f * Bump(t, 0.36f, 0.08f));
    }

    static float Bump(float t, float centre, float width)
    {
        float d = (t - centre) / width;
        return Mathf.Exp(-d * d);
    }

    void OnSpotted()
    {
        spottedUntil = Time.unscaledTime + spottedSeconds;
        if (cameraFollow != null) cameraFollow.Shake(shakeAmplitude, shakeSeconds);
    }
}
