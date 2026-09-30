using UnityEngine;

public sealed class RunMenuAudio : MonoBehaviour
{
    private static RunMenuAudio instance;
    private AudioSource source;
    private RunGameSettings settings;
    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("Persistent Menu Audio");
        instance = go.AddComponent<RunMenuAudio>();
        DontDestroyOnLoad(go);
        instance.settings = Resources.Load<RunGameSettings>("RunGameSettings");
        instance.source = go.AddComponent<AudioSource>();
        instance.source.playOnAwake = false;
        instance.source.ignoreListenerPause = true;
        instance.source.spatialBlend = 0f;
    }
    public static void Move() { Ensure(); instance.Play(instance.settings != null ? instance.settings.menuMoveSound : null); }
    public static void Select() { Ensure(); instance.Play(instance.settings != null ? instance.settings.menuSelectSound : null); }
    private void Play(AudioClip clip) { if (clip != null) source.PlayOneShot(clip); }
    private void OnDestroy() { if (instance == this) instance = null; }
}
