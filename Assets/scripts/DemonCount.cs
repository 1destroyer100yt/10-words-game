using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The difficulty. Every demon the building can hold is built into the scene and then switched off;
/// the menu turns on as many as the player asked for. Switching rather than spawning keeps the A*
/// graph and all the wiring untouched between runs.
///
/// The levels past the last demon keep every demon awake and make all of them more dangerous: see
/// hardTiers. Level 1..demons.Count is that many demons at their built strength.
/// </summary>
public class DemonCount : MonoBehaviour, IRunResettable
{
    /// <summary>How much more dangerous every demon is at one hard level.</summary>
    [System.Serializable]
    public struct Tier
    {
        [Tooltip("Multiplier on chase and search speed. The player walks at 8 and runs at 12.8; demons chase at 7.")]
        public float chaseSpeed;

        [Tooltip("Multiplier on wandering speed.")]
        public float wanderSpeed;

        [Tooltip("Multiplier on the seconds of sight before a chase starts. Lower is quicker.")]
        public float detectTime;

        [Tooltip("Units added to how far a demon sees.")]
        public float sight;

        [Tooltip("Units added to how close a search must end to a locker to find the player inside.")]
        public float findHidden;

        [Tooltip("Seconds added to how long a demon searches before it gives up.")]
        public float searchTime;
    }

    static readonly Tier Built = new Tier { chaseSpeed = 1f, wanderSpeed = 1f, detectTime = 1f };

    [Tooltip("All demons in the scene, in the order they are switched on.")]
    public List<GameObject> demons = new List<GameObject>();

    [Tooltip("Objects belonging to each demon that live outside it, such as its suspicion indicator.")]
    public List<GameObject> attachments = new List<GameObject>();

    [Tooltip("Levels past the last demon, easiest first. Every demon is awake on these.")]
    public Tier[] hardTiers =
    {
        // Faster than walking: a chase has to be outrun, not outwalked.
        new Tier { chaseSpeed = 1.2f, wanderSpeed = 1.1f, detectTime = 0.7f, sight = 2f, findHidden = 0.5f, searchTime = 1.5f },
        // Close to running speed once the jewel is taken, and they see as far as your light does.
        new Tier { chaseSpeed = 1.45f, wanderSpeed = 1.25f, detectTime = 0.45f, sight = 4f, findHidden = 1f, searchTime = 3f },
    };

    /// <summary>How many are currently awake.</summary>
    public int Active { get; private set; } = 1;

    /// <summary>The chosen difficulty, 1..Levels.</summary>
    public int Level { get; private set; } = 1;

    /// <summary>One level per demon, then one per hard tier.</summary>
    public int Levels => demons.Count + (hardTiers != null ? hardTiers.Length : 0);

    void Awake()
    {
        // Until the menu says otherwise, only the first one exists.
        SetLevel(Level);
    }

    /// <summary>Wakes the demons this level calls for and gives every one of them its strength.</summary>
    public void SetLevel(int level)
    {
        Level = Mathf.Clamp(level, 1, Mathf.Max(1, Levels));
        SetActiveDemons(Level);

        int tier = Level - demons.Count; // 1 is the first hard tier
        Tier strength = hardTiers != null && tier >= 1 && tier <= hardTiers.Length ? hardTiers[tier - 1] : Built;
        foreach (GameObject demon in demons)
        {
            if (demon == null) continue;
            var chaser = demon.GetComponent<NpcChaser>();
            if (chaser == null) continue;
            chaser.SetStrength(strength.chaseSpeed, strength.wanderSpeed, strength.detectTime, strength.findHidden, strength.searchTime);
            if (chaser.vision != null) chaser.vision.bonusDistance = strength.sight;
        }
    }

    public void SetActiveDemons(int count)
    {
        Active = Mathf.Clamp(count, 0, demons.Count);

        for (int i = 0; i < demons.Count; i++)
        {
            bool on = i < Active;
            if (demons[i] != null && demons[i].activeSelf != on) demons[i].SetActive(on);
            if (i < attachments.Count && attachments[i] != null && attachments[i].activeSelf != on)
            {
                attachments[i].SetActive(on);
            }
        }
    }

    /// <summary>A restart keeps the chosen difficulty; only the menu changes it.</summary>
    public void ResetRun()
    {
        SetLevel(Level);
    }
}
