using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

// Only the requested active flags and camera targets are backed up and restored.
[InitializeOnLoad]
public static class MapPlayerPreview
{
    const string BackupPath = "Library/MapPlayerPreview.json";
    const string RequestPath = "Library/MapPlayerPreview.request";
    static string Key => Application.dataPath + ".MapPreviewPlayer";
    [Serializable] class ActiveState { public string id; public bool active; }
    [Serializable] class CameraState { public string id, follow; }
    [Serializable] class Backup
    {
        public List<ActiveState> players = new List<ActiveState>();
        public List<CameraState> cameras = new List<CameraState>();
    }
    static MapPlayerPreview() { EditorApplication.delayCall += ApplyRequested; }
    static string Id(UnityEngine.Object obj) => obj == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
    static T Resolve<T>(string id) where T : UnityEngine.Object
    {
        return GlobalObjectId.TryParse(id, out var value)
            ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(value) as T : null;
    }
    static void ApplyRequested()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Apply()) File.Delete(RequestPath);
    }
    [MenuItem("Tools/CookieRun/Map Preview/Use Player (5)")]
    static void Enable() => Apply();
    static bool Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (File.Exists(BackupPath)) return false;
        var runners = UnityEngine.Object.FindObjectsByType<AutoRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AutoRunner target = null, original = null;
        foreach (var runner in runners)
        {
            if (runner.gameObject.scene.path != "Assets/Scenes/SampleScene.unity") continue;
            if (runner.name == "Player (5)") target = runner;
            if (runner.name == "Player") original = runner;
        }
        if (target == null || original == null) return false;
        var backup = new Backup();
        backup.players.Add(new ActiveState { id = Id(original.gameObject), active = original.gameObject.activeSelf });
        backup.players.Add(new ActiveState { id = Id(target.gameObject), active = target.gameObject.activeSelf });
        var cameras = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var camera in cameras)
            if (camera.gameObject.scene == target.gameObject.scene && camera.name == "CinemachineCamera")
                backup.cameras.Add(new CameraState { id = Id(camera), follow = Id(camera.Follow) });
        if (backup.cameras.Count == 0) return false;
        File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Preview Player (5)");
        Undo.RecordObjects(new UnityEngine.Object[] { original.gameObject, target.gameObject }, "Preview Player (5)");
        original.gameObject.SetActive(false);
        target.gameObject.SetActive(true);
        foreach (var item in backup.cameras)
        {
            var camera = Resolve<CinemachineCamera>(item.id);
            Undo.RecordObject(camera, "Preview Player (5)");
            camera.Follow = target.transform;
            EditorUtility.SetDirty(camera);
        }
        EditorPrefs.SetString(Key, target.name);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = target.gameObject;
        Debug.Log("[Map Preview] Player disabled; camera follows Player (5). Restore: Tools > CookieRun > Map Preview > Restore Previous Setup. Scene not saved.");
        return true;
    }
    [MenuItem("Tools/CookieRun/Map Preview/Restore Previous Setup")]
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(BackupPath)) { EditorPrefs.DeleteKey(Key); return; }
        var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
        foreach (var item in backup.players)
            if (Resolve<GameObject>(item.id) == null) { Debug.LogWarning("Open SampleScene to restore the map preview."); return; }
        foreach (var item in backup.cameras)
            if (Resolve<CinemachineCamera>(item.id) == null || (item.follow != "" && Resolve<Transform>(item.follow) == null)) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore Map Preview");
        foreach (var item in backup.players)
        {
            var obj = Resolve<GameObject>(item.id);
            Undo.RecordObject(obj, "Restore Map Preview");
            obj.SetActive(item.active);
            EditorSceneManager.MarkSceneDirty(obj.scene);
        }
        foreach (var item in backup.cameras)
        {
            var camera = Resolve<CinemachineCamera>(item.id);
            Undo.RecordObject(camera, "Restore Map Preview");
            camera.Follow = Resolve<Transform>(item.follow);
            EditorUtility.SetDirty(camera);
        }
        EditorPrefs.DeleteKey(Key);
        File.Delete(BackupPath);
        Undo.CollapseUndoOperations(group);
        Debug.Log("[Map Preview] Previous setup restored. Scene not saved.");
    }
}
