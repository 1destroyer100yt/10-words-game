using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Owns one "run": the survival timer, the best time (saved in PlayerPrefs) and the caught
/// sequence: slow motion, a red flash, the caught icon, then a soft reset of every IRunResettable
/// in the scene. R or the gamepad's Select button restarts at any time.
/// </summary>
public class GameRun : MonoBehaviour
{
    public static GameRun Instance { get; private set; }

    [Header("Caught sequence")]
    [Tooltip("Full-screen red image shown for a moment when caught.")]
    public Image flash;

    [Tooltip("Icon shown after the flash until the run restarts.")]
    public Image caughtIcon;

    [Range(0.01f, 1f)]
    public float slowMotionScale = 0.15f;
    public float slowMotionSeconds = 0.6f;
    public float flashSeconds = 0.12f;
    public float iconSeconds = 1.3f;

    [Header("Best time")]
    public string bestTimeKey = "Floorplan.BestTime";

    [Header("Story")]
    [Tooltip("Optional. When it has a card left for being caught, it takes that moment instead of the icon.")]
    public StoryCards story;

    [Tooltip("Optional. Plays the opening and ending picture frames and gates the jewel behind the clues.")]
    public StoryDirector director;

    [Tooltip("Seconds at the start of every run in which no demon can catch you, so one that happens " +
             "to wander past the spawn cannot end a run before it starts.")]
    public float catchGraceSeconds = 4f;

    /// <summary>True during the first catchGraceSeconds of a run.</summary>
    public bool InGrace => RunTime < catchGraceSeconds;

    [Tooltip("Hold still until the menu calls Begin. The menu sets this.")]
    public bool startPaused;

    bool running = true;
    public bool IsRunning { get => running && !IsPaused && !StoryPlaying; private set => running = value; }

    /// <summary>
    /// True while a story cutscene holds the screen. It stops the clock, the demons and the player
    /// without touching Time.timeScale, so a word card underneath still counts down on unscaled time.
    /// </summary>
    public bool StoryPlaying { get; set; }
    public bool IsPaused { get; private set; }
    int inputBlockedThroughFrame = -1;
    public bool AcceptsGameplayInput => IsRunning && Time.frameCount > inputBlockedThroughFrame;
    public bool IsCaught => caughtRoutine != null;

    /// <summary>Seconds survived in the current run.</summary>
    public float RunTime { get; private set; }

    /// <summary>Longest survival so far, including the current run.</summary>
    public float BestTime { get; private set; }

    /// <summary>Raised when an enemy starts chasing the player (camera shake, red flash).</summary>
    public event System.Action Spotted;
    public event System.Action Caught;
    public event System.Action Restarted;

    /// <summary>Raised the moment this run passes the best time you had when it started.</summary>
    public event System.Action NewBest;

    /// <summary>Raised once, when the first run actually starts (after the menu closes).</summary>
    public event System.Action Started;

    /// <summary>Raised when the jewel is carried out of the building.</summary>
    public event System.Action Won;

    [Header("Winning")]
    [Tooltip("Icon shown when you get out with the jewel.")]
    public Image wonIcon;
    public float wonSeconds = 1.8f;

    /// <summary>How long the last winning run took, for the title to show after the ending.</summary>
    public float LastWinSeconds { get; private set; }

    /// <summary>True when that run beat this difficulty's best win (or was the first).</summary>
    public bool LastWinWasRecord { get; private set; }

    /// <summary>Runs won this session.</summary>
    public int Wins { get; private set; }

    /// <summary>Fastest winning run, or 0 if none yet.</summary>
    public float BestWin { get; private set; }

    public string bestWinKey = "Floorplan.BestWin";

    Coroutine caughtRoutine;
    /// <summary>The best time as it stood when this run began; the current run cannot beat itself.</summary>
    float beatable;
    bool beaten;
    bool begun;

    /// <summary>
    /// The difficulty level the records belong to. Level 1 keeps the original keys, so records saved
    /// before levels existed stay where they were.
    /// </summary>
    public int Level { get; private set; } = 1;

    public static string KeyFor(string baseKey, int level) => level <= 1 ? baseKey : baseKey + ".L" + level;
    string TimeKey => KeyFor(bestTimeKey, Level);
    string WinKey => KeyFor(bestWinKey, Level);

    /// <summary>Switches the best time and best win shown and saved to this difficulty's own.</summary>
    public void SetLevel(int level)
    {
        level = Mathf.Max(1, level);
        if (level == Level) return;
        SaveBest();
        Level = level;
        LoadBest();
    }

    // MenuScreen runs first (execution order -100) and may set the level before this Awake has
    // loaded anything; saving then would write an empty record over a real one.
    bool recordsLoaded;

    void LoadBest()
    {
        recordsLoaded = true;
        BestTime = PlayerPrefs.GetFloat(TimeKey, 0f);
        beatable = BestTime;
        beaten = false;
        BestWin = PlayerPrefs.GetFloat(WinKey, 0f);
    }

    void Awake()
    {
        Instance = this;
        // A* logs every calculated path, even in release builds: hundreds of browser console lines a
        // minute. AstarPath's Awake runs first (execution order -10000), so it is already active here.
        if (AstarPath.active != null) AstarPath.active.logPathResults = Pathfinding.PathLog.None;
        LoadBest();
        if (startPaused) IsRunning = false;
        if (flash != null) flash.enabled = false;
        if (caughtIcon != null) caughtIcon.enabled = false;
        if (wonIcon != null) wonIcon.enabled = false;
    }

    void Start()
    {
        if (!startPaused) Begin();
    }

    /// <summary>True once the first run has started, for listeners that subscribed too late.</summary>
    public bool HasBegun => begun;

    /// <summary>Starts the first run. Called by the menu, or immediately when there is no menu.</summary>
    public void Begin()
    {
        if (begun) return;
        begun = true;
        IsRunning = true;
        RunTime = 0f;
        Started?.Invoke();
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        SaveBest();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (IsRunning)
        {
            RunTime += Time.deltaTime;
            if (RunTime > BestTime) BestTime = RunTime;

            if (!beaten && beatable > 0f && RunTime > beatable)
            {
                beaten = true;
                NewBest?.Invoke();
            }
        }

        if (begun && AcceptsGameplayInput && !IsCaught && RestartPressed()) Restart();
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        SuppressInputThisFrame();
        Time.timeScale = paused ? 0f : 1f;
    }

    public void SuppressInputThisFrame()
    {
        inputBlockedThroughFrame = Time.frameCount;
    }

    public void NotifySpotted()
    {
        if (IsRunning) Spotted?.Invoke();
    }

    /// <summary>Ends the run: slow motion, red flash, icon, then restart.</summary>
    public void CatchPlayer(Component by)
    {
        if (IsCaught || !IsRunning || InGrace) return;
        IsRunning = false;
        SaveBest();
        if (Debug.isDebugBuild) Debug.Log($"Player caught by {(by != null ? by.name : "unknown")} after {RunTime:F1} s.", this);
        Caught?.Invoke();
        caughtRoutine = StartCoroutine(CaughtSequence());
    }

    /// <summary>The jewel made it out. Ends the run the good way.</summary>
    public void WinRun()
    {
        if (IsCaught || !IsRunning) return;
        IsRunning = false;
        Wins++;
        LastWinSeconds = RunTime;
        LastWinWasRecord = BestWin <= 0f || RunTime < BestWin;
        if (BestWin <= 0f || RunTime < BestWin)
        {
            BestWin = RunTime;
            PlayerPrefs.SetFloat(WinKey, BestWin);
        }
        SaveBest();
        if (Debug.isDebugBuild) Debug.Log($"Jewel carried out after {RunTime:F1} s. Wins this session: {Wins}.", this);
        Won?.Invoke();
        caughtRoutine = StartCoroutine(WonSequence());
    }

    IEnumerator WonSequence()
    {
        Time.timeScale = 0f;

        // The ending is the last two picture frames. The icon is only the fallback for a scene
        // built without a director.
        if (director != null && director.PlayEnding())
        {
            while (director.IsPlaying) yield return null;
        }
        else
        {
            if (wonIcon != null) wonIcon.enabled = true;
            yield return new WaitForSecondsRealtime(wonSeconds);
            if (wonIcon != null) wonIcon.enabled = false;
        }

        caughtRoutine = null;
        Restart();
    }

    IEnumerator CaughtSequence()
    {
        Time.timeScale = slowMotionScale;
        yield return new WaitForSecondsRealtime(slowMotionSeconds);

        Time.timeScale = 0f;
        if (flash != null) flash.enabled = true;
        yield return new WaitForSecondsRealtime(flashSeconds);
        if (flash != null) flash.enabled = false;

        // The story gets the first death; every one after that is the skull.
        bool toldByStory = story != null && story.ShowCaughtCard();
        if (toldByStory)
        {
            // Wait for the card to actually leave the screen, however long its queue takes.
            while (story.IsShowing) yield return null;
        }
        else
        {
            if (caughtIcon != null) caughtIcon.enabled = true;
            yield return new WaitForSecondsRealtime(iconSeconds);
        }
        if (caughtIcon != null) caughtIcon.enabled = false;

        caughtRoutine = null;
        Restart();
    }

    /// <summary>True only while RestartFresh is resetting, so resettables can drop progress they would keep across a death.</summary>
    public bool RestartingFresh { get; private set; }

    /// <summary>
    /// A restart that also forgets story progress. The menu uses it when the difficulty changes,
    /// because a different number of demons is a different game, not a retry.
    /// </summary>
    public void RestartFresh()
    {
        RestartingFresh = true;
        try { Restart(); }
        finally { RestartingFresh = false; }
    }

    /// <summary>Soft reset: every IRunResettable goes back to its start state; no scene reload.</summary>
    public void Restart()
    {
        if (!begun || IsPaused) return;
        SaveBest();
        SuppressInputThisFrame();
        if (caughtRoutine != null)
        {
            StopCoroutine(caughtRoutine);
            caughtRoutine = null;
        }
        if (flash != null) flash.enabled = false;
        if (caughtIcon != null) caughtIcon.enabled = false;
        if (wonIcon != null) wonIcon.enabled = false;
        Time.timeScale = 1f;

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (behaviour is IRunResettable resettable) resettable.ResetRun();
        }

        // After the player has been moved back, so the camera does not glide across the map.
        foreach (CameraFollow2D follow in FindObjectsByType<CameraFollow2D>(FindObjectsInactive.Exclude))
        {
            follow.SnapToTarget();
        }

        RunTime = 0f;
        beatable = BestTime;
        beaten = false;
        IsRunning = true;
        Restarted?.Invoke();
    }

    void SaveBest()
    {
        if (!recordsLoaded) return;
        PlayerPrefs.SetFloat(TimeKey, BestTime);
        PlayerPrefs.Save();
    }

    static bool RestartPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.selectButton.wasPressedThisFrame;
    }

    // Enter Play Mode keeps the loaded domain (Reload Domain is off in this project), so statics
    // keep whatever the last play session left in them. This wipes them at the start of each one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
}
