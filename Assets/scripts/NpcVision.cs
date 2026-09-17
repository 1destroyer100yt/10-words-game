using UnityEngine;

/// <summary>
/// Field-of-view sensor with a wall-clipped vision cone mesh, like the cop cones on a GTA radar.
/// Forward is this transform's +Y (the NPC sprite faces up). Every frame the cone is rebuilt from
/// raycasts against the wall layer, so it stops at walls, and the target counts as seen when it
/// is inside the cone and nothing blocks the line to it. A target hidden in a HidingSpot (see
/// PlayerHider) is never seen. Put this object on a layer only the minimap camera renders to keep
/// the cone off the main view.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NpcVision : MonoBehaviour
{
    [Tooltip("Object the NPC looks for.")]
    public Transform target;

    [Range(5f, 180f)]
    public float viewAngle = 70f;

    public float viewDistance = 12f;

    [Tooltip("Rays across the cone; more rays hug walls more closely.")]
    [Range(3, 90)]
    public int rayCount = 30;

    public LayerMask wallMask;

    public Color idleColor = new Color(0.93f, 0.11f, 0.14f, 0.6f);
    public Color alertColor = new Color(0.93f, 0.11f, 0.14f, 1f);

    /// <summary>True on the frames the target is inside the cone with a clear line of sight.</summary>
    public bool CanSeeTarget { get; private set; }

    /// <summary>Where the target was the last time it was seen.</summary>
    public Vector3 LastSeenPosition { get; private set; }

    Mesh mesh;
    Vector3[] vertices;
    Color[] colors;
    int[] triangles;
    PlayerHider targetHider;
    Transform hiderTarget;

    void Awake()
    {
        mesh = new Mesh { name = "Vision Cone" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        Allocate();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    void LateUpdate()
    {
        // Nothing is moving while the title screen or the pause screen is up, and rebuilding every
        // cone behind them is the most expensive thing in the frame.
        GameRun run = GameRun.Instance;
        if (run != null && !run.IsRunning) return;

        CanSeeTarget = Sense();
        if (CanSeeTarget)
        {
            LastSeenPosition = target.position;
        }
        BuildCone(CanSeeTarget ? alertColor : idleColor);
    }

    bool Sense()
    {
        if (target == null) return false;

        if (hiderTarget != target)
        {
            hiderTarget = target;
            targetHider = target.GetComponentInParent<PlayerHider>();
        }
        if (targetHider != null && targetHider.IsHidden) return false;

        Vector2 origin = transform.position;
        Vector2 toTarget = (Vector2)target.position - origin;
        float distance = toTarget.magnitude;
        if (distance > viewDistance) return false;
        if (distance > 0.001f && Vector2.Angle(transform.up, toTarget) > viewAngle * 0.5f) return false;

        return Physics2D.Linecast(origin, target.position, wallMask).collider == null;
    }

    void BuildCone(Color color)
    {
        if (vertices == null || vertices.Length != rayCount + 2) Allocate();

        vertices[0] = Vector3.zero;
        colors[0] = color;

        float halfAngle = viewAngle * 0.5f;
        for (int i = 0; i <= rayCount; i++)
        {
            float angle = -halfAngle + viewAngle * i / rayCount;
            Vector3 localDirection = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            Vector2 worldDirection = transform.TransformDirection(localDirection);

            RaycastHit2D hit = Physics2D.Raycast(transform.position, worldDirection, viewDistance, wallMask);
            float length = hit.collider != null ? hit.distance : viewDistance;

            vertices[i + 1] = localDirection * length; // parents are unscaled, so local units are world units
            colors[i + 1] = color;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void Allocate()
    {
        vertices = new Vector3[rayCount + 2];
        colors = new Color[rayCount + 2];
        triangles = new int[rayCount * 3];
        for (int i = 0; i < rayCount; i++)
        {
            // Clockwise as seen by a camera looking down +Z, which is the front face in Unity.
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }
    }
}
