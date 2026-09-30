using UnityEngine;
using UnityEngine.Tilemaps;

public class Rainbow : MonoBehaviour
{
    [Header("레인보우 설정")]
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private float rainbowSpeed = 0.8f;
    [SerializeField, Range(0f, 1f)] private float saturation = 1f;
    [SerializeField, Range(0f, 1f)] private float brightness = 1f;

    private void Awake()
    {
        if (targetTilemap == null)
        {
            targetTilemap = GetComponent<Tilemap>();
        }
    }

    private void Update()
    {
        if (targetTilemap == null)
            return;

        float hue = Mathf.Repeat(Time.time * rainbowSpeed, 1f);

        targetTilemap.color = Color.HSVToRGB(
            hue,
            saturation,
            brightness
        );
    }
}