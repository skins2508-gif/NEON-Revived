using UnityEngine;

[DisallowMultipleComponent]
public sealed class LongNoteSettings : MonoBehaviour
{
    [Header("발사 거리 (월드 유닛)")]
    [Min(0f)] public float launchDistance = 2f;
    [Header("충전")]
    [Tooltip("거리와 관계없이 60%까지 충전하는 시간")]
    [Min(0.01f)] public float initialChargeSeconds = 0.2f;
    [Tooltip("이 경로 진행률에서 최대 충전")]
    [Range(0.01f, 1f)] public float fullChargePathRatio = 0.9f;

    public static float DefaultDistance(string objectName)
    {
        switch (objectName)
        {
            case "Long2": return 5f;
            case "Long3": return 10f;
            case "Long4": return 20f;
            default: return 2f;
        }
    }

    private void Reset() { launchDistance = DefaultDistance(gameObject.name); }

    public static float Charge(float elapsed, float pathProgress, float fastSeconds, float fullRatio)
    {
        float end = Mathf.Clamp(fullRatio, 0.01f, 1f);
        if (pathProgress >= end) return 1f;
        float fastDuration = Mathf.Max(0.01f, fastSeconds);
        if (elapsed <= fastDuration) return 0.6f * Mathf.Clamp01(elapsed / fastDuration);
        // Movement along the route is at constant speed. Find where the fast phase ended.
        float fastEndProgress = pathProgress * fastDuration / Mathf.Max(elapsed, 0.001f);
        return Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(fastEndProgress, end, pathProgress));
    }
}
