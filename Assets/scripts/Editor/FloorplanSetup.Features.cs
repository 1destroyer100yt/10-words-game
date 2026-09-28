using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// The stealth features layered on top of the base floorplan scene by Tools/Floorplan/Build Scene:
/// darkness with wall shadows, hiding lockers, the decoy coin, sweeping security cameras, the
/// minimap radar rule, the suspicion indicator, the heartbeat vignette and the run itself
/// (timer, best time, caught sequence). All of its icons are generated as tiny PNGs so the game
/// keeps to black, grey and red with no imported art.
/// </summary>
public static partial class FloorplanSetup
{
    // ------------------------------------------------------------------ names and tuning

    const string GameName = "Game";
    const string HudCanvasName = "HUD";
    const string SecurityCamerasName = "Security Cameras";
    const string HidingSpotsName = "Hiding Spots";
    const string GlobalLightName = "Global Light";
    const string IndicatorsName = "Indicators";
    const string PlayerLightName = "Player Light";
    const string StoryName = "Story";
    const string MenuName = "Menu";
    const string ObjectiveName = "Objective";
    const string AudioName = "Audio";
    const string DemonsName = "Demons";

    const string AmbienceClipPath = "Assets/Audio/horror_ambiance_loopable.wav";
    const string SpottedClipPath = "Assets/Audio/spotted.mp3";

    /// <summary>The jewel goes as far from the player's spawn as the building allows.</summary>
    static readonly Vector3 JewelPreferredSpawn = new Vector3(78f, 22f, 0f);

    const string IconFolder = "Assets/Floorplan/Icons";

    /// <summary>How far the player can see. The vignette hides the corners of the view anyway.</summary>
    const float PlayerLightRadius = 16f;
    const float PlayerLightInnerRadius = 3.5f;
    /// <summary>Never fully black: the walls stay faintly readable so the map is not a black screen.</summary>
    const float GlobalLightIntensity = 0.11f;

    const float SuspicionDetectTime = 1.5f;
    const float NpcCatchDistance = 0.75f;

    const int SecurityCameraCount = 6;
    const float SecurityCameraSpacing = 17f;
    const float SecurityCameraViewAngle = 45f;
    const float SecurityCameraViewDistance = 13f;

    const int HidingSpotCount = 9;
    const float HidingSpotSpacing = 13f;
    const float HidingSpotWidth = 1.1f;
    const float HidingSpotHeight = 1.65f;

    const float DecoyRange = 7f;
    const float DecoyNoiseRadius = 12f;
    const float DecoyCooldown = 1.5f;

    /// <summary>The palette. Everything generated here uses only these.</summary>
    static readonly Color32 Red = new Color32(237, 28, 36, 255);
    static readonly Color32 Black = new Color32(0, 0, 0, 255);
    static readonly Color32 Grey = new Color32(70, 70, 70, 255);
    static readonly Color32 White = new Color32(255, 255, 255, 255);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    /// <summary>Everything one build step needs to hand to the next.</summary>
    class FeatureContext
    {
        public GridGraph graph;
        public Texture2D map;
        public GameObject walls;
        public GameObject player;
        public GameObject npc;
        public List<GameObject> npcs;
        public Camera mainCamera;
        public GameObject volume;
        public int wallLayer;
        public int minimapLayer;
        public Material lit;
        public Material unlit;
    }

    static void BuildFeatures(FeatureContext ctx)
    {
        // The player and the NPC were created this frame; the wall spot search raycasts against them.
        Physics2D.SyncTransforms();

        Icons icons = EnsureIcons();

        AddWallShadows(ctx);
        AddLights(ctx);

        List<WallSpot> spots = FindWallSpots(ctx);
        CreateHidingSpots(ctx, icons, spots, out GameObject[] lockers);
        CreateSecurityCameras(ctx, icons, spots);

        AddPlayerGear(ctx, icons, lockers);
        AddNpcSenses(ctx, icons);
        GameAudio audioBank = CreateAudio(ctx);
        Objective objective = CreateObjective(ctx, icons, audioBank);
        GameObject hud = CreateGame(ctx, icons, out GameRun run, out StoryCards cards);
        AddCameraEffects(ctx);

        // Last, so the level, the goal and the HUD all exist for the story to point at.
        CreateStory(ctx, icons, audioBank, objective, hud, run, cards);

        Debug.Log($"Floorplan: features built. Darkness with {ctx.walls.transform.childCount} shadow casters, " +
                  $"{lockers.Length} hiding spots, {SecurityCamerasRoot(ctx)} security cameras, HUD '{hud.name}'.");
    }

    static int SecurityCamerasRoot(FeatureContext ctx)
    {
        GameObject root = GameObject.Find(SecurityCamerasName);
        return root != null ? root.transform.childCount : 0;
    }

    // ------------------------------------------------------------------ darkness

    /// <summary>
    /// Every baked wall box casts a shadow for the player's light, so a room you have no line to
    /// stays black. One CompositeShadowCaster2D on the root batches them into a single group.
    /// </summary>
    static void AddWallShadows(FeatureContext ctx)
    {
        if (ctx.walls == null) return;

        if (ctx.walls.GetComponent<CompositeShadowCaster2D>() == null) ctx.walls.AddComponent<CompositeShadowCaster2D>();

        foreach (Transform child in ctx.walls.transform)
        {
            if (child.GetComponent<ShadowCaster2D>() != null) continue;
            var caster = child.gameObject.AddComponent<ShadowCaster2D>();
            caster.selfShadows = false;
            caster.castsShadows = true;
        }
    }

    static void AddLights(FeatureContext ctx)
    {
        // A dim global light keeps the walls readable; the player's light is what actually reveals rooms.
        GameObject globalGo = CreateRoot(GlobalLightName);
        var global = globalGo.AddComponent<Light2D>();
        global.lightType = Light2D.LightType.Global;
        global.intensity = GlobalLightIntensity;
        global.color = Color.white;
        ApplyToAllSortingLayers(global);

        if (ctx.player == null) return;

        var lightGo = new GameObject(PlayerLightName);
        lightGo.transform.SetParent(ctx.player.transform, false);
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.pointLightOuterRadius = PlayerLightRadius;
        light.pointLightInnerRadius = PlayerLightInnerRadius;
        light.falloffIntensity = 0.6f;
        light.intensity = 1f;
        light.color = Color.white;
        light.shadowsEnabled = true;
        light.shadowIntensity = 1f;
        ApplyToAllSortingLayers(light);
    }

    /// <summary>Light every sorting layer. Light2D.Awake does this too, but only for a fresh component.</summary>
    static void ApplyToAllSortingLayers(Light2D light)
    {
        SortingLayer[] layers = SortingLayer.layers;
        var ids = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
        light.targetSortingLayers = ids;
    }

    // ------------------------------------------------------------------ wall spots

    /// <summary>A walkable point with a wall behind it, and how far the open space in front reaches.</summary>
    struct WallSpot
    {
        public Vector3 position;
        /// <summary>Unit vector pointing away from the wall, into the room.</summary>
        public Vector2 normal;
        /// <summary>Distance to the next wall straight ahead; long means a corridor.</summary>
        public float openness;
    }

    static readonly Vector2[] Cardinals = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    /// <summary>
    /// Samples the interior region the player spawned in for points that have a wall on one side
    /// and open floor on the other: the places a locker or a wall camera would actually be fitted.
    /// </summary>
    static List<WallSpot> FindWallSpots(FeatureContext ctx)
    {
        var spots = new List<WallSpot>();
        if (ctx.graph == null || ctx.player == null || AstarPath.active == null) return spots;

        GraphNode playerNode = AstarPath.active.GetNearest(ctx.player.transform.position, NNConstraint.Default).node;
        if (playerNode == null) return spots;
        uint area = playerNode.Area;

        int wallMask = 1 << ctx.wallLayer;
        const float probe = 0.75f;   // just past the 0.29 agent radius, inside a 0.86 wall
        const int stride = 11;       // sample every Nth walkable node; 63k nodes is far more than we need

        int index = 0;
        ctx.graph.GetNodes(node =>
        {
            if (!node.Walkable || node.Area != area) return;
            if (index++ % stride != 0) return;

            Vector3 position = (Vector3)node.position;
            Vector2 origin = position;
            Vector2 blocked = Vector2.zero;
            int hits = 0;

            foreach (Vector2 direction in Cardinals)
            {
                if (Physics2D.Raycast(origin, direction, probe, wallMask).collider == null) continue;
                blocked += direction;
                hits++;
            }

            // One or two walls meeting at a corner is a back to stand against; a corridor is not.
            if (hits == 0 || hits > 2 || blocked.sqrMagnitude < 0.001f) return;

            Vector2 normal = (-blocked).normalized;
            RaycastHit2D ahead = Physics2D.Raycast(origin + normal * 0.1f, normal, 30f, wallMask);
            float openness = ahead.collider != null ? ahead.distance : 30f;
            if (openness < 2f) return; // wedged in; nothing would fit here

            spots.Add(new WallSpot { position = position, normal = normal, openness = openness });
        });

        return spots;
    }

    /// <summary>
    /// Greedily takes the best-scoring spots that are at least 'spacing' apart, so they end up
    /// spread across the building instead of clustered in one corridor.
    /// </summary>
    static List<WallSpot> PickSpread(List<WallSpot> spots, System.Func<WallSpot, float> score, int count, float spacing)
    {
        var sorted = new List<WallSpot>(spots);
        sorted.Sort((a, b) => score(b).CompareTo(score(a)));

        var chosen = new List<WallSpot>();
        foreach (WallSpot spot in sorted)
        {
            if (chosen.Count >= count) break;
            bool tooClose = false;
            foreach (WallSpot taken in chosen)
            {
                if (Vector3.Distance(taken.position, spot.position) >= spacing) continue;
                tooClose = true;
                break;
            }
            if (!tooClose) chosen.Add(spot);
        }
        return chosen;
    }

    // ------------------------------------------------------------------ hiding spots

    static void CreateHidingSpots(FeatureContext ctx, Icons icons, List<WallSpot> spots, out GameObject[] lockers)
    {
        GameObject root = CreateRoot(HidingSpotsName);

        // A locker belongs in a nook, not down the middle of a hall: prefer modest openness.
        List<WallSpot> chosen = PickSpread(spots, s => 10f - Mathf.Abs(s.openness - 5f), HidingSpotCount, HidingSpotSpacing);

        var created = new List<GameObject>();
        for (int i = 0; i < chosen.Count; i++)
        {
            WallSpot spot = chosen[i];
            var go = new GameObject($"Locker {i + 1}");
            go.transform.SetParent(root.transform, false);
            // Pushed back against the wall, and turned so its door faces into the room.
            go.transform.position = spot.position - (Vector3)(spot.normal * 0.2f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(spot.normal.y, spot.normal.x) * Mathf.Rad2Deg - 90f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = icons.lockerEmpty;
            renderer.sortingOrder = -5; // under the player and the enemy
            if (ctx.lit != null) renderer.sharedMaterial = ctx.lit;
            ScaleSprite(go.transform, icons.lockerEmpty, HidingSpotWidth, HidingSpotHeight);

            var spotComponent = go.AddComponent<HidingSpot>();
            spotComponent.spriteRenderer = renderer;
            spotComponent.emptySprite = icons.lockerEmpty;
            spotComponent.occupiedSprite = icons.lockerFull;
            created.Add(go);
        }

        lockers = created.ToArray();
        if (lockers.Length < HidingSpotCount)
        {
            Debug.LogWarning($"Floorplan: only {lockers.Length} of {HidingSpotCount} hiding spots fitted; " +
                             "the building has few free wall sections that far apart.");
        }
    }

    // ------------------------------------------------------------------ security cameras

    const float CameraDomeSize = 0.9f;
    const float CameraMountHeight = 0.45f;

    /// <summary>
    /// The dome on its wall arm. The arm stays put while the dome turns with the sweep, the lens sits
    /// in the dome's eye so it swings with it, and the view starts from the dome rather than the
    /// wall. The camera's own origin is a little inside the wall.
    /// </summary>
    static void ApplyCameraLook(SecurityCamera camera, Icons icons)
    {
        Transform root = camera.transform;
        var housingRenderer = camera.housing.GetComponent<SpriteRenderer>();
        housingRenderer.sprite = icons.cameraBody;
        housingRenderer.color = White; // the art carries its own black and grey

        Transform mount = root.Find("Mount");
        if (mount == null)
        {
            mount = new GameObject("Mount").transform;
            mount.SetParent(root, false);
        }
        var mountRenderer = mount.GetComponent<SpriteRenderer>();
        if (mountRenderer == null) mountRenderer = mount.gameObject.AddComponent<SpriteRenderer>();
        mountRenderer.sprite = icons.cameraMount;
        mountRenderer.color = White;
        mountRenderer.sortingOrder = 3;
        mountRenderer.sharedMaterial = housingRenderer.sharedMaterial;
        // The plate overlaps the wall face a little so it reads as fixed to it.
        mount.localPosition = new Vector3(0f, 0.15f + CameraMountHeight * 0.5f, 0f);
        mount.localRotation = Quaternion.identity;
        ScaleSprite(mount, icons.cameraMount, CameraDomeSize, CameraMountHeight);

        float domeY = 0.15f + CameraMountHeight + CameraDomeSize * 0.5f - 0.05f;
        camera.housing.localPosition = new Vector3(0f, domeY, 0f);
        ScaleSprite(camera.housing, icons.cameraBody, CameraDomeSize, CameraDomeSize);
        if (camera.vision != null) camera.vision.transform.localPosition = new Vector3(0f, domeY, 0f);

        // The eye is 2 art pixels ahead of the dome's centre, out of 12; the dome sprite is 1 unit tall.
        Transform lens = camera.lens.transform;
        lens.SetParent(camera.housing, false);
        lens.localRotation = Quaternion.identity;
        lens.localPosition = new Vector3(0f, 2f / 12f, 0f);
        float lensSize = 0.22f / CameraDomeSize;
        Vector2 dot = camera.lens is SpriteRenderer lensSprite && lensSprite.sprite != null
            ? (Vector2)lensSprite.sprite.bounds.size : Vector2.one;
        lens.localScale = new Vector3(lensSize / dot.x, lensSize / dot.y, 1f);
    }

    /// <summary>Puts the current camera art on the cameras already in the scene, without a rebuild.</summary>
    [MenuItem("Tools/Floorplan/Refresh Security Cameras")]
    public static void RefreshSecurityCameras()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Security cameras: stop Play mode first.");
            return;
        }
        Icons icons = EnsureIcons();
        SecurityCamera[] cameras = Object.FindObjectsByType<SecurityCamera>(FindObjectsInactive.Include);
        if (cameras.Length == 0)
        {
            Debug.LogError("Security cameras: open the main scene first.");
            return;
        }
        foreach (SecurityCamera camera in cameras)
        {
            Undo.RegisterFullObjectHierarchyUndo(camera.gameObject, "Refresh security cameras");
            ApplyCameraLook(camera, icons);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cameras[0].gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(cameras[0].gameObject.scene);
        Debug.Log($"Security cameras: {cameras.Length} refreshed. Scene saved.");
    }

    static void CreateSecurityCameras(FeatureContext ctx, Icons icons, List<WallSpot> spots)
    {
        GameObject root = CreateRoot(SecurityCamerasName);
        if (ctx.player == null) return;

        // A camera earns its place where it can watch a long stretch of floor.
        List<WallSpot> chosen = PickSpread(spots, s => s.openness, SecurityCameraCount, SecurityCameraSpacing);

        for (int i = 0; i < chosen.Count; i++)
        {
            WallSpot spot = chosen[i];
            var go = new GameObject($"Security Camera {i + 1}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = spot.position - (Vector3)(spot.normal * 0.25f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(spot.normal.y, spot.normal.x) * Mathf.Rad2Deg - 90f);

            var housing = new GameObject("Housing");
            housing.transform.SetParent(go.transform, false);
            var housingRenderer = housing.AddComponent<SpriteRenderer>();
            housingRenderer.sortingOrder = 3;
            if (ctx.lit != null) housingRenderer.sharedMaterial = ctx.lit;

            // Unlit, so the blinking lens is the one thing you can see in a dark corridor.
            var lens = new GameObject("Lens");
            var lensRenderer = lens.AddComponent<SpriteRenderer>();
            lensRenderer.sprite = icons.dot;
            lensRenderer.color = Red;
            lensRenderer.sortingOrder = 4;
            if (ctx.unlit != null) lensRenderer.sharedMaterial = ctx.unlit;

            GameObject visionGo = CreateVisionCone(go.transform, ctx, ctx.player.transform,
                SecurityCameraViewAngle, SecurityCameraViewDistance, out NpcVision vision);

            var securityCamera = go.AddComponent<SecurityCamera>();
            securityCamera.vision = vision;
            securityCamera.housing = housing.transform;
            securityCamera.lens = lensRenderer;
            securityCamera.detectTime = 1f;
            ApplyCameraLook(securityCamera, icons);

            // Radar rule: a camera you have never walked past is not on your map.
            var reveal = go.AddComponent<MinimapReveal>();
            reveal.renderers = new Renderer[] { visionGo.GetComponent<MeshRenderer>() };
            reveal.player = ctx.player.transform;
            reveal.sightRadius = PlayerLightRadius;
            reveal.wallMask = 1 << ctx.wallLayer;
            reveal.securityCamera = securityCamera;
            reveal.rememberOnceSeen = true;
        }
    }

    /// <summary>A vision cone child on the minimap layer, the same one the enemy uses.</summary>
    static GameObject CreateVisionCone(Transform parent, FeatureContext ctx, Transform target,
                                       float angle, float distance, out NpcVision vision)
    {
        var go = new GameObject(VisionName);
        go.layer = ctx.minimapLayer >= 0 ? ctx.minimapLayer : parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = EnsureVisionConeMaterial();
        renderer.sortingOrder = 50;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        vision = go.AddComponent<NpcVision>();
        vision.target = target;
        vision.viewAngle = angle;
        vision.viewDistance = distance;
        vision.wallMask = 1 << ctx.wallLayer;
        vision.idleColor = new Color(Red.r / 255f, Red.g / 255f, Red.b / 255f, 0.55f);
        vision.alertColor = new Color(Red.r / 255f, Red.g / 255f, Red.b / 255f, 1f);
        return go;
    }

    // ------------------------------------------------------------------ player gear

    static void AddPlayerGear(FeatureContext ctx, Icons icons, GameObject[] lockers)
    {
        if (ctx.player == null) return;

        var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsPath);
        Transform visual = ctx.player.transform.Find(NpcVisualName);

        var hider = ctx.player.AddComponent<PlayerHider>();
        hider.actions = actions;
        hider.useRadius = 1.4f;
        // The minimap dot stays lit while hidden, so you can still see where you are.
        hider.hiddenWhileInside = visual != null ? new[] { visual.gameObject } : new GameObject[0];

        var thrower = ctx.player.AddComponent<DecoyThrower>();
        thrower.actions = actions;
        thrower.coinSprite = icons.coin;
        thrower.ringSprite = icons.ring;
        thrower.spriteMaterial = ctx.unlit;
        thrower.wallMask = 1 << ctx.wallLayer;
        thrower.minimapLayer = ctx.minimapLayer;
        thrower.range = DecoyRange;
        thrower.noiseRadius = DecoyNoiseRadius;
        thrower.cooldown = DecoyCooldown;
    }

    // ------------------------------------------------------------------ enemy senses

    static void AddNpcSenses(FeatureContext ctx, Icons icons)
    {
        if (ctx.npcs == null || ctx.npcs.Count == 0) return;

        GameObject indicators = CreateRoot(IndicatorsName);
        GameObject demonsRoot = CreateRoot(DemonsName);
        var counter = demonsRoot.AddComponent<DemonCount>();

        for (int i = 0; i < ctx.npcs.Count; i++)
        {
            GameObject indicator = AddOneNpcSenses(ctx, icons, ctx.npcs[i], i, indicators);
            counter.demons.Add(ctx.npcs[i]);
            counter.attachments.Add(indicator);
        }

        DemonCounter = counter;
    }

    static GameObject AddOneNpcSenses(FeatureContext ctx, Icons icons, GameObject npc, int index, GameObject indicators)
    {
        var chaser = npc.GetComponent<NpcChaser>();
        if (chaser != null)
        {
            chaser.detectTime = SuspicionDetectTime;
            chaser.catchDistance = NpcCatchDistance;
        }

        // "?" filling up, then "!": drawn unlit above the enemy so the warning always reads.
        var go = new GameObject($"{npc.name} Indicator");
        go.transform.SetParent(indicators.transform, false);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(go.transform, false);
        var iconRenderer = iconGo.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = icons.question;
        iconRenderer.color = Red;
        iconRenderer.sortingOrder = 60;
        if (ctx.unlit != null) iconRenderer.sharedMaterial = ctx.unlit;
        ScaleSprite(iconGo.transform, icons.question, 1f, 1f);

        var barGo = new GameObject("Bar");
        barGo.transform.SetParent(go.transform, false);
        var barRenderer = barGo.AddComponent<SpriteRenderer>();
        barRenderer.sprite = EnsurePixelSprite();
        barRenderer.color = Red;
        barRenderer.sortingOrder = 59;
        if (ctx.unlit != null) barRenderer.sharedMaterial = ctx.unlit;

        var indicator = go.AddComponent<NpcSuspicionIndicator>();
        indicator.chaser = chaser;
        indicator.icon = iconRenderer;
        indicator.questionSprite = icons.question;
        indicator.exclaimSprite = icons.exclaim;
        indicator.bar = barGo.transform;

        // Radar rule: the enemy dot and its cone only appear when it is alerted or you can see it.
        var renderers = new List<Renderer>();
        Transform vision = npc.transform.Find(VisionName);
        if (vision != null) renderers.Add(vision.GetComponent<MeshRenderer>());
        if (index < NpcMarkers.Count && NpcMarkers[index] != null)
        {
            renderers.Add(NpcMarkers[index].GetComponent<SpriteRenderer>());
        }

        var reveal = npc.AddComponent<MinimapReveal>();
        reveal.renderers = renderers.ToArray();
        reveal.player = ctx.player != null ? ctx.player.transform : null;
        reveal.sightRadius = PlayerLightRadius;
        reveal.wallMask = 1 << ctx.wallLayer;
        reveal.chaser = chaser;
        return go;
    }

    // ------------------------------------------------------------------ sound

    static GameAudio CreateAudio(FeatureContext ctx)
    {
        GameObject go = CreateRoot(AudioName);
        var bank = go.AddComponent<GameAudio>();
        bank.player = ctx.player != null ? ctx.player.transform : null;
        bank.ambience = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbienceClipPath);
        bank.spotted = AssetDatabase.LoadAssetAtPath<AudioClip>(SpottedClipPath);

        if (bank.ambience == null || bank.spotted == null)
        {
            Debug.LogWarning($"Floorplan: audio missing. Expected {AmbienceClipPath} and {SpottedClipPath}. " +
                             "The generated effects still play.");
        }
        return bank;
    }

    // ------------------------------------------------------------------ the goal

    /// <summary>
    /// The jewel in a far room, and the way out back where the player started. Both are wordless:
    /// a red gem you walk onto, and a ring that only comes alive once you are carrying it.
    /// </summary>
    static Objective CreateObjective(FeatureContext ctx, Icons icons, GameAudio audioBank)
    {
        GameObject root = CreateRoot(ObjectiveName);
        if (ctx.player == null || ctx.graph == null) return null;

        Vector3 exitAt = ctx.player.transform.position;
        Vector3 jewelAt = FindSpawnPosition(ctx.graph, JewelPreferredSpawn);

        // The way out: a dim ring at the spawn that pulses red once the jewel is yours.
        var exitGo = new GameObject("Way Out");
        exitGo.transform.SetParent(root.transform, false);
        exitGo.transform.position = exitAt;
        var exitSprite = exitGo.AddComponent<SpriteRenderer>();
        exitSprite.sprite = icons.ring;
        exitSprite.color = Grey;
        exitSprite.sortingOrder = -4;
        if (ctx.unlit != null) exitSprite.sharedMaterial = ctx.unlit;
        ScaleSprite(exitGo.transform, icons.ring, 2.8f, 2.8f);

        // The jewel itself, unlit so you can spot it from across a dark room.
        var jewelGo = new GameObject("Jewel");
        jewelGo.transform.SetParent(root.transform, false);
        jewelGo.transform.position = jewelAt;
        var jewelSprite = jewelGo.AddComponent<SpriteRenderer>();
        jewelSprite.sprite = icons.gem;
        jewelSprite.color = Red;
        jewelSprite.sortingOrder = 2;
        if (ctx.unlit != null) jewelSprite.sharedMaterial = ctx.unlit;
        ScaleSprite(jewelGo.transform, icons.gem, 1f, 1f);

        SpriteRenderer jewelMarker = CreateObjectiveMarker(jewelGo, icons.gem, 3f, Red, ctx);
        // A ring for the way out, not the jewel's shape: carrying the jewel, a jewel icon at the exit read as the jewel's location.
        SpriteRenderer exitMarker = CreateObjectiveMarker(exitGo, EnsureArtIcon("PortalMark", PortalMarkArt), ExitMarkerSize, Red, ctx);

        var objective = root.AddComponent<Objective>();
        objective.player = ctx.player.transform;
        objective.audioBank = audioBank;
        objective.jewel = jewelGo.transform;
        objective.jewelMarker = jewelMarker;
        objective.exit = exitGo.transform;
        objective.exitSprite = exitSprite;
        objective.exitMarker = exitMarker;
        objective.armedColor = Red;
        objective.dormantColor = Grey;

        Debug.Log($"Floorplan: goal placed. Jewel at {jewelAt.x:F0},{jewelAt.y:F0}; " +
                  $"way out at {exitAt.x:F0},{exitAt.y:F0}, {Vector2.Distance(jewelAt, exitAt):F0} units apart.");
        return objective;
    }

    /// <summary>A minimap dot on an objective, so the map shows where to go without a word.</summary>
    static SpriteRenderer CreateObjectiveMarker(GameObject owner, Sprite sprite, float size, Color color, FeatureContext ctx)
    {
        var go = new GameObject(MinimapMarkerName);
        go.layer = ctx.minimapLayer >= 0 ? ctx.minimapLayer : owner.layer;
        go.transform.SetParent(owner.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 95;
        if (ctx.unlit != null) renderer.sharedMaterial = ctx.unlit;

        // Undo the owner's scale so every marker is the same size on the map, keeping the art's proportions.
        FitSprite(go.transform, sprite, size);
        return renderer;
    }

    // ------------------------------------------------------------------ the run and its HUD

    static GameObject CreateGame(FeatureContext ctx, Icons icons, out GameRun run, out StoryCards cards)
    {
        GameObject canvasGo = CreateRoot(HudCanvasName);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        // Constant pixel size: RunHud positions its digits in real screen pixels, next to the minimap.
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        Image flash = CreateHudImage(canvasGo.transform, "Flash", icons.pixel);
        flash.color = new Color(Red.r / 255f, Red.g / 255f, Red.b / 255f, 0.55f);
        flash.rectTransform.anchorMin = Vector2.zero;
        flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.offsetMin = Vector2.zero;
        flash.rectTransform.offsetMax = Vector2.zero;
        flash.enabled = false;

        Image caught = CreateHudImage(canvasGo.transform, "Caught", icons.skull);
        caught.color = Red;
        caught.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        caught.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        caught.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        caught.rectTransform.anchoredPosition = Vector2.zero;
        caught.rectTransform.sizeDelta = new Vector2(160f, 160f);
        caught.enabled = false;

        // Getting out with the jewel: the same emblem as the title, so winning reads instantly.
        Image won = CreateHudImage(canvasGo.transform, "Won", icons.gem);
        won.color = Red;
        won.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        won.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        won.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        won.rectTransform.anchoredPosition = Vector2.zero;
        won.rectTransform.sizeDelta = new Vector2(180f, 180f);
        won.enabled = false;

        // Last child, so a word card draws over the flash and the icon rather than under them.
        var storyGo = new GameObject(StoryName, typeof(RectTransform));
        storyGo.transform.SetParent(canvasGo.transform, false);
        var storyRect = storyGo.GetComponent<RectTransform>();
        storyRect.anchorMin = new Vector2(0.5f, 0.5f);
        storyRect.anchorMax = new Vector2(0.5f, 0.5f);
        storyRect.pivot = new Vector2(0.5f, 0.5f);
        storyRect.anchoredPosition = Vector2.zero;
        storyRect.sizeDelta = Vector2.zero;

        cards = storyGo.AddComponent<StoryCards>();
        StoryCards story = cards;
        story.letters = icons.letters;
        story.root = storyRect;
        story.wordColor = Red;
        if (icons.letters == null || icons.letters.Length < 26)
        {
            Debug.LogWarning($"Floorplan: the generated alphabet came back with " +
                             $"{(icons.letters == null ? 0 : icons.letters.Length)} letters instead of 26; " +
                             "story cards will be missing glyphs.");
        }

        var hud = canvasGo.AddComponent<RunHud>();
        hud.glyphs = icons.digits;
        hud.minimapCamera = MinimapCamera;
        hud.timeColor = Grey;
        hud.bestColor = Red;

        // Title screen. Last of all, so it covers everything, and wordless: the ten words are spent.
        var menuGo = new GameObject(MenuName, typeof(RectTransform));
        menuGo.transform.SetParent(canvasGo.transform, false);
        // Stretched over the whole canvas, so the backdrop can actually cover the game behind it.
        var menuRect = menuGo.GetComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;

        Image menuBackdrop = CreateHudImage(menuGo.transform, "Backdrop", icons.pixel);
        menuBackdrop.color = Color.black;
        menuBackdrop.rectTransform.anchorMin = Vector2.zero;
        menuBackdrop.rectTransform.anchorMax = Vector2.one;
        menuBackdrop.rectTransform.offsetMin = Vector2.zero;
        menuBackdrop.rectTransform.offsetMax = Vector2.zero;

        var menu = menuGo.AddComponent<MenuScreen>();
        menu.backdrop = menuBackdrop;
        menu.root = menuRect;
        menu.keyCap = icons.keyCap;
        menu.mouse = icons.mouse;
        menu.playMark = icons.playMark;
        menu.coin = icons.coin;
        menu.letters = icons.letters;
        menu.digits = icons.digits;
        menu.demonMark = icons.demonMark;
        menu.title = icons.title;
        menu.arrowLeft = icons.arrowLeft;
        menu.arrowRight = icons.arrowRight;
        menu.speakerOn = icons.speakerOn;
        menu.speakerOff = icons.speakerOff;
        menu.frameCorner = icons.frameCorner;
        menu.pixel = icons.pixel;
        menu.demonCount = DemonCounter;
        menu.maxDemons = MaxDemons;
        menu.demons = 1;
        menu.markColor = Red;
        menu.hintColor = Grey;
        menu.hud = hud; // the clock stays out of the way while the title screen is up

        GameObject gameGo = CreateRoot(GameName);
        run = gameGo.AddComponent<GameRun>();
        run.flash = flash;
        run.caughtIcon = caught;
        run.wonIcon = won;
        run.story = story;
        run.startPaused = true; // the menu decides when the first run starts
        menu.run = run;

        Debug.Log("Floorplan: story wired. " +
                  $"\"{story.openingWords}\" / \"{story.spottedWords}\" / " +
                  $"\"{story.caughtWords}\" / \"{story.bestWords}\" = ten words, each shown once.");
        return canvasGo;
    }

    static Image CreateHudImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static void AddCameraEffects(FeatureContext ctx)
    {
        if (ctx.volume == null) return;

        var effects = ctx.volume.AddComponent<PlayerCameraEffects>();
        effects.player = ctx.player != null ? ctx.player.transform : null;
        effects.cameraFollow = ctx.mainCamera != null ? ctx.mainCamera.GetComponent<CameraFollow2D>() : null;
        effects.baseIntensity = VignetteIntensity;
        effects.baseSmoothness = VignetteSmoothness;
        effects.spottedColor = Red;
    }

    // ------------------------------------------------------------------ generated icons

    class Icons
    {
        public Sprite question;
        public Sprite exclaim;
        public Sprite skull;
        public Sprite coin;
        public Sprite ring;
        public Sprite dot;
        public Sprite cameraBody;
        public Sprite cameraMount;
        public Sprite lockerEmpty;
        public Sprite lockerFull;
        public Sprite pixel;
        public Sprite[] digits;
        public Sprite[] letters;
        public Sprite gem;
        public Sprite keyCap;
        public Sprite mouse;
        public Sprite playMark;
        public Sprite demonMark;
        public Sprite title;
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite speakerOn;
        public Sprite speakerOff;
        public Sprite frameCorner;
    }

    /// <summary>Set by AddNpcSenses so the menu can hand it the chosen difficulty.</summary>
    static DemonCount DemonCounter;

    // The jewel the building is named for; the title screen's only large mark.
    static readonly string[] GemArt =
    {
        "..wwwwwwwwwwww..",
        ".wwwwwwwwwwwwww.",
        "wwwwwwwwwwwwwwww",
        "wwwwwwwwwwwwwwww",
        ".wwwwwwwwwwwwww.",
        ".wwwwwwwwwwwwww.",
        "..wwwwwwwwwwww..",
        "..wwwwwwwwwwww..",
        "...wwwwwwwwww...",
        "....wwwwwwww....",
        ".....wwwwww.....",
        "......wwww......",
        ".......ww.......",
        "................",
    };

    static readonly string[] MouseArt =
    {
        "..wwwwww..",
        ".wwwwwwww.",
        "ww..ww..ww",
        "ww..ww..ww",
        "wwwwwwwwww",
        "wwwwwwwwww",
        "wwwwwwwwww",
        "wwwwwwwwww",
        ".wwwwwwww.",
        "..wwwwww..",
    };

    static Icons EnsureIcons()
    {
        EnsureFolder(IconFolder);
        return new Icons
        {
            question = EnsureArtIcon("Question", QuestionArt),
            exclaim = EnsureArtIcon("Exclaim", ExclaimArt),
            skull = EnsureArtIcon("Skull", SkullArt),
            cameraBody = EnsureArtIcon("CameraBody", CameraBodyArt),
            cameraMount = EnsureArtIcon("CameraMount", CameraMountArt),
            coin = EnsureDisc("Coin", 8, White, false),
            dot = EnsureDisc("Dot", 8, White, false),
            ring = EnsureDisc("Ring", 64, White, true),
            lockerEmpty = EnsureLocker("LockerEmpty", false),
            lockerFull = EnsureLocker("LockerFull", true),
            pixel = EnsurePixelSprite(),
            digits = EnsureDigits(),
            letters = EnsureLetters(),
            gem = EnsureArtIcon("Gem", GemArt),
            mouse = EnsureArtIcon("Mouse", MouseArt),
            keyCap = EnsureKeyCap(),
            playMark = EnsurePlayMark(),
            demonMark = EnsureArtIcon("DemonMark", DemonHeadArt),
            title = EnsureArtIcon("Title", TitleArt),
            arrowLeft = EnsureArtIcon("ArrowLeft", ArrowLeftArt),
            arrowRight = EnsureArtIcon("ArrowRight", ArrowRightArt),
            speakerOn = EnsureArtIcon("SpeakerOn", SpeakerOnArt),
            speakerOff = EnsureArtIcon("SpeakerOff", SpeakerOffArt),
            frameCorner = EnsureArtIcon("FrameCorner", FrameCornerArt),
        };
    }

    /// <summary>A blank key outline; the letter is drawn on top from the alphabet.</summary>
    static Sprite EnsureKeyCap()
    {
        const int size = 12;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = edge && !corner ? White : Clear;
            }
        }
        return WriteIcon("KeyCap", size, size, pixels, size);
    }

    /// <summary>A right-pointing triangle: the one prompt on the title screen, and it blinks.</summary>
    static Sprite EnsurePlayMark()
    {
        const int size = 12;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            // Widest at the vertical centre, tapering to a point on the right.
            int reach = size - 1 - 2 * Mathf.Abs(y - (size - 1) / 2);
            for (int x = 0; x <= reach && x < size; x++) pixels[y * size + x] = White;
        }
        return WriteIcon("PlayMark", size, size, pixels, size);
    }

    /// <summary>
    /// A to Z as 5x7 glyphs, each centred in a 7x7 cell so SliceStrip's square-cell slicing lands
    /// on them exactly. White, so a renderer's colour picks the shade.
    /// </summary>
    static readonly string[] Alphabet =
    {
        ".###." + "#...#" + "#...#" + "#####" + "#...#" + "#...#" + "#...#", // A
        "####." + "#...#" + "#...#" + "####." + "#...#" + "#...#" + "####.", // B
        ".###." + "#...#" + "#...." + "#...." + "#...." + "#...#" + ".###.", // C
        "####." + "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + "####.", // D
        "#####" + "#...." + "#...." + "####." + "#...." + "#...." + "#####", // E
        "#####" + "#...." + "#...." + "####." + "#...." + "#...." + "#....", // F
        ".###." + "#...#" + "#...." + "#..##" + "#...#" + "#...#" + ".###.", // G
        "#...#" + "#...#" + "#...#" + "#####" + "#...#" + "#...#" + "#...#", // H
        "#####" + "..#.." + "..#.." + "..#.." + "..#.." + "..#.." + "#####", // I
        "....#" + "....#" + "....#" + "....#" + "....#" + "#...#" + ".###.", // J
        "#...#" + "#..#." + "#.#.." + "##..." + "#.#.." + "#..#." + "#...#", // K
        "#...." + "#...." + "#...." + "#...." + "#...." + "#...." + "#####", // L
        "#...#" + "##.##" + "#.#.#" + "#.#.#" + "#...#" + "#...#" + "#...#", // M
        "#...#" + "##..#" + "#.#.#" + "#.#.#" + "#..##" + "#...#" + "#...#", // N
        ".###." + "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".###.", // O
        "####." + "#...#" + "#...#" + "####." + "#...." + "#...." + "#....", // P
        ".###." + "#...#" + "#...#" + "#...#" + "#.#.#" + "#..#." + ".##.#", // Q
        "####." + "#...#" + "#...#" + "####." + "#.#.." + "#..#." + "#...#", // R
        ".####" + "#...." + "#...." + ".###." + "....#" + "....#" + "####.", // S
        "#####" + "..#.." + "..#.." + "..#.." + "..#.." + "..#.." + "..#..", // T
        "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".###.", // U
        "#...#" + "#...#" + "#...#" + "#...#" + "#...#" + ".#.#." + "..#..", // V
        "#...#" + "#...#" + "#...#" + "#.#.#" + "#.#.#" + "##.##" + "#...#", // W
        "#...#" + "#...#" + ".#.#." + "..#.." + ".#.#." + "#...#" + "#...#", // X
        "#...#" + "#...#" + ".#.#." + "..#.." + "..#.." + "..#.." + "..#..", // Y
        "#####" + "....#" + "...#." + "..#.." + ".#..." + "#...." + "#####", // Z
    };

    static Sprite[] EnsureLetters()
    {
        const int cell = 7;   // square, so SliceStrip finds 26 frames in a 182x7 strip
        const int glyph = 5;
        const int rows = 7;
        const int pad = (cell - glyph) / 2;

        int count = Alphabet.Length;
        int width = cell * count;
        var pixels = new Color32[width * cell];

        for (int i = 0; i < count; i++)
        {
            string art = Alphabet[i];
            for (int row = 0; row < rows; row++)
            {
                int y = rows - 1 - row; // art reads top-down, textures build bottom-up
                for (int x = 0; x < glyph; x++)
                {
                    if (art[row * glyph + x] != '#') continue;
                    pixels[y * width + i * cell + x + pad] = White;
                }
            }
        }

        string path = $"{IconFolder}/Letters.png";
        WritePng(path, width, cell, pixels);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return texture != null ? SliceStrip(texture) : new Sprite[0];
    }

    // Rows read top-down. '#' red, 'x' black, 'o' grey, 'w' white, '.' transparent.
    static readonly string[] QuestionArt =
    {
        "................",
        "................",
        "....wwwwwwww....",
        "...wwwwwwwwww...",
        "...www....www...",
        "..........www...",
        ".........wwww...",
        "......wwwwww....",
        "......wwww......",
        "......www.......",
        "......www.......",
        "................",
        "......www.......",
        "......www.......",
        "................",
        "................",
    };

    static readonly string[] ExclaimArt =
    {
        "................",
        "................",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "......wwww......",
        "................",
        "................",
        "......wwww......",
        "......wwww......",
        "................",
        "................",
    };

    static readonly string[] SkullArt =
    {
        "................",
        "....wwwwwwww....",
        "..wwwwwwwwwwww..",
        ".wwwwwwwwwwwwww.",
        ".wwwwwwwwwwwwww.",
        ".www......wwwww.",
        ".ww........wwww.",
        ".ww........wwww.",
        ".www......wwwww.",
        ".wwwwwwwwwwwwww.",
        "..wwwwwwwwwwww..",
        "..wwwwwwwwwwww..",
        "...ww.ww.ww.ww..",
        "...wwwwwwwwww...",
        "....wwwwwwww....",
        "................",
    };

    // The lens end points up (+Y), which is the direction the cone looks.
    // A dome seen from above, facing up; the eye sits forward so you can see where it looks. Drawn
    // in its own black and grey, because a grey body on the grey floor vanished and left only the
    // red lens. From Assets/work folder/camera-art/draw_camera.py, option B.
    static readonly string[] CameraBodyArt =
    {
        "....xxxx....",
        "..xxooooxx..",
        ".xooxxxxoox.",
        ".xoxxxxxxox.",
        "xooxxxxxxoox",
        "xoooxxxxooox",
        "xoooooooooox",
        "xoooooooooox",
        ".xoooooooox.",
        ".xoooooooox.",
        "..xxooooxx..",
        "....xxxx....",
    };

    // The arm and wall plate, which stay still while the dome sweeps.
    static readonly string[] CameraMountArt =
    {
        "....xoox....",
        "....xoox....",
        "....xoox....",
        "..xxxxxxxx..",
        "..xoooooox..",
        "..xxxxxxxx..",
    };

    static Color32 FromCode(char code)
    {
        switch (code)
        {
            case '#': return Red;
            case 'x': return Black;
            case 'o': return Grey;
            case 'w': return White;
            default: return Clear;
        }
    }

    /// <summary>White art so a SpriteRenderer's colour decides the shade; keeps one sprite reusable.</summary>
    static Sprite EnsureArtIcon(string name, string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var pixels = new Color32[width * height];
        for (int row = 0; row < height; row++)
        {
            // Row 0 is the top of the art, but texture row 0 is the bottom.
            int y = height - 1 - row;
            for (int x = 0; x < width; x++) pixels[y * width + x] = FromCode(rows[row][x]);
        }
        return WriteIcon(name, width, height, pixels, height);
    }

    static Sprite EnsureDisc(string name, int size, Color32 color, bool hollow)
    {
        var pixels = new Color32[size * size];
        float centre = (size - 1) * 0.5f;
        float outer = size * 0.5f;
        float inner = outer - Mathf.Max(1.5f, size * 0.06f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                bool on = distance <= outer - 0.5f && (!hollow || distance >= inner);
                pixels[y * size + x] = on ? color : Clear;
            }
        }
        return WriteIcon(name, size, size, pixels, size);
    }

    /// <summary>A black-edged grey locker; the occupied one shows a red door.</summary>
    static Sprite EnsureLocker(string name, bool occupied)
    {
        const int width = 16;
        const int height = 24;
        var pixels = new Color32[width * height];
        Color32 door = occupied ? Red : Grey;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool edge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                bool split = x == width / 2;                       // the two doors meet here
                bool handle = (x == width / 2 - 2 || x == width / 2 + 2) && y > height / 2 - 2 && y < height / 2 + 2;
                Color32 color = edge || split ? Black : handle ? Black : door;
                pixels[y * width + x] = color;
            }
        }
        return WriteIcon(name, width, height, pixels, height);
    }

    /// <summary>0-9 then ':' and '.', as one strip of 5x5 cells for SliceStrip.</summary>
    static readonly string[] DigitArt =
    {
        "###" + "#.#" + "#.#" + "#.#" + "###", // 0
        "..#" + "..#" + "..#" + "..#" + "..#", // 1
        "###" + "..#" + "###" + "#.." + "###", // 2
        "###" + "..#" + "###" + "..#" + "###", // 3
        "#.#" + "#.#" + "###" + "..#" + "..#", // 4
        "###" + "#.." + "###" + "..#" + "###", // 5
        "###" + "#.." + "###" + "#.#" + "###", // 6
        "###" + "..#" + "..#" + "..#" + "..#", // 7
        "###" + "#.#" + "###" + "#.#" + "###", // 8
        "###" + "#.#" + "###" + "..#" + "###", // 9
        "..." + ".#." + "..." + ".#." + "...", // :
        "..." + "..." + "..." + "..." + ".#.", // .
    };

    static Sprite[] EnsureDigits()
    {
        const int cell = 5;
        const int glyph = 3;
        int count = DigitArt.Length;
        int width = cell * count;
        var pixels = new Color32[width * cell];

        for (int i = 0; i < count; i++)
        {
            string art = DigitArt[i];
            for (int row = 0; row < cell; row++)
            {
                int y = cell - 1 - row; // art rows read top-down
                for (int x = 0; x < glyph; x++)
                {
                    bool on = art[row * glyph + x] == '#';
                    pixels[y * width + i * cell + x + 1] = on ? White : Clear;
                }
            }
        }

        string path = $"{IconFolder}/Digits.png";
        WritePng(path, width, cell, pixels);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return texture != null ? SliceStrip(texture) : new Sprite[0];
    }

    static Sprite WriteIcon(string name, int width, int height, Color32[] pixels, float pixelsPerUnit)
    {
        string path = $"{IconFolder}/{name}.png";
        WritePng(path, width, height, pixels);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Floorplan: could not import the generated icon {path}.");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void WritePng(string path, int width, int height, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>Scales a transform so its sprite covers the given world size.</summary>
    static void ScaleSprite(Transform transform, Sprite sprite, float width, float height)
    {
        if (sprite == null) return;
        Vector2 size = sprite.bounds.size;
        if (size.x <= 0f || size.y <= 0f) return;
        transform.localScale = new Vector3(width / size.x, height / size.y, 1f);
    }
}
