using UnityEngine;

/// <summary>
/// The goal, told without a single word. A red jewel sits in a far room; walk onto it to take it,
/// and the way out starts pulsing. Get back there carrying it and the run is won. The exit is dim
/// and inert until you actually have something worth carrying out.
/// </summary>
public class Objective : MonoBehaviour, IRunResettable
{
    [Header("Who")]
    public Transform player;
    public GameAudio audioBank;

    [Header("Gates (the story director owns these)")]
    [Tooltip("While true the jewel cannot be taken and stays off the map: the clues are not done.")]
    public bool locked;

    [Tooltip("While true the way out stays dead even with the jewel: the portal activator is missing.")]
    public bool exitLocked;

    [Tooltip("Off for the story, where the map shows only a wing and you have to search it yourself.")]
    public bool markJewel = true;

    /// <summary>Raised the moment the jewel is taken.</summary>
    public event System.Action PickedUp;

    [Header("The jewel")]
    public Transform jewel;
    public SpriteRenderer jewelMarker;
    public float pickupRadius = 1f;

    [Header("The way out")]
    public Transform exit;
    public SpriteRenderer exitSprite;
    public SpriteRenderer exitMarker;
    public float extractRadius = 1.4f;

    [Header("Look")]
    public Color armedColor = new Color32(237, 28, 36, 255);
    public Color dormantColor = new Color32(70, 70, 70, 255);
    public float pulseSeconds = 1.1f;

    /// <summary>True once the jewel has been taken and not yet carried out.</summary>
    public bool Carrying { get; private set; }

    Vector3 jewelHome;
    Vector3 exitScale = Vector3.one;
    Vector3 carryOffset = new Vector3(0f, 0.75f, 0f);

    void Awake()
    {
        if (jewel != null) jewelHome = jewel.position;
        if (exitSprite != null) exitScale = exitSprite.transform.localScale;
    }

    void Start()
    {
        Apply();
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        if (run == null || !run.IsRunning || player == null) return;

        if (!Carrying)
        {
            if (!locked && !PlayerHidden() && jewel != null && Vector2.Distance(player.position, jewel.position) <= pickupRadius) Take();
        }
        else
        {
            // Carried just above the player, so you can see you have it.
            if (jewel != null) jewel.position = player.position + carryOffset;
            if (!exitLocked && !PlayerHidden() && exit != null && Vector2.Distance(player.position, exit.position) <= extractRadius)
            {
                run.WinRun();
                return;
            }
        }

        Pulse();
    }

    PlayerHider hider;
    bool hiderResolved;

    /// <summary>
    /// True while the player is inside a hiding spot. Hiding moves the player onto the spot, so
    /// without this anything within reach of a locker could be used from total safety.
    /// </summary>
    bool PlayerHidden()
    {
        if (!hiderResolved && player != null)
        {
            hider = player.GetComponentInParent<PlayerHider>();
            hiderResolved = true;
        }
        return hider != null && hider.IsHidden;
    }

    void Take()
    {
        Carrying = true;
        if (audioBank != null) audioBank.PlayPickup();
        Apply();
        PickedUp?.Invoke();
    }

    /// <summary>
    /// Moves where the jewel lies and where every reset puts it back. The story director calls this
    /// when it deals a new layout; a jewel being carried stays in the player's hands until the reset.
    /// </summary>
    public void SetJewelHome(Vector3 at)
    {
        jewelHome = at;
        if (!Carrying && jewel != null) jewel.position = at;
    }

    /// <summary>Sets both gates and redraws. The story director calls this as the beats change.</summary>
    public void SetLocks(bool jewelLocked, bool wayOutLocked)
    {
        locked = jewelLocked;
        exitLocked = wayOutLocked;
        Apply();
    }

    /// <summary>The exit only reads as live once you are carrying something.</summary>
    void Apply()
    {
        if (jewelMarker != null) jewelMarker.enabled = !Carrying && !locked && markJewel;
        // Carrying it is not enough while a socket is still empty: the way out stays dead grey.
        bool live = Carrying && !exitLocked;
        if (exitSprite != null) exitSprite.color = live ? armedColor : dormantColor;
        if (exitMarker != null)
        {
            exitMarker.enabled = Carrying;
            exitMarker.color = armedColor;
        }
    }

    void Pulse()
    {
        if (exitSprite == null) return;
        if (!Carrying || exitLocked)
        {
            exitSprite.transform.localScale = exitScale;
            return;
        }
        float t = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.01f, pulseSeconds)) * 0.5f + 0.5f;
        exitSprite.transform.localScale = exitScale * Mathf.Lerp(0.85f, 1.2f, t);
    }

    public void ResetRun()
    {
        Carrying = false;
        if (jewel != null) jewel.position = jewelHome;
        if (exitSprite != null) exitSprite.transform.localScale = exitScale;
        Apply();
    }
}
