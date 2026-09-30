using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class AttackTriggerVerification
{
    private const string Key = "AttackTriggerVerification.Active";
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static double deadline;
    private static float started;
    private static AttackTriggerFollow attack;
    private static AutoRunner runner;
    private static bool approaching;

    static AttackTriggerVerification()
    {
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode) SessionState.SetBool(Key, false);
        };
    }

    [MenuItem("Tools/Attack/Verify 2EnemayTrigger (1) %&y")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play mode before starting a fresh attack verification.");
            return;
        }
        SessionState.SetBool(Key, true);
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.EnterPlaymode();
    }

    private static object Read(string field) => typeof(AttackTriggerFollow).GetField(field, Fields).GetValue(attack);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("[Attack verification FAIL] " + message);
        Debug.Log("[Attack verification PASS] " + message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Attack verification timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused) return;
            if (SceneManager.GetActiveScene().name == RunGameFlow.MenuScene)
            {
                foreach (var button in UnityEngine.Object.FindObjectsByType<Button>())
                    if (button.name == "START RUN") { button.onClick.Invoke(); return; }
                return;
            }
            if (SceneManager.GetActiveScene().name != RunGameFlow.GameScene || RunGameFlow.Instance == null) return;
            if (attack == null)
            {
                attack = GameObject.Find("2EnemayTrigger (1)").GetComponent<AttackTriggerFollow>();
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<AutoRunner>())
                    if (candidate.name == "Player") runner = candidate;
                Check(runner != null && (Transform)Read("targetPlayer") == runner.transform,
                    "Trigger targets the original active Player, replacing Player (4).");
                Check(!(bool)Read("hasTriggered"), "Attack does not start before player entry.");
                // Settle the camera before approaching, as it would be during a normal run.
                var bounds = attack.GetComponent<Collider2D>().bounds;
                runner.enabled = false;
                var body = runner.GetComponent<Rigidbody2D>();
                body.position = new Vector2(bounds.min.x - 2f, bounds.center.y);
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                started = Time.time;
                return;
            }
            if (!approaching)
            {
                if (Time.time - started < 1f) return;
                runner.enabled = true;
                approaching = true;
                started = Time.time;
                return;
            }
            if (Time.time - started < 4f) return;
            Check((bool)Read("hasTriggered"), "Original Player entry triggers the attack.");
            var visual = (GameObject)Read("animationObject");
            Check(visual.activeInHierarchy, "Attack visual is active.");
            Check(visual.GetComponentInChildren<Renderer>().bounds.Intersects(
                new Bounds(new Vector3(Camera.main.transform.position.x, Camera.main.transform.position.y, visual.transform.position.z),
                    new Vector3(Camera.main.orthographicSize * Camera.main.aspect * 2f, Camera.main.orthographicSize * 2f, 100f))),
                "Attack visual is inside the camera view after descent.");
            Check(!attack.GetComponent<Collider2D>().enabled, "One-shot trigger is consumed.");
            int falling = 0;
            foreach (var body in UnityEngine.Object.FindObjectsByType<Rigidbody2D>())
                if (body.name == "Falling BOTTOM Tile") falling++;
            Check(falling > 0, "Tile collapse creates falling tiles: " + falling);
            SessionState.SetBool(Key, false);
            EditorApplication.isPaused = true;
            Debug.Log("[Attack verification] COMPLETE. Stop Play mode to restore the course.");
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.isPaused = true;
            Debug.LogException(exception);
        }
    }
}
