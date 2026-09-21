using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Points the project at the browser. The scene list, the player settings and the active platform
/// are all set from one place so a fresh clone can be made web-ready in a single menu click, and so
/// the settings that actually matter for a static host (no threads, a decompression fallback) are
/// written down rather than remembered.
/// </summary>
public static class WebBuild
{
    const string ConfigurePath = "Tools/Floorplan/Configure Web Build";
    const string BuildPath = "Tools/Floorplan/Build Web Player";

    const string MainScene = "Assets/Scenes/main.unity";
    const string Company = "Andrew";
    const string Product = "Jewel of the Devil";
    const string Version = "1.0";

    /// <summary>Where the player lands; kept beside the project rather than inside Assets.</summary>
    static string OutputDirectory =>
        Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "Web");

    [MenuItem(ConfigurePath)]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new BuildFailedException("Stop Play mode before configuring a Web build.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Install WebGL Build Support for this editor through Unity Hub.");
        ConfigureScenes();
        ConfigureIdentity();
        ConfigureWeb();
        AssetDatabase.SaveAssets();

        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
        {
            Debug.Log("Web build: configured. The platform is already WebGL.");
            return;
        }

        Debug.Log($"Web build: configured. Switching the platform from " +
                  $"{EditorUserBuildSettings.activeBuildTarget} to WebGL; this reimports the project " +
                  "and takes a while.");
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Unity could not switch to WebGL. Check the editor console.");
    }

    /// <summary>The explanation loads first, then the real level with its title menu.</summary>
    static void ConfigureScenes()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
        {
            throw new BuildFailedException($"Web build: required scene {MainScene} is missing.");
        }

        StartupSetup.Configure();
        Debug.Log($"Web build: {StartupSetup.IntroPath}, then {MainScene}.");
    }

    static void ConfigureIdentity()
    {
        PlayerSettings.companyName = Company;
        PlayerSettings.productName = Product;
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.runInBackground = true;
    }

    static void ConfigureWeb()
    {
        var web = NamedBuildTarget.WebGL;

        // A 16:9 canvas. The menu and the HUD both lay themselves out from the window size, so this
        // is a starting shape rather than a requirement.
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        // Gzip with a JavaScript fallback decompressor: this runs on a plain static host with no
        // special response headers, which Brotli would need.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "PROJECT:TheDebt";

        // Threads stay off. They would require SharedArrayBuffer, which needs cross-origin isolation
        // headers most game hosts do not send; A* falls back to single-threaded scanning anyway.
        PlayerSettings.WebGL.threadsSupport = false;

        // Enough to say which script threw without paying for full stack unwinding.
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.showDiagnostics = false;

        // WebGL 2 only. WebGPU is still uneven across browsers and this game asks nothing of it.
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });

        // A* reaches for types by name in a few places, so nothing above Low.
        PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Low);
        PlayerSettings.SetIl2CppCompilerConfiguration(web, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);
    }

    [MenuItem(BuildPath)]
    public static void BuildPlayer()
    {
        Configure();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            Debug.LogError("Web build: the platform switch has not finished yet. Run this again once " +
                           "the editor is idle.");
            return;
        }

        Directory.CreateDirectory(OutputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { StartupSetup.IntroPath, MainScene },
            locationPathName = OutputDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        UnityEditor.Build.Reporting.BuildSummary summary = report.summary;
        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Web build: succeeded in {summary.totalTime}. " +
                      $"{summary.totalSize / (1024 * 1024)} MB at {OutputDirectory}");
        }
        else
        {
            throw new BuildFailedException($"Web build: {summary.result} with {summary.totalErrors} errors.");
        }
    }
}
