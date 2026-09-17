using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Drives the whole story in play mode and checks every beat actually happens: the opening frames,
/// each clue crossing its wing off, the search zone appearing, the jewel becoming takeable only
/// inside it, the escalation when it is taken, the activator filling the second socket, and the
/// win that plays the ending. The player is teleported from objective to objective, so this is a
/// test of the story's wiring, not of the pathfinding, which Play Test already covers.
/// </summary>
public static class FloorplanStoryTest
{
    const string MenuPath = "Tools/Floorplan/Story Test";
    const string ActiveKey = "Floorplan.StoryTest.Active";
    const string StartSceneKey = "Floorplan.StoryTest.StartScene";

    /// <summary>Real seconds to wait for a step before calling it a failure.</summary>
    const double StepTimeout = 30.0;

    enum Step
    {
        WaitForMenu, DismissMenu, WaitForOpening, CheckLocked,
        SolveClues, CheckSearch, WalkToJewel, TakeJewel, CheckEscalation,
        TakeActivator, CheckPortalLive, WalkOut, WaitForEnding, Done,
    }

    static Step step;
    static double stepStarted;
    static int clueIndex;
    static readonly List<string> report = new List<string>();
    static bool failed;

    static MenuScreen menu;
    static GameRun run;
    static StoryDirector director;
    static Objective objective;
    static Transform player;
    static Rigidbody2D playerBody;

    static float baseChaseSpeed, baseDetect, baseVignette, baseHeart;
    static bool sawEnding;

    /// <summary>
    /// Writes every story frame, both blink variants, to Logs/StoryFrames as PNGs scaled 8x with
    /// hard pixel edges. It is the only way to actually look at the pictures without sitting
    /// through the cutscene, and it renders through the shipping code, not a copy of it.
    /// </summary>
    [MenuItem("Tools/Floorplan/Dump Story Frames")]
    public static void DumpFrames()
    {
        const int scale = 8;
        string folder = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "StoryFrames");
        System.IO.Directory.CreateDirectory(folder);

        int written = 0;
        foreach (PixelFrames.Frame frame in System.Enum.GetValues(typeof(PixelFrames.Frame)))
        {
            for (int variant = 0; variant < 2; variant++)
            {
                Texture2D small = PixelFrames.Render(frame, variant, true);
                var big = new Texture2D(PixelFrames.Width * scale, PixelFrames.Height * scale, TextureFormat.RGBA32, false);
                for (int y = 0; y < big.height; y++)
                {
                    for (int x = 0; x < big.width; x++) big.SetPixel(x, y, small.GetPixel(x / scale, y / scale));
                }
                big.Apply();

                string path = System.IO.Path.Combine(folder, $"{(int)frame}_{frame}_{variant}.png");
                System.IO.File.WriteAllBytes(path, big.EncodeToPNG());
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(small);
                written++;
            }
        }

        Debug.Log($"Story frames: wrote {written} PNGs at {scale}x into {System.IO.Path.GetFullPath(folder)}");
    }

    [MenuItem(MenuPath)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Story test: stop play mode first.");
            return;
        }

        report.Clear();
        failed = false;
        sawEnding = false;
        step = Step.WaitForMenu;
        clueIndex = 0;

        // The project starts play mode on the Intro scene. Park that and put it back in Finish.
        SceneAsset startScene = EditorSceneManager.playModeStartScene;
        SessionState.SetString(StartSceneKey, startScene != null ? AssetDatabase.GetAssetPath(startScene) : "");
        EditorSceneManager.playModeStartScene = null;

        SessionState.SetBool(ActiveKey, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        stepStarted = EditorApplication.timeSinceStartup;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Rehook()
    {
        // The domain reloads on entering play mode; pick the test back up on the other side.
        if (SessionState.GetBool(ActiveKey, false))
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)) { EditorApplication.update -= Tick; return; }
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (step != Step.WaitForMenu) Fail("play mode ended early");
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now - stepStarted > StepTimeout && step != Step.Done)
        {
            Fail($"step {step} timed out after {StepTimeout:F0} s");
            return;
        }

        if (!Resolve()) return;

        switch (step)
        {
            case Step.WaitForMenu: if (menu != null) Next(Step.DismissMenu); break;

            case Step.DismissMenu:
                if (menu.IsOpen) menu.Close();
                Pass($"title screen dismissed; run begun = {run.HasBegun}");
                Check(run.StoryPlaying, "the opening frames froze the run (StoryPlaying)");
                Check(!run.IsRunning, "the run is held while the frames are up");
                Next(Step.WaitForOpening);
                break;

            case Step.WaitForOpening:
                if (director.IsPlaying || run.StoryPlaying) break;
                Pass($"opening frames finished; beat = {director.Current}");
                Check(director.Current == StoryDirector.Beat.Clues, "the story moved to the clue hunt");
                Check(run.IsRunning, "the run is live again after the frames");
                Next(Step.CheckLocked);
                break;

            case Step.CheckLocked:
                Check(objective.locked, "the jewel cannot be taken before the clues are done");
                Check(objective.exitLocked, "the way out is dead before the activator is found");
                Check(!director.searchBox.activeSelf, "no search zone is shown yet");
                Check(director.clues.Length >= 2, $"{director.clues.Length} clues were placed");
                // Snapshot what escalation is supposed to change, before it changes.
                NpcChaser first = Object.FindAnyObjectByType<NpcChaser>();
                baseChaseSpeed = first.chaseSpeed;
                baseDetect = first.detectTime;
                baseVignette = director.effects.baseIntensity;
                baseHeart = director.audioBank.farDistance;
                Next(Step.SolveClues);
                break;

            case Step.SolveClues:
                if (clueIndex >= director.clues.Length) { Next(Step.CheckSearch); break; }
                Clue clue = director.clues[clueIndex];
                if (clue.Solved)
                {
                    Pass($"clue {clueIndex} solved; wing {clue.zone} crossed off " +
                         $"(cross active = {director.zoneCrosses[clue.zone].activeSelf})");
                    Check(director.zoneCrosses[clue.zone].activeSelf, $"wing {clue.zone} is crossed off the map");
                    Check(director.pips[clueIndex].color.r > 0.8f, $"progress mark {clueIndex} turned red");
                    clueIndex++;
                    Next(Step.SolveClues);
                    break;
                }
                // Stand on it. The director does the timing itself.
                Teleport(clue.transform.position);
                break;

            case Step.CheckSearch:
                Check(director.Current == StoryDirector.Beat.Search, "every wing but one is eliminated");
                Check(director.searchBox.activeSelf, "the last wing is boxed in red on the map");
                Check(!objective.locked, "the jewel can now be taken");
                Check(!objective.jewelMarker.enabled, "the map still shows a wing, never a dot");
                Next(Step.WalkToJewel);
                break;

            case Step.WalkToJewel:
                // Stand just outside the reveal radius: the jewel must still be invisible there.
                Vector3 away = objective.jewel.position + Vector3.right * (director.searchRevealRadius + 3f);
                Teleport(away);
                if (now - stepStarted < 0.4) break;
                Check(!director.jewelBody.enabled, "the jewel stays hidden until you are close");
                Next(Step.TakeJewel);
                break;

            case Step.TakeJewel:
                Teleport(objective.jewel.position);
                if (!objective.Carrying) break;
                Pass("jewel taken");
                Check(director.Current == StoryDirector.Beat.Carrying, "the story moved to carrying it");
                Check(director.jewelBody.enabled, "the jewel is visible above the player");
                Check(director.activatorBody.enabled, "the portal activator appeared");
                Check(director.activatorMarker.enabled, "the activator is marked on the map");
                Check(objective.exitLocked, "the way out is still dead with only one socket filled");
                Check(director.jewelSocket.color.r > 0.8f, "the jewel's socket filled red");
                Check(director.activatorSocket.color.r < 0.5f, "the activator's socket is still empty");
                Next(Step.CheckEscalation);
                break;

            case Step.CheckEscalation:
                NpcChaser demon = Object.FindAnyObjectByType<NpcChaser>();
                Check(demon.chaseSpeed > baseChaseSpeed + 0.01f,
                      $"demons got faster ({baseChaseSpeed:F2} -> {demon.chaseSpeed:F2})");
                Check(demon.detectTime < baseDetect - 0.01f,
                      $"demons commit quicker ({baseDetect:F2} -> {demon.detectTime:F2} s)");
                Check(director.effects.baseIntensity > baseVignette + 0.001f,
                      $"the dark closed in ({baseVignette:F2} -> {director.effects.baseIntensity:F2})");
                Check(director.audioBank.farDistance > baseHeart + 0.01f,
                      $"the heartbeat reaches further ({baseHeart:F0} -> {director.audioBank.farDistance:F0})");
                Next(Step.TakeActivator);
                break;

            case Step.TakeActivator:
                Teleport(director.activator.position);
                if (!director.HasActivator) break;
                Pass("portal activator taken");
                Check(!director.activatorBody.enabled, "the activator left the world");
                Check(director.activatorSocket.color.r > 0.8f, "the second socket filled red");
                Next(Step.CheckPortalLive);
                break;

            case Step.CheckPortalLive:
                Check(director.Current == StoryDirector.Beat.Escaping, "the story moved to the escape");
                Check(!objective.exitLocked, "the way out is live with both sockets filled");
                Check(objective.exitSprite.color.r > 0.8f, "the portal burns red");
                Next(Step.WalkOut);
                break;

            case Step.WalkOut:
                Teleport(objective.exit.position);
                if (!director.IsPlaying && run.IsRunning) break;
                sawEnding = director.IsPlaying;
                Pass($"carried the jewel out; ending frames playing = {sawEnding}");
                Check(sawEnding, "the ending frames took the win, not the icon");
                Next(Step.WaitForEnding);
                break;

            case Step.WaitForEnding:
                if (director.IsPlaying) break;
                Pass("ending frames finished");
                Check(!run.StoryPlaying, "the run was handed back cleanly");
                Check(Time.timeScale > 0.5f, $"time is running again (timeScale {Time.timeScale:F2})");
                Check(director.Current == StoryDirector.Beat.Clues,
                      $"a win starts the building over (beat is {director.Current})");
                Check(objective.locked, "the jewel is locked again for the next run");
                Check(!director.zoneCrosses[director.clues[0].zone].activeSelf,
                      "the crosses were wiped for the next run");
                Finish();
                break;
        }
    }

    static bool Resolve()
    {
        if (menu == null) menu = Object.FindAnyObjectByType<MenuScreen>();
        if (run == null) run = Object.FindAnyObjectByType<GameRun>();
        if (director == null) director = Object.FindAnyObjectByType<StoryDirector>();
        if (objective == null) objective = Object.FindAnyObjectByType<Objective>();
        if (player == null && director != null) player = director.player;
        if (playerBody == null && player != null) playerBody = player.GetComponent<Rigidbody2D>();

        if (director == null) { Fail("no StoryDirector in the scene: rebuild the scene first"); return false; }
        return menu != null && run != null && objective != null && player != null;
    }

    /// <summary>Moves the player without physics fighting it, so a step lands exactly where it means to.</summary>
    static void Teleport(Vector3 to)
    {
        to.z = player.position.z;
        if (playerBody != null)
        {
            playerBody.position = to;
            playerBody.linearVelocity = Vector2.zero;
        }
        player.position = to;
    }

    static void Next(Step to)
    {
        step = to;
        stepStarted = EditorApplication.timeSinceStartup;
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failed = true;
    }

    static void Pass(string what)
    {
        report.Add("  ----  " + what);
    }

    static void Fail(string why)
    {
        report.Add("  FAIL  " + why);
        failed = true;
        Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);

        string startScenePath = SessionState.GetString(StartSceneKey, "");
        SessionState.SetString(StartSceneKey, "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startScenePath)
            ? null
            : AssetDatabase.LoadAssetAtPath<SceneAsset>(startScenePath);

        string body = "Floorplan story test: " + (failed ? "FAILED" : "passed") + "\n" + string.Join("\n", report);
        if (failed) Debug.LogError(body);
        else Debug.Log(body);

        menu = null; run = null; director = null; objective = null; player = null; playerBody = null;
        EditorApplication.isPlaying = false;
    }
}
