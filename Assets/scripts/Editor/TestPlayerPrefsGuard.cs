using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the editor's automated runs (Play Test and Story Test) from overwriting the player's real
/// records. A test run is a real run, and GameRun saves its best time in OnDisable on the way OUT of
/// play mode, which happens after the test has already finished. So the records are stashed before
/// play starts and put back only once the editor is fully back in edit mode.
/// </summary>
[InitializeOnLoad]
public static class TestPlayerPrefsGuard
{
    const string PendingKey = "Floorplan.PrefsGuard.Pending";
    const string KeysKey = "Floorplan.PrefsGuard.Keys";

    static TestPlayerPrefsGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>Call just before a test enters play mode.</summary>
    public static void Stash()
    {
        string[] keys = RecordKeys();
        foreach (string key in keys)
        {
            SessionState.SetBool(key + ".has", PlayerPrefs.HasKey(key));
            SessionState.SetFloat(key + ".value", PlayerPrefs.GetFloat(key, 0f));
        }
        SessionState.SetString(KeysKey, string.Join("\n", keys));
        SessionState.SetBool(PendingKey, true);
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);

        string stored = SessionState.GetString(KeysKey, "");
        if (stored.Length == 0) return;
        foreach (string key in stored.Split('\n'))
        {
            if (SessionState.GetBool(key + ".has", false)) PlayerPrefs.SetFloat(key, SessionState.GetFloat(key + ".value", 0f));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        Debug.Log("Test run finished: the real best-time records were put back as they were.");
    }

    /// <summary>The keys GameRun writes, read off the open scene's GameRun so a renamed key is still covered.</summary>
    static string[] RecordKeys()
    {
        var run = Object.FindAnyObjectByType<GameRun>();
        string best = run != null ? run.bestTimeKey : "Floorplan.BestTime";
        string win = run != null ? run.bestWinKey : "Floorplan.BestWin";
        // Every difficulty keeps its own pair (GameRun.KeyFor); cover more levels than exist.
        var keys = new System.Collections.Generic.List<string>();
        for (int level = 1; level <= 9; level++)
        {
            keys.Add(GameRun.KeyFor(best, level));
            keys.Add(GameRun.KeyFor(win, level));
        }
        return keys.ToArray();
    }
}
