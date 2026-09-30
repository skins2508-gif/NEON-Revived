using UnityEngine;
using UnityEngine.UI;

public sealed class LongNoteSliderHud : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform fillRect;
    private RectTransform fillContent;
    private RectTransform currentRect;
    private RectTransform currentContent;
    private RawImage frame;
    private RawImage current;
    private RawImage flashImage;
    private CanvasGroup visibility;
    private ChargeElectricGraphic electricity;
    private bool wasCharging;
    private bool reachedMaximum;
    private float maximumFlash;
    private float completionFlash;
    public float Width { get; private set; }

    public void Initialize(RunGameSettings settings)
    {
        rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -12);
        visibility = gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = false; visibility.interactable = false; visibility.alpha = 0;

        var track = new GameObject("Fill window", typeof(RectTransform), typeof(RectMask2D));
        track.transform.SetParent(transform, false);
        fillRect = (RectTransform)track.transform;
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.255f, 0.525f);
        fillRect.pivot = new Vector2(0, 0.5f);
        var fill = Artwork(fillRect, "Charge fill PNG", settings.chargeFillTexture, new Rect(0.08f, 0.34f, 0.84f, 0.28f));
        fillContent = fill.rectTransform;
        fillContent.anchorMin = new Vector2(0, 0); fillContent.anchorMax = new Vector2(0, 1);
        fillContent.pivot = new Vector2(0, 0.5f);

        // The frame renders above the fill to preserve the provided hollow bezel.
        frame = Artwork(transform, "Frame PNG", settings.chargeFrameTexture != null ? settings.chargeFrameTexture : settings.longNoteSliderTexture,
            new Rect(0, 0.23f, 1, 0.58f));
        var flowClip = new GameObject("Current window", typeof(RectTransform), typeof(RectMask2D));
        flowClip.transform.SetParent(transform, false);
        currentRect = (RectTransform)flowClip.transform;
        currentRect.anchorMin = currentRect.anchorMax = new Vector2(0.255f, 0.525f);
        currentRect.pivot = new Vector2(0, 0.5f);
        current = Artwork(currentRect, "Flowing current PNG", settings.chargeCurrentTexture, new Rect(0.26f, 0.28f, 0.53f, 0.42f));
        current.color = new Color(1, 0.7f, 0.85f, 0.4f);
        currentContent = current.rectTransform;
        currentContent.anchorMin = Vector2.zero; currentContent.anchorMax = new Vector2(0, 1);
        currentContent.pivot = new Vector2(0, 0.5f);
        var particles = new GameObject("Electric spark particles", typeof(RectTransform), typeof(ChargeElectricGraphic));
        particles.transform.SetParent(transform, false);
        var particlesRect = (RectTransform)particles.transform;
        particlesRect.anchorMin = Vector2.zero; particlesRect.anchorMax = Vector2.one; particlesRect.sizeDelta = Vector2.zero;
        electricity = particles.GetComponent<ChargeElectricGraphic>(); electricity.raycastTarget = false; electricity.enabled = false;
        flashImage = Artwork(transform, "Maximum charge burst PNG", settings.chargeCurrentTexture, new Rect(0, 0.23f, 1, 0.58f));
        flashImage.color = new Color(1, 0.65f, 0.85f, 0);
    }

    private static RawImage Artwork(Transform parent, string name, Texture texture, Rect uv)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<RawImage>(); image.texture = texture; image.uvRect = uv; image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.sizeDelta = Vector2.zero;
        return image;
    }

    public void Refresh(float canvasWidth, float progress, bool charging, bool launching)
    {
        progress = Mathf.Clamp01(progress);
        Width = Mathf.Min(660f, canvasWidth * 0.5f);
        float aspect = frame.texture != null ? frame.texture.width / (frame.texture.height * 0.58f) : 5.17f;
        float height = Width / aspect;
        rect.sizeDelta = new Vector2(Width, height);
        // Crop transparent padding and overlap the inner bezel vertically.
        // The frame is drawn above the fill, hiding the overlap.
        float trackWidth = Width * 0.535f;
        fillRect.sizeDelta = new Vector2(trackWidth * progress, height * 0.34f);
        fillContent.sizeDelta = new Vector2(trackWidth, 0);
        currentRect.sizeDelta = new Vector2(trackWidth * progress, height * 0.34f);
        currentContent.sizeDelta = new Vector2(trackWidth, 0);

        if (charging && !wasCharging) { reachedMaximum = false; maximumFlash = completionFlash = 0; }
        if (charging && progress >= 0.999f && !reachedMaximum)
        { maximumFlash = 0.14f; reachedMaximum = true; }
        if (!charging && wasCharging && launching) completionFlash = reachedMaximum ? maximumFlash : 0.14f;
        if (!charging && !launching) maximumFlash = completionFlash = 0f;
        wasCharging = charging;
        float flash = Mathf.Clamp01(Mathf.Max(maximumFlash, completionFlash) / 0.14f);
        visibility.alpha = charging || completionFlash > 0 ? 1f : 0f;
        maximumFlash = Mathf.Max(0, maximumFlash - Time.deltaTime);
        completionFlash = Mathf.Max(0, completionFlash - Time.deltaTime);
        current.enabled = charging;
        current.uvRect = new Rect(0.26f - Time.time * (0.08f + progress * 0.13f), 0.28f, 0.53f, 0.42f);
        current.color = new Color(1, 0.7f, 0.85f, (0.15f + progress * 0.4f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 27)));
        flashImage.color = new Color(1, 0.65f, 0.85f, flash * 0.85f);
        electricity.SetCharge(progress, charging, flash);
    }
}
