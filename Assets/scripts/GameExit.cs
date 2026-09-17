using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class GameExit
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void QuitDebtGame();
#endif
    public static void Quit()
    {
        PlayerPrefs.Save();
        AudioListener.pause = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Time.timeScale = 0f;
        QuitDebtGame();
#else
        Application.Quit();
#endif
    }
}
