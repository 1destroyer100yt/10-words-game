/// <summary>
/// Implemented by anything that must go back to its start state when a run restarts (the player
/// was caught or pressed R). GameRun calls ResetRun on every implementation in the scene instead of
/// reloading the scene, so the A* graph is never rescanned.
/// </summary>
public interface IRunResettable
{
    void ResetRun();
}
