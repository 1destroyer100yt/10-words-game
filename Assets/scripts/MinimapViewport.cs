using UnityEngine;

/// <summary>
/// Keeps a minimap camera's viewport square (1:1 in screen pixels) in a screen corner and fits a
/// world-space rectangle inside it, whatever the window size or aspect ratio.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MinimapViewport : MonoBehaviour
{
    [Tooltip("Side of the square minimap as a fraction of the screen height.")]
    [Range(0.05f, 0.6f)]
    public float size = 0.28f;

    [Tooltip("Gap between the minimap and the screen edges, as a fraction of the screen height.")]
    [Range(0f, 0.2f)]
    public float margin = 0.01f;

    [Tooltip("World-space width and height that must stay fully visible in the minimap.")]
    public Vector2 worldSize = new Vector2(96f, 82.29f);

    [Tooltip("Extra space around the fitted area, as a fraction of it.")]
    [Range(0f, 0.5f)]
    public float padding = 0.02f;

    Camera minimapCamera;

    void OnEnable()
    {
        minimapCamera = GetComponent<Camera>();
        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    void Apply()
    {
        if (minimapCamera == null || Screen.width <= 0 || Screen.height <= 0) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float height = size;
        float width = size / screenAspect;      // same number of pixels as the height
        float marginY = margin;
        float marginX = margin / screenAspect;
        minimapCamera.rect = new Rect(1f - marginX - width, 1f - marginY - height, width, height);

        // A square viewport shows 2 * orthographicSize units in both directions.
        float largest = Mathf.Max(worldSize.x, worldSize.y);
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = largest * 0.5f * (1f + padding);
    }
}
