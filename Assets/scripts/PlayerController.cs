using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down player movement using the Input System. Move with WASD, the arrow keys or a gamepad
/// stick; hold Sprint (Shift) to go faster. The visual child turns to face the movement direction
/// and the Animator's "Boost" bool is true while moving, so the boost animation plays when travelling.
/// Input is ignored while movement is disabled (hiding) or the run is not going (caught).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, IRunResettable
{
    public float moveSpeed = 8f;
    public float sprintMultiplier = 1.6f;

    [Tooltip("Degrees per second the visual turns toward the movement direction.")]
    public float turnSpeed = 720f;

    [Tooltip("Optional. Uses the Player/Move and Player/Sprint actions. Empty = keyboard and gamepad directly.")]
    public InputActionAsset actions;

    [Tooltip("Child carrying the sprite; it is rotated to face the movement direction (sprite faces +Y).")]
    public Transform visual;

    public Animator animator;

    [Tooltip("Cleared while hiding: the body stops and input is ignored.")]
    public bool movementEnabled = true;

    /// <summary>Unit vector the visual faces; +Y until the player first moves.</summary>
    public Vector2 Facing => visual != null ? (Vector2)visual.up : Vector2.up;

    static readonly int BoostParameter = Animator.StringToHash("Boost");

    Rigidbody2D body;
    InputAction moveAction;
    InputAction sprintAction;
    Vector2 moveInput;
    bool sprinting;
    Vector3 spawnPosition;
    Quaternion spawnFacing;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        spawnPosition = transform.position;
        spawnFacing = visual != null ? visual.rotation : Quaternion.identity;
    }

    void OnEnable()
    {
        if (actions == null) return;
        moveAction = actions.FindAction("Player/Move");
        sprintAction = actions.FindAction("Player/Sprint");
        actions.Enable();
    }

    void OnDisable()
    {
        if (actions != null) actions.Disable();
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        bool active = movementEnabled && (run == null || run.AcceptsGameplayInput);
        moveInput = active ? ReadMove() : Vector2.zero;
        sprinting = active && ReadSprint();
        bool moving = moveInput.sqrMagnitude > 0.0001f;

        if (moving && visual != null)
        {
            float target = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(visual.eulerAngles.z, target, turnSpeed * Time.deltaTime);
            visual.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(BoostParameter, moving);
        }
    }

    void FixedUpdate()
    {
        float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f);
        body.linearVelocity = moveInput * speed;
    }

    /// <summary>Back to the spawn point, standing still and facing the way it started.</summary>
    public void ResetRun()
    {
        moveInput = Vector2.zero;
        sprinting = false;
        movementEnabled = true;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = spawnPosition;
        }
        transform.position = spawnPosition;
        if (visual != null) visual.rotation = spawnFacing;
    }

    Vector2 ReadMove()
    {
        if (moveAction != null) return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        Vector2 value = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && value == Vector2.zero) value = gamepad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(value, 1f);
    }

    bool ReadSprint()
    {
        if (sprintAction != null) return sprintAction.IsPressed();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.leftStickButton.isPressed;
    }
}
