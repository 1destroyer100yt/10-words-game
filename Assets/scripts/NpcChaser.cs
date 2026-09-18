using Pathfinding;
using UnityEngine;

/// <summary>
/// Enemy behaviour on top of AstarWanderer. Seeing the player fills a suspicion meter while the
/// enemy stops and turns to look; when it fills the enemy chases. Losing sight sends it to the
/// last seen position to search, after which it wanders again. Noises (decoys) and camera alarms
/// send it to investigate a position. Touching the player ends the run; a hidden player is only
/// found by an enemy that searches right next to the hiding spot.
/// </summary>
[RequireComponent(typeof(AstarWanderer))]
public class NpcChaser : MonoBehaviour, IRunResettable
{
    public enum Mode { Wander, Chase, Search, Suspicious }

    [Tooltip("Sensor that decides whether the player is visible. Found in children if empty.")]
    public NpcVision vision;

    [Tooltip("Movement speed while chasing or searching; wandering keeps the AI's own speed.")]
    public float chaseSpeed = 7f;

    [Tooltip("Seconds to look around the last seen position before wandering again.")]
    public float searchTime = 3f;

    [Tooltip("Seconds without progress before giving up a search whose target cannot be reached.")]
    public float searchTimeout = 8f;

    [Tooltip("Seconds of continuous sight before the enemy starts chasing.")]
    public float detectTime = 1.5f;

    [Tooltip("Seconds for a full meter to drain once the player is out of sight.")]
    public float suspicionDecayTime = 2f;

    [Tooltip("Degrees per second the enemy turns toward the player while suspicious.")]
    public float turnSpeed = 360f;

    [Tooltip("Distance at which the enemy catches the player.")]
    public float catchDistance = 0.75f;

    [Tooltip("A hidden player is found when the enemy searches within this distance of the hiding spot.")]
    public float hidingDiscoverRadius = 2.5f;

    public bool logStateChanges = true;

    public Mode CurrentMode { get; private set; }

    /// <summary>0..1; the enemy chases at 1.</summary>
    public float Suspicion { get; private set; }

    /// <summary>True whenever the enemy is doing anything but plain wandering.</summary>
    public bool IsAlerted => CurrentMode != Mode.Wander || Suspicion > 0.001f;

    IAstarAI ai;
    AstarWanderer wanderer;
    PlayerHider hider;
    Transform hiderTarget;
    float wanderSpeed;
    float searchStarted;
    float arrivedAt = -1f;
    Vector3 searchTarget;
    Vector3 spawn;
    Quaternion spawnRotation;

    void Awake()
    {
        ai = GetComponent<IAstarAI>();
        wanderer = GetComponent<AstarWanderer>();
        if (vision == null) vision = GetComponentInChildren<NpcVision>();
        spawn = transform.position;
        spawnRotation = transform.rotation;
        if (ai != null) wanderSpeed = ai.maxSpeed;
    }

    void OnEnable()
    {
        Decoy.NoiseEmitted += OnNoise;
    }

    void OnDisable()
    {
        Decoy.NoiseEmitted -= OnNoise;
    }

    void Update()
    {
        if (ai == null || vision == null || vision.target == null) return;
        GameRun run = GameRun.Instance;
        if (run != null && !run.IsRunning) return;
        ResolveHider();

        bool seen = vision.CanSeeTarget;
        if (CurrentMode == Mode.Chase)
        {
            if (seen) ai.destination = vision.target.position; // AIPath repaths to it on its own schedule
            else Enter(Mode.Search, vision.LastSeenPosition);
            TryCatch();
            return;
        }

        if (seen)
        {
            Suspicion = Mathf.Min(1f, Suspicion + Time.deltaTime / Mathf.Max(0.01f, detectTime));
            if (CurrentMode != Mode.Suspicious) Enter(Mode.Suspicious);
            if (Suspicion >= 1f) Enter(Mode.Chase);
        }
        else
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime / Mathf.Max(0.01f, suspicionDecayTime));
            if (CurrentMode == Mode.Suspicious && Suspicion <= 0f) Enter(Mode.Search, vision.LastSeenPosition);
        }

        if (CurrentMode == Mode.Search) UpdateSearch();
        TryCatch();
    }

    void LateUpdate()
    {
        if (CurrentMode == Mode.Suspicious && vision != null && vision.target != null) FaceTarget();
    }

    void UpdateSearch()
    {
        if (ai.reachedEndOfPath && !ai.pathPending)
        {
            if (arrivedAt < 0f) arrivedAt = Time.time;
            if (hider != null && hider.IsHidden &&
                Vector2.Distance(searchTarget, hider.SpotPosition) <= hidingDiscoverRadius)
            {
                Catch();
                return;
            }
            if (Time.time - arrivedAt >= searchTime) Enter(Mode.Wander);
        }
        else if (Time.time - searchStarted > searchTimeout)
        {
            Enter(Mode.Wander);
        }
    }

    void TryCatch()
    {
        if (hider != null && hider.IsHidden) return; // a hidden player is only found by searching
        if (Vector2.Distance(transform.position, vision.target.position) > catchDistance) return;
        Catch();
    }

    void Catch()
    {
        GameRun run = GameRun.Instance;
        if (run != null) run.CatchPlayer(this);
    }

    void FaceTarget()
    {
        Vector2 to = vision.target.position - transform.position;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion desired = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
    }

    void ResolveHider()
    {
        if (hiderTarget == vision.target) return;
        hiderTarget = vision.target;
        hider = hiderTarget != null ? hiderTarget.GetComponentInParent<PlayerHider>() : null;
    }

    /// <summary>Go and look at a position (a noise, a camera alarm) unless already chasing or watching.</summary>
    public void Investigate(Vector3 point)
    {
        if (CurrentMode == Mode.Chase || CurrentMode == Mode.Suspicious) return;
        Enter(Mode.Search, point);
    }

    void OnNoise(Vector3 point, float radius)
    {
        if (!isActiveAndEnabled) return;
        if (Vector2.Distance(transform.position, point) <= radius) Investigate(point);
    }

    void Enter(Mode mode, Vector3 point = default)
    {
        Mode previous = CurrentMode;
        CurrentMode = mode;
        bool wandering = mode == Mode.Wander;
        wanderer.enabled = wandering;
        ai.isStopped = mode == Mode.Suspicious;
        ai.maxSpeed = wandering ? wanderSpeed : chaseSpeed;

        switch (mode)
        {
            case Mode.Chase:
                Suspicion = 1f;
                ai.destination = vision.target.position;
                ai.SearchPath();
                GameRun run = GameRun.Instance;
                if (run != null) run.NotifySpotted();
                Log("NPC spotted the player and is chasing.");
                break;
            case Mode.Search:
                searchStarted = Time.time;
                arrivedAt = -1f;
                searchTarget = point;
                ai.destination = point;
                ai.SearchPath();
                Log(previous == Mode.Chase ? "NPC lost the player; searching the last seen position." : "NPC is going to investigate.");
                break;
            case Mode.Suspicious:
                Log("NPC noticed something and is looking.");
                break;
            case Mode.Wander:
                Suspicion = 0f;
                if (previous != Mode.Wander) Log("NPC gave up and is wandering again.");
                break;
        }
    }

    void Log(string message)
    {
        // Editor and development builds only: in a release build these reached every player's browser console.
        if (logStateChanges && Debug.isDebugBuild) Debug.Log(message, this);
    }

    public void ResetRun()
    {
        Suspicion = 0f;
        arrivedAt = -1f;
        if (ai != null)
        {
            ai.Teleport(spawn);
            ai.destination = spawn;
        }
        transform.rotation = spawnRotation;
        if (ai != null && wanderer != null) Enter(Mode.Wander);
    }
}
