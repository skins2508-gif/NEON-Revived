using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LongNoteSettingsSetup
{
    private const string PendingRequest = "Temp/LongNote34Settings.pending";

    [InitializeOnLoadMethod]
    private static void RegisterPendingSetup()
    {
        EditorApplication.delayCall += ApplyPendingSetup;
        EditorSceneManager.sceneOpened += (_, __) => ApplyPendingSetup();
    }

    private static void ApplyPendingSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(PendingRequest)) return;
        bool found3 = false, found4 = false;
        foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tilemap.gameObject.scene.path != "Assets/Scenes/SampleScene.unity") continue;
            if (tilemap.name != "Long3" && tilemap.name != "Long4") continue;
            if (tilemap.name == "Long3") found3 = true; else found4 = true;
            // Preserve existing settings, including values changed by the user.
            if (tilemap.GetComponent<LongNoteSettings>() != null) continue;
            var settings = Undo.AddComponent<LongNoteSettings>(tilemap.gameObject);
            settings.launchDistance = tilemap.name == "Long3" ? 10f : 20f;
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        }
        if (found3 && found4)
        {
            System.IO.File.Delete(PendingRequest);
            Debug.Log("Long3 / Long4 settings ready. Existing settings preserved; scene not automatically saved.");
        }
    }

    [MenuItem("Tools/CookieRun/Setup Long Note Settings")]
    private static void Setup()
    {
        if (Application.isPlaying) return;
        foreach (var tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tilemap.name != "Long" && tilemap.name != "Long1" && tilemap.name != "Long2" &&
                tilemap.name != "Long3" && tilemap.name != "Long4") continue;
            if (tilemap.GetComponent<LongNoteSettings>() != null) continue;
            var settings = Undo.AddComponent<LongNoteSettings>(tilemap.gameObject);
            settings.launchDistance = LongNoteSettings.DefaultDistance(tilemap.name);
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        }
    }
}
