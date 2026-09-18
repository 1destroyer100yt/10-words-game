using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Automated play test for the floorplan NPC: Tools/Floorplan/Play Test (30 s).
/// Enters play mode, samples the NPC every editor update, then logs a report and leaves play mode.
/// The report counts destinations reached, distance travelled, the longest stall, samples where the
/// NPC's centre was inside a wall collider (a real wall crossing) and samples where its radius
/// merely overlapped one (visual clipping).
/// </summary>
public static class FloorplanPlaytest
{
    const string MenuPath = "Tools/Floorplan/Play Test (30 s)";
    const string ActiveKey = "Floorplan.Playtest.Active";
    const string StartSceneKey = "Floorplan.Playtest.StartScene";
    const string WallLayerName = "Walls";
    const float DefaultDuration = 30f;
    const float AgentRadius = FloorplanSetup.AgentRadius; // one definition, so the two cannot drift
    const float StallLimit = 4f;
    const float StallSnapshotAfter = 1f;

    static float duration = DefaultDuration;

    /// <summary>True while a play test is running (survives domain reloads via SessionState).</summary>
    public static bool IsActive => SessionState.GetBool(ActiveKey, false);

    static AIPath ai;
    static int wallMask;
    static double startTime;
    static double lastMoveTime;
    static Vector3 lastPosition;
    static Vector3 lastDestination;
    static bool wasAtEnd;
    static bool hasSample;
    static float distance;
    static float longestStall;
    static float longestStallAt;
    static float longestGameStall;
    static float longestGameStallAt;
    static bool previousRunInBackground;
    static float firstMoveAt;
    static int lastMoveFrame;
    static float lastMoveGameTime;
    static bool stallSnapshotTaken;
    static int stallSnapshots;
    static int destinationsReached;
    static int destinationsPicked;
    static int centreInWall;
    static int radiusOverlap;
    static int halfRadiusOverlap;
    static int quarterRadiusOverlap;
    static int samples;

    [InitializeOnLoadMethod]
    static void OnScriptsReloaded()
    {
        // A domain reload when entering play mode wipes the static state; resume from SessionState.
        if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying) Hook();
    }

    [MenuItem(MenuPath)]
    public static void Start()
    {
        Start(DefaultDuration);
    }

    public static void Start(float seconds)
    {
        duration = seconds > 0f ? seconds : DefaultDuration;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Floorplan playtest: exit play mode first.");
            return;
        }

        if (Object.FindAnyObjectByType<AIPath>() == null)
        {
            Debug.LogError("Floorplan playtest: no AIPath in the scene. Run Tools/Floorplan/Build Scene first.");
            return;
        }

        SessionState.SetBool(ActiveKey, true);

        // StartupSetup points play mode at the Intro scene, which has no AIPath. Park that choice
        // over the test and restore it in Finish, or the test would measure the wrong scene and
        // give up with "no AIPath found". The path goes through SessionState to survive the reload.
        SceneAsset startScene = EditorSceneManager.playModeStartScene;
        SessionState.SetString(StartSceneKey, startScene != null ? AssetDatabase.GetAssetPath(startScene) : "");
        EditorSceneManager.playModeStartScene = null;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        TestPlayerPrefsGuard.Stash(); // the run saves a best time on the way out; put the real one back after
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) Hook();
    }

    static void Hook()
    {
        ai = null;
        hasSample = false;
        wasAtEnd = false;
        distance = 0f;
        longestStall = 0f;
        longestStallAt = 0f;
        longestGameStall = 0f;
        longestGameStallAt = 0f;
        firstMoveAt = -1f;

        // Keep the game loop ticking if the editor window loses focus during the test.
        previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        lastMoveFrame = 0;
        lastMoveGameTime = 0f;
        stallSnapshotTaken = false;
        stallSnapshots = 0;
        destinationsReached = 0;
        destinationsPicked = 0;
        centreInWall = 0;
        radiusOverlap = 0;
        halfRadiusOverlap = 0;
        quarterRadiusOverlap = 0;
        samples = 0;
        startTime = EditorApplication.timeSinceStartup;
        lastMoveTime = startTime;
        notPlayingSince = -1.0;
        wallMask = LayerMask.GetMask(WallLayerName);
        if (wallMask == 0)
        {
            // Every wall check would quietly return "clear" and the run would report success.
            Debug.LogError($"Floorplan playtest: no '{WallLayerName}' layer, so wall checks would all pass. Stopping.");
            Finish("walls layer missing");
            return;
        }

        // The title screen holds timeScale at 0 until a player presses something. Without this the
        // test measures a frozen game: 0 units travelled and no game time at all.
        var menu = Object.FindAnyObjectByType<MenuScreen>();
        if (menu != null && menu.IsOpen)
        {
            menu.Close();
            Debug.Log("Floorplan playtest: dismissed the title screen so the run can start.");
        }

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static double notPlayingSince = -1.0;

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;

        // isPlaying can read false for a moment while the editor is still switching modes.
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (notPlayingSince < 0.0) notPlayingSince = now;
            if (now - notPlayingSince < 1.0) return;
            Finish("play mode ended early");
            return;
        }
        notPlayingSince = -1.0;

        // The opening picture frames freeze the run on purpose. Measuring through them would score
        // the story's length as a ten-second stall and report a broken level. Hold the clock at the
        // start line until the last frame leaves the screen.
        var director = Object.FindAnyObjectByType<StoryDirector>();
        if (director != null && director.IsPlaying)
        {
            startTime = now;
            lastMoveTime = now;
            lastMoveFrame = Time.frameCount;
            lastMoveGameTime = Time.time;
            return;
        }

        if (ai == null)
        {
            ai = Object.FindAnyObjectByType<AIPath>();
            if (ai == null)
            {
                if (now - startTime > 5.0) Finish("no AIPath found");
                return;
            }
        }

        Vector3 position = ai.transform.position;
        if (!hasSample)
        {
            lastPosition = position;
            lastDestination = ai.destination;
            lastMoveTime = now;
            lastMoveFrame = Time.frameCount;
            lastMoveGameTime = Time.time;
            hasSample = true;
        }

        float step = Vector3.Distance(position, lastPosition);
        distance += step;
        if (step > 0.0005f)
        {
            if (firstMoveAt < 0f) firstMoveAt = (float)(now - startTime);
            lastMoveTime = now;
            lastMoveFrame = Time.frameCount;
            lastMoveGameTime = Time.time;
            stallSnapshotTaken = false;
        }
        else
        {
            float stall = (float)(now - lastMoveTime);
            if (stall > longestStall)
            {
                longestStall = stall;
                longestStallAt = (float)(lastMoveTime - startTime);
            }

            // Game-time stall: only grows while the player loop is actually running.
            float gameStall = Time.time - lastMoveGameTime;
            if (gameStall > longestGameStall)
            {
                longestGameStall = gameStall;
                longestGameStallAt = (float)(lastMoveTime - startTime);
            }

            if (stall > StallSnapshotAfter && !stallSnapshotTaken)
            {
                stallSnapshotTaken = true;
                stallSnapshots++;
                LogStallSnapshot(stall, position);
            }
        }

        if (ai.destination != lastDestination)
        {
            destinationsPicked++;
            lastDestination = ai.destination;
        }

        bool atEnd = ai.reachedEndOfPath;
        if (atEnd && !wasAtEnd) destinationsReached++;
        wasAtEnd = atEnd;

        // Overlap depth buckets: full radius = any visual clipping, half and quarter = deeper penetration.
        if (Physics2D.OverlapPoint(position, wallMask) != null) centreInWall++;
        else if (Physics2D.OverlapCircle(position, AgentRadius, wallMask) != null)
        {
            radiusOverlap++;
            if (Physics2D.OverlapCircle(position, AgentRadius * 0.5f, wallMask) != null) halfRadiusOverlap++;
            if (Physics2D.OverlapCircle(position, AgentRadius * 0.25f, wallMask) != null) quarterRadiusOverlap++;
        }

        samples++;
        lastPosition = position;

        if (now - startTime >= duration) Finish("complete");
    }

    /// <summary>
    /// Logged once per stall longer than StallSnapshotAfter. Frames advanced = 0 means the editor
    /// itself hitched; frames advancing with no movement means the agent or wanderer is stuck.
    /// </summary>
    static void LogStallSnapshot(float stall, Vector3 position)
    {
        int framesAdvanced = Time.frameCount - lastMoveFrame;
        float gameTimeAdvanced = Time.time - lastMoveGameTime;
        var wanderer = ai.GetComponent<AstarWanderer>();
        string wandererState = wanderer != null
            ? $"picks {wanderer.Picks}, fallbacks {wanderer.Fallbacks}, last pick attempts {wanderer.LastAttempts}"
            : "no AstarWanderer";

        GraphNode node = AstarPath.active != null ? AstarPath.active.GetNearest(position, NNConstraint.Default).node : null;
        string nodeState = node != null ? $"nearest node walkable {node.Walkable} area {node.Area}" : "no nearest node";

        Debug.Log($"Floorplan playtest stall snapshot at {(float)(EditorApplication.timeSinceStartup - startTime):F2} s: " +
                  $"stalled {stall:F2} s, frames advanced {framesAdvanced}, game time advanced {gameTimeAdvanced:F2} s, " +
                  $"pathPending {ai.pathPending}, hasPath {ai.hasPath}, reachedEndOfPath {ai.reachedEndOfPath}, " +
                  $"remainingDistance {ai.remainingDistance:F2}, velocity {ai.velocity.magnitude:F2}, " +
                  $"canSearch {ai.canSearch}, isStopped {ai.isStopped}, position {position}, destination {ai.destination}; " +
                  $"{wandererState}; {nodeState}.");
    }

    static void Finish(string reason)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SessionState.SetBool(ActiveKey, false);

        string startScenePath = SessionState.GetString(StartSceneKey, "");
        SessionState.SetString(StartSceneKey, "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startScenePath)
            ? null
            : AssetDatabase.LoadAssetAtPath<SceneAsset>(startScenePath);

        double elapsed = EditorApplication.timeSinceStartup - startTime;
        string report = $"Floorplan playtest ({reason}, {elapsed:F1} s, {samples} samples): " +
                        $"{destinationsReached} destinations reached, {destinationsPicked} new destinations picked, " +
                        $"{distance:F1} units travelled, first movement at {firstMoveAt:F2} s, " +
                        $"longest game-time stall {longestGameStall:F2} s starting at {longestGameStallAt:F2} s, " +
                        $"longest wall-clock stall {longestStall:F2} s starting at {longestStallAt:F2} s, {stallSnapshots} stalls over {StallSnapshotAfter:F0} s, " +
                        $"centre inside wall {centreInWall} samples, wall overlap at full radius {radiusOverlap}, " +
                        $"at half radius {halfRadiusOverlap}, at quarter radius {quarterRadiusOverlap} samples.";

        bool failed = centreInWall > 0 || destinationsReached == 0 || longestGameStall > StallLimit;
        if (failed) Debug.LogError(report);
        else Debug.Log(report);

        Application.runInBackground = previousRunInBackground;

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failed ? 1 : 0);
            return;
        }

        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }
}
