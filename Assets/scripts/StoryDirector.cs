using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The story, run without words. It opens with four pixel frames (the dying world, the portal, the
/// theft, the jewel dropped in the school), then gates the game behind clues: every clue you solve
/// crosses a wing off the minimap, and only when the building is eliminated down to one wing does
/// the jewel become findable. Taking it makes you loud, reveals the portal activator, and fills the
/// first of the way out's two sockets. Filling the second opens the way home; walking out plays the
/// ending. Nothing here draws a letter: the ten words are already spent.
/// </summary>
public class StoryDirector : MonoBehaviour, IRunResettable
{
    /// <summary>Where the player is in the story. Only ever moves forward within a run.</summary>
    public enum Beat { Opening, Clues, Search, Carrying, Escaping }

    [Header("Who")]
    public GameRun run;
    public Objective objective;
    public StoryCards cards;
    public PixelCutscene cutscene;
    public Transform player;
    public GameAudio audioBank;
    public PlayerCameraEffects effects;

    [Header("The jewel's body, hidden until the search narrows to one wing")]
    public SpriteRenderer jewelBody;

    [Header("Clues")]
    public Clue[] clues = new Clue[0];

    [Tooltip("Seconds standing on a clue before it is solved.")]
    public float solveSeconds = 2.2f;

    [Tooltip("How close you must be to work on a clue.")]
    public float clueRadius = 1.8f;

    [Tooltip("A solved clue is loud: demons within this range come to look.")]
    public float clueNoiseRadius = 14f;

    [Header("The map marks")]
    [Tooltip("One grey cross per zone, switched on as that wing is eliminated. Indexed by zone.")]
    public GameObject[] zoneCrosses = new GameObject[0];

    [Tooltip("The red box around the one wing left standing.")]
    public GameObject searchBox;

    [Tooltip("One mark per clue along the bottom of the minimap.")]
    public SpriteRenderer[] pips = new SpriteRenderer[0];

    [Header("The portal activator")]
    public Transform activator;
    public SpriteRenderer activatorBody;
    public SpriteRenderer activatorMarker;
    public float activatorRadius = 1.2f;

    [Header("The way out's two sockets")]
    public SpriteRenderer jewelSocket;
    public SpriteRenderer activatorSocket;

    [Header("How close you must get before the jewel shows itself")]
    public float searchRevealRadius = 7f;

    [Header("Carrying it makes you loud")]
    [Tooltip("Chase speed multiplier once the jewel is yours.")]
    public float carryChaseSpeed = 1.22f;

    [Tooltip("How much quicker a demon commits to a chase once the jewel is yours.")]
    public float carryDetectTime = 0.6f;

    [Tooltip("Extra sight range, in units, once the jewel is yours.")]
    public float carrySightBonus = 3f;

    [Tooltip("The heartbeat reaches further, so it runs fast more of the time.")]
    public float carryHeartDistance = 1.5f;

    [Tooltip("Added to the vignette, so the dark closes in around the edges.")]
    public float carryVignette = 0.08f;

    [Header("Look")]
    public Color armedColor = new Color32(237, 28, 36, 255);
    public Color dormantColor = new Color32(70, 70, 70, 255);

    [Header("Cutscene timing")]
    public float[] openingHolds = { 3.2f, 2.8f, 4.2f, 3.6f };
    public float[] endingHolds = { 3f, 4.6f };

    /// <summary>Where the story has got to in this run.</summary>
    public Beat Current { get; private set; }

    /// <summary>True while a cutscene is on screen.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>True once the portal activator has been collected.</summary>
    public bool HasActivator { get; private set; }

    static readonly PixelFrames.Frame[] OpeningFrames =
    {
        PixelFrames.Frame.WorldGoingOut,
        PixelFrames.Frame.WayDown,
        PixelFrames.Frame.Theft,
        PixelFrames.Frame.FallsInSchool,
    };

    static readonly PixelFrames.Frame[] EndingFrames =
    {
        PixelFrames.Frame.BurnsOpen,
        PixelFrames.Frame.Filled,
    };

    /// <summary>The frame the opening card lands on: the jewel leaving his hands.</summary>
    const int TheftFrame = 2;

    class Escalation
    {
        public NpcChaser chaser;
        public float chaseSpeed;
        public float detectTime;
        public float sight;
    }

    readonly List<Escalation> demons = new List<Escalation>();
    Coroutine playing;
    GameRun subscribedTo;
    float heartDistance;
    float vignette;
    bool escalated;
    bool playedOpening;
    bool restartClean;
    int solved;

    void Awake()
    {
        if (run == null) run = FindAnyObjectByType<GameRun>();
        if (objective == null) objective = FindAnyObjectByType<Objective>();
        if (player == null && objective != null) player = objective.player;

        foreach (NpcChaser chaser in FindObjectsByType<NpcChaser>(FindObjectsInactive.Include))
        {
            // Include the inactive ones: the difficulty menu can switch a demon on after this runs.
            demons.Add(new Escalation
            {
                chaser = chaser,
                chaseSpeed = chaser.chaseSpeed,
                detectTime = chaser.detectTime,
                sight = chaser.vision != null ? chaser.vision.viewDistance : 0f,
            });
        }

        if (audioBank != null) heartDistance = audioBank.farDistance;
        if (effects != null) vignette = effects.baseIntensity;
    }

    void OnEnable()
    {
        Subscribe();
    }

    void Start()
    {
        Subscribe();
        Restore();
    }

    void OnDisable()
    {
        Unsubscribe();
        // Never leave the run frozen behind a cutscene that is no longer on screen.
        if (run != null) run.StoryPlaying = false;
        IsPlaying = false;
        playing = null;
    }

    void Subscribe()
    {
        GameRun found = GameRun.Instance != null ? GameRun.Instance : run;
        if (found == null || found == subscribedTo) return;
        Unsubscribe();
        subscribedTo = found;
        subscribedTo.Started += OnStarted;
        if (objective != null) objective.PickedUp += OnJewelTaken;

        // Script execution order is not guaranteed; if the run already began, catch up now.
        if (subscribedTo.HasBegun) OnStarted();
    }

    void Unsubscribe()
    {
        if (subscribedTo == null) return;
        subscribedTo.Started -= OnStarted;
        if (objective != null) objective.PickedUp -= OnJewelTaken;
        subscribedTo = null;
    }

    // ------------------------------------------------------------------ the opening

    void OnStarted()
    {
        if (playedOpening) return;
        playedOpening = true;
        if (cutscene == null || run == null) { Restore(); return; }
        playing = StartCoroutine(OpeningRoutine());
    }

    IEnumerator OpeningRoutine()
    {
        IsPlaying = true;
        // Freezes the clock, the demons and the player without touching Time.timeScale, so the
        // word card underneath still counts down on unscaled time.
        run.StoryPlaying = true;

        yield return cutscene.Play(OpeningFrames, openingHolds, frame =>
        {
            // "YOU BORROWED THE LIGHT" lands on the theft, over the picture of it happening.
            if (frame == TheftFrame && cards != null) cards.ShowOpeningCard();
        });

        // Let the card finish rather than snapping to gameplay mid-word.
        while (cards != null && cards.IsShowing) yield return null;

        run.StoryPlaying = false;
        IsPlaying = false;
        playing = null;
        Enter(Beat.Clues);
    }

    // ------------------------------------------------------------------ the ending

    /// <summary>
    /// Called by GameRun when the jewel is carried out. Returns true when it took the moment, so
    /// the won icon stands down and the run waits for the frames instead.
    /// </summary>
    public bool PlayEnding()
    {
        if (cutscene == null || IsPlaying) return false;
        restartClean = true; // the story is over; the next run starts from nothing
        playing = StartCoroutine(EndingRoutine());
        return true;
    }

    IEnumerator EndingRoutine()
    {
        IsPlaying = true;
        yield return cutscene.Play(EndingFrames, endingHolds, null);
        IsPlaying = false;
        playing = null;
    }

    // ------------------------------------------------------------------ the game

    void Update()
    {
        if (run == null || !run.IsRunning || player == null) return;

        switch (Current)
        {
            case Beat.Clues: UpdateClues(); break;
            case Beat.Search: UpdateSearch(); break;
            case Beat.Carrying: UpdateActivator(); break;
        }
    }

    void UpdateClues()
    {
        Clue working = null;
        float nearest = float.MaxValue;

        foreach (Clue clue in clues)
        {
            if (clue == null || clue.Solved) continue;
            float distance = Vector2.Distance(player.position, clue.transform.position);
            if (distance > clueRadius || distance >= nearest) continue;
            nearest = distance;
            working = clue;
        }

        // Only the one you are standing on makes progress; stepping off gives that progress back.
        foreach (Clue clue in clues)
        {
            if (clue == null || clue.Solved || clue == working) continue;
            clue.Progress = Mathf.MoveTowards(clue.Progress, 0f, Time.deltaTime / Mathf.Max(0.01f, solveSeconds));
        }

        if (working == null) return;
        working.Progress += Time.deltaTime / Mathf.Max(0.01f, solveSeconds);
        if (working.Progress >= 1f) Solve(working);
    }

    void Solve(Clue clue)
    {
        clue.SetSolved(true);
        solved++;
        Cross(clue.zone);
        PaintPips();
        if (audioBank != null) audioBank.PlayClue();

        // Working a mark loose is not quiet. Whatever is nearby comes to look.
        Decoy.Emit(clue.transform.position, clueNoiseRadius);

        if (AllSolved()) Enter(Beat.Search);
    }

    void UpdateSearch()
    {
        // The map only ever showed you a wing. You still have to walk it until the jewel shows up.
        if (jewelBody == null || objective == null || objective.jewel == null) return;
        float distance = Vector2.Distance(player.position, objective.jewel.position);
        jewelBody.enabled = distance <= searchRevealRadius;
    }

    void OnJewelTaken()
    {
        Enter(Beat.Carrying);
    }

    void UpdateActivator()
    {
        if (activator == null || HasActivator) return;
        if (Vector2.Distance(player.position, activator.position) > activatorRadius) return;
        TakeActivator();
    }

    void TakeActivator()
    {
        HasActivator = true;
        if (activatorBody != null) activatorBody.enabled = false;
        if (activatorMarker != null) activatorMarker.enabled = false;
        if (audioBank != null) audioBank.PlayUnlock();
        Enter(Beat.Escaping);
    }

    // ------------------------------------------------------------------ state

    void Enter(Beat beat)
    {
        Current = beat;
        Apply();
    }

    /// <summary>Everything the current beat implies, in one place, so a restart can just re-run it.</summary>
    void Apply()
    {
        bool searching = Current == Beat.Search;
        bool carrying = Current == Beat.Carrying || Current == Beat.Escaping;

        if (objective != null) objective.SetLocks(!searching && !carrying, Current != Beat.Escaping);

        if (jewelBody != null) jewelBody.enabled = carrying;
        if (searchBox != null) searchBox.SetActive(searching);

        bool showActivator = Current == Beat.Carrying;
        if (activatorBody != null) activatorBody.enabled = showActivator;
        if (activatorMarker != null) activatorMarker.enabled = showActivator;

        if (jewelSocket != null) jewelSocket.color = carrying ? armedColor : dormantColor;
        if (activatorSocket != null) activatorSocket.color = HasActivator ? armedColor : dormantColor;

        Escalate(carrying);
    }

    /// <summary>Carrying it turns every demon toward you and tightens the screen.</summary>
    void Escalate(bool on)
    {
        if (on == escalated) return;
        escalated = on;

        foreach (Escalation demon in demons)
        {
            if (demon.chaser == null) continue;
            demon.chaser.chaseSpeed = on ? demon.chaseSpeed * carryChaseSpeed : demon.chaseSpeed;
            demon.chaser.detectTime = on ? demon.detectTime * carryDetectTime : demon.detectTime;
            if (demon.chaser.vision != null)
                demon.chaser.vision.viewDistance = on ? demon.sight + carrySightBonus : demon.sight;

            // They know it is gone, and roughly from where.
            if (on && demon.chaser.isActiveAndEnabled && player != null)
                demon.chaser.Investigate(player.position);
        }

        if (audioBank != null) audioBank.farDistance = on ? heartDistance * carryHeartDistance : heartDistance;
        if (effects != null) effects.baseIntensity = on ? vignette + carryVignette : vignette;
    }

    bool AllSolved()
    {
        foreach (Clue clue in clues)
        {
            if (clue != null && !clue.Solved) return false;
        }
        return true;
    }

    void Cross(int zone)
    {
        if (zoneCrosses == null || zone < 0 || zone >= zoneCrosses.Length) return;
        if (zoneCrosses[zone] != null) zoneCrosses[zone].SetActive(true);
    }

    void PaintPips()
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] != null) pips[i].color = i < solved ? armedColor : dormantColor;
        }
    }

    /// <summary>Puts the world back where the current progress says it should be.</summary>
    void Restore()
    {
        solved = 0;
        foreach (Clue clue in clues)
        {
            if (clue == null) continue;
            clue.Progress = 0f;
            if (clue.Solved) { solved++; Cross(clue.zone); }
        }
        PaintPips();
        if (Current != Beat.Opening || playedOpening) Current = AllSolved() ? Beat.Search : Beat.Clues;
        Apply();
    }

    /// <summary>
    /// A death costs you the walk back, not the search: solved clues stay solved. Winning ends the
    /// story, so the next run starts the building over from nothing.
    /// </summary>
    public void ResetRun()
    {
        HasActivator = false;

        if (restartClean)
        {
            restartClean = false;
            foreach (Clue clue in clues)
            {
                if (clue != null) clue.SetSolved(false);
            }
            foreach (GameObject cross in zoneCrosses)
            {
                if (cross != null) cross.SetActive(false);
            }
        }

        Restore();
    }
}
