using System.Collections.Generic;
using Pathfinding;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click scene builder for the floorplan map: Tools/Floorplan/Build Scene.
/// Creates the Walls layer, configures the floorplan importer, bakes wall colliders,
/// sets up and scans an A* grid graph, spawns a wandering NPC and frames the camera.
/// </summary>
public static partial class FloorplanSetup
{
    const string MenuPath = "Tools/Floorplan/Build Scene";
    const string UndoLabel = "Build Floorplan Scene";

    const string WallLayerName = "Walls";
    const string PreferredMapPath = "Assets/Art/jewelofthedevil.png";
    const string MapAssetName = "jewelofthedevil";
    const string NpcSpriteAssetName = "demonguy";
    const string BuiltinFallbackSprite = "UI/Skin/Knob.psd";
    const string UrpUnlitSpriteMaterial =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    /// <summary>Everything the darkness is allowed to hide uses this; the minimap and icons stay unlit.</summary>
    const string UrpLitSpriteMaterial =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

    const string BackdropName = "Floorplan";
    const string WallsName = "Walls";
    const string PathfinderName = "Pathfinder";
    const string NpcName = "NPC";

    const string PlayerName = "Player";
    const string PlayerFolder = "Assets/Player";
    const string PlayerControllerPath = "Assets/Player/Player.controller";
    const string PlayerIdleClipPath = "Assets/Player/Player_Idle.anim";
    const string PlayerBoostClipPath = "Assets/Player/Player_Boost.anim";
    const string PlayerSprintClipPath = "Assets/Player/Player_Sprint.anim";
    const string PlayerCaughtClipPath = "Assets/Player/Player_Caught.anim";
    const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
    const string SpriteChildName = "Sprite";
    const float PlayerVisualSize = 1.8f;
    const float PlayerMoveSpeed = 8f;
    const float PlayerAnimationFps = 10f;
    const int BoostFrameCount = 6;
    const int IdleFrameCount = 8;
    /// <summary>The player spawns on the walkable node nearest this point inside the building.</summary>
    static readonly Vector3 PlayerPreferredSpawn = new Vector3(30f, 55f, 0f);
    static readonly Color32 PlayerMarkerColor = new Color32(0, 0, 0, 255);

    // Enemy animation: sprite strips under Assets/Enemy named with a state word are used when present,
    // otherwise procedural motion clips animate the single demonguy sprite.
    const string EnemyFolder = "Assets/Enemy";
    const string EnemyControllerPath = "Assets/Enemy/Enemy.controller";
    const float EnemyAnimationFps = 8f;
    static readonly string[] EnemyStates = { "Idle", "Walk", "Chase", "Search", "Alert" };
    static readonly string[][] EnemyStateKeywords =
    {
        new[] { "idle", "stand" },
        new[] { "walk", "wander", "move" },
        new[] { "chase", "run", "attack" },
        new[] { "search", "look", "confused" },
        new[] { "alert", "suspicious", "spot" },
    };

    // NPC vision and chase
    const string VisionName = "Vision";
    const float NpcViewAngle = 70f;
    const float NpcViewDistance = 12f;
    const float NpcChaseSpeed = 7f;
    const string VisionConeMaterialPath = "Assets/Floorplan/VisionCone.mat";
    const string UrpSpriteUnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    const string FallbackSpriteShader = "Sprites/Default";

    // Player camera post-processing
    const string PostProcessingName = "Post Processing Volume";
    const string PostProcessingProfilePath = "Assets/Floorplan/PlayerCamera.asset";
    const float VignetteIntensity = 0.58f;
    const float VignetteSmoothness = 0.62f;

    const float PixelsPerUnit = 7f;
    const float DarkThreshold = 0.15f;
    const int ExpectedWallCount = 518;

    const int GraphWidth = 480;
    const int GraphDepth = 412;
    const float NodeSize = 0.2f;
    static readonly Vector3 GraphCenter = new Vector3(48f, 41.15f, 0f);

    /// <summary>2 px at 7 px per unit. Hard ceiling: 3 px fragments the interior into islands.</summary>
    public const float AgentRadius = 2f / PixelsPerUnit;

    const float NpcMaxSpeed = 6f;
    const float NpcEndReachedDistance = 0.2f;
    /// <summary>
    /// AIPath's default of 2 units steers across thin walls; keep it under the corridor width.
    /// Measured: 0.5 never put the agent's centre inside a wall; 0.3 let it overshoot corners into walls.
    /// </summary>
    const float NpcPickNextWaypointDist = 0.5f;
    const float NpcSlowdownDistance = 0.6f;
    /// <summary>
    /// World size of the NPC's visible pixels (its canvas is mostly transparent). The camera shows the
    /// whole 82-unit-tall building, so anything under about 2 units is a speck; walls are 0.86 units.
    /// </summary>
    const float NpcVisualSize = 1.8f;
    const string NpcVisualName = "Visual";

    /// <summary>Main camera zoom while following the player (half the visible height in units).</summary>
    const float CameraFollowZoom = 12f;
    const float CameraFollowSmoothTime = 0.15f;
    /// <summary>Whole-building view, used only when there is no player to follow.</summary>
    const float CameraSize = 41f;
    static readonly Vector3 CameraPosition = new Vector3(48f, 41f, -10f);

    const string MinimapCameraName = "Minimap Camera";
    const string MinimapLayerName = "Minimap";
    const string MinimapMarkerName = "Minimap Marker";
    /// <summary>Side of the square minimap as a fraction of the screen height; MinimapViewport keeps it 1:1.</summary>
    const float MinimapSize = 0.28f;
    const float MinimapMargin = 0.01f;
    /// <summary>Black frame drawn inside the minimap, in world units (walls are 0.86).</summary>
    const float MinimapFrame = 1f;
    /// <summary>Grey gap between the frame and the map, in world units.</summary>
    const float MinimapInset = 0.5f;
    const string MinimapFrameName = "Minimap Frame";
    const string MinimapPanelName = "Minimap Panel";
    const string MinimapMapName = "Minimap Map";

    /// <summary>Set while building, for the feature steps that need them (see FloorplanSetup.Features.cs).</summary>
    static Camera MinimapCamera;

    /// <summary>The one enemy animator every demon shares; cleared at the start of each build.</summary>
    static RuntimeAnimatorController EnemyController;
    static readonly List<GameObject> NpcMarkers = new List<GameObject>();

    /// <summary>How many demons the scene holds. The menu decides how many are awake.</summary>
    const int MaxDemons = 3;

    /// <summary>Each demon starts near a different part of the building.</summary>
    static readonly Vector3[] DemonSpawnHints =
    {
        new Vector3(48f, 41f, 0f),
        new Vector3(72f, 62f, 0f),
        new Vector3(24f, 24f, 0f),
    };
    /// <summary>The floor grey of the floorplan image, so the panel and the map blend seamlessly.</summary>
    static readonly Color32 MinimapPanelColor = new Color32(70, 70, 70, 255);
    static readonly Color MinimapBackground = Color.black;
    const string PixelSpritePath = "Assets/Floorplan/Pixel.png";
    /// <summary>The red used by the NPC sprite.</summary>
    static readonly Color32 MinimapMarkerColor = new Color32(237, 28, 36, 255);
    /// <summary>World size of the NPC dot on the minimap.</summary>
    const float MinimapMarkerSize = 3f;

    [MenuItem(MenuPath)]
    public static void BuildScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Floorplan: exit play mode before building the scene.");
            return;
        }

        // These survive between builds; a build that skips the minimap would otherwise wire the HUD
        // to the camera from the previous one.
        MinimapCamera = null;
        NpcMarkers.Clear();
        EnemyController = null;

        int wallLayer = EnsureLayer(WallLayerName);
        if (wallLayer < 0) return;

        string mapPath = FindTexturePath(PreferredMapPath, MapAssetName);
        if (mapPath == null)
        {
            Debug.LogError($"Floorplan: could not find '{MapAssetName}.png' under Assets. Expected it at {PreferredMapPath}.");
            return;
        }

        if (!ConfigureFloorplanImporter(mapPath)) return;

        var map = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
        var mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(mapPath);
        if (map == null || mapSprite == null)
        {
            Debug.LogError($"Floorplan: '{mapPath}' did not import as a sprite texture.");
            return;
        }

        // Lit for the world, so the player's light and the wall shadows decide what is visible;
        // unlit for the minimap and the icons, which must read whatever the lighting is doing.
        var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpUnlitSpriteMaterial);
        var litMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpLitSpriteMaterial);
        if (litMaterial == null)
        {
            Debug.LogWarning("Floorplan: the URP lit sprite material was not found; the map will not be darkened.");
            litMaterial = unlitMaterial;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveExistingObjects();

        CreateBackdrop(mapSprite, litMaterial);

        int wallCount = CreateWalls(map, wallLayer, out GameObject walls);
        if (wallCount != ExpectedWallCount)
        {
            Debug.LogError($"Floorplan: expected {ExpectedWallCount} wall colliders but baked {wallCount}. " +
                           "Check that the texture is 672x576, two-tone, uncompressed and Read/Write enabled.");
        }

        GridGraph graph = CreatePathfinder(wallLayer);
        if (graph == null) return;

        int minimapLayer = EnsureLayer(MinimapLayerName);

        GameObject player = CreatePlayer(graph, litMaterial);

        // Every demon the building can hold is built now; DemonCount switches off the unused ones.
        var npcs = new List<GameObject>();
        for (int i = 0; i < MaxDemons; i++)
        {
            string name = i == 0 ? NpcName : $"{NpcName} {i + 1}";
            npcs.Add(CreateNpc(graph, litMaterial, player, wallLayer, minimapLayer, name, DemonSpawnHints[i]));
        }
        GameObject npc = npcs[0];

        Camera mainCamera = ConfigureCamera(player, map);
        GameObject volume = CreatePostProcessing(mainCamera);

        if (minimapLayer >= 0) CreateMinimap(minimapLayer, npcs, player, unlitMaterial, map, mapSprite);
        else Debug.LogWarning("Floorplan: minimap skipped because the Minimap layer could not be created.");

        BuildFeatures(new FeatureContext
        {
            graph = graph,
            map = map,
            walls = walls,
            player = player,
            npc = npc,
            npcs = npcs,
            mainCamera = mainCamera,
            volume = volume,
            wallLayer = wallLayer,
            minimapLayer = minimapLayer,
            lit = litMaterial,
            unlit = unlitMaterial,
        });

        Undo.CollapseUndoOperations(undoGroup);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);

        Debug.Log($"Floorplan: scene built. {wallCount} wall colliders, {GraphWidth}x{GraphDepth} grid graph scanned, NPC placed.");
    }

    // ------------------------------------------------------------------ a. layer

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
        {
            Debug.LogError(LayerFailureMessage(layerName, "TagManager.asset could not be loaded"));
            return -1;
        }

        var tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogError(LayerFailureMessage(layerName, "the layers array was not found in TagManager.asset"));
            return -1;
        }

        // Layers 0-7 are reserved by Unity; user layers start at 8.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;
            slot.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            break;
        }

        int created = LayerMask.NameToLayer(layerName);
        if (created < 0) Debug.LogError(LayerFailureMessage(layerName, "no free user layer slot was available"));
        return created;
    }

    static string LayerFailureMessage(string layerName, string reason)
    {
        return $"Floorplan: could not create the '{layerName}' layer ({reason}). Add it manually under " +
               $"Edit > Project Settings > Tags and Layers, then run {MenuPath} again.";
    }

    // ------------------------------------------------------------------ b. importer

    static string FindTexturePath(string preferredPath, string assetName)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPath) != null) return preferredPath;

        foreach (string guid in AssetDatabase.FindAssets($"{assetName} t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName) return path;
        }
        return null;
    }

    static bool ConfigureFloorplanImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Floorplan: '{path}' has no TextureImporter.");
            return false;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.mipmapEnabled = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        settings.spritePivot = Vector2.zero;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
        return true;
    }

    // ------------------------------------------------------------------ scene objects

    /// <summary>
    /// Root objects replaced on every build: this script's own objects plus the leftovers of the
    /// earlier manual setup (grid, pathfinder, sprite-named objects). Compared case-insensitively.
    /// </summary>
    static readonly string[] ReplacedObjectNames =
    {
        BackdropName, WallsName, PathfinderName, NpcName, PlayerName, MinimapCameraName, PostProcessingName,
        GameName, HudCanvasName, SecurityCamerasName, HidingSpotsName, GlobalLightName, IndicatorsName,
        ObjectiveName, AudioName, DemonsName,
        StoryDirectorName, CluesName, ZoneMarksName, ActivatorName,
        "NPC 2", "NPC 3", "NPC 4",
        "grid", "jewelofthedevil_0", "demonguy_0",
    };

    static void RemoveExistingObjects()
    {
        var doomed = new HashSet<GameObject>();

        foreach (AstarPath astar in Object.FindObjectsByType<AstarPath>(FindObjectsInactive.Include))
        {
            doomed.Add(astar.gameObject);
        }

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root == null) continue;
            string lowerName = root.name.ToLowerInvariant();
            foreach (string replaced in ReplacedObjectNames)
            {
                if (lowerName != replaced.ToLowerInvariant()) continue;
                doomed.Add(root);
                break;
            }
        }

        foreach (GameObject go in doomed)
        {
            Debug.Log($"Floorplan: removing previous object '{go.name}'.");
            Undo.DestroyObjectImmediate(go);
        }

        // Anything else still carrying a deleted script (the old FloorplanGrid / GridWanderer)
        // keeps its object but loses the dead component, which silences the missing-script errors.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root == null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0) continue;
            Undo.RegisterFullObjectHierarchyUndo(root, UndoLabel);
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            Debug.Log($"Floorplan: removed {removed} missing-script component(s) from '{root.name}'.");
        }
    }

    static GameObject CreateRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        go.transform.position = Vector3.zero;
        return go;
    }

    // c. backdrop
    static void CreateBackdrop(Sprite sprite, Material material)
    {
        GameObject go = CreateRoot(BackdropName);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -10;
        if (material != null) renderer.sharedMaterial = material;
    }

    // d. walls
    static int CreateWalls(Texture2D map, int wallLayer, out GameObject go)
    {
        go = CreateRoot(WallsName);
        go.layer = wallLayer;

        var baker = go.AddComponent<WallColliderBaker>();
        baker.map = map;
        baker.pixelsPerUnit = PixelsPerUnit;
        baker.darkThreshold = DarkThreshold;
        baker.wallLayer = WallLayerName;
        baker.Bake();
        return baker.LastBakeCount;
    }

    // e. pathfinder
    static GridGraph CreatePathfinder(int wallLayer)
    {
        GameObject go = CreateRoot(PathfinderName);
        var astar = go.AddComponent<AstarPath>();

        // In edit mode AstarPath.Awake returns before initializing its data, so do it here.
        AstarPath.active = astar;
        astar.ConfigureReferencesInternal();
        astar.data.FindGraphTypes();

        var graph = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
        if (graph == null)
        {
            Debug.LogError("Floorplan: could not add a GridGraph to the AstarPath component.");
            return null;
        }

        graph.name = "Floorplan";
        graph.is2D = true;
        graph.center = GraphCenter;
        graph.SetDimensions(GraphWidth, GraphDepth, NodeSize);
        graph.neighbours = NumNeighbours.Eight;
        graph.cutCorners = true; // measured: disabling it made corner turns sharper and worsened wall contact
        graph.erodeIterations = 0;

        GraphCollision collision = graph.collision;
        collision.use2D = true;
        collision.collisionCheck = true;
        collision.type = ColliderType.Sphere;
        // GraphCollision measures diameter in node-size units: world radius = diameter * nodeSize / 2.
        collision.diameter = AgentRadius * 2f / NodeSize;
        collision.mask = 1 << wallLayer;
        collision.heightCheck = false;

        // The wall colliders were created this frame; make sure the 2D physics world sees them.
        Physics2D.SyncTransforms();
        astar.Scan();

        int walkable = 0;
        graph.GetNodes(node => { if (node.Walkable) walkable++; });
        Debug.Log($"Floorplan: grid graph scanned, {walkable} walkable of {graph.nodes.Length} nodes.");

        // Persist the graph settings in the scene the same way the AstarPath inspector does.
        astar.data.SetData(astar.data.SerializeGraphs());
        EditorUtility.SetDirty(astar);
        return graph;
    }

    // f. NPC
    static GameObject CreateNpc(GridGraph graph, Material material, GameObject player, int wallLayer, int minimapLayer,
                               string name, Vector3 preferredSpawn)
    {
        GameObject go = CreateRoot(name);

        // Visual (scaled) > Sprite (renderer + animator, offset so the pixels sit on the agent).
        // The root is moved and rotated by AIPath, so the animation clips only touch the Sprite child.
        EnsureFolder(EnemyFolder); // drop enemy animation strips here
        Dictionary<string, Sprite[]> enemyFrames = FindEnemyStrips();
        Sprite sprite = enemyFrames.TryGetValue("Idle", out Sprite[] idleFrames) && idleFrames.Length > 0
            ? idleFrames[0]
            : FindNpcSprite();

        var visual = new GameObject(NpcVisualName);
        visual.transform.SetParent(go.transform, false);
        var spriteGo = new GameObject(SpriteChildName);
        spriteGo.transform.SetParent(visual.transform, false);
        var renderer = spriteGo.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 0;
        if (material != null) renderer.sharedMaterial = material;
        // Size and centre from the original demon sprite when there is one. The animation strips are
        // drawn on its exact 100 px grid, but their flame length changes frame to frame, so measuring
        // a strip frame instead would make the demon grow or shrink whenever the art is redrawn.
        Sprite original = FindNpcSprite();
        Sprite sizing = original != null && original.name.StartsWith(NpcSpriteAssetName) ? original : sprite;
        if (sizing != null && sprite != null)
        {
            Bounds visible = VisibleBounds(sizing);
            float size = Mathf.Max(visible.size.x, visible.size.y);
            if (size > 0f)
            {
                float scale = NpcVisualSize / size;
                visual.transform.localScale = new Vector3(scale, scale, 1f);
                // Centre on the frame that is actually drawn: the original is cropped tight to the
                // figure, but a strip frame is a full cell with the figure sitting off its middle.
                spriteGo.transform.localPosition = -VisibleBounds(sprite).center;
            }
        }

        var npcAnimator = spriteGo.AddComponent<Animator>();
        // Built once per scene build and shared. BuildEnemyAnimator deletes and recreates the asset, and
        // a recreated asset gets a new GUID, so building it per demon left every demon but the last
        // pointing at a deleted controller: the first demon, the one always awake, had no animation.
        if (EnemyController == null) EnemyController = BuildEnemyAnimator(enemyFrames);
        npcAnimator.runtimeAnimatorController = EnemyController;

        go.AddComponent<Seeker>();

        var ai = go.AddComponent<AIPath>();
        ai.orientation = OrientationMode.YAxisForward;
        ai.gravity = Vector3.zero;
        ai.maxSpeed = NpcMaxSpeed;
        ai.endReachedDistance = NpcEndReachedDistance;
        ai.radius = AgentRadius;
        ai.pickNextWaypointDist = NpcPickNextWaypointDist;
        ai.slowdownDistance = NpcSlowdownDistance;

        go.AddComponent<FunnelModifier>();
        go.AddComponent<AstarWanderer>();

        // Vision cone: a child mesh on the minimap layer, so the main view never shows it.
        var visionGo = new GameObject(VisionName);
        visionGo.layer = minimapLayer >= 0 ? minimapLayer : go.layer;
        visionGo.transform.SetParent(go.transform, false);
        visionGo.AddComponent<MeshFilter>();
        var coneRenderer = visionGo.AddComponent<MeshRenderer>();
        coneRenderer.sharedMaterial = EnsureVisionConeMaterial();
        coneRenderer.sortingOrder = 50;
        coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        coneRenderer.receiveShadows = false;
        var vision = visionGo.AddComponent<NpcVision>();
        vision.target = player != null ? player.transform : null;
        vision.viewAngle = NpcViewAngle;
        vision.viewDistance = NpcViewDistance;
        vision.wallMask = 1 << wallLayer;
        vision.idleColor = new Color(MinimapMarkerColor.r / 255f, MinimapMarkerColor.g / 255f, MinimapMarkerColor.b / 255f, 0.6f);
        vision.alertColor = new Color(MinimapMarkerColor.r / 255f, MinimapMarkerColor.g / 255f, MinimapMarkerColor.b / 255f, 1f);

        var chaser = go.AddComponent<NpcChaser>();
        chaser.vision = vision;
        chaser.chaseSpeed = NpcChaseSpeed;

        var animatorDriver = go.AddComponent<NpcAnimator>();
        animatorDriver.animator = npcAnimator;
        animatorDriver.chaser = chaser;

        go.transform.position = FindSpawnPosition(graph, preferredSpawn);
        return go;
    }

    // ------------------------------------------------------------------ enemy animation

    /// <summary>
    /// Strips under Assets/Enemy (or anywhere, when the file name mentions enemy/npc/demon) whose
    /// name also contains a state word: idle, walk, chase, search. Returns sliced frames per state.
    /// </summary>
    static Dictionary<string, Sprite[]> FindEnemyStrips()
    {
        var result = new Dictionary<string, Sprite[]>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/AstarPathfindingProject") || path.StartsWith("Assets/Welcome")) continue;
            if (path.StartsWith(PlayerFolder + "/")) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool inEnemyFolder = path.StartsWith(EnemyFolder + "/");
            bool enemyNamed = name.Contains("enemy") || name.Contains("npc") || name.Contains("demon");
            if (!inEnemyFolder && !enemyNamed) continue;
            if (name == "demonguy") continue; // the single-frame sprite, not a strip

            for (int state = 0; state < EnemyStates.Length; state++)
            {
                if (result.ContainsKey(EnemyStates[state])) continue;
                bool matches = false;
                foreach (string keyword in EnemyStateKeywords[state]) matches |= name.Contains(keyword);
                if (!matches) continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) break;
                Sprite[] frames = SliceStrip(texture);
                if (frames.Length > 0) result[EnemyStates[state]] = frames;
                break;
            }
        }
        return result;
    }

    /// <summary>
    /// Idle / Walk / Chase / Search animator driven by "Moving" (bool) and "Mode" (int: 0 wander,
    /// 1 chase, 2 search). Each state uses a sprite strip when one exists, otherwise a motion clip.
    /// </summary>
    static RuntimeAnimatorController BuildEnemyAnimator(Dictionary<string, Sprite[]> frames)
    {
        EnsureFolder(EnemyFolder);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string state in EnemyStates)
        {
            string path = $"{EnemyFolder}/Enemy_{state}.anim";
            string clipName = $"Enemy_{state}";
            clips[state] = frames.TryGetValue(state, out Sprite[] stateFrames)
                ? CreateSpriteClip(path, clipName, stateFrames, EnemyAnimationFps)
                : CreateEnemyMotionClip(path, clipName, state);
        }

        string used = string.Join(", ", System.Array.ConvertAll(EnemyStates, s => frames.ContainsKey(s) ? $"{s} (strip)" : $"{s} (motion)"));
        Debug.Log($"Floorplan: enemy animation states: {used}. Drop strips named with a state word into {EnemyFolder} to replace the motion clips.");

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(EnemyControllerPath);
        }
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(EnemyControllerPath);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Mode", AnimatorControllerParameterType.Int);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        foreach (string state in EnemyStates)
        {
            AnimatorState animatorState = machine.AddState(state);
            animatorState.motion = clips[state];
            states[state] = animatorState;
        }
        machine.defaultState = states["Idle"];

        // Any State picks the state from the two parameters; no transition may re-enter its own state.
        // Mode matches NpcChaser.Mode: 0 wander, 1 chase, 2 search, 3 suspicious.
        AddAnyStateTransition(machine, states["Idle"], 0, false);
        AddAnyStateTransition(machine, states["Walk"], 0, true);
        AddAnyStateTransition(machine, states["Chase"], 1, null);
        AddAnyStateTransition(machine, states["Search"], 2, null);
        AddAnyStateTransition(machine, states["Alert"], 3, null);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState target, int mode, bool? moving)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(target);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, mode, "Mode");
        if (moving.HasValue) transition.AddCondition(moving.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Moving");
    }

    /// <summary>
    /// Procedural motion for a single-frame sprite: breathing, walking bob, frantic chase pulse and a
    /// looking-around sweep. Animates only the Sprite child's own transform.
    /// </summary>
    static AnimationClip CreateEnemyMotionClip(string path, string name, string state)
    {
        var clip = new AnimationClip { name = name, frameRate = 30f };

        switch (state)
        {
            case "Walk":
                SetCurve(clip, "m_LocalPosition.y", Loop(0.5f, 0f, 0.06f));
                SetCurve(clip, "localEulerAnglesRaw.z", Loop(0.5f, -6f, 6f));
                break;
            case "Chase":
                SetCurve(clip, "m_LocalPosition.y", Loop(0.25f, 0f, 0.08f));
                SetCurve(clip, "m_LocalScale.x", Loop(0.25f, 1f, 1.12f));
                SetCurve(clip, "m_LocalScale.y", Loop(0.25f, 1f, 1.12f));
                break;
            case "Search":
                SetCurve(clip, "localEulerAnglesRaw.z", Sweep(1.6f, 35f));
                SetCurve(clip, "m_LocalScale.y", Loop(0.8f, 1f, 1.05f));
                break;
            case "Alert": // frozen mid-step, drawing itself up as the suspicion meter fills
                SetCurve(clip, "m_LocalScale.y", Loop(0.45f, 1.06f, 1.12f));
                SetCurve(clip, "m_LocalScale.x", Loop(0.45f, 1.06f, 1f));
                SetCurve(clip, "m_LocalPosition.y", Loop(0.45f, 0.02f, 0.05f));
                break;
            default: // Idle: slow breathing
                SetCurve(clip, "m_LocalScale.x", Loop(1.2f, 1f, 1.04f));
                SetCurve(clip, "m_LocalScale.y", Loop(1.2f, 1f, 0.96f));
                break;
        }

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void SetCurve(AnimationClip clip, string property, AnimationCurve curve)
    {
        var binding = new EditorCurveBinding { type = typeof(Transform), path = "", propertyName = property };
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    /// <summary>from -> to -> from over one period, smooth.</summary>
    static AnimationCurve Loop(float period, float from, float to)
    {
        var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(period * 0.5f, to), new Keyframe(period, from));
        Smooth(curve);
        return curve;
    }

    /// <summary>0 -> +amplitude -> 0 -> -amplitude -> 0 over one period, smooth.</summary>
    static AnimationCurve Sweep(float period, float amplitude)
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(period * 0.25f, amplitude), new Keyframe(period * 0.5f, 0f),
            new Keyframe(period * 0.75f, -amplitude), new Keyframe(period, 0f));
        Smooth(curve);
        return curve;
    }

    static void Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
    }

    /// <summary>Vertex-coloured, alpha-blended material for the vision cone mesh.</summary>
    static Material EnsureVisionConeMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(VisionConeMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find(UrpSpriteUnlitShader);
        if (shader == null) shader = Shader.Find(FallbackSpriteShader);
        if (shader == null)
        {
            Debug.LogWarning("Floorplan: no sprite shader found for the vision cone; using the default material.");
            return null;
        }

        EnsureFolder(System.IO.Path.GetDirectoryName(VisionConeMaterialPath).Replace('\\', '/'));
        var material = new Material(shader) { name = "VisionCone" };
        AssetDatabase.CreateAsset(material, VisionConeMaterialPath);
        return material;
    }

    // h2. player camera post-processing
    static GameObject CreatePostProcessing(Camera mainCamera)
    {
        if (mainCamera == null) return null;

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingProfilePath);
        if (profile == null)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(PostProcessingProfilePath).Replace('\\', '/'));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessingProfilePath);
        }

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
            vignette.hideFlags = HideFlags.HideInHierarchy; // stored inside the profile asset, as the URP UI does
            AssetDatabase.AddObjectToAsset(vignette, profile);
        }
        vignette.active = true;
        vignette.color.Override(Color.black);
        vignette.intensity.Override(VignetteIntensity);
        vignette.smoothness.Override(VignetteSmoothness);
        vignette.rounded.Override(false);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject go = CreateRoot(PostProcessingName);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        // Only the player camera gets post-processing; the minimap camera is left plain.
        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);
        }
        return go;
    }

    static Sprite FindNpcSprite()
    {
        foreach (string guid in AssetDatabase.FindAssets($"{NpcSpriteAssetName} t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != NpcSpriteAssetName) continue;
            ConfigureNpcImporter(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinFallbackSprite);
    }

    /// <summary>Pixel art settings plus Read/Write, which VisibleBounds needs. Sprite slicing is left alone.</summary>
    static void ConfigureNpcImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.isReadable && importer.filterMode == FilterMode.Point &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled) return;

        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Bounds of the sprite's opaque pixels in the sprite's local units, relative to its pivot.
    /// Falls back to the full sprite bounds when the texture is not readable.
    /// </summary>
    /// <param name="bodyOnly">Ignore pure red pixels: the player's jets, which flicker and would
    /// otherwise make its size depend on which frame happens to be first.</param>
    static Bounds VisibleBounds(Sprite sprite, bool bodyOnly = false)
    {
        Texture2D texture = sprite.texture;
        if (texture == null || !texture.isReadable) return sprite.bounds;

        Rect rect = sprite.rect;
        int x0 = Mathf.RoundToInt(rect.x);
        int y0 = Mathf.RoundToInt(rect.y);
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Color32[] pixels = texture.GetPixels32();
        int textureWidth = texture.width;

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = (y0 + y) * textureWidth + x0;
            for (int x = 0; x < width; x++)
            {
                Color32 pixel = pixels[row + x];
                if (pixel.a <= 8) continue;
                if (bodyOnly && pixel.r == 237 && pixel.g == 28 && pixel.b == 36) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < 0) return sprite.bounds;

        float ppu = sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot; // pixels from the rect's bottom-left
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3((minX - pivot.x) / ppu, (minY - pivot.y) / ppu, 0f),
                         new Vector3((maxX + 1 - pivot.x) / ppu, (maxY + 1 - pivot.y) / ppu, 0f));
        return bounds;
    }

    // h. minimap
    static void CreateMinimap(int minimapLayer, List<GameObject> npcs, GameObject player, Material material, Texture2D map, Sprite mapSprite)
    {
        // The main view must not draw the oversized marker.
        Camera main = Camera.main;
        if (main != null) main.cullingMask &= ~(1 << minimapLayer);

        GameObject cameraGo = CreateRoot(MinimapCameraName);
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.depth = (main != null ? main.depth : 0f) + 1f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = MinimapBackground;
        // Only the Minimap layer, so the darkness over the world never reaches the map.
        camera.cullingMask = 1 << minimapLayer;
        MinimapCamera = camera;

        // Centre on the floorplan image; the viewport component keeps the view square and sized to
        // the frame, so the whole map plus its border is always visible.
        var mapSize = new Vector2(map.width / PixelsPerUnit, map.height / PixelsPerUnit);
        var mapCentre = new Vector3(mapSize.x * 0.5f, mapSize.y * 0.5f, 0f);
        float span = Mathf.Max(mapSize.x, mapSize.y) + 2f * (MinimapFrame + MinimapInset);
        camera.transform.position = mapCentre + new Vector3(0f, 0f, CameraPosition.z);

        var viewport = cameraGo.AddComponent<MinimapViewport>();
        viewport.size = MinimapSize;
        viewport.margin = MinimapMargin;
        viewport.worldSize = new Vector2(span, span);
        viewport.padding = 0f;

        // Black frame with a floor-grey panel inside it; the map draws on top and blends into the panel.
        Sprite pixel = EnsurePixelSprite();
        if (pixel != null)
        {
            CreateMinimapQuad(MinimapFrameName, pixel, Color.black, span, -30, minimapLayer, mapCentre, cameraGo.transform, material);
            CreateMinimapQuad(MinimapPanelName, pixel, MinimapPanelColor, span - 2f * MinimapFrame, -20, minimapLayer, mapCentre, cameraGo.transform, material);
        }

        // Unlit copy of the floorplan on the minimap layer: the main view's copy is lit and goes
        // dark with the rest of the world, but the map the player carries must stay readable.
        if (mapSprite != null)
        {
            var mapGo = new GameObject(MinimapMapName);
            mapGo.layer = minimapLayer;
            mapGo.transform.SetParent(cameraGo.transform, false);
            mapGo.transform.position = Vector3.zero; // the backdrop's pivot is its bottom-left corner
            var mapRenderer = mapGo.AddComponent<SpriteRenderer>();
            mapRenderer.sprite = mapSprite;
            mapRenderer.sortingOrder = -15;
            if (material != null) mapRenderer.sharedMaterial = material;
        }

        // Large dots that follow each demon and the player by being their children; only the minimap camera sees them.
        NpcMarkers.Clear();
        foreach (GameObject npc in npcs)
        {
            NpcMarkers.Add(CreateMinimapMarker(npc, MinimapMarkerColor, minimapLayer, material));
        }
        if (player != null) CreateMinimapMarker(player, PlayerMarkerColor, minimapLayer, material);
    }

    static GameObject CreateMinimapMarker(GameObject owner, Color color, int layer, Material material)
    {
        Sprite dot = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinFallbackSprite);
        var marker = new GameObject(MinimapMarkerName);
        marker.layer = layer;
        marker.transform.SetParent(owner.transform, false);
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = dot;
        renderer.color = color;
        renderer.sortingOrder = 100;
        if (material != null) renderer.sharedMaterial = material;
        if (dot != null)
        {
            float dotSize = Mathf.Max(dot.bounds.size.x, dot.bounds.size.y);
            float parentScale = Mathf.Max(owner.transform.lossyScale.x, 0.0001f);
            float scale = MinimapMarkerSize / (dotSize * parentScale);
            marker.transform.localScale = new Vector3(scale, scale, 1f);
        }
        return marker;
    }

    // ------------------------------------------------------------------ player

    struct PlayerSheets
    {
        public Texture2D idle;
        public Texture2D boost;
        public Texture2D sprint; // optional: running with Shift
        public Texture2D caught; // optional: plays once when a demon catches the player
    }

    static GameObject CreatePlayer(GridGraph graph, Material material)
    {
        EnsureFolder(PlayerFolder); // where the animation strips and generated assets live
        GameObject go = CreateRoot(PlayerName);

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = go.AddComponent<CircleCollider2D>();
        collider.radius = AgentRadius;

        // Visual rotates to face the movement direction; Sprite (its child) is offset so the
        // visible pixels are centred on the collider and rotate about their own centre.
        var visual = new GameObject(NpcVisualName);
        visual.transform.SetParent(go.transform, false);
        var spriteGo = new GameObject(SpriteChildName);
        spriteGo.transform.SetParent(visual.transform, false);
        var renderer = spriteGo.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 1;
        if (material != null) renderer.sharedMaterial = material;
        var animator = spriteGo.AddComponent<Animator>();

        Sprite firstFrame = null;
        PlayerSheets sheets = FindPlayerSheets();
        if (sheets.idle != null && sheets.boost != null)
        {
            Sprite[] idleFrames = SliceStrip(sheets.idle);
            Sprite[] boostFrames = SliceStrip(sheets.boost);
            if (idleFrames.Length > 0 && boostFrames.Length > 0)
            {
                animator.runtimeAnimatorController = BuildPlayerAnimator(idleFrames, boostFrames, SliceOptional(sheets.sprint), SliceOptional(sheets.caught));
                firstFrame = idleFrames[0];
                Debug.Log($"Floorplan: player animations built from '{sheets.idle.name}' ({idleFrames.Length} idle frames) " +
                          $"and '{sheets.boost.name}' ({boostFrames.Length} boost frames).");
            }
        }
        else
        {
            Debug.LogWarning($"Floorplan: player sprite sheets not found. Save the idle strip and the boost strip as PNG files " +
                             $"under {PlayerFolder} with 'idle' and 'boost' in their file names, then run {MenuPath} again. " +
                             "Using a placeholder sprite for now.");
        }

        if (firstFrame == null)
        {
            firstFrame = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinFallbackSprite);
            renderer.color = Color.black; // placeholder dot in the palette's black
        }
        renderer.sprite = firstFrame;
        if (firstFrame != null)
        {
            Bounds visible = VisibleBounds(firstFrame, bodyOnly: true); // the hover jets are not the body
            float size = Mathf.Max(visible.size.x, visible.size.y);
            if (size > 0f)
            {
                float scale = PlayerVisualSize / size;
                visual.transform.localScale = new Vector3(scale, scale, 1f);
                spriteGo.transform.localPosition = -visible.center;
            }
        }

        var controller = go.AddComponent<PlayerController>();
        controller.moveSpeed = PlayerMoveSpeed;
        controller.visual = visual.transform;
        controller.animator = animator;
        controller.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (controller.actions == null)
        {
            Debug.LogWarning($"Floorplan: '{InputActionsPath}' not found; the player falls back to direct keyboard and gamepad input.");
        }

        go.transform.position = FindSpawnPosition(graph, PlayerPreferredSpawn);
        return go;
    }

    /// <summary>
    /// Finds the two animation strips: first by file name ('idle' / 'boost'), then by shape, where a
    /// horizontal strip of square cells with 6 frames is boost and one with 8 frames is idle.
    /// </summary>
    static PlayerSheets FindPlayerSheets()
    {
        var sheets = new PlayerSheets();
        var strips = new List<Texture2D>();

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/AstarPathfindingProject") || path.StartsWith("Assets/Welcome")) continue;
            if (path.StartsWith(EnemyFolder + "/")) continue; // enemy strips are named with state words too
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;

            string name = texture.name.ToLowerInvariant();
            // The optional strips first, by name only: their frame counts match idle and boost.
            if (name.Contains("sprint")) { if (sheets.sprint == null) sheets.sprint = texture; }
            else if (name.Contains("caught")) { if (sheets.caught == null) sheets.caught = texture; }
            else if ((name.Contains("boost") || name.Contains("bost")) && sheets.boost == null) sheets.boost = texture;
            else if (name.Contains("idle") && sheets.idle == null) sheets.idle = texture;
            else if (texture.height > 0 && texture.width >= 2 * texture.height && texture.width % texture.height == 0) strips.Add(texture);
        }

        foreach (Texture2D strip in strips)
        {
            int frames = strip.width / strip.height;
            if (frames == BoostFrameCount && sheets.boost == null) sheets.boost = strip;
            else if (frames == IdleFrameCount && sheets.idle == null) sheets.idle = strip;
        }

        return sheets;
    }

    /// <summary>Imports a horizontal strip of square cells as a multi-sprite texture and returns the frames in order.</summary>
    static Sprite[] SliceStrip(Texture2D sheet)
    {
        string path = AssetDatabase.GetAssetPath(sheet);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return new Sprite[0];

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 8192;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cell = sheet.height;
        int frames = Mathf.Max(1, sheet.width / cell);

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = new SpriteRect[frames];
        var pairs = new List<SpriteNameFileIdPair>();
        for (int i = 0; i < frames; i++)
        {
            rects[i] = new SpriteRect
            {
                name = $"{sheet.name}_{i}",
                rect = new Rect(i * cell, 0, cell, sheet.height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate(),
            };
            pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
        }
        provider.SetSpriteRects(rects);
        var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null) nameProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();

        var byName = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            if (asset is Sprite sprite) byName[sprite.name] = sprite;
        }

        var ordered = new List<Sprite>();
        for (int i = 0; i < frames; i++)
        {
            if (byName.TryGetValue($"{sheet.name}_{i}", out Sprite sprite)) ordered.Add(sprite);
        }
        return ordered.ToArray();
    }

    static Sprite[] SliceOptional(Texture2D sheet) => sheet != null ? SliceStrip(sheet) : new Sprite[0];

    /// <summary>
    /// Idle and Boost always; Sprint and Caught when their strips exist. PlayerController drives
    /// Boost (moving), Sprint (moving with Shift held) and Caught (a demon got you).
    /// </summary>
    static RuntimeAnimatorController BuildPlayerAnimator(Sprite[] idleFrames, Sprite[] boostFrames, Sprite[] sprintFrames, Sprite[] caughtFrames)
    {
        EnsureFolder(PlayerFolder);
        AnimationClip idleClip = CreateSpriteClip(PlayerIdleClipPath, "Player_Idle", idleFrames);
        AnimationClip boostClip = CreateSpriteClip(PlayerBoostClipPath, "Player_Boost", boostFrames);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(PlayerControllerPath);
        }
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
        controller.AddParameter("Boost", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState("Idle");
        idle.motion = idleClip;
        AnimatorState boost = machine.AddState("Boost");
        boost.motion = boostClip;
        machine.defaultState = idle;

        Link(idle, boost, AnimatorConditionMode.If, "Boost");
        Link(boost, idle, AnimatorConditionMode.IfNot, "Boost");

        if (sprintFrames != null && sprintFrames.Length > 0)
        {
            // Sprint is only ever set while moving, so it can be reached from standing too.
            controller.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            AnimatorState sprint = machine.AddState("Sprint");
            sprint.motion = CreateSpriteClip(PlayerSprintClipPath, "Player_Sprint", sprintFrames);
            Link(idle, sprint, AnimatorConditionMode.If, "Sprint");
            Link(boost, sprint, AnimatorConditionMode.If, "Sprint");
            Link(sprint, boost, AnimatorConditionMode.IfNot, "Sprint", "Boost");
            Link(sprint, idle, AnimatorConditionMode.IfNot, "Boost");
        }

        if (caughtFrames != null && caughtFrames.Length > 0)
        {
            // From anywhere, once, then held on its last frame until the run resets.
            controller.AddParameter("Caught", AnimatorControllerParameterType.Bool);
            AnimatorState caught = machine.AddState("Caught");
            caught.motion = CreateSpriteClip(PlayerCaughtClipPath, "Player_Caught", caughtFrames, loop: false);
            AnimatorStateTransition fromAny = machine.AddAnyStateTransition(caught);
            fromAny.hasExitTime = false;
            fromAny.duration = 0f;
            fromAny.canTransitionToSelf = false;
            fromAny.AddCondition(AnimatorConditionMode.If, 0f, "Caught");
            Link(caught, idle, AnimatorConditionMode.IfNot, "Caught");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    /// <summary>An instant transition on one bool, optionally with a second bool that must be true.</summary>
    static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter, string alsoTrue = null)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(mode, 0f, parameter);
        if (alsoTrue != null) transition.AddCondition(AnimatorConditionMode.If, 0f, alsoTrue);
    }

    /// <summary>
    /// Rebuilds only the player's animator from the strips in Assets/Player, for new animation art,
    /// without regenerating the level.
    /// </summary>
    [MenuItem("Tools/Floorplan/Refresh Player Animations")]
    public static void RefreshPlayerAnimations()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Player animations: stop Play mode first.");
            return;
        }

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null || player.animator == null)
        {
            Debug.LogError("Player animations: open the main scene first.");
            return;
        }

        PlayerSheets sheets = FindPlayerSheets();
        if (sheets.idle == null || sheets.boost == null)
        {
            Debug.LogError($"Player animations: no idle and boost strips under {PlayerFolder}.");
            return;
        }

        Sprite[] idleFrames = SliceStrip(sheets.idle);
        Sprite[] boostFrames = SliceStrip(sheets.boost);
        Sprite[] sprintFrames = SliceOptional(sheets.sprint);
        Sprite[] caughtFrames = SliceOptional(sheets.caught);

        Undo.RecordObject(player.animator, "Refresh player animations");
        player.animator.runtimeAnimatorController = BuildPlayerAnimator(idleFrames, boostFrames, sprintFrames, caughtFrames);
        var renderer = player.animator.GetComponent<SpriteRenderer>();
        if (renderer != null && idleFrames.Length > 0)
        {
            Undo.RecordObject(renderer, "Refresh player animations");
            renderer.sprite = idleFrames[0];
        }

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log($"Player animations: idle {idleFrames.Length}, boost {boostFrames.Length}, " +
                  $"sprint {sprintFrames.Length}, caught {caughtFrames.Length} frames. Scene saved.");
    }

    /// <summary>A sprite-swap clip; the last frame is held for a full step (then loops, if looping).</summary>
    static AnimationClip CreateSpriteClip(string path, string name, Sprite[] frames, float fps = PlayerAnimationFps, bool loop = true)
    {
        var clip = new AnimationClip { name = name, frameRate = fps };
        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        }
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void CreateMinimapQuad(string name, Sprite pixel, Color color, float size, int sortingOrder,
                                  int layer, Vector3 centre, Transform parent, Material material)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (material != null) renderer.sharedMaterial = material;
        float pixelSize = Mathf.Max(pixel.bounds.size.x, pixel.bounds.size.y);
        float scale = pixelSize > 0f ? size / pixelSize : size;
        go.transform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>A 4x4 white sprite (1 unit square) generated once under Assets/Floorplan for flat quads.</summary>
    static Sprite EnsurePixelSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PixelSpritePath);
        if (existing != null) return existing;

        string folder = System.IO.Path.GetDirectoryName(PixelSpritePath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'),
                                       System.IO.Path.GetFileName(folder));
        }

        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var white = new Color32[16];
        for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(white);
        texture.Apply();
        System.IO.File.WriteAllBytes(PixelSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(PixelSpritePath, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(PixelSpritePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Floorplan: could not import {PixelSpritePath}.");
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(PixelSpritePath);
    }

    /// <summary>
    /// Walkable node nearest to the preferred point inside the largest enclosed region.
    /// Regions that reach the graph edge are the outside of the building and are skipped
    /// unless nothing else exists.
    /// </summary>
    static Vector3 FindSpawnPosition(GridGraph graph, Vector3 preferred)
    {
        var areaSizes = new Dictionary<uint, int>();
        var edgeAreas = new HashSet<uint>();
        int maxX = graph.width - 1;
        int maxZ = graph.depth - 1;

        graph.GetNodes(node =>
        {
            if (!node.Walkable) return;
            areaSizes.TryGetValue(node.Area, out int count);
            areaSizes[node.Area] = count + 1;

            if (node is GridNodeBase gridNode &&
                (gridNode.XCoordinateInGrid == 0 || gridNode.XCoordinateInGrid == maxX ||
                 gridNode.ZCoordinateInGrid == 0 || gridNode.ZCoordinateInGrid == maxZ))
            {
                edgeAreas.Add(node.Area);
            }
        });

        if (areaSizes.Count == 0)
        {
            Debug.LogError("Floorplan: the grid graph has no walkable nodes after scanning.");
            return preferred;
        }

        uint chosenArea = LargestArea(areaSizes, edgeAreas, out bool found);
        if (!found) chosenArea = LargestArea(areaSizes, null, out found);

        string edgeNote = edgeAreas.Contains(chosenArea) ? " that touches the graph edge" : "";
        Debug.Log($"Floorplan: {areaSizes.Count} connected walkable regions ({edgeAreas.Count} touch the graph edge); " +
                  $"spawning in a region of {areaSizes[chosenArea]} nodes{edgeNote}.");

        GraphNode best = null;
        float bestDistance = float.MaxValue;
        graph.GetNodes(node =>
        {
            if (!node.Walkable || node.Area != chosenArea) return;
            float distance = ((Vector3)node.position - preferred).sqrMagnitude;
            if (distance >= bestDistance) return;
            bestDistance = distance;
            best = node;
        });

        return best != null ? (Vector3)best.position : preferred;
    }

    static uint LargestArea(Dictionary<uint, int> areaSizes, HashSet<uint> excluded, out bool found)
    {
        uint largest = 0;
        int largestSize = -1;
        found = false;
        foreach (KeyValuePair<uint, int> pair in areaSizes)
        {
            if (excluded != null && excluded.Contains(pair.Key)) continue;
            if (pair.Value <= largestSize) continue;
            largestSize = pair.Value;
            largest = pair.Key;
            found = true;
        }
        return largest;
    }

    // g. camera
    static Camera ConfigureCamera(GameObject player, Texture2D map)
    {
        Camera camera = Camera.main;
        if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null)
        {
            var go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.tag = "MainCamera";
            camera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        camera.orthographic = true;
        // Anything the floorplan does not cover clears to black, not Unity's default blue.
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        if (player == null)
        {
            camera.orthographicSize = CameraSize;
            camera.transform.position = CameraPosition;
            EditorUtility.SetDirty(camera);
            return camera;
        }

        // Follow the player zoomed in, never showing anything outside the floorplan image.
        var follow = camera.GetComponent<CameraFollow2D>();
        if (follow == null) follow = Undo.AddComponent<CameraFollow2D>(camera.gameObject);
        follow.target = player.transform;
        follow.zoom = CameraFollowZoom;
        follow.smoothTime = CameraFollowSmoothTime;
        follow.clampToBounds = true;
        follow.bounds = new Rect(0f, 0f, map.width / PixelsPerUnit, map.height / PixelsPerUnit);

        camera.orthographicSize = CameraFollowZoom;
        Vector3 start = player.transform.position;
        camera.transform.position = new Vector3(start.x, start.y, CameraPosition.z);
        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(follow);
        return camera;
    }
}
