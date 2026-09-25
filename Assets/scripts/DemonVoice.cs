using UnityEngine;

/// <summary>
/// One demon's voice. It mutters while it wanders, snarls when it notices something and keeps
/// snarling through a chase, and screams if it is the one that catches you. The sound sits left or
/// right with the demon, fades with distance and is muffled by walls, so you can hear where it is.
/// GameAudio adds one to every demon at the start and hands out the voices.
/// </summary>
[RequireComponent(typeof(NpcChaser))]
public class DemonVoice : MonoBehaviour
{
    public DemonSounds.Voice voice;
    public Transform listener;

    [Tooltip("Beyond this distance the demon cannot be heard.")]
    public float hearDistance = 22f;
    [Tooltip("Volume share left when a wall stands between the demon and the player.")]
    [Range(0f, 1f)] public float throughWall = 0.4f;
    [Range(0f, 1f)] public float volume = 0.55f;

    public Vector2 wanderGap = new Vector2(5f, 10f);
    public Vector2 searchGap = new Vector2(3f, 5f);
    public Vector2 chaseGap = new Vector2(1.6f, 2.8f);

    NpcChaser chaser;
    NpcVision vision;
    AudioSource source;
    DemonSounds.Set sounds;
    NpcChaser.Mode lastMode;
    float nextCall;
    int lastNotice = -1;

    void Awake()
    {
        chaser = GetComponent<NpcChaser>();
        vision = chaser.vision != null ? chaser.vision : GetComponentInChildren<NpcVision>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void Start()
    {
        sounds = DemonSounds.For(voice);
        // Demons do not all start talking together.
        nextCall = Time.time + Random.Range(1f, wanderGap.y);
    }

    void OnEnable()
    {
        if (chaser != null) lastMode = chaser.CurrentMode;
    }

    void Update()
    {
        GameRun run = GameRun.Instance;
        if (run != null && !run.IsRunning) return;
        if (listener == null || sounds.idle == null) return;

        NpcChaser.Mode mode = chaser.CurrentMode;
        if (mode != lastMode)
        {
            bool waking = lastMode == NpcChaser.Mode.Wander || mode == NpcChaser.Mode.Chase;
            lastMode = mode;
            if (mode != NpcChaser.Mode.Wander && waking) Call(Notice(), 1f);
            return;
        }

        if (Time.time < nextCall) return;
        switch (mode)
        {
            case NpcChaser.Mode.Chase: Call(Notice(), 0.9f); break;
            case NpcChaser.Mode.Search: Call(Random.value < 0.5f ? sounds.idle : Notice(), 0.8f); break;
            case NpcChaser.Mode.Wander: Call(sounds.idle, 0.7f); break;
            default: nextCall = Time.time + 1f; break;
        }
    }

    /// <summary>A notice sound, never the same one twice running when there is a choice.</summary>
    AudioClip Notice()
    {
        AudioClip[] pool = sounds.notices;
        if (pool == null || pool.Length == 0) return sounds.idle;
        int pick = Random.Range(0, pool.Length);
        if (pool.Length > 1 && pick == lastNotice) pick = (pick + 1) % pool.Length;
        lastNotice = pick;
        return pool[pick];
    }

    void Call(AudioClip clip, float level)
    {
        Vector2 gap = chaser.CurrentMode == NpcChaser.Mode.Chase ? chaseGap
            : chaser.CurrentMode == NpcChaser.Mode.Search ? searchGap : wanderGap;
        nextCall = Time.time + Random.Range(gap.x, gap.y) + (clip != null ? clip.length : 0f);
        if (clip == null) return;

        Vector2 offset = transform.position - listener.position;
        float distance = offset.magnitude;
        if (distance > hearDistance) return;

        float loudness = 1f - distance / hearDistance;
        loudness *= loudness;
        if (vision != null && Physics2D.Linecast(transform.position, listener.position, vision.wallMask).collider != null)
            loudness *= throughWall;

        source.panStereo = Mathf.Clamp(offset.x / 12f, -0.85f, 0.85f);
        source.PlayOneShot(clip, volume * level * loudness);
    }

    /// <summary>This demon caught the player: its scream, full and close.</summary>
    public void Scream()
    {
        if (sounds.scream == null) return;
        source.panStereo = 0f;
        source.PlayOneShot(sounds.scream, volume * 1.4f);
    }
}
