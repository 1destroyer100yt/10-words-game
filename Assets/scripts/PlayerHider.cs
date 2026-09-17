using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Interact (E, or the gamepad's north button) next to a HidingSpot puts the player inside it:
/// invisible to enemy vision, unable to move, sprite hidden. Interact again steps back out.
/// An enemy that watched the player go in still finds them (see NpcChaser).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerHider : MonoBehaviour, IRunResettable
{
    [Tooltip("Optional. Uses the Player/Interact action. Empty = keyboard E and gamepad north.")]
    public InputActionAsset actions;

    [Tooltip("How close to a hiding spot the player must be to use it.")]
    public float useRadius = 1.3f;

    [Tooltip("Objects turned off while hidden, such as the player's visual.")]
    public GameObject[] hiddenWhileInside;

    public bool IsHidden { get; private set; }
    public HidingSpot Spot { get; private set; }
    public Vector3 SpotPosition => Spot != null ? Spot.transform.position : transform.position;

    PlayerController controller;
    Rigidbody2D body;
    InputAction interact;
    Vector3 stepOutPosition;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (actions != null) interact = actions.FindAction("Player/Interact");
        // Enabling our own action is idempotent; without it hiding works only while a
        // PlayerController happens to be enabled on the same object and enables the whole asset.
        if (interact != null) interact.Enable();
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        if (run != null && !run.AcceptsGameplayInput) return;
        if (!InteractPressed()) return;

        if (IsHidden)
        {
            Unhide(true);
            return;
        }

        HidingSpot spot = HidingSpot.Nearest(transform.position, useRadius);
        if (spot != null && !spot.Occupied) Hide(spot);
    }

    void Hide(HidingSpot spot)
    {
        IsHidden = true;
        Spot = spot;
        spot.SetOccupied(true);
        stepOutPosition = transform.position;
        controller.movementEnabled = false;
        MoveTo(spot.transform.position);
        SetVisible(false);
    }

    /// <summary>Steps out. With restorePosition the player returns to where they stood before hiding.</summary>
    public void Unhide(bool restorePosition)
    {
        if (!IsHidden) return;
        IsHidden = false;
        if (Spot != null) Spot.SetOccupied(false);
        Spot = null;
        if (restorePosition) MoveTo(stepOutPosition);
        controller.movementEnabled = true;
        SetVisible(true);
    }

    public void ResetRun()
    {
        Unhide(false); // PlayerController.ResetRun moves the player back to the spawn
    }

    void MoveTo(Vector3 position)
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.position = position;
        }
        transform.position = position;
    }

    void SetVisible(bool visible)
    {
        if (hiddenWhileInside == null) return;
        foreach (GameObject go in hiddenWhileInside)
        {
            if (go != null) go.SetActive(visible);
        }
    }

    bool InteractPressed()
    {
        if (interact != null) return interact.WasPressedThisFrame();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame) return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonNorth.wasPressedThisFrame;
    }
}
