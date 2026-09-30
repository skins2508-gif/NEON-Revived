using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Deterministic pixel city built from real Grid/Tilemap layers.</summary>
public class NeonMenuCity : MonoBehaviour
{
    private readonly List<Object> generated = new List<Object>();
    private Camera cityCamera;
    private Tilemap lights;
    private Sprite pixel;
    private Material material;

    private void Start()
    {
        var cameraObject = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(transform);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        cityCamera = cameraObject.GetComponent<Camera>();
        cityCamera.orthographic = true;
        cityCamera.clearFlags = CameraClearFlags.SolidColor;
        cityCamera.backgroundColor = new Color(0.018f, 0.022f, 0.075f);
        cityCamera.tag = "MainCamera";

        var texture = new Texture2D(8, 8) { filterMode = FilterMode.Point };
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels); texture.Apply(); generated.Add(texture);
        // 8 pixels / 32 PPU = 0.25 world units, exactly one Grid cell.
        pixel = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32);
        generated.Add(pixel);
        material = new Material(Shader.Find("Sprites/Default")); generated.Add(material);
        var grid = new GameObject("Cyberpunk City Grid", typeof(Grid)); grid.transform.SetParent(transform);
        grid.GetComponent<Grid>().cellSize = new Vector3(0.25f, 0.25f, 1);
        var distant = Layer(grid.transform, "01 Distant skyline", 0);
        var towers = Layer(grid.transform, "02 City towers", 1);
        lights = Layer(grid.transform, "03 Neon windows and signs", 2);
        var road = Layer(grid.transform, "04 Elevated rail", 3);
        var farTile = MakeTile(new Color(0.055f, 0.065f, 0.17f));
        var building = MakeTile(new Color(0.035f, 0.04f, 0.105f));
        var cyan = MakeTile(new Color(0.06f, 0.7f, 0.82f));
        var pink = MakeTile(new Color(0.85f, 0.1f, 0.46f));
        var dim = MakeTile(new Color(0.12f, 0.18f, 0.32f));
        var asphalt = MakeTile(new Color(0.012f, 0.015f, 0.04f));
        var random = new System.Random(12305);
        for (int x = -110; x < 110; x += 7)
        {
            int height = random.Next(12, 42);
            Fill(distant, farTile, x, -20, 6, height);
        }
        for (int x = -108; x < 108; x += 12)
        {
            int height = random.Next(18, 49);
            // Leave quiet space behind the title and controls.
            if (Mathf.Abs(x) < 24) height = random.Next(9, 15);
            Fill(towers, building, x, -22, 10, height);
            Fill(lights, x % 24 == 0 ? cyan : pink, x, height - 22, 10, 1);
            for (int y = -18; y < height - 24; y += 3)
                for (int wx = 1; wx < 9; wx += 3)
                    if (random.NextDouble() > 0.35)
                        lights.SetTile(new Vector3Int(x + wx, y, 0), random.NextDouble() > 0.7 ? pink : dim);
            if (Mathf.Abs(x) > 28)
                Fill(lights, cyan, x + 1, height - 20, 1, random.Next(2, 7));
        }
        Fill(road, asphalt, -110, -35, 220, 12);
        Fill(road, cyan, -110, -24, 220, 1);
        Fill(road, pink, -110, -27, 220, 1);
        for (int x = -110; x < 110; x += 8) Fill(road, dim, x, -32, 4, 1);
    }

    private void Update()
    {
        if (cityCamera != null) cityCamera.orthographicSize = Mathf.Max(11.25f, 20f / cityCamera.aspect);
        if (lights != null) lights.color = new Color(1, 1, 1, 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 1.4f));
    }

    private Tilemap Layer(Transform grid, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(grid, false);
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sortingOrder = order; renderer.sharedMaterial = material;
        return go.GetComponent<Tilemap>();
    }

    private Tile MakeTile(Color color)
    {
        var tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = pixel; tile.color = color;
        tile.colliderType = Tile.ColliderType.None; generated.Add(tile); return tile;
    }

    private static void Fill(Tilemap map, Tile tile, int x, int y, int width, int height)
    {
        for (int dx = 0; dx < width; dx++)
            for (int dy = 0; dy < height; dy++) map.SetTile(new Vector3Int(x + dx, y + dy, 0), tile);
    }

    private void OnDestroy()
    {
        foreach (var asset in generated) if (asset != null) Destroy(asset);
    }
}
