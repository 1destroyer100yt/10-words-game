using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates the dedicated opening scene without regenerating or editing the game level.</summary>
public static class StartupSetup
{
    public const string IntroPath = "Assets/Scenes/Intro.unity";

    // Unity's play-mode start scene is session state; restore it after an editor restart.
    [InitializeOnLoadMethod]
    static void RestorePlayEntryPoint()
    {
        EditorApplication.delayCall += () =>
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath);
            if (scene != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.playModeStartScene = scene;
        };
    }

    [MenuItem("Tools/Floorplan/Configure Startup Flow")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath) == null) CreateIntro();
        else UpdateStoryIcons();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene(IntroPath, true),
            new EditorBuildSettingsScene("Assets/Scenes/main.unity", true)
        };
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Startup flow: Intro -> main title -> game. Editor Play also starts with Intro.");
    }

    static void CreateIntro()
    {
        Scene original = SceneManager.GetActiveScene();
        Scene introScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(introScene);
        try
        {
            var camera = new GameObject("Intro Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.gameObject.AddComponent<AudioListener>();
            var canvas = new GameObject("Opening explanation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var intro = canvas.gameObject.AddComponent<IntroSequence>();
            intro.pixel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Floorplan/Pixel.png");
            intro.gem = Icon("Gem");
            intro.ring = Icon("Ring");
            intro.demon = Icon("DemonMark");
            intro.mouse = Icon("Mouse");
            intro.keyCap = Icon("KeyCap");
            intro.arrow = Icon("ArrowRight");
            intro.clue = Icon("ClueMark");
            intro.activator = Icon("ActivatorMark");
            intro.actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            intro.letters = AssetDatabase.LoadAllAssetsAtPath("Assets/Floorplan/Icons/Letters.png")
                .OfType<Sprite>().OrderBy(s => GlyphIndex(s.name)).ToArray();
            if (intro.pixel == null || intro.gem == null || intro.letters.Length != 26)
                throw new InvalidOperationException("Intro requires the existing floorplan pixel, gem and alphabet assets.");
            if (!EditorSceneManager.SaveScene(introScene, IntroPath)) throw new InvalidOperationException("Could not save Intro scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(introScene, true);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
        }
    }

    static void UpdateStoryIcons()
    {
        Scene original = SceneManager.GetActiveScene();
        Scene introScene = SceneManager.GetSceneByPath(IntroPath);
        bool opened = !introScene.IsValid() || !introScene.isLoaded;
        if (opened) introScene = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in introScene.GetRootGameObjects())
            foreach (IntroSequence intro in root.GetComponentsInChildren<IntroSequence>(true))
            {
                Undo.RecordObject(intro, "Update intro story icons");
                intro.clue = Icon("ClueMark");
                intro.activator = Icon("ActivatorMark");
                EditorSceneManager.MarkSceneDirty(introScene);
            }
            if (!EditorSceneManager.SaveScene(introScene)) throw new InvalidOperationException("Could not save updated Intro scene.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(introScene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
    }

    static int GlyphIndex(string name)
    {
        int underscore = name.LastIndexOf('_');
        return int.TryParse(name.Substring(underscore + 1), out int value) ? value : 0;
    }

    static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Floorplan/Icons/" + name + ".png");
}
