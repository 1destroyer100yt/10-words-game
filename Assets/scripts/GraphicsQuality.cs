using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The player's graphics setting, chosen on the title and pause screens and saved like the volume.
/// Every quality level in this project shares one render pipeline asset, so switching Unity's quality
/// level changes almost nothing here. What costs the most is the 2D lighting, drawn at the render
/// resolution, so the setting lowers that resolution and scales it back up with sharp pixels, which
/// keeps the pixel art crisp rather than blurred. The menu and HUD are overlays and stay full size.
/// </summary>
public static class GraphicsQuality
{
    const string Key = "Floorplan.Quality";

    /// <summary>Low, Medium, High.</summary>
    public const int Levels = 3;

    static readonly float[] RenderScales = { 0.5f, 0.75f, 1f };

#if UNITY_EDITOR
    static bool savedOriginal;
    static float originalScale;
    static UpscalingFilterSelection originalFilter;
#endif

    /// <summary>0 = low, Levels - 1 = high. High by default: the game as it was drawn.</summary>
    public static int Level
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(Key, Levels - 1), 0, Levels - 1);
        set
        {
            PlayerPrefs.SetInt(Key, Mathf.Clamp(value, 0, Levels - 1));
            PlayerPrefs.Save();
            Apply();
        }
    }

    /// <summary>One step down, wrapping from low back to high.</summary>
    public static void Cycle()
    {
        Level = Level == 0 ? Levels - 1 : Level - 1;
    }

    public static void Apply()
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return;
#if UNITY_EDITOR
        // In the editor this is the real asset on disk; put it back when play ends.
        if (!savedOriginal)
        {
            savedOriginal = true;
            originalScale = asset.renderScale;
            originalFilter = asset.upscalingFilter;
            UnityEditor.EditorApplication.playModeStateChanged += RestoreOnExit;
        }
#endif
        asset.renderScale = RenderScales[Level];
        asset.upscalingFilter = UpscalingFilterSelection.Point;
    }

#if UNITY_EDITOR
    static void RestoreOnExit(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
        UnityEditor.EditorApplication.playModeStateChanged -= RestoreOnExit;
        savedOriginal = false;
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) return;
        asset.renderScale = originalScale;
        asset.upscalingFilter = originalFilter;
    }
#endif
}
