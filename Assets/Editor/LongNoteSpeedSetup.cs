using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LongNoteSpeedSetup
{
    private const string SetupKey = "CookieRun.LongNoteSpeed.SetupApplied";

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.delayCall += AddOnce;
        EditorSceneManager.sceneOpened += (_, __) => AddOnce();
    }

    private static void AddOnce()
    {
        if (SessionState.GetBool(SetupKey, false)) return;
        if (AddToLoadedPlayers()) SessionState.SetBool(SetupKey, true);
    }

    [MenuItem("Tools/CookieRun/Add Long Note Speed Component")]
    private static void AddFromMenu() => AddToLoadedPlayers();

    private static bool AddToLoadedPlayers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        bool found = false;
        foreach (var runner in Object.FindObjectsByType<AutoRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (runner.gameObject.scene.path != "Assets/Scenes/SampleScene.unity") continue;
            found = true;
            if (runner.GetComponent<LongNoteSpeed>() != null) continue;
            Undo.AddComponent<LongNoteSpeed>(runner.gameObject);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        }
        return found;
    }
}
