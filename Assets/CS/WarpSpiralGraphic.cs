using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class WarpSpiralGraphic : MaskableGraphic
{
    private const int SegmentCount = 160;
    private float progress;
    private float rotation;
    private float spiralTurns = 2.5f;
    private float edgeWaves = 7f;
    private float edgeStrength = 0.12f;

    public void Initialize(float spiralTurns, float edgeWaves, float edgeStrength)
    {
        this.spiralTurns = spiralTurns;
        this.edgeWaves = edgeWaves;
        this.edgeStrength = edgeStrength;
        SetState(0f, 0f);
    }

    public void SetState(float progress, float rotation)
    {
        this.progress = Mathf.Clamp01(progress);
        this.rotation = rotation;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (progress <= 0.0001f)
            return;

        Rect rect = GetPixelAdjustedRect();
        Color32 vertexColor = color;
        Vector2 center = rect.center;
        float cornerRadius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f;
        float activeEdge = Mathf.Sin(Mathf.PI * progress);

        vertexHelper.AddVert(center, vertexColor, new Vector2(0.5f, 0.5f));

        for (int i = 0; i <= SegmentCount; i++)
        {
            float normalized = i / (float)SegmentCount;
            float angle = normalized * Mathf.PI * 2f;
            float spiralPhase = angle * edgeWaves - progress * spiralTurns * Mathf.PI * 2f + rotation;
            float distortion = Mathf.Sin(spiralPhase) * edgeStrength * activeEdge;
            float radius = cornerRadius * Mathf.Max(0f, progress * 1.04f + distortion);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = center + direction * radius;
            vertexHelper.AddVert(position, vertexColor, normalized * Vector2.one);

            if (i > 0)
                vertexHelper.AddTriangle(0, i, i + 1);
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class WarpTilesGraphic : MaskableGraphic
{
    private int columns = 24;
    private int rows = 14;
    private float progress;

    public void Initialize(int columns, int rows)
    {
        this.columns = Mathf.Max(2, columns);
        this.rows = Mathf.Max(2, rows);
        SetState(0f);
    }

    public void SetState(float progress)
    {
        this.progress = Mathf.Clamp01(progress);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (progress <= 0f)
            return;

        Rect rect = GetPixelAdjustedRect();
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        float maximumOrder = columns + rows - 2f;
        Color32 vertexColor = color;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                // A small deterministic offset keeps the edge from looking too rigid.
                float jitter = Mathf.Repeat(x * 0.37f + y * 0.61f, 1f) * 0.8f;
                float revealPoint = (x + y + jitter) / (maximumOrder + 0.8f);
                if (progress + 0.0001f < revealPoint)
                    continue;

                float xMin = rect.xMin + x * cellWidth;
                float yMin = rect.yMin + y * cellHeight;
                int start = vertexHelper.currentVertCount;

                vertexHelper.AddVert(new Vector2(xMin, yMin), vertexColor, Vector2.zero);
                vertexHelper.AddVert(new Vector2(xMin, yMin + cellHeight), vertexColor, Vector2.up);
                vertexHelper.AddVert(new Vector2(xMin + cellWidth, yMin + cellHeight), vertexColor, Vector2.one);
                vertexHelper.AddVert(new Vector2(xMin + cellWidth, yMin), vertexColor, Vector2.right);
                vertexHelper.AddTriangle(start, start + 1, start + 2);
                vertexHelper.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
