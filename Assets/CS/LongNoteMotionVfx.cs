using System.Collections.Generic;
using UnityEngine;

/// <summary>Pooled world-space silhouettes, speed lines, path currents and electrical sparks.</summary>
public sealed class LongNoteMotionVfx : MonoBehaviour
{
    private GameObject effectsRoot;
    private Material material;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[4];
    private readonly float[] ghostAge = new float[4];
    private readonly LineRenderer[] speedLines = new LineRenderer[6];
    private readonly LineRenderer[] sparks = new LineRenderer[20];
    private readonly Vector3[] sparkPosition = new Vector3[20];
    private readonly Vector3[] sparkVelocity = new Vector3[20];
    private readonly float[] sparkAge = new float[20];
    private readonly LineRenderer[] currents = new LineRenderer[8];
    private LineRenderer chargedPath;
    private List<Vector3> path;
    private float[] pathDistances;
    private float totalDistance;
    private Vector3 previousPosition;
    private Vector3 travelDirection = Vector3.right;
    private float ghostTimer;
    private float sparkTimer;
    private int ghostIndex;
    private int sparkIndex;
    private uint noise = 73;
    private readonly Color cyan = new Color(0.12f, 0.95f, 1f);
    private readonly Color magenta = new Color(1f, 0.08f, 0.45f);

    public void Initialize(SpriteRenderer reference)
    {
        if (reference == null || effectsRoot != null) return;
        effectsRoot = new GameObject("Long Note World VFX");
        material = Resources.Load<Material>("LongNoteVfx");
        for (int i = 0; i < ghosts.Length; i++)
        {
            var go = new GameObject("Acceleration silhouette " + i);
            go.transform.SetParent(effectsRoot.transform, false);
            ghosts[i] = go.AddComponent<SpriteRenderer>();
            ghosts[i].sharedMaterial = material;
            ghosts[i].sortingLayerID = reference.sortingLayerID;
            ghosts[i].sortingOrder = reference.sortingOrder - 1;
            ghosts[i].enabled = false;
        }
        for (int i = 0; i < speedLines.Length; i++) speedLines[i] = Line("Neon speed line", reference, 0.025f);
        for (int i = 0; i < sparks.Length; i++) sparks[i] = Line("Electric spark", reference, 0.018f);
        for (int i = 0; i < currents.Length; i++) currents[i] = Line("Long path current", reference, 0.055f);
        chargedPath = Line("Charged long path", reference, 0.07f);
        previousPosition = reference.bounds.center;
    }

    private LineRenderer Line(string name, SpriteRenderer reference, float width)
    {
        var go = new GameObject(name); go.transform.SetParent(effectsRoot.transform, false);
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.useWorldSpace = true; line.positionCount = 2; line.widthMultiplier = width;
        line.numCapVertices = 2; line.sortingLayerID = reference.sortingLayerID;
        line.sortingOrder = reference.sortingOrder - 1; line.enabled = false;
        return line;
    }

    private float Next()
    {
        noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
        return noise / (float)uint.MaxValue;
    }

    public void SetPath(List<Vector3> points)
    {
        path = points;
        totalDistance = 0f;
        pathDistances = new float[path.Count];
        for (int i = 1; i < path.Count; i++)
        {
            totalDistance += Vector3.Distance(path[i - 1], path[i]);
            pathDistances[i] = totalDistance;
        }
    }

    private Vector3 PathPoint(float distance)
    {
        if (path == null || path.Count == 0) return previousPosition;
        distance = Mathf.Clamp(distance, 0, totalDistance);
        int lo = 0, hi = path.Count - 1;
        while (lo < hi) { int mid = (lo + hi) / 2; if (pathDistances[mid] < distance) lo = mid + 1; else hi = mid; }
        if (lo == 0) return path[0];
        float length = pathDistances[lo] - pathDistances[lo - 1];
        return Vector3.Lerp(path[lo - 1], path[lo], length > 0 ? (distance - pathDistances[lo - 1]) / length : 1);
    }

    public void Tick(SpriteRenderer display, float charge, bool charging, bool accelerating, float length, int ghostCount)
    {
        if (effectsRoot == null || display == null) return;
        float dt = Time.deltaTime;
        Vector3 position = display.bounds.center;
        Vector3 delta = position - previousPosition;
        if (delta.sqrMagnitude > 0.00001f) travelDirection = delta.normalized;
        previousPosition = position;
        Vector3 side = new Vector3(-travelDirection.y, travelDirection.x, 0);
        int count = Mathf.Clamp(ghostCount, 2, 4);
        ghostTimer -= dt;
        if (accelerating && ghostTimer <= 0f)
        {
            ghostTimer = 0.03f;
            int index = ghostIndex++ % count;
            var ghost = ghosts[index]; ghostAge[index] = 0.18f;
            ghost.sprite = display.sprite; ghost.flipX = display.flipX; ghost.flipY = display.flipY;
            ghost.transform.SetPositionAndRotation(display.transform.position, display.transform.rotation);
            ghost.transform.localScale = display.transform.lossyScale;
            ghost.enabled = true;
        }
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghostAge[i] -= dt;
            ghosts[i].enabled = ghostAge[i] > 0 && i < count;
            Color tint = i % 2 == 0 ? cyan : magenta; tint.a = Mathf.Clamp01(ghostAge[i] / 0.18f) * 0.5f;
            ghosts[i].color = tint;
        }
        for (int i = 0; i < speedLines.Length; i++)
        {
            var line = speedLines[i]; line.enabled = accelerating;
            if (!accelerating) continue;
            float offset = (i - 2.5f) * 0.16f;
            Vector3 start = position + side * offset - travelDirection * 0.2f;
            line.SetPosition(0, start); line.SetPosition(1, start - travelDirection * length * (0.55f + 0.09f * i));
            Color tint = i % 2 == 0 ? cyan : magenta; tint.a = 0.7f;
            line.startColor = tint; tint.a = 0; line.endColor = tint;
        }
        sparkTimer -= dt;
        if ((charging || accelerating) && sparkTimer <= 0f)
        {
            sparkTimer = Mathf.Lerp(0.11f, 0.025f, charge);
            int index = sparkIndex++ % sparks.Length;
            sparkPosition[index] = position + side * (Next() - 0.5f) * 0.5f;
            sparkVelocity[index] = side * (Next() - 0.5f) * (3 + charge * 5) - travelDirection * (1 + Next() * 3);
            sparkAge[index] = 0.12f + Next() * 0.12f;
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            sparkAge[i] -= dt;
            sparks[i].enabled = (charging || accelerating) && sparkAge[i] > 0;
            if (!sparks[i].enabled) continue;
            sparkPosition[i] += sparkVelocity[i] * dt;
            sparks[i].SetPosition(0, sparkPosition[i]);
            sparks[i].SetPosition(1, sparkPosition[i] - sparkVelocity[i].normalized * (0.08f + charge * 0.12f));
            Color tint = i % 4 == 0 ? cyan : magenta; tint.a = Mathf.Clamp01(sparkAge[i] * 5);
            sparks[i].startColor = sparks[i].endColor = tint;
        }
        bool hasPath = charging && path != null && path.Count > 1 && totalDistance > 0;
        for (int i = 0; i < currents.Length; i++)
        {
            var current = currents[i]; current.enabled = hasPath && i < 2 + Mathf.RoundToInt(charge * 6);
            if (!current.enabled) continue;
            // Travel in the actual tile-path direction, including vertical turns.
            float distance = Mathf.Repeat(Time.time * (7 + charge * 14) + i * totalDistance / currents.Length, totalDistance);
            current.SetPosition(0, PathPoint(distance));
            current.SetPosition(1, PathPoint(Mathf.Min(totalDistance, distance + 0.22f)));
            Color tint = i % 3 == 0 ? cyan : magenta; tint.a = 0.5f + charge * 0.5f;
            current.startColor = current.endColor = tint;
        }
        chargedPath.enabled = hasPath;
        if (hasPath)
        {
            int visible = 1;
            float end = totalDistance * charge;
            while (visible < path.Count && pathDistances[visible] < end) visible++;
            chargedPath.positionCount = visible + 1;
            for (int i = 0; i < visible; i++) chargedPath.SetPosition(i, path[i]);
            chargedPath.SetPosition(visible, PathPoint(end));
            chargedPath.startColor = chargedPath.endColor = new Color(1, 0.04f, 0.2f, 0.15f + charge * 0.3f);
        }
    }

    public void ResetEffects()
    {
        if (effectsRoot == null) return;
        foreach (var ghost in ghosts) ghost.enabled = false;
        foreach (var line in speedLines) line.enabled = false;
        foreach (var line in sparks) line.enabled = false;
        foreach (var line in currents) line.enabled = false;
        chargedPath.enabled = false;
        System.Array.Clear(ghostAge, 0, ghostAge.Length); System.Array.Clear(sparkAge, 0, sparkAge.Length);
        ghostTimer = sparkTimer = 0f;
        previousPosition = transform.position;
    }
    private void OnDisable() => ResetEffects();
    private void OnDestroy() { if (effectsRoot != null) Destroy(effectsRoot); }
}
