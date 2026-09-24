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

    [Tooltip("An unsolved clue's mark under the map. Black, because the map panel is the same grey as dormantColor and a grey mark there is invisible.")]
    public Color pipEmptyColor = Color.black;

    [Header("The portal activator")]
    public Transform activator;
    public SpriteRenderer activatorBody;
    public SpriteRenderer activatorMarker;
    public float activatorRadius = 1.2f;

    [Header("The way out's two sockets")]
    public SpriteRenderer jewelSocket;
    public SpriteRenderer activatorSocket;

    [Header("Finding the clues")]
    [Tooltip("A clue's minimap mark appears once the player has been this close with nothing in between.")]
    public float clueRevealRadius = 12f;

    [Tooltip("Walls, for the line of sight above. Empty = the 'Walls' layer.")]
    public LayerMask wallMask;

    [Header("A new building every time")]
    [Tooltip("On every fresh start (first run, after a win, a new difficulty) the jewel moves to a random " +
             "wing other than the one you start in, and the clues and the key are dealt around it. A death " +
             "keeps the layout.")]
    public bool shuffleLayout = true;

    [Tooltip("The key goes to a random wing at least this share as far (from the start and the jewel) as the furthest one.")]
    [Range(0f, 1f)] public float keyDistanceShare = 0.7f;

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

    /// <summary>True once any clue is solved. The menu locks the difficulty from then on, so a stray key can't wipe the search.</summary>
    public bool HasProgress => solved > 0;

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
        if (wallMask == 0) wallMask = LayerMask.GetMask("Walls");
        ReadWings();
    }

    // ------------------------------------------------------------------ the layout

    /// <summary>Per wing, indexed like zoneCrosses: where its middle is on the map and a walkable spot in it.</summary>
    Vector3[] wingCentre;
    Vector3[] wingSpot;
    bool[] wingKnown;
    int builtJewelWing = -1;
    int lastJewelWing = -1;
    Vector3 playerSpawn;
    bool spawnKnown;

    /// <summary>
    /// The builder placed a clue in five wings and the jewel in the sixth, and drew a cross over the
    /// middle of every wing but the jewel's, where the search box sits instead. That is everything a
    /// new layout needs, so it is read back from the scene rather than rebuilt.
    /// </summary>
    void ReadWings()
    {
        int count = zoneCrosses != null ? zoneCrosses.Length : 0;
        wingCentre = new Vector3[count];
        wingSpot = new Vector3[count];
        wingKnown = new bool[count];
        if (count == 0) return;

        var claimed = new bool[count];
        foreach (Clue clue in clues)
        {
            if (clue == null || clue.zone < 0 || clue.zone >= count) continue;
            wingSpot[clue.zone] = clue.transform.position;
            wingKnown[clue.zone] = true;
            claimed[clue.zone] = true;
        }
        for (int i = 0; i < count; i++)
        {
            if (zoneCrosses[i] != null) wingCentre[i] = zoneCrosses[i].transform.position;
            else if (!claimed[i] && builtJewelWing < 0) builtJewelWing = i;
        }
        if (builtJewelWing < 0 || objective == null || objective.jewel == null || searchBox == null) return;

        wingCentre[builtJewelWing] = searchBox.transform.position;
        wingSpot[builtJewelWing] = objective.jewel.position;
        wingKnown[builtJewelWing] = true;

        // That wing never needed a cross. Now any wing can be ruled out, so give it one: every wing
        // is the same size, so a copy of another wing's cross fits exactly.
        foreach (GameObject cross in zoneCrosses)
        {
            if (cross == null) continue;
            GameObject copy = Instantiate(cross, cross.transform.parent);
            copy.name = $"Crossed {builtJewelWing}";
            copy.transform.position = wingCentre[builtJewelWing];
            copy.SetActive(false);
            zoneCrosses[builtJewelWing] = copy;
            break;
        }
    }

    /// <summary>Deals a new building: the jewel into one wing, a clue into each of the others, the key far from both.</summary>
    void Shuffle()
    {
        if (!shuffleLayout || wingKnown == null || builtJewelWing < 0 || player == null) return;
        int count = wingKnown.Length;

        // Not the wing you start in: the search would be over before it began. The spawn is read once,
        // because a fresh start after a win runs while the player is still standing at the exit.
        if (!spawnKnown) { playerSpawn = player.position; spawnKnown = true; }
        int startWing = NearestWing(playerSpawn);
        var choices = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (wingKnown[i] && i != startWing && zoneCrosses[i] != null) choices.Add(i);
        }
        if (choices.Count > 1) choices.Remove(lastJewelWing); // two runs in a row never match
        if (choices.Count == 0) return;
        int jewelWing = choices[Random.Range(0, choices.Count)];
        lastJewelWing = jewelWing;

        int next = 0;
        for (int i = 0; i < count && next < clues.Length; i++)
        {
            if (i == jewelWing || !wingKnown[i]) continue;
            Clue clue = clues[next++];
            if (clue == null) continue;
            clue.zone = i;
            clue.transform.position = wingSpot[i];
        }

        if (searchBox != null) searchBox.transform.position = wingCentre[jewelWing];
        if (objective != null) objective.SetJewelHome(wingSpot[jewelWing]);

        // The key: somewhere that is a journey from both the way in and the jewel.
        if (activator != null)
        {
            // Any wing nearly as far as the furthest will do, so the key moves about too.
            var score = new float[count];
            float best = -1f;
            for (int i = 0; i < count; i++)
            {
                score[i] = -1f;
                if (i == jewelWing || !wingKnown[i]) continue;
                score[i] = Mathf.Min(Vector2.Distance(wingSpot[i], playerSpawn),
                                     Vector2.Distance(wingSpot[i], wingSpot[jewelWing]));
                best = Mathf.Max(best, score[i]);
            }
            var far = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (score[i] >= 0f && score[i] >= best * keyDistanceShare) far.Add(i);
            }
            if (far.Count > 0) activator.position = wingSpot[far[Random.Range(0, far.Count)]];
        }
    }

    int NearestWing(Vector3 at)
    {
        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < wingCentre.Length; i++)
        {
            if (!wingKnown[i]) continue;
            float distance = Vector2.Distance(wingCentre[i], at);
            if (distance >= best) continue;
            best = distance;
            nearest = i;
        }
        return nearest;
    }

    /// <summary>A clue joins the map once you have had it in sight.</summary>
    void RevealClues()
    {
        foreach (Clue clue in clues)
        {
            if (clue == null || clue.Revealed || clue.Solved) continue;
            Vector2 from = player.position;
            Vector2 to = clue.transform.position;
            if (Vector2.Distance(from, to) > clueRevealRadius) continue;
            if (Physics2D.Linecast(from, to, wallMask).collider != null) continue;
            clue.SetRevealed(true);
        }
    }

    /// <summary>A fresh building: a new layout and nothing found yet.</summary>
    void NewBuilding()
    {
        Shuffle();
        foreach (Clue clue in clues)
        {
            if (clue != null) clue.SetRevealed(false);
        }
    }

    void OnEnable()
    {
        Subscribe();
    }

    void Start()
    {
        NewBuilding();
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

        // Objective keeps the jewel over your head even in a locker, which left it floating above
        // the locker for every demon to see. Hide it with you.
        if ((Current == Beat.Carrying || Current == Beat.Escaping) && jewelBody != null)
            jewelBody.enabled = !PlayerHidden();

        if (Current == Beat.Clues) RevealClues();

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

        // From a locker you can see a mark but not work it: solving is meant to be the loud, exposed part.
        bool hidden = PlayerHidden();

        foreach (Clue clue in clues)
        {
            if (hidden || clue == null || clue.Solved) continue;
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
        if (activator == null || HasActivator || PlayerHidden()) return;
        if (Vector2.Distance(player.position, activator.position) > activatorRadius) return;
        TakeActivator();
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
            if (pips[i] != null) pips[i].color = i < solved ? armedColor : pipEmptyColor;
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
    /// story, and a difficulty change makes it a different game, so both start the building over.
    /// </summary>
    public void ResetRun()
    {
        HasActivator = false;

        if (restartClean || (run != null && run.RestartingFresh))
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
            NewBuilding();
        }

        Restore();
    }
}
