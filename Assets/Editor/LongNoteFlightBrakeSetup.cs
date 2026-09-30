using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LongNoteBrakeImageImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/LongNoteBrake/Capsule.png" &&
            assetPath != "Assets/Resources/LongNoteBrake/Vfx.png") return;
        var importer = (TextureImporter)assetImporter;
        // Apply defaults only on the first import, preserving later user edits.
        if (!importer.importSettingsMissing) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
    }
}

public static class LongNoteFlightBrakeSetup
{
    private const string SetupKey = "CookieRun.LongNoteFlightBrake.SetupApplied";

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.delayCall += AddOnce;
        EditorSceneManager.sceneOpened += (_, __) => AddOnce();
    }

    private static void AddOnce()
    {
        if (!SessionState.GetBool(SetupKey, false) && AddToLoadedPlayers())
            SessionState.SetBool(SetupKey, true);
    }

    [MenuItem("Tools/CookieRun/Add Long Note Flight Brake Component")]
    private static void AddFromMenu() => AddToLoadedPlayers();

    private static bool AddToLoadedPlayers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        var capsule = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/LongNoteBrake/Capsule.png");
        var vfx = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/LongNoteBrake/Vfx.png");
        if (capsule == null || vfx == null) return false;
        bool found = false;
        foreach (var runner in Object.FindObjectsByType<AutoRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (runner.gameObject.scene.path != "Assets/Scenes/SampleScene.unity") continue;
            found = true;
            if (runner.GetComponent<LongNoteFlightBrake>() != null) continue;
            var brake = Undo.AddComponent<LongNoteFlightBrake>(runner.gameObject);
            var serialized = new SerializedObject(brake);
            serialized.FindProperty("capsuleSprite").objectReferenceValue = capsule;
            serialized.FindProperty("brakeVfxSprite").objectReferenceValue = vfx;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
        }
        return found;
    }
}
