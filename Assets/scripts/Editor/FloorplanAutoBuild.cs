using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the floorplan tools without a menu click. Two triggers:
///
/// 1. Request file. Write words into Library/FloorplanRequest.txt, then give the Unity window
///    focus so scripts reload. The file is consumed once. Words: "build" runs
///    Tools/Floorplan/Build Scene, "playtest" runs Tools/Floorplan/Play Test afterwards,
///    "scene=Assets/Scenes/x.unity" picks the scene (default Assets/Scenes/main.unity).
///
/// 2. Command line, with the editor closed:
///    Unity.exe -batchmode -nographics -projectPath "..." -executeMethod FloorplanAutoBuild.RunFromCommandLine
///              -floorplanRequest "build playtest" -logFile build.log
///    The process exits by itself: 0 on success, 1 when the play test fails.
/// </summary>
public static class FloorplanAutoBuild
{
    const string RequestFile = "Library/FloorplanRequest.txt";
    const string DefaultScenePath = "Assets/Scenes/main.unity";
    const string CommandLineFlag = "-floorplanRequest";

    /// <summary>Project root. The editor's working directory is not reliably the project folder.</summary>
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    static string RequestPath => Path.Combine(ProjectRoot, RequestFile);

    struct Request
    {
        public bool refresh;
        public bool build;
        public bool playtest;
        public float seconds;
        public string scenePath;
    }

    const double PollIntervalSeconds = 2.0;
    /// <summary>A consumed request waits here until it runs, so a domain reload in between cannot drop it.</summary>
    const string PendingKey = "Floorplan.AutoBuild.Pending";
    static double nextPollTime;

    [InitializeOnLoadMethod]
    static void OnScriptsReloaded()
    {
        // Poll from the update loop as well, so a request written while the editor is idle
        // is picked up without needing a script reload or a window focus change.
        EditorApplication.update -= PollRequestFile;
        EditorApplication.update += PollRequestFile;

        string pending = SessionState.GetString(PendingKey, "");
        if (!string.IsNullOrEmpty(pending))
        {
            Debug.Log("Floorplan: resuming request interrupted by a script reload.");
            Queue(Parse(pending));
            return;
        }
        ConsumeRequestFile();
    }

    static void PollRequestFile()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPollTime) return;
        nextPollTime = now + PollIntervalSeconds;
        if (FloorplanPlaytest.IsActive) return;
        ConsumeRequestFile();
    }

    static void ConsumeRequestFile()
    {
        string path = RequestPath;
        if (!File.Exists(path)) return;

        string text;
        try
        {
            text = File.ReadAllText(path);
            File.Delete(path);
        }
        catch (IOException)
        {
            return; // still being written; the next poll will get it
        }

        SessionState.SetString(PendingKey, text);
        Queue(Parse(text));
    }

    /// <summary>Entry point for -executeMethod. Reads the request from -floorplanRequest, default "build".</summary>
    public static void RunFromCommandLine()
    {
        string text = "build";
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == CommandLineFlag) text = args[i + 1];
        }
        Queue(Parse(text));
    }

    static Request Parse(string text)
    {
        var request = new Request { scenePath = DefaultScenePath };
        char[] separators = { ' ', '\t', '\r', '\n', ',', ';' };
        foreach (string token in text.Split(separators, System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("refresh", System.StringComparison.OrdinalIgnoreCase)) request.refresh = true;
            else if (token.Equals("build", System.StringComparison.OrdinalIgnoreCase)) request.build = true;
            else if (token.Equals("playtest", System.StringComparison.OrdinalIgnoreCase)) request.playtest = true;
            else if (token.StartsWith("scene=", System.StringComparison.OrdinalIgnoreCase)) request.scenePath = token.Substring(6);
            else if (token.StartsWith("seconds=", System.StringComparison.OrdinalIgnoreCase))
            {
                float.TryParse(token.Substring(8), System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out request.seconds);
            }
        }
        return request;
    }

    static void Queue(Request request)
    {
        if (request.refresh)
        {
            // Recompiles changed scripts. Anything else in the same request is dropped because the
            // domain reload that follows would discard it; send it as a second request.
            SessionState.SetString(PendingKey, "");
            Debug.Log("Floorplan: auto-run: refreshing the asset database.");
            AssetDatabase.Refresh();
            return;
        }

        if (!request.build && !request.playtest)
        {
            // Drop it, or every later script reload would re-read it from SessionState and warn again.
            SessionState.SetString(PendingKey, "");
            Debug.LogWarning("Floorplan: auto-run request contained neither 'build' nor 'playtest'.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        EditorApplication.delayCall += () => RunWhenReady(request);
    }

    static void RunWhenReady(Request request)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += () => RunWhenReady(request);
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("Floorplan: auto-run requested; leaving play mode first.");

            void ResumeInEditMode(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.playModeStateChanged -= ResumeInEditMode;
                EditorApplication.delayCall += () => RunWhenReady(request);
            }

            EditorApplication.playModeStateChanged += ResumeInEditMode;
            EditorApplication.isPlaying = false;
            return;
        }

        Run(request);
    }

    static void Run(Request request)
    {
        SessionState.SetString(PendingKey, "");

        if (!EnsureSceneOpen(request.scenePath))
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        if (request.build)
        {
            Debug.Log("Floorplan: auto-run: Build Scene.");
            FloorplanSetup.BuildScene();
        }

        if (request.playtest)
        {
            Debug.Log("Floorplan: auto-run: Play Test.");
            FloorplanPlaytest.Start(request.seconds);
        }
        else if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }

    /// <summary>Opens the requested scene unless it is already active. Never discards unsaved work.</summary>
    static bool EnsureSceneOpen(string scenePath)
    {
        Scene active = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scenePath) || active.path == scenePath) return true;

        if (!File.Exists(Path.Combine(ProjectRoot, scenePath)))
        {
            Debug.LogError($"Floorplan: scene '{scenePath}' does not exist.");
            return false;
        }

        if (active.isDirty && !string.IsNullOrEmpty(active.path))
        {
            Debug.LogError($"Floorplan: '{active.path}' has unsaved changes, so '{scenePath}' was not opened. " +
                           "Save or discard them, then run again.");
            return false;
        }

        Debug.Log($"Floorplan: auto-run: opening {scenePath}.");
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        return SceneManager.GetActiveScene().path == scenePath;
    }
}
