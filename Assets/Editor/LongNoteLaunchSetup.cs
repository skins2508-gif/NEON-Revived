using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LongNoteLaunchSetup
{
    [MenuItem("Tools/CookieRun/Add Projectile Launch Settings")]
    private static void AddSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Long projectile launch settings");
        foreach (var map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map.gameObject.scene.path != "Assets/Scenes/SampleScene.unity") continue;
            if (map.name != "Long" && map.name != "Long2" && map.name != "Long3" && map.name != "Long4") continue;
            if (map.GetComponent<LongNoteLaunchSettings>() != null) continue;
            Undo.AddComponent<LongNoteLaunchSettings>(map.gameObject);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        }
        Undo.CollapseUndoOperations(group);
    }
}
