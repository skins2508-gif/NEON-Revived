using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider2D))]
public class TeleportZone : MonoBehaviour
{
    private enum WarpVisualStyle
    {
        Spiral,
        Tiles
    }

    [Header("이동 위치")]
    [SerializeField] private Transform exitZone;

    [Header("전체 화면 워프 UI")]
    [Tooltip("Canvas 아래의 WarpEffect에 붙인 CanvasGroup")]
    [SerializeField] private CanvasGroup warpEffect;
    [SerializeField] private Animator warpAnimator;
    [Tooltip("존에 들어온 뒤 워프 UI가 나타나기까지의 시간")]
    [SerializeField, Min(0f)] private float entryDelay = 0.3f;
    [Tooltip("전체 화면 워프 애니메이션 재생 시간")]
    [SerializeField, Min(0f)] private float warpDuration = 1.5f;
    [SerializeField] private float warpRotation = 540f;
    [SerializeField, Min(0f)] private float revealDuration = 0.8f;
    [SerializeField, Range(0.5f, 6f)] private float spiralTurns = 2.5f;
    [SerializeField, Range(2f, 16f)] private float edgeWaves = 7f;
    [SerializeField, Range(0f, 0.3f)] private float edgeStrength = 0.12f;
    [SerializeField] private WarpVisualStyle visualStyle = WarpVisualStyle.Spiral;
    [SerializeField, Range(4, 48)] private int tileColumns = 24;
    [SerializeField, Range(3, 32)] private int tileRows = 14;

    [Header("화면 암전 (선택)")]
    [SerializeField] private CanvasGroup screenFade;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

    [Header("도착 설정")]
    [SerializeField, Min(0f)] private float controlReturnDelay = 0.2f;
    [SerializeField] private bool useOnce = true;

    private bool isTeleporting;
    private bool hasTeleported;
    private Collider2D zoneCollider;
    private WarpSpiralGraphic spiralGraphic;
    private WarpTilesGraphic tilesGraphic;
    private GameObject generatedWarpCanvas;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;

        if (screenFade != null)
        {
            screenFade.alpha = 0f;
            screenFade.blocksRaycasts = false;
        }

        EnsureWarpEffect();
        SetWarpEffectVisible(false);
        EnsureWarpVisuals();

        // UI RectTransform은 스크립트에서 직접 애니메이션하여 바인딩 문제 없이 재생합니다.
        if (warpAnimator != null)
            warpAnimator.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartTeleport(other);
    }

    // Fallback for colliders re-enabled while already inside the zone (for example,
    // after riding a Long tile) or for a fast runner whose enter callback was missed.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartTeleport(other);
    }

    private void TryStartTeleport(Collider2D other)
    {
        if (isTeleporting || (useOnce && hasTeleported))
            return;

        Transform player = GetPlayerTransform(other);
        if (player == null)
            return;

        if (exitZone == null)
        {
            Debug.LogWarning($"{name}: ExitZone이 연결되지 않았습니다.");
            return;
        }

        StartCoroutine(TeleportSequence(player));
    }

    private IEnumerator TeleportSequence(Transform player)
    {
        isTeleporting = true;
        Debug.Log($"{name}: teleport started for {player.name}.", this);

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        AutoRunner autoRunner = player.GetComponent<AutoRunner>();
        Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        bool[] rendererStates = SaveRendererStates(playerRenderers);

        if (autoRunner != null)
            autoRunner.enabled = false;

        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        if (entryDelay > 0f)
            yield return new WaitForSeconds(entryDelay);

        SetRenderersVisible(playerRenderers, false);
        SetWarpEffectVisible(true);

        yield return AnimateWarpEffect(0f, 1f, warpDuration);
        yield return FadeTo(1f);

        if (playerBody != null)
        {
            playerBody.position = exitZone.position;
            playerBody.linearVelocity = Vector2.zero;
        }
        else
        {
            player.position = exitZone.position;
        }

        Physics2D.SyncTransforms();
        Debug.Log($"{name}: player moved to {exitZone.position}.", this);

        RestoreRendererStates(playerRenderers, rendererStates);
        yield return FadeTo(0f);
        yield return AnimateWarpEffect(1f, 0f, revealDuration);
        SetWarpEffectVisible(false);
        if (controlReturnDelay > 0f)
            yield return new WaitForSeconds(controlReturnDelay);

        if (autoRunner != null)
            autoRunner.enabled = true;

        hasTeleported = true;
        isTeleporting = false;
        Debug.Log($"{name}: teleport completed.", this);

        if (useOnce && zoneCollider != null)
            zoneCollider.enabled = false;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (screenFade == null)
            yield break;

        float startAlpha = screenFade.alpha;
        if (fadeDuration <= 0f)
        {
            screenFade.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            screenFade.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        screenFade.alpha = targetAlpha;
    }

    private void SetWarpEffectVisible(bool visible)
    {
        if (warpEffect == null)
            return;

        warpEffect.alpha = visible ? 1f : 0f;
        warpEffect.interactable = false;
        warpEffect.blocksRaycasts = visible;
    }

    private void EnsureWarpEffect()
    {
        if (warpEffect != null)
            return;

        // Scenes without an assigned UI still need a full-screen transition.
        // Keep this canvas outside the zone's scaled world-space transform.
        generatedWarpCanvas = new GameObject(name + " Warp Canvas",
            typeof(RectTransform), typeof(Canvas));
        var canvas = generatedWarpCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;

        var effect = new GameObject("WarpEffect", typeof(RectTransform), typeof(CanvasGroup));
        effect.transform.SetParent(generatedWarpCanvas.transform, false);
        warpEffect = effect.GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        if (generatedWarpCanvas != null)
            Destroy(generatedWarpCanvas);
    }

    private IEnumerator AnimateWarpEffect(float startProgress, float endProgress, float duration)
    {
        if (spiralGraphic == null && tilesGraphic == null)
        {
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
            yield break;
        }

        if (duration <= 0f)
        {
            SetWarpProgress(endProgress, warpRotation * Mathf.Deg2Rad);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            float maskProgress = Mathf.Lerp(startProgress, endProgress, easedProgress);
            float angle = Mathf.Lerp(0f, warpRotation, easedProgress) * Mathf.Deg2Rad;
            SetWarpProgress(maskProgress, angle);
            yield return null;
        }

        SetWarpProgress(endProgress, warpRotation * Mathf.Deg2Rad);
    }

    private void SetWarpProgress(float progress, float angle)
    {
        if (tilesGraphic != null)
            tilesGraphic.SetState(progress);
        else if (spiralGraphic != null)
            spiralGraphic.SetState(progress, angle);
    }

    private void EnsureWarpVisuals()
    {
        if (warpEffect == null)
            return;

        RectTransform root = warpEffect.transform as RectTransform;
        if (root == null)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;

        if (visualStyle == WarpVisualStyle.Tiles)
        {
            tilesGraphic = warpEffect.GetComponentInChildren<WarpTilesGraphic>(true);
            if (tilesGraphic == null)
            {
                GameObject graphicObject = new GameObject("BlackWarpTiles", typeof(RectTransform), typeof(CanvasRenderer), typeof(WarpTilesGraphic));
                graphicObject.layer = warpEffect.gameObject.layer;
                graphicObject.transform.SetParent(root, false);
                tilesGraphic = graphicObject.GetComponent<WarpTilesGraphic>();
            }

            StretchGraphic(tilesGraphic.rectTransform);
            tilesGraphic.color = Color.black;
            tilesGraphic.raycastTarget = false;
            tilesGraphic.Initialize(tileColumns, tileRows);
            return;
        }

        spiralGraphic = warpEffect.GetComponentInChildren<WarpSpiralGraphic>(true);
        if (spiralGraphic == null)
        {
            GameObject graphicObject = new GameObject("BlackWarpSpiral", typeof(RectTransform), typeof(CanvasRenderer), typeof(WarpSpiralGraphic));
            graphicObject.layer = warpEffect.gameObject.layer;
            graphicObject.transform.SetParent(root, false);
            spiralGraphic = graphicObject.GetComponent<WarpSpiralGraphic>();
        }

        RectTransform graphicTransform = spiralGraphic.rectTransform;
        StretchGraphic(graphicTransform);
        spiralGraphic.color = Color.black;
        spiralGraphic.raycastTarget = false;
        spiralGraphic.Initialize(spiralTurns, edgeWaves, edgeStrength);
    }

    private static void StretchGraphic(RectTransform graphicTransform)
    {
        graphicTransform.anchorMin = Vector2.zero;
        graphicTransform.anchorMax = Vector2.one;
        graphicTransform.anchoredPosition = Vector2.zero;
        graphicTransform.sizeDelta = Vector2.zero;
    }

    private static Transform GetPlayerTransform(Collider2D other)
    {
        Transform candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform.root;

        return other.CompareTag("Player") || candidate.CompareTag("Player")
            ? candidate
            : null;
    }

    private static bool[] SaveRendererStates(Renderer[] renderers)
    {
        bool[] states = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            states[i] = renderers[i].enabled;

        return states;
    }

    private static void SetRenderersVisible(Renderer[] renderers, bool visible)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    private static void RestoreRendererStates(Renderer[] renderers, bool[] states)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = states[i];
    }
}
