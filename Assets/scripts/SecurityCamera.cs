using UnityEngine;

/// <summary>
/// Wall-mounted camera that sweeps its vision cone back and forth. After watching the player for
/// detectTime it raises the alarm: the nearest enemy is sent to the player's position, refreshed
/// while the player stays in view. Its lens blinks slowly when idle and fast while alarmed, and
/// being unlit it stays visible in the dark.
/// </summary>
public class SecurityCamera : MonoBehaviour, IRunResettable
{
    [Tooltip("Sensor child; its transform is rotated for the sweep (forward is +Y).")]
    public NpcVision vision;

    [Tooltip("Body that turns with the sweep.")]
    public Transform housing;

    [Tooltip("Small unlit renderer toggled to blink.")]
    public Renderer lens;

    public float sweepAngle = 50f;
    public float sweepSpeed = 35f;
    public float detectTime = 1f;
    public float suspicionDecayTime = 1.5f;

    [Tooltip("Seconds between alarm updates while the player stays in view.")]
    public float alarmRepeat = 0.5f;

    public float idleBlinkSeconds = 1.2f;
    public float alarmBlinkSeconds = 0.15f;

    public float Suspicion { get; private set; }
    public bool Alarmed => Suspicion >= 1f;

    float baseAngle;
    float phase;
    float nextAlarm;
    NpcChaser[] enemies;

    void Awake()
    {
        if (vision == null) vision = GetComponentInChildren<NpcVision>();
        baseAngle = CurrentAngle();
    }

    void Start()
    {
        FindEnemies();
    }

    /// <summary>
    /// Include inactive ones: DemonCount switches demons 2 and 3 off in Awake, which runs before
    /// this Start, and the menu can switch them on afterwards. Excluding them left every camera
    /// able to alert only the first demon, however many were actually in the building.
    /// </summary>
    void FindEnemies()
    {
        enemies = FindObjectsByType<NpcChaser>(FindObjectsInactive.Include);
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        bool running = run == null || run.IsRunning;
        bool seen = running && vision != null && vision.CanSeeTarget;

        if (seen)
        {
            Suspicion = Mathf.Min(1f, Suspicion + Time.deltaTime / Mathf.Max(0.01f, detectTime));
            Aim(vision.target.position);
            if (Alarmed && Time.time >= nextAlarm) RaiseAlarm(vision.target.position);
        }
        else
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime / Mathf.Max(0.01f, suspicionDecayTime));
            Sweep();
        }

        Blink();
    }

    void Sweep()
    {
        if (sweepAngle <= 0f)
        {
            SetAngle(baseAngle);
            return;
        }
        phase += Time.deltaTime * sweepSpeed / sweepAngle; // peak angular speed = sweepSpeed
        SetAngle(baseAngle + Mathf.Sin(phase) * sweepAngle);
    }

    /// <summary>Turns toward the target within the sweep limits, keeping the sweep phase continuous.</summary>
    void Aim(Vector3 target)
    {
        Vector2 to = target - transform.position;
        float desired = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
        float limited = baseAngle + Mathf.Clamp(Mathf.DeltaAngle(baseAngle, desired), -sweepAngle, sweepAngle);
        SetAngle(Mathf.MoveTowardsAngle(CurrentAngle(), limited, sweepSpeed * 2f * Time.deltaTime));

        float offset = Mathf.DeltaAngle(baseAngle, CurrentAngle());
        phase = Mathf.Asin(Mathf.Clamp(offset / Mathf.Max(0.01f, sweepAngle), -1f, 1f));
    }

    float CurrentAngle()
    {
        Transform t = vision != null ? vision.transform : transform;
        return t.eulerAngles.z;
    }

    void SetAngle(float degrees)
    {
        Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
        if (vision != null) vision.transform.rotation = rotation;
        if (housing != null) housing.rotation = rotation;
    }

    void RaiseAlarm(Vector3 at)
    {
        nextAlarm = Time.time + alarmRepeat;
        if (enemies == null) return;

        NpcChaser nearest = null;
        float best = float.MaxValue;
        foreach (NpcChaser enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue; // a demon this difficulty left out
            float distance = Vector2.Distance(enemy.transform.position, at);
            if (distance >= best) continue;
            best = distance;
            nearest = enemy;
        }
        if (nearest != null) nearest.Investigate(at);
    }

    void Blink()
    {
        if (lens == null) return;
        bool on;
        if (Alarmed)
        {
            on = Mathf.Repeat(Time.time, alarmBlinkSeconds * 2f) < alarmBlinkSeconds;
        }
        else
        {
            float period = Mathf.Max(0.05f, idleBlinkSeconds);
            on = Mathf.Repeat(Time.time, period) > period * 0.12f; // steady, with a short wink
        }
        if (lens.enabled != on) lens.enabled = on;
    }

    public void ResetRun()
    {
        Suspicion = 0f;
        nextAlarm = 0f;
        FindEnemies(); // in case the run was restarted with a different number of demons
    }
}
