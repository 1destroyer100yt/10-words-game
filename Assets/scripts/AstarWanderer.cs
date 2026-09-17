using Pathfinding;
using UnityEngine;

/// <summary>
/// Keeps an A* Pathfinding Project agent (an IAstarAI such as AIPath) wandering between random
/// reachable points on the grid graph. Candidates are rejected when they are not walkable or lie
/// in a different connected region than the agent, so sealed rooms are never targeted.
/// </summary>
public class AstarWanderer : MonoBehaviour
{
    [Tooltip("Random samples tried per repath before falling back to the current position.")]
    public int maxAttempts = 50;

    /// <summary>Diagnostics: destinations picked so far.</summary>
    public int Picks { get; private set; }

    /// <summary>Diagnostics: picks that gave up and used the current position.</summary>
    public int Fallbacks { get; private set; }

    /// <summary>Diagnostics: samples the last pick needed.</summary>
    public int LastAttempts { get; private set; }

    IAstarAI ai;

    void OnEnable()
    {
        ai = GetComponent<IAstarAI>();
        if (ai == null)
        {
            Debug.LogError("AstarWanderer needs an IAstarAI component such as AIPath on the same object.", this);
        }
    }

    void Update()
    {
        if (GameRun.Instance != null && !GameRun.Instance.IsRunning) return;
        if (ai == null || AstarPath.active == null) return;
        if (ai.pathPending) return;
        if (ai.hasPath && !ai.reachedEndOfPath) return;

        ai.destination = PickDestination();
        ai.SearchPath();
    }

    Vector3 PickDestination()
    {
        Picks++;
        LastAttempts = 0;

        Vector3 fallback = ai.position;
        GridGraph graph = AstarPath.active.data.gridGraph;
        if (graph == null || graph.nodes == null) return Fallback(fallback);

        GraphNode current = AstarPath.active.GetNearest(fallback, NNConstraint.Default).node;
        if (current == null) return Fallback(fallback);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            LastAttempts = attempt + 1;

            // Graph space is measured in nodes, so this range covers exactly the graph bounds.
            Vector3 sample = graph.transform.Transform(new Vector3(
                UnityEngine.Random.Range(0f, graph.width), 0f, UnityEngine.Random.Range(0f, graph.depth)));

            GraphNode candidate = AstarPath.active.GetNearest(sample, NNConstraint.Default).node;
            if (candidate == null || !candidate.Walkable) continue;
            if (!PathUtilities.IsPathPossible(current, candidate)) continue;

            return (Vector3)candidate.position;
        }

        return Fallback(fallback);
    }

    Vector3 Fallback(Vector3 position)
    {
        Fallbacks++;
        return position;
    }
}
