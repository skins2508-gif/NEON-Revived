using UnityEngine;
using UnityEngine.UI;

/// <summary>UI mesh particles: pooled by the graphic, no per-frame GameObject creation.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ChargeElectricGraphic : MaskableGraphic
{
    private float progress;
    private float intensity;
    private float clock;
    public void SetCharge(float value, bool active, float flash)
    {
        progress = Mathf.Clamp01(value);
        intensity = 0.25f + progress * 0.75f;
        enabled = active; // Release/completion immediately stops emission.
        if (enabled) { clock = Time.time; SetVerticesDirty(); }
    }
    private static float Noise(int value)
    {
        uint h = (uint)value * 747796405u + 2891336453u;
        h = ((h >> (int)((h >> 28) + 4)) ^ h) * 277803737u;
        return ((h >> 22) ^ h) / (float)uint.MaxValue;
    }
    private void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        int n = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero); vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero); vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float left = r.xMin + r.width * 0.255f;
        float right = Mathf.Lerp(left, r.xMin + r.width * 0.79f, progress);
        float center = r.yMin + r.height * 0.525f;
        int tick = Mathf.FloorToInt(clock * 24f);
        int arcCount = 1 + Mathf.RoundToInt(progress * 2);
        for (int arc = 0; arc < arcCount; arc++)
        {
            Vector2 previous = new Vector2(left, center);
            for (int i = 1; i <= 24; i++)
            {
                float jitter = (Noise(tick * 137 + arc * 41 + i * 17) - 0.5f) * r.height * 0.12f * intensity;
                Vector2 next = new Vector2(Mathf.Lerp(left, right, i / 24f), center + jitter);
                Segment(vh, previous, next, 4f, new Color(1f, 0.01f, 0.12f, 0.15f * intensity));
                Segment(vh, previous, next, 1f, new Color(1f, 0.2f, 0.35f, 0.6f * intensity));
                previous = next;
            }
        }
        // Small luminous packets flow left-to-right inside the filled rail.
        for (int i = 0; i < 3 + Mathf.RoundToInt(progress * 5); i++)
        {
            float t = Mathf.Repeat(clock * (0.65f + intensity) + i * 0.173f, 1f);
            Vector2 p = new Vector2(Mathf.Lerp(left, right, t), center + (Noise(i * 11) - 0.5f) * r.height * 0.1f);
            Color tint = i % 3 == 0 ? new Color(0.5f, 1f, 1f, 0.9f) : new Color(1f, 0.45f, 0.6f, 0.9f);
            Segment(vh, p - Vector2.right * 6, p + Vector2.right * 2, 2.2f, tint);
        }
        int particles = 4 + Mathf.RoundToInt(progress * 16);
        for (int i = 0; i < particles; i++)
        {
            float age = Mathf.Repeat(clock * (2f + progress * 2f) + Noise(i * 23), 1f);
            float angle = Noise(i * 41) * Mathf.PI * 2f;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 origin = new Vector2(Mathf.Lerp(left, right, Noise(i * 29)), center);
            Vector2 p = origin + velocity * age * r.height * (0.12f + intensity * 0.18f);
            Color tint = new Color(1f, 0.18f + age * 0.3f, 0.32f, (1f - age) * intensity);
            Segment(vh, p, p - velocity * (2 + 4 * intensity), 1.4f, tint);
        }
    }
}
