using UnityEngine;

/// <summary>
/// Bakes the wall pixels of a two-tone floorplan texture into BoxCollider2D children.
/// Solid pixels are greedy-merged into maximal axis-aligned rectangles: scanning the image
/// from its top row, take the widest unused solid run on the row, then extend it downward
/// while the full width stays solid and unused.
/// Keep this object at the same position as the floorplan sprite (bottom-left pivot at the origin).
/// </summary>
public class WallColliderBaker : MonoBehaviour
{
    [Tooltip("Two-tone floorplan: dark = wall, light = floor. Needs Read/Write enabled in the importer.")]
    public Texture2D map;

    [Tooltip("Must match the floorplan sprite's Pixels Per Unit.")]
    public float pixelsPerUnit = 7f;

    [Tooltip("Pixels whose normalized luminance is below this count as wall.")]
    public float darkThreshold = 0.15f;

    [Tooltip("Layer the generated collider children are placed on.")]
    public string wallLayer = "Walls";

    /// <summary>Number of rectangles produced by the last successful Bake().</summary>
    public int LastBakeCount { get; private set; }

    [ContextMenu("Bake")]
    public void Bake()
    {
        LastBakeCount = 0;

        if (map == null)
        {
            Debug.LogError("WallColliderBaker: no map texture assigned.", this);
            return;
        }

        if (!map.isReadable)
        {
            Debug.LogError($"WallColliderBaker: '{map.name}' is not readable. Select the texture, open " +
                           "Advanced in the texture importer, tick Read/Write and press Apply.", this);
            return;
        }

        int layer = LayerMask.NameToLayer(wallLayer);
        if (layer < 0)
        {
            Debug.LogError($"WallColliderBaker: layer '{wallLayer}' does not exist. Add it under " +
                           "Edit > Project Settings > Tags and Layers.", this);
            return;
        }

        if (pixelsPerUnit <= 0f)
        {
            Debug.LogError("WallColliderBaker: pixelsPerUnit must be positive.", this);
            return;
        }

        ClearChildren();

        int w = map.width;
        int h = map.height;
        Color32[] pixels = map.GetPixels32(); // row 0 is the bottom row of the image
        bool[] solid = new bool[w * h];
        for (int i = 0; i < solid.Length; i++)
        {
            Color32 c = pixels[i];
            float luminance = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
            solid[i] = luminance < darkThreshold;
        }

        bool[] used = new bool[w * h];
        float unit = 1f / pixelsPerUnit;
        int count = 0;

        // Start at the top image row (y = h - 1) so rectangles grow downward toward y = 0.
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = 0; x < w; x++)
            {
                int index = y * w + x;
                if (!solid[index] || used[index]) continue;

                int rectWidth = 1;
                while (x + rectWidth < w && solid[index + rectWidth] && !used[index + rectWidth]) rectWidth++;

                int rectHeight = 1;
                while (y - rectHeight >= 0 && RowIsFree(solid, used, w, x, y - rectHeight, rectWidth)) rectHeight++;

                for (int j = 0; j < rectHeight; j++)
                {
                    int rowStart = (y - j) * w + x;
                    for (int i = 0; i < rectWidth; i++) used[rowStart + i] = true;
                }

                int bottom = y - rectHeight + 1;
                var wall = new GameObject($"Wall_{count}");
                wall.layer = layer;
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = new Vector3((x + rectWidth * 0.5f) * unit,
                                                           (bottom + rectHeight * 0.5f) * unit, 0f);
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = new Vector2(rectWidth * unit, rectHeight * unit);
                count++;

                x += rectWidth - 1;
            }
        }

        LastBakeCount = count;
        Debug.Log($"Baked {count} wall colliders.", this);
    }

    static bool RowIsFree(bool[] solid, bool[] used, int w, int x, int y, int width)
    {
        int start = y * w + x;
        for (int i = 0; i < width; i++)
        {
            if (!solid[start + i] || used[start + i]) return false;
        }
        return true;
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
