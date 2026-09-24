using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attack (left mouse, Enter, or the gamepad's west button) throws a coin the way the player is
/// facing. It stops short of walls, and where it lands it makes a noise that enemies in range
/// come to investigate. A ring on the minimap shows how far the noise carried.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class DecoyThrower : MonoBehaviour, IRunResettable
{
    [Tooltip("Optional. Uses the Player/Attack action. Empty = mouse, Enter and gamepad directly.")]
    public InputActionAsset actions;
    public Sprite coinSprite;
    public Sprite ringSprite;
    public Material spriteMaterial;
    public LayerMask wallMask;

    [Tooltip("Layer only the minimap camera renders, for the noise ring.")]
    public int minimapLayer;

    public float range = 7f;
    public float speed = 18f;
    public float noiseRadius = 12f;
    public float cooldown = 1.5f;
    public float coinLifetime = 8f;
    public float coinSize = 0.5f;

    [Tooltip("A mouse click throws toward the pointer (up to range); Enter and the gamepad throw the way you face.")]
    public bool aimWithMouse = true;

    [Tooltip("The coin sprite is a white template (the menu tints it grey), so the thrown coin is tinted here.")]
    public Color coinColor = new Color32(237, 28, 36, 255);

    [Tooltip("The ring sprite is a white template too; untinted, the minimap drew the noise in white.")]
    public Color ringColor = new Color32(237, 28, 36, 255);
    public float ringSeconds = 0.6f;

    PlayerController controller;
    PlayerHider hider;
    InputAction attack;
    float nextThrow;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        hider = GetComponent<PlayerHider>();
    }

    void OnEnable()
    {
        if (actions != null) attack = actions.FindAction("Player/Attack");
        if (attack != null) attack.Enable(); // ours to enable; see PlayerHider.OnEnable
    }

    void OnDisable()
    {
        if (attack != null) attack.Disable();
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        if (run != null && !run.AcceptsGameplayInput) return;
        if (hider != null && hider.IsHidden) return;
        if (Time.time < nextThrow || !ThrowPressed()) return;
        Throw();
    }

    void Throw()
    {
        nextThrow = Time.time + cooldown;

        Vector2 origin = transform.position;
        Vector2 direction = controller.Facing;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
        float reach = range;

        Mouse mouse = Mouse.current;
        Camera view = Camera.main;
        if (aimWithMouse && mouse != null && view != null && mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 pointer = view.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 toPointer = pointer - origin;
            if (toPointer.sqrMagnitude > 0.01f)
            {
                direction = toPointer.normalized;
                reach = Mathf.Min(range, toPointer.magnitude); // land where you clicked, if it is in range
            }
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, reach, wallMask);
        float distance = hit.collider != null ? Mathf.Max(0f, hit.distance - 0.4f) /* never past a close wall */ : reach;
        Vector3 landing = origin + direction * distance;

        var coin = new GameObject("Coin");
        coin.transform.position = origin;
        var renderer = coin.AddComponent<SpriteRenderer>();
        renderer.sprite = coinSprite;
        renderer.color = coinColor;
        renderer.sortingOrder = 2;
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
        if (coinSprite != null)
        {
            float size = Mathf.Max(coinSprite.bounds.size.x, coinSprite.bounds.size.y);
            if (size > 0f) coin.transform.localScale = Vector3.one * (coinSize / size);
        }

        var decoy = coin.AddComponent<Decoy>();
        decoy.noiseRadius = noiseRadius;
        decoy.lifetime = coinLifetime;
        decoy.Launch(origin, landing, speed, SpawnRing);
    }

    public void ResetRun()
    {
        nextThrow = 0f;
    }

    void SpawnRing(Vector3 at) => ShowNoise(at, noiseRadius);

    /// <summary>
    /// The red ring on the minimap that shows how far a noise carries. The coin uses it, and so does
    /// running (PlayerController), so the player can see that running is loud.
    /// </summary>
    public void ShowNoise(Vector3 at, float radius)
    {
        if (ringSprite == null) return;
        var go = new GameObject("Noise Ring");
        // -1 means the build could not create the Minimap layer; Unity throws on an out-of-range layer.
        if (minimapLayer >= 0) go.layer = minimapLayer;
        go.transform.position = at;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ringSprite;
        renderer.color = ringColor;
        renderer.sortingOrder = 90;
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
        var ring = go.AddComponent<NoiseRing>();
        ring.radius = radius;
        ring.seconds = ringSeconds;
    }

    bool ThrowPressed()
    {
        if (attack != null) return attack.WasPressedThisFrame();

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.enterKey.wasPressedThisFrame) return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
    }
}
