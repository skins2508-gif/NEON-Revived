using UnityEngine;
using UnityEngine.InputSystem;

public static class RunKeyBindings
{
    private const string GravityPreference = "Run.Keys.Gravity";
    private const string CrossPreference = "Run.Keys.Cross";
    public static Key Gravity => Read(GravityPreference, Key.J);
    public static Key Cross => Read(CrossPreference, Key.K);

    private static Key Read(string preference, Key fallback)
    {
        var key = (Key)PlayerPrefs.GetInt(preference, (int)fallback);
        return key != Key.None && key != Key.Escape && System.Enum.IsDefined(typeof(Key), key) ? key : fallback;
    }

    public static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    public static bool Held(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;

    public static bool Assign(bool gravity, Key key)
    {
        if (key == Key.None || key == Key.Escape || !System.Enum.IsDefined(typeof(Key), key)) return false;
        Key previous = gravity ? Gravity : Cross;
        Key other = gravity ? Cross : Gravity;
        if (key == other)
            PlayerPrefs.SetInt(gravity ? CrossPreference : GravityPreference, (int)previous);
        PlayerPrefs.SetInt(gravity ? GravityPreference : CrossPreference, (int)key);
        PlayerPrefs.Save();
        return true;
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(GravityPreference);
        PlayerPrefs.DeleteKey(CrossPreference);
        PlayerPrefs.Save();
    }
}
