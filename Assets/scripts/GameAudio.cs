using UnityEngine;

/// <summary>
/// The game's sound. Two real recordings carry the atmosphere (a loopable horror ambience and a
/// sting for being spotted); everything else is generated as samples at runtime so no extra files
/// are needed. The heartbeat is driven by how close the nearest demon is, whether or not you can
/// see it, so the room tells you something the screen does not.
/// </summary>
public class GameAudio : MonoBehaviour
{
    [Header("Recordings")]
    public AudioClip ambience;
    public AudioClip spotted;

    [Header("Mix")]
    [Range(0f, 1f)] public float ambienceVolume = 0.15f;
    [Range(0f, 1f)] public float spottedVolume = 0.7f;
    [Range(0f, 1f)] public float effectsVolume = 0.6f;
    [Range(0f, 1f)] public float heartVolume = 1f;

    [Header("Heartbeat")]
    public Transform player;
    [Tooltip("Distance at which the heartbeat is silent.")]
    public float farDistance = 30f;
    [Tooltip("Distance at which it is loudest and fastest.")]
    public float nearDistance = 4f;
    public float slowBeatSeconds = 1f;
    public float fastBeatSeconds = 0.3f;
    [Tooltip("How far the ambience ducks when a demon is on top of you, so the heart takes over.")]
    [Range(0f, 1f)] public float ambienceDuck = 0.6f;

    [Header("Footsteps")]
    public float stepSeconds = 0.34f;
    [Range(0f, 1f)] public float stepVolume = 0.22f;

    AudioSource ambienceSource;
    AudioSource effectsSource;
    AudioClip heartbeat, footstep, pickup, win, caught, clue, unlock;
    NpcChaser[] enemies;
    Rigidbody2D playerBody;
    PlayerHider hider;
    GameRun subscribedTo;
    float nextBeat;

    /// <summary>0 with no demon in range, 1 with one at nearDistance. Updated every frame.</summary>
    public static float DemonCloseness { get; private set; }

    /// <summary>Raised on every heartbeat with how close the nearest demon is, so visuals beat in time.</summary>
    public static event System.Action<float> HeartBeat;
    float nextStep;

    void Awake()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.loop = true;
        ambienceSource.playOnAwake = false;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.volume = ambienceVolume;

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;
        effectsSource.spatialBlend = 0f;

        heartbeat = ProceduralTones.HeartBeat("Sfx_Heart", 0.9f);
        footstep = ProceduralTones.Click("Sfx_Step", 900f, 0.07f, 0.5f);
        pickup = ProceduralTones.Arpeggio("Sfx_Pickup", new[] { 659.25f, 987.77f }, 0.08f, 0.3f);
        win = ProceduralTones.Arpeggio("Sfx_Win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.13f, 0.3f);
        caught = ProceduralTones.Thump("Sfx_Caught", 96f, 0.85f, 0.6f);
        clue = ProceduralTones.Arpeggio("Sfx_Clue", new[] { 392f, 523.25f }, 0.07f, 0.26f);
        unlock = ProceduralTones.Arpeggio("Sfx_Unlock", new[] { 440f, 587.33f, 880f }, 0.09f, 0.3f);
    }

    void Start()
    {
        enemies = FindObjectsByType<NpcChaser>(FindObjectsInactive.Include);
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody2D>();
            hider = player.GetComponent<PlayerHider>();
        }
        if (ambience != null)
        {
            ambienceSource.clip = ambience;
            ambienceSource.Play();
        }
        Subscribe();
    }

    void OnDisable()
    {
        DemonCloseness = 0f;
        if (subscribedTo == null) return;
        subscribedTo.Spotted -= OnSpotted;
        subscribedTo.Caught -= OnCaught;
        subscribedTo.Won -= OnWon;
        subscribedTo = null;
    }

    void Subscribe()
    {
        GameRun run = GameRun.Instance;
        if (run == null || run == subscribedTo) return;
        if (subscribedTo != null)
        {
            subscribedTo.Spotted -= OnSpotted;
            subscribedTo.Caught -= OnCaught;
            subscribedTo.Won -= OnWon;
        }
        subscribedTo = run;
        run.Spotted += OnSpotted;
        run.Caught += OnCaught;
        run.Won += OnWon;
    }

    void Update()
    {
        Subscribe();

        GameRun run = GameRun.Instance;
        bool playing = run == null || run.IsRunning;
        DemonCloseness = playing ? Closeness() : 0f;
        float duck = Mathf.Lerp(1f, 1f - ambienceDuck, DemonCloseness * DemonCloseness);
        ambienceSource.volume = playing ? ambienceVolume * duck : ambienceVolume * 0.3f;
        if (!playing) return;

        Heartbeat();
        Footsteps();
    }

    void Heartbeat()
    {
        float closeness = DemonCloseness;
        if (closeness <= 0.01f) return;

        if (Time.unscaledTime < nextBeat) return;
        // The pace climbs faster than the distance closes, so the last few metres are the worst.
        float urgency = Mathf.Sqrt(closeness);
        nextBeat = Time.unscaledTime + Mathf.Lerp(slowBeatSeconds, fastBeatSeconds, urgency);
        effectsSource.PlayOneShot(heartbeat, heartVolume * Mathf.Lerp(0.35f, 1f, urgency));
        HeartBeat?.Invoke(closeness);
    }
    /// <summary>
    /// Footstep ticks while the player moves, quicker while sprinting. Sound only: demons do not
    /// hear them.
    /// </summary>
    void Footsteps()
    {
        if (playerBody == null || (hider != null && hider.IsHidden)) return;
        if (playerBody.linearVelocity.sqrMagnitude < 1f) return;
        if (Time.time < nextStep) return;

        // Faster feet while sprinting, so boosting sounds like the risk it is.
        float speed = playerBody.linearVelocity.magnitude;
        nextStep = Time.time + stepSeconds * Mathf.Clamp(8f / Mathf.Max(1f, speed), 0.55f, 1.4f);
        effectsSource.PlayOneShot(footstep, stepVolume);
    }

    /// <summary>1 when the nearest active demon is at nearDistance or closer, 0 at farDistance.</summary>
    float Closeness()
    {
        if (player == null || enemies == null) return 0f;
        float nearest = float.MaxValue;
        foreach (NpcChaser enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            nearest = Mathf.Min(nearest, Vector2.Distance(enemy.transform.position, player.position));
        }
        if (nearest == float.MaxValue) return 0f;
        return 1f - Mathf.InverseLerp(nearDistance, farDistance, nearest);
    }

    void OnSpotted()
    {
        if (spotted != null) effectsSource.PlayOneShot(spotted, spottedVolume);
    }

    void OnCaught()
    {
        effectsSource.PlayOneShot(caught, effectsVolume);
    }

    void OnWon()
    {
        effectsSource.PlayOneShot(win, effectsVolume);
    }

    /// <summary>Called when the jewel is taken.</summary>
    public void PlayPickup()
    {
        effectsSource.PlayOneShot(pickup, effectsVolume);
    }

    /// <summary>Called when a clue is worked loose and a wing is crossed off the map.</summary>
    public void PlayClue()
    {
        effectsSource.PlayOneShot(clue, effectsVolume);
    }

    /// <summary>Called when the portal activator is collected and the second socket fills.</summary>
    public void PlayUnlock()
    {
        effectsSource.PlayOneShot(unlock, effectsVolume);
    }

    // Enter Play Mode keeps the loaded domain (Reload Domain is off in this project), so statics
    // keep whatever the last play session left in them. This wipes them at the start of each one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        HeartBeat = null;
        DemonCloseness = 0f;
    }
}
