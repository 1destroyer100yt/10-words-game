using System.Collections.Generic;
using UnityEngine;

/// <summary>A locker the player can step into. Enemy vision ignores a hidden player.</summary>
public class HidingSpot : MonoBehaviour, IRunResettable
{
    public SpriteRenderer spriteRenderer;
    public Sprite emptySprite;
    public Sprite occupiedSprite;

    public bool Occupied { get; private set; }

    public static readonly List<HidingSpot> All = new List<HidingSpot>();

    void OnEnable()
    {
        All.Add(this);
        Apply();
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public void SetOccupied(bool occupied)
    {
        Occupied = occupied;
        Apply();
    }

    public void ResetRun()
    {
        SetOccupied(false);
    }

    void Apply()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        Sprite sprite = Occupied ? occupiedSprite : emptySprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }

    /// <summary>The closest spot within maxDistance, or null.</summary>
    public static HidingSpot Nearest(Vector3 position, float maxDistance)
    {
        HidingSpot best = null;
        float bestDistance = maxDistance;
        foreach (HidingSpot spot in All)
        {
            float distance = Vector2.Distance(spot.transform.position, position);
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = spot;
        }
        return best;
    }

    // Enter Play Mode keeps the loaded domain (Reload Domain is off in this project), so statics
    // keep whatever the last play session left in them. This wipes them at the start of each one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        All.Clear();
    }
}
