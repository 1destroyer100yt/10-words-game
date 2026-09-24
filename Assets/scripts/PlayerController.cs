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

    [Tooltip("Running is loud: demons within this many units come to look where you are. 0 = silent.")]
    public float sprintNoiseRadius = 7f;

    [Tooltip("Seconds between those noises while running.")]
    public float sprintNoiseEvery = 0.5f;

    float nextSprintNoise;
    DecoyThrower noiseShower; // draws the same ring on the minimap as the coin

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
    static readonly int SprintParameter = Animator.StringToHash("Sprint");
    static readonly int CaughtParameter = Animator.StringToHash("Caught");

    // Sprint and Caught exist only when their strips were built into the animator; setting a
    // parameter an animator lacks logs a warning every frame, so look once.
    RuntimeAnimatorController checkedController;
    bool hasSprint, hasCaught;
    GameRun subscribedTo;

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
        // Only our own actions. PlayerHider and DecoyThrower enable theirs, and switching the whole
        // asset off here would switch theirs off too.
        moveAction?.Enable();
        sprintAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        sprintAction?.Disable();
        if (subscribedTo != null) subscribedTo.Caught -= OnCaught;
        subscribedTo = null;
    }

    void Subscribe()
    {
        GameRun run = GameRun.Instance;
        if (run == null || run == subscribedTo) return;
        if (subscribedTo != null) subscribedTo.Caught -= OnCaught;
        subscribedTo = run;
        run.Caught += OnCaught;
    }

    /// <summary>
    /// The death plays on real time: the caught sequence slows the game to a crawl and then freezes
    /// it, and on game time the whole animation would be one frame.
    /// </summary>
    void OnCaught()
    {
        if (animator == null || !CheckParameters()) return;
        if (!hasCaught) return;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.SetBool(CaughtParameter, true);
    }

    /// <summary>True when there is a controller to talk to; refreshes the parameter check if it changed.</summary>
    bool CheckParameters()
    {
        RuntimeAnimatorController current = animator.runtimeAnimatorController;
        if (current == null) return false;
        if (current == checkedController) return true;
        checkedController = current;
        hasSprint = hasCaught = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == SprintParameter) hasSprint = true;
            if (parameter.nameHash == CaughtParameter) hasCaught = true;
        }
        return true;
    }

    void Update()
    {
        Subscribe();
        GameRun run = GameRun.Instance;
        bool active = movementEnabled && (run == null || run.AcceptsGameplayInput);
        moveInput = active ? ReadMove() : Vector2.zero;
        sprinting = active && ReadSprint();
        bool moving = moveInput.sqrMagnitude > 0.0001f;

        // The price of speed: every few steps, anything close enough hears you and comes to look.
        if (moving && sprinting && sprintNoiseRadius > 0f && Time.time >= nextSprintNoise)
        {
            nextSprintNoise = Time.time + sprintNoiseEvery;
            Decoy.Emit(transform.position, sprintNoiseRadius);
            if (noiseShower == null) noiseShower = GetComponent<DecoyThrower>();
            if (noiseShower != null) noiseShower.ShowNoise(transform.position, sprintNoiseRadius);
        }

        if (moving && visual != null)
        {
            float target = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(visual.eulerAngles.z, target, turnSpeed * Time.deltaTime);
            visual.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (animator != null && CheckParameters())
        {
            animator.SetBool(BoostParameter, moving);
            if (hasSprint) animator.SetBool(SprintParameter, moving && sprinting);
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
        nextSprintNoise = 0f;
        movementEnabled = true;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = spawnPosition;
        }
        transform.position = spawnPosition;
        if (visual != null) visual.rotation = spawnFacing;

        // Back from the death pose to a hovering player on game time.
        if (animator != null && CheckParameters())
        {
            if (hasCaught) animator.SetBool(CaughtParameter, false);
            animator.updateMode = AnimatorUpdateMode.Normal;
        }
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
