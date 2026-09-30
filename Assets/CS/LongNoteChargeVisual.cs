using System.Collections.Generic;
using UnityEngine;

/// <summary>Charge appearance and acceleration VFX without changing the physics shape.</summary>
public sealed class LongNoteChargeVisual : MonoBehaviour
{
    [Header("가속 잔광")]
    [Tooltip("스피드 라인 / 잔광의 목표 길이(월드 유닛)입니다.")]
    [SerializeField, Min(0.1f)] private float trailLength = 5f;
    [SerializeField, Range(2, 4)] private int afterimageCount = 3;
    public bool IsCharging => charging;
    public float Progress => charging ? charge : (burstRemaining > 0f ? 1f : 0f);
    public bool IsLaunching => LaunchActive || burstRemaining > 0f;
    private AutoRunner runner;
    private bool LaunchActive => runner != null && runner.IsLongNoteInFlight;
    private SpriteRenderer target;
    private SpriteRenderer replacementRenderer;
    private Color originalColor;
    private bool originalEnabled;
    private bool ownsAppearance;
    private bool charging;
    private float charge;
    private float burstRemaining;
    private LongNoteMotionVfx motion;
    private readonly Color chargeColor = new Color(1f, 0.25f, 0.4f);

    public void Initialize(SpriteRenderer renderer, float runSpeed)
    {
        runner = GetComponent<AutoRunner>();
        target = renderer;
        if (target == null) return;
        motion = GetComponent<LongNoteMotionVfx>();
        if (motion == null) motion = gameObject.AddComponent<LongNoteMotionVfx>();
        motion.Initialize(target);
        if (replacementRenderer == null)
        {
            var go = new GameObject("Long Note Player Appearance");
            go.transform.SetParent(target.transform, false);
            replacementRenderer = go.AddComponent<SpriteRenderer>();
            replacementRenderer.sharedMaterial = target.sharedMaterial;
            replacementRenderer.sortingLayerID = target.sortingLayerID;
            replacementRenderer.sortingOrder = target.sortingOrder;
            replacementRenderer.enabled = false;
        }
    }

    public void BeginCharge(Sprite replacement)
    {
        ResetVisuals();
        if (target == null) return;
        originalColor = target.color;
        originalEnabled = target.enabled;
        ownsAppearance = true;
        charging = true;
        charge = 0f;
        if (replacement != null && target.sprite != null)
        {
            replacementRenderer.sprite = replacement;
            // Scale the renderer child only: the player, collider and movement stay unchanged.
            Vector2 normalSize = target.sprite.bounds.size;
            Vector2 replacementSize = replacement.bounds.size;
            replacementRenderer.transform.localScale = new Vector3(normalSize.x / Mathf.Max(0.001f, replacementSize.x),
                normalSize.y / Mathf.Max(0.001f, replacementSize.y), 1f);
            replacementRenderer.transform.localPosition = target.sprite.bounds.center -
                Vector3.Scale(replacement.bounds.center, replacementRenderer.transform.localScale);
            replacementRenderer.flipX = target.flipX; replacementRenderer.flipY = target.flipY;
            replacementRenderer.color = originalColor;
            replacementRenderer.enabled = originalEnabled;
            target.enabled = false;
        }
    }

    public void SetPath(List<Vector3> points) { if (motion != null) motion.SetPath(points); }
    public void SetCharge(float progress, bool failed)
    {
        if (failed) { ResetVisuals(); return; }
        if (charging) charge = Mathf.Clamp01(progress);
    }

    public void EndCharge(bool success)
    {
        if (!success || !charging) { ResetVisuals(); return; }
        charging = false;
        burstRemaining = 0.3f;
        replacementRenderer.enabled = false;
        target.enabled = originalEnabled;
    }

    private void LateUpdate()
    {
        if (target == null || Time.timeScale == 0f) return;
        var display = replacementRenderer != null && replacementRenderer.enabled ? replacementRenderer : target;
        if (charging)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            display.color = Color.Lerp(originalColor, chargeColor, 0.12f + charge * 0.2f + pulse * 0.08f);
        }
        else if (LaunchActive)
        {
            // Keep the launch appearance for the actual flight, not a fixed 0.3 seconds.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 24f);
            target.color = Color.Lerp(originalColor, chargeColor, 0.65f + pulse * 0.2f);
            burstRemaining = Mathf.Max(0f, burstRemaining - Time.deltaTime);
        }
        else if (ownsAppearance)
        {
            ResetVisuals();
        }
        if (motion != null) motion.Tick(display, charge, charging, LaunchActive, Mathf.Max(0.1f, trailLength), afterimageCount);
    }

    public void ResetVisuals()
    {
        if (ownsAppearance && target != null)
        {
            target.color = originalColor;
            target.enabled = originalEnabled;
        }
        ownsAppearance = false;
        if (replacementRenderer != null) replacementRenderer.enabled = false;
        charging = false; burstRemaining = charge = 0f;
        if (motion != null) motion.ResetEffects();
    }
    private void OnDisable() => ResetVisuals();
}
