using System.Collections.Generic;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The Listener: a blind demon that hunts by sound, awake only on the hard levels. It is a copy of the
/// last ordinary demon (so it has the same minimap dot, pathfinding and warning icon), with its own
/// art, no sight and sharper hearing. Build Scene adds it at the end; Tools/Floorplan/Add Listener adds
/// it to the open scene without rebuilding the level.
/// </summary>
public static partial class FloorplanSetup
{
    const string ListenerFolder = "Assets/Listener";
    const string ListenerControllerPath = "Assets/Listener/Listener.overrideController";
    const string ListenerName = "Listener";
    const float ListenerHearing = 1.6f;

    /// <summary>The far side of the building from the player's spawn and the other demons.</summary>
    static readonly Vector3 ListenerSpawnHint = new Vector3(82f, 30f, 0f);

    [MenuItem("Tools/Floorplan/Add Listener")]
    public static void AddListenerToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Listener: stop Play mode first.");
            return;
        }
        GameObject listener = AddListener();
        if (listener == null) return;
        var scene = listener.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Listener: added at {listener.transform.position:F0}, awake on the hard levels. Scene saved.");
    }

    /// <summary>Adds (or replaces) the Listener in the open scene. Returns it, or null when it cannot.</summary>
    static GameObject AddListener()
    {
        var counter = Object.FindAnyObjectByType<DemonCount>(FindObjectsInactive.Include);
        if (counter == null || counter.demons.Count == 0)
        {
            Debug.LogError("Listener: no demons in the open scene. Open the main scene first.");
            return null;
        }

        Dictionary<string, Sprite[]> frames = FindListenerStrips();
        foreach (string state in EnemyStates)
        {
            if (!frames.ContainsKey(state))
            {
                Debug.LogWarning($"Listener: no '{state}' strip in {ListenerFolder}; the Listener was not added.");
                return null;
            }
        }

        // Replace, never stack: a second run of this removes the first Listener.
        for (int i = counter.hardOnly.Count - 1; i >= 0; i--)
        {
            if (counter.hardOnly[i] != null) Object.DestroyImmediate(counter.hardOnly[i]);
            if (i < counter.hardOnlyAttachments.Count && counter.hardOnlyAttachments[i] != null)
                Object.DestroyImmediate(counter.hardOnlyAttachments[i]);
        }
        counter.hardOnly.Clear();
        counter.hardOnlyAttachments.Clear();

        int last = counter.demons.Count - 1;
        GameObject source = counter.demons[last];
        GameObject sourceAttachment = last < counter.attachments.Count ? counter.attachments[last] : null;
        bool sourceWasActive = source.activeSelf;
        source.SetActive(true); // a copy of an inactive object would carry state from the wrong moment

        GameObject listener = Object.Instantiate(source, source.transform.parent);
        listener.name = ListenerName;
        source.SetActive(sourceWasActive);
        Undo.RegisterCreatedObjectUndo(listener, "Add Listener");

        listener.transform.position = ListenerSpawn();
        listener.transform.rotation = Quaternion.identity;

        // Its own art, through an override of the shared demon animator, so both keep the same states.
        var animator = listener.GetComponentInChildren<Animator>(true);
        var baseController = animator != null ? animator.runtimeAnimatorController : null;
        if (baseController is AnimatorOverrideController existingOverride) baseController = existingOverride.runtimeAnimatorController;
        if (animator != null && baseController != null)
        {
            EnsureFolder(ListenerFolder);
            if (AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ListenerControllerPath) != null)
                AssetDatabase.DeleteAsset(ListenerControllerPath);
            var overrides = new AnimatorOverrideController(baseController) { name = "Listener" };
            foreach (string state in EnemyStates)
            {
                AnimationClip original = null;
                foreach (AnimationClip clip in baseController.animationClips)
                {
                    if (clip != null && clip.name == $"Enemy_{state}") original = clip;
                }
                if (original == null) continue;
                overrides[original] = CreateSpriteClip($"{ListenerFolder}/Listener_{state}.anim", $"Listener_{state}",
                                                       frames[state], EnemyAnimationFps);
            }
            AssetDatabase.CreateAsset(overrides, ListenerControllerPath);
            animator.runtimeAnimatorController = overrides;

            var renderer = animator.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = frames["Idle"][0];
                // Same 100 px grid and scale as the other demon; only re-centre on its own drawing.
                animator.transform.localPosition = -VisibleBounds(frames["Idle"][0]).center;
            }
        }

        var chaser = listener.GetComponent<NpcChaser>();
        if (chaser != null)
        {
            chaser.hearingMultiplier = ListenerHearing;
            if (chaser.vision != null) chaser.vision.blind = true;
        }

        GameObject attachment = null;
        if (sourceAttachment != null)
        {
            attachment = Object.Instantiate(sourceAttachment, sourceAttachment.transform.parent);
            attachment.name = $"{sourceAttachment.name} ({ListenerName})";
            Undo.RegisterCreatedObjectUndo(attachment, "Add Listener");
            var indicator = attachment.GetComponent<NpcSuspicionIndicator>();
            if (indicator != null)
            {
                indicator.chaser = chaser;
                indicator.reveal = listener.GetComponent<MinimapReveal>();
            }
            attachment.SetActive(false);
        }

        listener.SetActive(false); // DemonCount wakes it on the hard levels
        counter.hardOnly.Add(listener);
        counter.hardOnlyAttachments.Add(attachment);
        EditorUtility.SetDirty(counter);
        AssetDatabase.SaveAssets();
        return listener;
    }

    static Vector3 ListenerSpawn()
    {
        var astar = Object.FindAnyObjectByType<AstarPath>();
        if (astar == null) return ListenerSpawnHint;
        if (astar.data.graphs == null || astar.data.graphs.Length == 0) astar.data.DeserializeGraphs();
        // In edit mode the graph has settings but no nodes until it is scanned, and the hint alone can
        // land inside a wall (it did: 82,30 is a wall). Scan, then take the nearest walkable floor.
        astar.Scan();
        GridGraph graph = astar.data.gridGraph;
        if (graph == null || graph.nodes == null || graph.nodes.Length == 0)
        {
            Debug.LogWarning("Listener: the graph could not be scanned; placed at the hint, check it is on the floor.");
            return ListenerSpawnHint;
        }
        return FindSpawnPosition(graph, ListenerSpawnHint);
    }

    /// <summary>The Listener's strips, by state word, from its own folder only.</summary>
    static Dictionary<string, Sprite[]> FindListenerStrips()
    {
        var result = new Dictionary<string, Sprite[]>();
        if (!AssetDatabase.IsValidFolder(ListenerFolder)) return result;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ListenerFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            for (int state = 0; state < EnemyStates.Length; state++)
            {
                if (result.ContainsKey(EnemyStates[state])) continue;
                bool matches = false;
                foreach (string keyword in EnemyStateKeywords[state]) matches |= name.Contains(keyword);
                if (!matches) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) break;
                Sprite[] sliced = SliceStrip(texture);
                if (sliced.Length > 0) result[EnemyStates[state]] = sliced;
                break;
            }
        }
        return result;
    }
}
