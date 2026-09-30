using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CapsulePreview : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float radius = Mathf.Min(r.width, r.height) * 0.5f;
        vh.AddVert(r.center, color, Vector2.zero);
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float y = Mathf.Sin(angle);
            vh.AddVert(new Vector2(r.center.x + Mathf.Cos(angle) * radius,
                r.center.y + y * radius + (y >= 0 ? 1 : -1) * (r.height * 0.5f - radius)), color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) vh.AddTriangle(0, (i + 1) % segments + 1, i + 1);
    }
}
