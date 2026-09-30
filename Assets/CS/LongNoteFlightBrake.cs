using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AutoRunner))]
public sealed class LongNoteFlightBrake : MonoBehaviour
{
    [Header("발사 중 K 연타 정지")]
    [Tooltip("이 시간보다 오래 연타를 쉬면 다시 시작합니다. 길게 누르기는 1회만 인정합니다.")]
    [SerializeField, Min(0.1f)] private float maximumTapGap = 0.45f;
    [Header("정지 연출")]
    [SerializeField] private Sprite capsuleSprite;
    [SerializeField] private Sprite brakeVfxSprite;
    [SerializeField, Min(0.01f)] private float visualSeconds = 0.3f;

    private readonly LongNoteBrakeMash mash = new LongNoteBrakeMash();
    private AutoRunner runner;
    private SpriteRenderer target;
    private SpriteRenderer capsule;
    private SpriteRenderer vfx;
    private bool savedEnabled;
    private bool showing;
    private float remaining;
    private GameObject gaugeCanvas;
    private RectTransform gauge;
    private Image gaugeFill;
    private Camera gaugeCamera;
    private Collider2D playerCollider;

    private void Awake()
    {
        runner = GetComponent<AutoRunner>();
        target = GetComponentInChildren<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
        if (capsuleSprite == null) capsuleSprite = Resources.Load<Sprite>("LongNoteBrake/Capsule");
        if (brakeVfxSprite == null) brakeVfxSprite = Resources.Load<Sprite>("LongNoteBrake/Vfx");
    }

    // Called by AutoRunner before its ordinary K action, avoiding Update-order races.
    public void BeginFlight(float projectedDistance)
    {
        mash.Begin(projectedDistance);
    }

    public bool HandleFlightInput(bool pressedThisFrame, float deltaTime)
    {
        bool inFlight = isActiveAndEnabled && runner != null && runner.IsLongNoteInFlight;
        if (mash.Tick(inFlight, pressedThisFrame, deltaTime, maximumTapGap) && runner.StopLongNoteFlight())
            ShowBrake();
        return inFlight;
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(target.transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = target.sharedMaterial;
        renderer.sortingLayerID = target.sortingLayerID;
        renderer.sortingOrder = target.sortingOrder + order;
        renderer.flipX = target.flipX;
        renderer.flipY = target.flipY;
        return renderer;
    }

    private void ShowBrake()
    {
        if (target == null || target.sprite == null) return;
        HideBrake();
        savedEnabled = target.enabled;
        showing = true;
        remaining = Mathf.Max(0.01f, visualSeconds);
        Bounds bounds = target.sprite.bounds;
        if (capsuleSprite != null)
        {
            if (capsule == null) capsule = CreateRenderer("Long Note Brake Capsule", capsuleSprite, 0);
            capsule.sprite = capsuleSprite;
            float scale = bounds.size.y / Mathf.Max(0.001f, capsuleSprite.bounds.size.y);
            capsule.transform.localScale = Vector3.one * scale;
            capsule.transform.localPosition = bounds.center - capsuleSprite.bounds.center * scale;
            capsule.flipX = target.flipX; capsule.flipY = target.flipY;
            capsule.color = target.color;
            capsule.enabled = savedEnabled;
            target.enabled = false;
        }
        if (brakeVfxSprite != null)
        {
            if (vfx == null) vfx = CreateRenderer("Long Note Brake VFX", brakeVfxSprite, 1);
            vfx.sprite = brakeVfxSprite;
            float scale = bounds.size.y * 1.35f / Mathf.Max(0.001f, brakeVfxSprite.bounds.size.x);
            vfx.transform.localScale = Vector3.one * scale;
            // Place the splash at the capsule's feet, with the spray trailing behind.
            vfx.transform.localPosition = new Vector3(bounds.center.x - bounds.size.x * 0.25f,
                bounds.min.y - brakeVfxSprite.bounds.min.y * scale, bounds.center.z);
            vfx.flipX = target.flipX; vfx.flipY = target.flipY;
            vfx.color = Color.white;
            vfx.enabled = savedEnabled;
        }
    }

    private void LateUpdate()
    {
        UpdateGauge();
        if (!showing) return;
        if (runner == null || !runner.enabled || runner.HasFinished || runner.IsLongNoteCharging || runner.IsLongNoteInFlight || runner.IsOnGravitySurface())
        {
            HideBrake();
            return;
        }
        remaining -= Time.deltaTime;
        if (vfx != null) vfx.color = new Color(1f, 1f, 1f,
            Mathf.Clamp01(remaining / Mathf.Max(0.01f, visualSeconds)));
        // The splash fades quickly; the capsule stays visible throughout the fall.
        if (remaining <= 0f && vfx != null) vfx.enabled = false;
    }

    private void UpdateGauge()
    {
        bool visible = runner != null && runner.IsLongNoteInFlight && playerCollider != null;
        if (gaugeCamera == null) gaugeCamera = Camera.main;
        visible &= gaugeCamera != null;
        if (!visible)
        {
            if (gaugeCanvas != null) gaugeCanvas.SetActive(false);
            return;
        }
        if (gaugeCanvas == null)
        {
            gaugeCanvas = new GameObject("Flight brake gauge", typeof(Canvas));
            var canvas = gaugeCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1001;
            var border = CreateGaugeImage(gaugeCanvas.transform, "Border", new Color(0.05f, 0.12f, 0.18f, 0.95f));
            gauge = border.rectTransform;
            gauge.anchorMin = gauge.anchorMax = Vector2.zero;
            gauge.sizeDelta = new Vector2(48f, 7f);
            var fill = CreateGaugeImage(gauge, "Progress", new Color(0f, 1f, 1f));
            gaugeFill = fill;
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(1f, 0f);
        }
        Vector3 top = playerCollider.bounds.center;
        top.y = playerCollider.bounds.max.y;
        Vector3 screen = gaugeCamera.WorldToScreenPoint(top);
        gaugeCanvas.SetActive(screen.z > 0f);
        gauge.anchoredPosition = new Vector2(screen.x, screen.y + 12f);
        gaugeFill.rectTransform.sizeDelta = new Vector2(46f * (float)mash.Progress, 5f);
    }

    private static Image CreateGaugeImage(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void OnDestroy()
    {
        if (gaugeCanvas != null) Destroy(gaugeCanvas);
    }

    public void HideBrake()
    {
        if (showing && target != null) target.enabled = savedEnabled;
        if (capsule != null) capsule.enabled = false;
        if (vfx != null) vfx.enabled = false;
        showing = false;
    }

    private void OnDisable()
    {
        mash.Reset();
        if (gaugeCanvas != null) gaugeCanvas.SetActive(false);
        HideBrake();
    }
}

// Counts fresh key-downs, not held frames. A new launch always gets a new budget.
internal sealed class LongNoteBrakeMash
{
    public int RequiredPresses { get; private set; } = 3;
    private int presses;
    public double Progress => (double)presses / RequiredPresses;
    private double gap;
    public void Begin(double projectedDistance)
    {
        Reset();
        // Long4: 90 units/s at 10 degrees for 1.4s, about 124 units.
        // Six presses at 5 Hz take about one second from first to last.
        double referenceDistance = 90 * System.Math.Cos(10 * System.Math.PI / 180) * 1.4;
        RequiredPresses = System.Math.Max(3, (int)System.Math.Ceiling(
            6 * System.Math.Sqrt(System.Math.Max(0, projectedDistance) / referenceDistance) - 0.000001));
    }
    public void Reset() { presses = 0; gap = 0; }
    public bool Tick(bool inFlight, bool pressedThisFrame, float deltaTime, float maximumGap)
    {
        if (!inFlight) { Reset(); return false; }
        if (deltaTime <= 0f) return false;
        gap += deltaTime;
        if (gap > System.Math.Max(0.1f, maximumGap)) presses = 0;
        if (!pressedThisFrame) return false;
        gap = 0;
        if (++presses < RequiredPresses) return false;
        Reset();
        return true;
    }
}
