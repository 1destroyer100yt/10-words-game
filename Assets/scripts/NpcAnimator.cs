using Pathfinding;
using UnityEngine;

/// <summary>
/// Feeds the enemy's Animator from its behaviour: "Moving" (bool) from the agent's velocity and
/// "Mode" (int, 0 wander / 1 chase / 2 search) from NpcChaser. The animator picks Idle, Walk,
/// Chase or Search from those two values.
/// </summary>
public class NpcAnimator : MonoBehaviour
{
    public Animator animator;
    public NpcChaser chaser;

    [Tooltip("Speed above which the enemy counts as moving.")]
    public float movingSpeed = 0.2f;

    static readonly int MovingParameter = Animator.StringToHash("Moving");
    static readonly int ModeParameter = Animator.StringToHash("Mode");

    IAstarAI ai;

    void Awake()
    {
        ai = GetComponent<IAstarAI>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (chaser == null) chaser = GetComponent<NpcChaser>();
    }

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        bool moving = ai != null && ai.velocity.magnitude > movingSpeed;
        animator.SetBool(MovingParameter, moving);
        animator.SetInteger(ModeParameter, chaser != null ? (int)chaser.CurrentMode : 0);
    }
}
