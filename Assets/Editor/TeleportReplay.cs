using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only replay: all position and zone-state changes are confined to Play mode.
[InitializeOnLoad]
public static class TeleportReplay
{
    private const string Key = "TeleportReplay.Active";
    private static double next;
    private static double deadline;
    private static int phase;
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    static TeleportReplay()
    {
        EditorApplication.update += Tick;
        deadline = EditorApplication.timeSinceStartup + 120;
    }

    [MenuItem("Tools/Teleport/Replay Both Effects %&t")]
    public static void Replay()
    {
        SessionState.SetBool(Key, true);
        phase = 0;
        next = 0;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.isPaused = false;
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup > deadline)
        {
            SessionState.SetBool(Key, false);
            Debug.LogWarning("Teleport replay timed out. Open MainMenu or SampleScene and retry.");
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
        if (EditorApplication.timeSinceStartup < next) return;
        if (SceneManager.GetActiveScene().name == RunGameFlow.MenuScene)
        {
            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (button.name == "START RUN")
                {
                    button.onClick.Invoke();
                    next = EditorApplication.timeSinceStartup + 2;
                    return;
                }
            return;
        }
        if (SceneManager.GetActiveScene().name != RunGameFlow.GameScene) return;
        try
        {
            if (phase >= 2)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.isPaused = true;
                Debug.Log("[Teleport replay] Both effects completed. Use Tools/Teleport/Replay Both Effects to replay.");
                return;
            }
            var zoneObject = GameObject.Find(phase == 0 ? "TeleportZone" : "TeleportZone2");
            var player = GameObject.Find("Player");
            if (zoneObject == null || player == null) return;
            var zone = zoneObject.GetComponent<TeleportZone>();
            var body = player.GetComponent<Rigidbody2D>();
            zone.GetComponent<Collider2D>().enabled = true;
            body.position = zone.GetComponent<Collider2D>().bounds.center;
            body.linearVelocity = Vector2.zero;
            typeof(TeleportZone).GetField("hasTeleported", Fields).SetValue(zone, false);
            zone.GetComponent<Collider2D>().enabled = true;
            Physics2D.SyncTransforms();
            zone.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
            Debug.Log("[Teleport replay] Playing " + zone.name);
            phase++;
            next = EditorApplication.timeSinceStartup + 5;
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
        }
    }
}
