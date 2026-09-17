using UnityEngine;

/// <summary>
/// Smoothly follows a target in 2D at a fixed orthographic zoom, optionally clamped so the view
/// never leaves a world rectangle (the floorplan). Runs in LateUpdate so it sees the final
/// position of the target for the frame. Shake() adds a decaying random offset on top.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;

    [Tooltip("Orthographic size: half the visible height in world units. Smaller is more zoomed in.")]
    public float zoom = 12f;

    [Tooltip("Seconds the camera takes to catch up with the target.")]
    public float smoothTime = 0.15f;

    [Tooltip("Keep the whole view inside 'bounds'.")]
    public bool clampToBounds = true;

    [Tooltip("World rectangle the view stays inside when clamping.")]
    public Rect bounds = new Rect(0f, 0f, 96f, 82.29f);

    Camera followCamera;
    Vector3 velocity;
    Vector3 basePosition;
    float shakeAmplitude;
    float shakeSeconds = 0.3f;
    float shakeUntil = -1f;

    void OnEnable()
    {
        followCamera = GetComponent<Camera>();
        followCamera.orthographic = true;
        followCamera.orthographicSize = zoom;
        basePosition = transform.position;
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (!Mathf.Approximately(followCamera.orthographicSize, zoom)) followCamera.orthographicSize = zoom;

        Vector3 desired = ClampToBounds(target.position);
        desired.z = basePosition.z;
        basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime);
        transform.position = basePosition + ShakeOffset();
    }

    /// <summary>Jumps to the target immediately, for example right after spawning or teleporting.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 position = ClampToBounds(target.position);
        position.z = transform.position.z;
        basePosition = position;
        transform.position = position;
        velocity = Vector3.zero;
    }

    /// <summary>Random offset of up to 'amplitude' units, fading out over 'seconds' (unscaled time).</summary>
    public void Shake(float amplitude, float seconds)
    {
        shakeAmplitude = amplitude;
        shakeSeconds = Mathf.Max(0.01f, seconds);
        shakeUntil = Time.unscaledTime + shakeSeconds;
    }

    Vector3 ShakeOffset()
    {
        if (Time.unscaledTime >= shakeUntil) return Vector3.zero;
        float remaining = (shakeUntil - Time.unscaledTime) / shakeSeconds;
        Vector2 offset = Random.insideUnitCircle * (shakeAmplitude * remaining);
        return new Vector3(offset.x, offset.y, 0f);
    }

    Vector3 ClampToBounds(Vector3 position)
    {
        if (!clampToBounds || followCamera == null) return position;

        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;
        position.x = ClampAxis(position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        position.y = ClampAxis(position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
        return position;
    }

    /// <summary>Clamps, or centres when the view is wider than the allowed range.</summary>
    static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
