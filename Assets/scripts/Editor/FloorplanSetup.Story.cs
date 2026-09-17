using System.Collections.Generic;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the story on top of the finished level: the clue marks that eliminate the building wing
/// by wing, the minimap crosses and search box that show it happening, the portal activator, the
/// way out's two sockets, and the full-screen picture frames that open and close the game. Not one
/// letter is placed here; the ten words are already spent.
/// </summary>
public static partial class FloorplanSetup
{
    const string StoryDirectorName = "Story Director";
    const string CluesName = "Clues";
    const string ZoneMarksName = "Zone Marks";
    const string ActivatorName = "Portal Activator";
    const string CutsceneName = "Cutscene";

    /// <summary>The building is cut into a 3x2 grid of wings; a clue crosses one of them off.</summary>
    const int ZoneColumns = 3;
    const int ZoneRows = 2;
    const int ZoneCount = ZoneColumns * ZoneRows;

    /// <summary>A wing needs at least this many walkable nodes to be worth sending the player to.</summary>
    const int MinZoneNodes = 400;

    const float ClueSize = 1.5f;
    const float ActivatorSize = 1.4f;
    const float SocketSize = 0.9f;
    const float ZoneLineThickness = 1.1f;
    const float ZoneInset = 2.5f;
    const float PipSize = 2.4f;
    const float PipSpacing = 3.6f;

    /// <summary>A mark burned into a wall. White, so the renderer's colour decides the shade.</summary>
    static readonly string[] ClueArt =
    {
        "...ww........",
        "..ww...www...",
        ".ww...ww.ww..",
        ".w........ww.",
        "ww..www....w.",
        "w..ww.ww.....",
        "...w...ww..ww",
        ".ww....w...w.",
        "..ww......ww.",
        "...www...ww..",
        ".....w..ww...",
        ".....ww......",
        "......w......",
    };

    /// <summary>The portal activator: a key, because it is the thing the portal is missing.</summary>
    static readonly string[] ActivatorArt =
    {
        "..w............",
        "..ww..ww.......",
        ".wwwwwww.......",
        "ww.....ww......",
        "w...w...w......",
        "ww..w..ww......",
        ".ww...www......",
        "..wwwwwwww.....",
        ".......wwww..ww",
        "........wwwwww.",
        ".........www...",
        "..........wwwww",
        "...........www.",
    };

    /// <summary>Applies an art redraw without regenerating the level or moving its objectives.</summary>
    [MenuItem("Tools/Floorplan/Refresh Story Artwork")]
    public static void RefreshStoryArtwork()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Story artwork: stop Play mode first.");
            return;
        }

        var director = Object.FindAnyObjectByType<StoryDirector>();
        if (director == null)
        {
            Debug.LogError("Story artwork: open the main scene first.");
            return;
        }

        Sprite clue = EnsureArtIcon("ClueMark", ClueArt);
        Sprite key = EnsureArtIcon("ActivatorMark", ActivatorArt);
        foreach (Clue mark in director.clues)
        {
            if (mark == null) continue;
            RefreshStoryRenderer(mark.body, clue, ClueSize);
            RefreshStoryRenderer(mark.marker, clue, 2.4f);
        }
        RefreshStoryRenderer(director.activatorBody, key, ActivatorSize);
        RefreshStoryRenderer(director.activatorMarker, key, 3f);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        Debug.Log("Story artwork refreshed: scratched clues and eye key. Save the main scene to keep the updated sizing.");
    }

    static void RefreshStoryRenderer(SpriteRenderer renderer, Sprite sprite, float worldSize)
    {
        if (renderer == null || sprite == null) return;
        Undo.RecordObjects(new Object[] { renderer, renderer.transform }, "Refresh story artwork");
        renderer.sprite = sprite;
        Vector3 parent = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        ScaleSprite(renderer.transform, sprite, worldSize / parent.x, worldSize / parent.y);
    }

    struct Zone
    {
        public Rect area;
        public Vector3 spot;   // a walkable position in it, as near the middle as the walls allow
        public int nodes;
        public bool viable;
    }

    /// <summary>
    /// Places everything the story needs and wires the director. Runs last, so the level, the
    /// objective and the HUD all exist to be pointed at.
    /// </summary>
    static void CreateStory(FeatureContext ctx, Icons icons, GameAudio audioBank, Objective objective,
                            GameObject hud, GameRun run, StoryCards cards)
    {
        if (objective == null || run == null || ctx.player == null || ctx.graph == null)
        {
            Debug.LogWarning("Floorplan: the story was skipped because the level did not finish building.");
            return;
        }

        GameObject root = CreateRoot(StoryDirectorName);
        var director = root.AddComponent<StoryDirector>();

        Vector3 playerAt = ctx.player.transform.position;
        var mapSize = new Vector2(ctx.map.width / PixelsPerUnit, ctx.map.height / PixelsPerUnit);
        Zone[] zones = ScanZones(ctx.graph, mapSize, playerAt);

        int jewelZone = PickJewelZone(zones, playerAt);
        if (jewelZone < 0)
        {
            Debug.LogError("Floorplan: no wing of the building had enough walkable floor to hide the jewel in. " +
                           "The story was skipped and the jewel stays where the objective put it.");
            Object.DestroyImmediate(root);
            return;
        }

        // The jewel moves into the one wing the clues cannot eliminate.
        Vector3 jewelAt = zones[jewelZone].spot;
        if (objective.jewel != null) objective.jewel.position = jewelAt;
        objective.markJewel = false; // the map gives you a wing, never a dot

        Sprite clueSprite = EnsureArtIcon("ClueMark", ClueArt);
        Sprite activatorSprite = EnsureArtIcon("ActivatorMark", ActivatorArt);

        Clue[] clues = CreateClues(ctx, clueSprite, zones, jewelZone);
        GameObject[] crosses = CreateZoneMarks(ctx, zones, jewelZone, out GameObject searchBox);
        SpriteRenderer[] pips = CreatePips(ctx, clues.Length);

        GameObject activator = CreateActivator(ctx, activatorSprite, zones, jewelZone, playerAt,
                                               out SpriteRenderer activatorBody, out SpriteRenderer activatorMarker);
        CreateSockets(ctx, icons, objective, out SpriteRenderer jewelSocket, out SpriteRenderer activatorSocket);
        PixelCutscene cutscene = CreateCutscene(hud, cards);

        director.run = run;
        director.objective = objective;
        director.cards = cards;
        director.cutscene = cutscene;
        director.player = ctx.player.transform;
        director.audioBank = audioBank;
        director.effects = ctx.volume != null ? ctx.volume.GetComponent<PlayerCameraEffects>() : null;
        director.jewelBody = objective.jewel != null ? objective.jewel.GetComponent<SpriteRenderer>() : null;
        director.clues = clues;
        director.zoneCrosses = crosses;
        director.searchBox = searchBox;
        director.pips = pips;
        director.activator = activator != null ? activator.transform : null;
        director.activatorBody = activatorBody;
        director.activatorMarker = activatorMarker;
        director.jewelSocket = jewelSocket;
        director.activatorSocket = activatorSocket;
        director.armedColor = Red;
        director.dormantColor = Grey;

        run.director = director;
        if (cards != null) cards.deferOpening = true; // the director lands it on the theft frame

        Debug.Log($"Floorplan: story built. {clues.Length} clues eliminate {clues.Length} of {ZoneCount} wings; " +
                  $"the jewel hides in wing {jewelZone} at {jewelAt.x:F0},{jewelAt.y:F0}; " +
                  $"the activator is {(activator != null ? Vector2.Distance(activator.transform.position, playerAt).ToString("F0") : "?")} " +
                  "units from the way out. Four opening frames, two ending frames, no new words.");
    }

    // ------------------------------------------------------------------ the wings

    /// <summary>
    /// One pass over the graph: buckets every walkable node of the player's own region into a wing,
    /// and keeps the node nearest each wing's middle as the spot to put things.
    /// </summary>
    static Zone[] ScanZones(GridGraph graph, Vector2 mapSize, Vector3 playerAt)
    {
        var walkable = new List<KeyValuePair<uint, Vector3>>();
        uint playerArea = 0;
        float nearestToPlayer = float.MaxValue;

        graph.GetNodes(node =>
        {
            if (!node.Walkable) return;
            Vector3 at = (Vector3)node.position;
            walkable.Add(new KeyValuePair<uint, Vector3>(node.Area, at));

            float distance = (at - playerAt).sqrMagnitude;
            if (distance >= nearestToPlayer) return;
            nearestToPlayer = distance;
            playerArea = node.Area;
        });

        var zones = new Zone[ZoneCount];
        var best = new float[ZoneCount];
        float width = mapSize.x / ZoneColumns;
        float height = mapSize.y / ZoneRows;

        for (int i = 0; i < ZoneCount; i++)
        {
            int column = i % ZoneColumns;
            int row = i / ZoneColumns;
            zones[i].area = new Rect(column * width, row * height, width, height);
            zones[i].spot = zones[i].area.center;
            best[i] = float.MaxValue;
        }

        // Only the region the player can actually walk out of; an island behind a wall is no good.
        foreach (KeyValuePair<uint, Vector3> node in walkable)
        {
            if (node.Key != playerArea) continue;
            int index = ZoneOf(node.Value, mapSize);
            zones[index].nodes++;
            float distance = ((Vector2)node.Value - zones[index].area.center).sqrMagnitude;
            if (distance >= best[index]) continue;
            best[index] = distance;
            zones[index].spot = node.Value;
        }

        for (int i = 0; i < ZoneCount; i++) zones[i].viable = zones[i].nodes >= MinZoneNodes;
        return zones;
    }

    static int ZoneOf(Vector3 at, Vector2 mapSize)
    {
        int column = Mathf.Clamp(Mathf.FloorToInt(at.x / (mapSize.x / ZoneColumns)), 0, ZoneColumns - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt(at.y / (mapSize.y / ZoneRows)), 0, ZoneRows - 1);
        return row * ZoneColumns + column;
    }

    /// <summary>The jewel hides in the viable wing furthest from where the player comes in.</summary>
    static int PickJewelZone(Zone[] zones, Vector3 playerAt)
    {
        int chosen = -1;
        float furthest = -1f;
        for (int i = 0; i < zones.Length; i++)
        {
            if (!zones[i].viable) continue;
            float distance = Vector2.Distance(zones[i].spot, playerAt);
            if (distance <= furthest) continue;
            furthest = distance;
            chosen = i;
        }
        return chosen;
    }

    // ------------------------------------------------------------------ the clues

    static Clue[] CreateClues(FeatureContext ctx, Sprite sprite, Zone[] zones, int jewelZone)
    {
        GameObject root = CreateRoot(CluesName);
        var clues = new List<Clue>();

        for (int i = 0; i < zones.Length; i++)
        {
            // A clue sits in the wing it rules out: you search it, find nothing, and cross it off.
            if (i == jewelZone || !zones[i].viable) continue;

            var go = new GameObject($"Clue {i}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = zones[i].spot;

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.color = Red;
            body.sortingOrder = 1;
            if (ctx.unlit != null) body.sharedMaterial = ctx.unlit; // readable across a dark room
            ScaleSprite(go.transform, sprite, ClueSize, ClueSize);

            var clue = go.AddComponent<Clue>();
            clue.zone = i;
            clue.body = body;
            clue.marker = CreateStoryMarker(go, sprite, Red, ctx, 2.4f);
            clue.armedColor = Red;
            clue.dormantColor = Grey;
            clues.Add(clue);
        }

        return clues.ToArray();
    }

    // ------------------------------------------------------------------ what the map shows

    /// <summary>A grey cross per wing, off until it is eliminated, plus the red box round the last one.</summary>
    static GameObject[] CreateZoneMarks(FeatureContext ctx, Zone[] zones, int jewelZone, out GameObject searchBox)
    {
        GameObject root = CreateRoot(ZoneMarksName);
        var crosses = new GameObject[zones.Length];
        searchBox = null;
        if (ctx.minimapLayer < 0) return crosses;

        Sprite pixel = EnsurePixelSprite();
        if (pixel == null) return crosses;

        for (int i = 0; i < zones.Length; i++)
        {
            Rect area = Inset(zones[i].area, ZoneInset);

            if (i == jewelZone)
            {
                // The one wing left standing: a hollow red box, so the player is sent to an area
                // rather than a point. They still have to walk it.
                searchBox = new GameObject("Search Zone");
                searchBox.transform.SetParent(root.transform, false);
                searchBox.transform.position = area.center;
                Bar(searchBox, pixel, ctx, Red, 61, new Vector2(0f, area.height * 0.5f), area.width, ZoneLineThickness, 0f);
                Bar(searchBox, pixel, ctx, Red, 61, new Vector2(0f, -area.height * 0.5f), area.width, ZoneLineThickness, 0f);
                Bar(searchBox, pixel, ctx, Red, 61, new Vector2(-area.width * 0.5f, 0f), area.height, ZoneLineThickness, 90f);
                Bar(searchBox, pixel, ctx, Red, 61, new Vector2(area.width * 0.5f, 0f), area.height, ZoneLineThickness, 90f);
                searchBox.SetActive(false);
                continue;
            }

            var cross = new GameObject($"Crossed {i}");
            cross.transform.SetParent(root.transform, false);
            cross.transform.position = area.center;
            float diagonal = Mathf.Sqrt(area.width * area.width + area.height * area.height);
            float angle = Mathf.Atan2(area.height, area.width) * Mathf.Rad2Deg;
            Bar(cross, pixel, ctx, Grey, 60, Vector2.zero, diagonal, ZoneLineThickness, angle);
            Bar(cross, pixel, ctx, Grey, 60, Vector2.zero, diagonal, ZoneLineThickness, -angle);
            cross.SetActive(false);
            crosses[i] = cross;
        }

        return crosses;
    }

    static Rect Inset(Rect area, float by)
    {
        return new Rect(area.x + by, area.y + by, Mathf.Max(1f, area.width - 2f * by), Mathf.Max(1f, area.height - 2f * by));
    }

    /// <summary>One straight line of the map overlay, drawn as a scaled pixel quad.</summary>
    static void Bar(GameObject parent, Sprite pixel, FeatureContext ctx, Color color, int sortingOrder,
                    Vector2 offset, float length, float thickness, float degrees)
    {
        var go = new GameObject("Bar");
        go.layer = ctx.minimapLayer;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = offset;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (ctx.unlit != null) renderer.sharedMaterial = ctx.unlit;

        Vector2 size = pixel.bounds.size;
        go.transform.localScale = new Vector3(length / size.x, thickness / size.y, 1f);
    }

    /// <summary>
    /// One mark per clue, inside the bottom of the minimap frame where the floorplan does not
    /// reach. They go red as wings are crossed off, so progress is always on screen without a word.
    /// </summary>
    static SpriteRenderer[] CreatePips(FeatureContext ctx, int count)
    {
        var pips = new SpriteRenderer[count];
        if (count <= 0 || ctx.minimapLayer < 0 || MinimapCamera == null) return pips;

        Sprite pixel = EnsurePixelSprite();
        if (pixel == null) return pips;

        var mapSize = new Vector2(ctx.map.width / PixelsPerUnit, ctx.map.height / PixelsPerUnit);
        float span = Mathf.Max(mapSize.x, mapSize.y) + 2f * (MinimapFrame + MinimapInset);
        // The square minimap is taller than the floorplan, leaving dead panel under the building.
        float y = (mapSize.y - span) * 0.5f + MinimapFrame + PipSize;
        float left = mapSize.x * 0.5f - (count - 1) * PipSpacing * 0.5f;

        var root = new GameObject("Clue Marks");
        root.transform.SetParent(MinimapCamera.transform, false);
        root.transform.position = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Mark {i}");
            go.layer = ctx.minimapLayer;
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(left + i * PipSpacing, y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = pixel;
            renderer.color = Grey;
            renderer.sortingOrder = 70;
            if (ctx.unlit != null) renderer.sharedMaterial = ctx.unlit;

            Vector2 size = pixel.bounds.size;
            go.transform.localScale = new Vector3(PipSize / size.x, PipSize / size.y, 1f);
            pips[i] = renderer;
        }

        return pips;
    }

    // ------------------------------------------------------------------ the activator and the sockets

    static GameObject CreateActivator(FeatureContext ctx, Sprite sprite, Zone[] zones, int jewelZone,
                                      Vector3 playerAt, out SpriteRenderer body, out SpriteRenderer marker)
    {
        body = null;
        marker = null;

        // Not in the jewel's wing and not on the doorstep: finding it has to be its own journey.
        int chosen = -1;
        float best = -1f;
        for (int i = 0; i < zones.Length; i++)
        {
            if (i == jewelZone || !zones[i].viable) continue;
            float score = Mathf.Min(Vector2.Distance(zones[i].spot, playerAt),
                                    Vector2.Distance(zones[i].spot, zones[jewelZone].spot));
            if (score <= best) continue;
            best = score;
            chosen = i;
        }
        if (chosen < 0) return null;

        GameObject root = CreateRoot(ActivatorName);
        root.transform.position = zones[chosen].spot;

        body = root.AddComponent<SpriteRenderer>();
        body.sprite = sprite;
        body.color = Red;
        body.sortingOrder = 2;
        if (ctx.unlit != null) body.sharedMaterial = ctx.unlit;
        ScaleSprite(root.transform, sprite, ActivatorSize, ActivatorSize);

        marker = CreateStoryMarker(root, sprite, Red, ctx, 3f);
        body.enabled = false;   // hidden until the jewel is yours
        marker.enabled = false;
        return root;
    }

    /// <summary>
    /// The way out's two sockets: one for the jewel, one for the activator. One filled and one
    /// empty is a list of what is left to do, with no words in it.
    /// </summary>
    static void CreateSockets(FeatureContext ctx, Icons icons, Objective objective,
                              out SpriteRenderer jewelSocket, out SpriteRenderer activatorSocket)
    {
        jewelSocket = null;
        activatorSocket = null;
        if (objective.exit == null || icons.ring == null) return;

        jewelSocket = Socket(ctx, icons, objective.exit, "Jewel Socket", new Vector3(0f, 2f, 0f));
        activatorSocket = Socket(ctx, icons, objective.exit, "Activator Socket", new Vector3(0f, -2f, 0f));
    }

    static SpriteRenderer Socket(FeatureContext ctx, Icons icons, Transform exit, string name, Vector3 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(exit, false);
        // The exit sprite is scaled to cover 2.8 units, so undo that before positioning the socket.
        float parentScale = Mathf.Max(exit.lossyScale.x, 0.0001f);
        go.transform.localPosition = offset / parentScale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = icons.ring;
        renderer.color = Grey;
        renderer.sortingOrder = -3;
        if (ctx.unlit != null) renderer.sharedMaterial = ctx.unlit;
        ScaleSprite(go.transform, icons.ring, SocketSize / parentScale, SocketSize / parentScale);
        return renderer;
    }

    /// <summary>A dot on the minimap for a story object, sized past whatever its owner is scaled to.</summary>
    static SpriteRenderer CreateStoryMarker(GameObject owner, Sprite sprite, Color color, FeatureContext ctx, float size)
    {
        var go = new GameObject(MinimapMarkerName);
        go.layer = ctx.minimapLayer >= 0 ? ctx.minimapLayer : owner.layer;
        go.transform.SetParent(owner.transform, false);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 96;
        if (ctx.unlit != null) renderer.sharedMaterial = ctx.unlit;

        float parentScale = Mathf.Max(owner.transform.lossyScale.x, 0.0001f);
        ScaleSprite(go.transform, sprite, size / parentScale, size / parentScale);
        return renderer;
    }

    // ------------------------------------------------------------------ the picture frames

    /// <summary>
    /// Parked directly under the word cards on the HUD, so the four opening words land on top of
    /// the picture of the theft instead of behind it.
    /// </summary>
    static PixelCutscene CreateCutscene(GameObject hud, StoryCards cards)
    {
        if (hud == null) return null;

        var go = new GameObject(CutsceneName, typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (cards != null) rect.SetSiblingIndex(cards.transform.GetSiblingIndex());
        return go.AddComponent<PixelCutscene>();
    }
}
