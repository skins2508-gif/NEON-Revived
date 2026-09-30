using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Collider2D))]
public class AttackTriggerFollow : MonoBehaviour
{
    [Header("따라갈 애니메이션")]
    [SerializeField] private GameObject animationObject;

    [Header("감지할 플레이어")]
    [Tooltip("비워두면 트리거에 들어온 Player 태그 오브젝트를 따라갑니다.")]
    [SerializeField] private Transform targetPlayer;

    [Header("추적 설정")]
    [Tooltip("비워두면 MainCamera 태그가 붙은 카메라의 중심을 따라갑니다.")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 followOffset;
    [Tooltip("소환 완료 후 위아래로 작게 왕복하는 거리입니다.")]
    [SerializeField, Min(0f)] private float floatingDistance = 0.4f;
    [Tooltip("소환 완료 후 1초 동안 위아래 왕복하는 횟수입니다.")]
    [SerializeField, Min(0f)] private float floatingSpeed = 0.35f;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("등장 연출")]
    [SerializeField, Min(0.01f)] private float descentDuration = 3f;
    [Tooltip("카메라 화면 위쪽에서 얼마나 떨어진 곳에서 등장할지 정합니다.")]
    [SerializeField, Min(0f)] private float descentStartOffset = 1f;

    [Header("타일 붕괴")]
    [Tooltip("기본값은 BOTTOM(3)과 middle(6) 레이어입니다.")]
    [SerializeField] private LayerMask destructibleLayerMask = (1 << 3) | (1 << 6);
    [Tooltip("Attack 소환 후 타일 붕괴가 시작될 때까지의 시간입니다.")]
    [SerializeField, Min(0f)] private float collapseStartDelay = 0.15f;
    [Tooltip("플레이어보다 이 거리만큼 앞에 있는 타일부터 붕괴시킵니다.")]
    [SerializeField, Min(0f)] private float collapseAheadOffset = 1.5f;
    [Tooltip("카메라 오른쪽 바깥까지 미리 붕괴시킬 거리입니다.")]
    [SerializeField, Min(0f)] private float forwardExtraDistance = 2f;
    [SerializeField, Min(0f)] private float horizontalSpreadDelay = 0.005f;
    [Tooltip("위아래 통로를 번갈아 남겨 두는 구간 길이입니다.")]
    [SerializeField, Min(2f)] private float routeSectionLength = 12f;
    [Tooltip("붕괴 파동 사이의 대기 시간입니다.")]
    [SerializeField, Min(0.02f)] private float continuousCollapseInterval = 0.03f;
    [Tooltip("한 번의 붕괴 파동에서 생성할 최대 낙하 타일 수입니다.")]
    [SerializeField, Min(1)] private int maximumFallingTiles = 50;
    [SerializeField, Min(0f)] private float fallingTileGravity = 2.5f;
    [SerializeField, Min(0f)] private float fallingTileSideSpeed = 1.5f;
    [SerializeField, Min(0.1f)] private float fallingTileLifeTime = 4f;

    private Collider2D triggerCollider;
    private bool isFollowing;
    private bool hasTriggered;
    private float animationZ;
    private float followStartTime;
    private float descentStartY;
    private float floatingCenterY;

    private struct CollapseCell
    {
        public Tilemap tilemap;
        public Vector3Int cell;
        public Vector3 worldPosition;
        public float delay;
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        int bottomLayer = LayerMask.NameToLayer("BOTTOM");
        int middleLayer = LayerMask.NameToLayer("middle");
        if (bottomLayer >= 0)
            destructibleLayerMask |= 1 << bottomLayer;
        if (middleLayer >= 0)
            destructibleLayerMask |= 1 << middleLayer;

        horizontalSpreadDelay = Mathf.Max(horizontalSpreadDelay, 0.035f);

        if (animationObject != null)
        {
            animationZ = animationObject.transform.position.z;
            animationObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartAttack(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartAttack(other);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        // 플레이 시작 시 플레이어가 이미 넓은 트리거 안에 있는 경우도 놓치지 않습니다.
        if (!hasTriggered && targetPlayer != null && targetPlayer.gameObject.activeInHierarchy &&
            triggerCollider.enabled &&
            triggerCollider.bounds.Contains(targetPlayer.position))
            StartAttack();
    }

    public void SetTargetPlayer(Transform player)
    {
        targetPlayer = player;
    }

    private void TryStartAttack(Collider2D other)
    {
        if (hasTriggered)
            return;

        Transform detectedPlayer = GetPlayerTransform(other);
        if (detectedPlayer == null)
            return;

        targetPlayer = detectedPlayer;
        StartAttack();
    }

    private void StartAttack()
    {
        if (hasTriggered)
            return;

        if (animationObject == null)
        {
            Debug.LogWarning($"{name}: 따라갈 Attack 애니메이션이 연결되지 않았습니다.", this);
            return;
        }

        hasTriggered = true;
        isFollowing = true;
        followStartTime = Time.time;

        Camera cameraToFollow = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToFollow != null)
        {
            floatingCenterY = cameraToFollow.transform.position.y + followOffset.y;
            descentStartY = cameraToFollow.transform.position.y +
                cameraToFollow.orthographicSize + descentStartOffset;
        }

        animationObject.SetActive(true);
        MoveAnimation();
        StartCoroutine(ContinuousBottomCollapse());

        if (triggerOnlyOnce)
            triggerCollider.enabled = false;
    }

    private void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        if (isFollowing && animationObject != null)
            MoveAnimation();
    }

    private void MoveAnimation()
    {
        Camera cameraToFollow = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToFollow == null)
        {
            Debug.LogWarning($"{name}: 따라갈 메인 카메라를 찾을 수 없습니다.", this);
            return;
        }

        Vector3 cameraPosition = cameraToFollow.transform.position;
        float elapsedTime = Time.time - followStartTime;
        float descentProgress = Mathf.Clamp01(elapsedTime / descentDuration);
        float baseY = Mathf.Lerp(
            descentStartY,
            floatingCenterY,
            Mathf.SmoothStep(0f, 1f, descentProgress));
        float verticalMovement = descentProgress >= 1f
            ? Mathf.Sin((elapsedTime - descentDuration) * floatingSpeed * Mathf.PI * 2f)
                * floatingDistance
            : 0f;

        animationObject.transform.position = new Vector3(
            cameraPosition.x + followOffset.x,
            baseY + verticalMovement,
            animationZ + followOffset.z);
    }

    private IEnumerator ContinuousBottomCollapse()
    {
        if (collapseStartDelay > 0f)
            yield return new WaitForSeconds(collapseStartDelay);

        while (isFollowing && animationObject != null && animationObject.activeInHierarchy)
        {
            yield return CollapseVisibleBottomTiles();
            yield return new WaitForSeconds(continuousCollapseInterval);
        }
    }

    private IEnumerator CollapseVisibleBottomTiles()
    {

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
            yield break;

        float halfHeight = cameraToUse.orthographicSize;
        float halfWidth = halfHeight * cameraToUse.aspect;
        Vector3 cameraCenter = cameraToUse.transform.position;
        float playerX = targetPlayer != null ? targetPlayer.position.x : cameraCenter.x;
        float direction = targetPlayer != null && targetPlayer.GetComponent<AutoRunner>() != null &&
            targetPlayer.GetComponent<AutoRunner>().RunSpeed < 0f ? -1f : 1f;
        float minX = direction > 0 ? Mathf.Max(cameraCenter.x - halfWidth, playerX + collapseAheadOffset)
            : cameraCenter.x - halfWidth - forwardExtraDistance;
        float maxX = direction > 0 ? cameraCenter.x + halfWidth + forwardExtraDistance
            : Mathf.Min(cameraCenter.x + halfWidth, playerX - collapseAheadOffset);
        float minY = cameraCenter.y - halfHeight;
        float maxY = cameraCenter.y + halfHeight;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<CollapseCell> cells = new List<CollapseCell>();

        foreach (Tilemap tilemap in tilemaps)
        {
            if (((1 << tilemap.gameObject.layer) & destructibleLayerMask.value) == 0 ||
                tilemap.gameObject.name == "Background")
                continue;

            Vector3Int minCell = tilemap.WorldToCell(new Vector3(minX, minY, 0f));
            Vector3Int maxCell = tilemap.WorldToCell(new Vector3(maxX, maxY, 0f));

            for (int y = minCell.y - 1; y <= maxCell.y + 1; y++)
            {
                for (int x = minCell.x - 1; x <= maxCell.x + 1; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!tilemap.HasTile(cell))
                        continue;

                    Vector3 worldPosition = tilemap.GetCellCenterWorld(cell);
                    if (worldPosition.x < minX || worldPosition.x > maxX ||
                        worldPosition.y < minY || worldPosition.y > maxY)
                        continue;

                    // Alternate intact lanes across fixed world-space sections. Repeated waves cannot
                    // randomly erase the surviving route through the same section.
                    bool lower = worldPosition.y < cameraCenter.y;
                    bool lowerSection = (Mathf.FloorToInt(worldPosition.x / Mathf.Max(2f, routeSectionLength)) & 1) == 0;
                    if (lower != lowerSection) continue;

                    cells.Add(new CollapseCell
                    {
                        tilemap = tilemap,
                        cell = cell,
                        worldPosition = worldPosition
                    });
                }
            }
        }

        if (cells.Count == 0)
            yield break;

        for (int i = 0; i < cells.Count; i++)
        {
            CollapseCell collapseCell = cells[i];
            bool lower = collapseCell.worldPosition.y < cameraCenter.y;
            // Front = furthest along travel; rear = nearest along travel.
            float frontDistance = direction > 0 ? maxX - collapseCell.worldPosition.x : collapseCell.worldPosition.x - minX;
            float rearDistance = direction > 0 ? collapseCell.worldPosition.x - minX : maxX - collapseCell.worldPosition.x;
            collapseCell.delay = (lower ? frontDistance : rearDistance) * horizontalSpreadDelay;
            cells[i] = collapseCell;
        }

        cells.Sort((left, right) => left.delay.CompareTo(right.delay));
        if (cells.Count > maximumFallingTiles)
            cells.RemoveRange(maximumFallingTiles, cells.Count - maximumFallingTiles);
        float elapsedDelay = 0f;

        foreach (CollapseCell collapseCell in cells)
        {
            float waitTime = collapseCell.delay - elapsedDelay;
            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);

            elapsedDelay = collapseCell.delay;
            DropTile(collapseCell);

            // 여러 Rigidbody2D를 한 프레임에 만들지 않아 소환 순간 멈춤을 방지합니다.
            yield return null;
        }
    }

    private void DropTile(CollapseCell collapseCell)
    {
        Sprite sprite = collapseCell.tilemap.GetSprite(collapseCell.cell);
        if (sprite == null)
            return;

        collapseCell.tilemap.SetTile(collapseCell.cell, null);

        GameObject fallingTile = new GameObject("Falling BOTTOM Tile");
        fallingTile.layer = LayerMask.NameToLayer("Ignore Raycast");
        fallingTile.transform.SetPositionAndRotation(
            collapseCell.worldPosition,
            collapseCell.tilemap.transform.rotation);
        fallingTile.transform.localScale = collapseCell.tilemap.transform.lossyScale;

        SpriteRenderer spriteRenderer = fallingTile.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = collapseCell.tilemap.color;

        TilemapRenderer tilemapRenderer = collapseCell.tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null)
        {
            spriteRenderer.sharedMaterial = tilemapRenderer.sharedMaterial;
            spriteRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = tilemapRenderer.sortingOrder;
        }

        Rigidbody2D body = fallingTile.AddComponent<Rigidbody2D>();
        body.gravityScale = fallingTileGravity;
        float sideDirection = Mathf.Sign(collapseCell.worldPosition.x - animationObject.transform.position.x);
        if (Mathf.Approximately(sideDirection, 0f))
            sideDirection = Random.value < 0.5f ? -1f : 1f;

        body.linearVelocity = new Vector2(
            sideDirection * Random.Range(fallingTileSideSpeed * 0.5f, fallingTileSideSpeed),
            Random.Range(0.2f, 1f));
        body.angularVelocity = Random.Range(-120f, 120f);
        Destroy(fallingTile, fallingTileLifeTime);
    }

    private Transform GetPlayerTransform(Collider2D other)
    {
        Transform detectedPlayer = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform.root;

        if (targetPlayer != null)
        {
            bool isAssignedPlayer =
                detectedPlayer == targetPlayer ||
                detectedPlayer.IsChildOf(targetPlayer) ||
                targetPlayer.IsChildOf(detectedPlayer);
            return isAssignedPlayer ? detectedPlayer : null;
        }

        return other.CompareTag("Player") || detectedPlayer.CompareTag("Player")
            ? detectedPlayer
            : null;
    }
}
