using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// The player's own keys. Seven actions each have one keyboard key the player can change on the
/// settings panel: up, left, down, right, run, hide and throw. The arrow keys, right Shift, the mouse
/// click and the gamepad keep working alongside them and are not changed. Choices are stored as
/// Input System binding overrides, saved like the volume, and loaded by the title and the intro.
///
/// Only keys the game can draw without a word are allowed: letters, Space, Enter and left Shift.
/// R (restart) and Esc (pause) are kept for what they already do.
/// </summary>
public static class ControlBindings
{
    const string Key = "Floorplan.Controls";

    public const int Up = 0, Left = 1, Down = 2, Right = 3, Run = 4, Hide = 5, Throw = 6;

    struct Slot
    {
        public string action;
        public string original;
    }

    static readonly Slot[] Slots =
    {
        new Slot { action = "Player/Move", original = "<Keyboard>/w" },
        new Slot { action = "Player/Move", original = "<Keyboard>/a" },
        new Slot { action = "Player/Move", original = "<Keyboard>/s" },
        new Slot { action = "Player/Move", original = "<Keyboard>/d" },
        new Slot { action = "Player/Sprint", original = "<Keyboard>/leftShift" },
        new Slot { action = "Player/Interact", original = "<Keyboard>/e" },
        new Slot { action = "Player/Attack", original = "<Keyboard>/enter" },
    };

    public static int Count => Slots.Length;

    /// <summary>How a key is drawn: its letter, or the symbol for one of the three named keys.</summary>
    public enum Glyph { Letter, Space, Enter, Shift }

    public static void Load(InputActionAsset asset)
    {
        if (asset == null) return;
        string json = PlayerPrefs.GetString(Key, "");
        if (json.Length == 0) return;
        try { asset.LoadBindingOverridesFromJson(json); }
        catch (System.Exception) { PlayerPrefs.DeleteKey(Key); } // an unreadable save is dropped, not fatal
    }

    static void Save(InputActionAsset asset)
    {
        PlayerPrefs.SetString(Key, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    /// <summary>The binding this slot changes, found by the key it had originally.</summary>
    static InputAction Find(InputActionAsset asset, int slot, out int index)
    {
        index = -1;
        InputAction action = asset != null ? asset.FindAction(Slots[slot].action) : null;
        if (action == null) return null;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].path == Slots[slot].original) { index = i; break; }
        }
        return action;
    }

    /// <summary>The key a slot uses now, as a binding path such as "&lt;Keyboard&gt;/w".</summary>
    public static string PathOf(InputActionAsset asset, int slot)
    {
        InputAction action = Find(asset, slot, out int index);
        return action != null && index >= 0 ? action.bindings[index].effectivePath : Slots[slot].original;
    }

    static void Apply(InputActionAsset asset, int slot, string path)
    {
        InputAction action = Find(asset, slot, out int index);
        if (action == null || index < 0) return;
        if (path == Slots[slot].original) action.RemoveBindingOverride(index);
        else action.ApplyBindingOverride(index, path);
    }

    /// <summary>Gives a slot a new key. Another slot already on that key takes this slot's old one.</summary>
    public static void Set(InputActionAsset asset, int slot, string path)
    {
        if (asset == null) return;
        string old = PathOf(asset, slot);
        for (int other = 0; other < Slots.Length; other++)
        {
            if (other != slot && PathOf(asset, other) == path) Apply(asset, other, old);
        }
        Apply(asset, slot, path);
        Save(asset);
    }

    public static void ResetAll(InputActionAsset asset)
    {
        if (asset == null) return;
        for (int slot = 0; slot < Slots.Length; slot++) Apply(asset, slot, Slots[slot].original);
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    /// <summary>True for the keys a player may choose: letters but R, Space, Enter and left Shift.</summary>
    public static bool Allowed(KeyControl key)
    {
        if (key == null) return false;
        string name = key.name;
        if (name.Length == 1 && name[0] >= 'a' && name[0] <= 'z') return name != "r";
        return name == "space" || name == "enter" || name == "leftShift";
    }

    public static string PathFor(KeyControl key) => "<Keyboard>/" + key.name;

    /// <summary>What to draw for a binding path; letter is set for Glyph.Letter.</summary>
    public static Glyph GlyphOf(string path, out char letter)
    {
        letter = '?';
        string name = path.Substring(path.LastIndexOf('/') + 1);
        if (name == "space") return Glyph.Space;
        if (name == "enter") return Glyph.Enter;
        if (name == "leftShift" || name == "rightShift") return Glyph.Shift;
        if (name.Length == 1) letter = char.ToUpperInvariant(name[0]);
        return Glyph.Letter;
    }
}
