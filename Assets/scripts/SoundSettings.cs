using UnityEngine;

/// <summary>
/// The player's master volume and mute switch, saved between visits. Both scenes apply it on
/// load, so the intro and the game always agree on how loud things are.
/// </summary>
public static class SoundSettings
{
    const string VolumeKey = "Floorplan.Volume";
    const string MuteKey = "Floorplan.Mute";
    const float DefaultVolume = 0.8f;

    public static float Volume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            Save();
        }
    }

    public static bool Muted
    {
        get => PlayerPrefs.GetInt(MuteKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
            Save();
        }
    }

    /// <summary>What the listener should actually hear: nothing while muted, else the slider.</summary>
    public static float Heard => Muted ? 0f : Volume;

    public static void Apply()
    {
        AudioListener.volume = Heard;
    }

    static void Save()
    {
        PlayerPrefs.Save(); // a browser tab can close without ever telling the game it is quitting
        Apply();
    }
}
