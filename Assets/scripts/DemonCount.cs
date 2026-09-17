using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The difficulty. Every demon the building can hold is built into the scene and then switched off;
/// the menu turns on as many as the player asked for. Switching rather than spawning keeps the A*
/// graph and all the wiring untouched between runs.
/// </summary>
public class DemonCount : MonoBehaviour, IRunResettable
{
    [Tooltip("All demons in the scene, in the order they are switched on.")]
    public List<GameObject> demons = new List<GameObject>();

    [Tooltip("Objects belonging to each demon that live outside it, such as its suspicion indicator.")]
    public List<GameObject> attachments = new List<GameObject>();

    /// <summary>How many are currently awake.</summary>
    public int Active { get; private set; } = 1;

    void Awake()
    {
        // Until the menu says otherwise, only the first one exists.
        SetActiveDemons(Active);
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
        SetActiveDemons(Active);
    }
}
