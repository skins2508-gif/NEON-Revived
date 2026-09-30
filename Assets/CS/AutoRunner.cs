using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/// <summary>
/// 2D 자동 달리기, 중력 반전(J키), 타일 및 정면 벽 관통 이동(K키)을 제어하는 컨트롤러입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AutoRunner : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float runSpeed = 20f;
    [SerializeField] private float verticalMoveSpeed = 15f;

    [Header("점프판")]
    [InspectorName("Jumping 비행 속도 배율")]
    [Tooltip("Jumping에 부딪혀 충돌한 면의 반대 방향으로 날아가는 속도 배율")]
    [SerializeField, Min(1.01f)] private float jumpingGravitySpeedMultiplier = 1.5f;
    [InspectorName("MiniJumping 비행 속도 배율")]
    [Tooltip("MiniJumping의 비행 속도 배율. Jumping보다 항상 빠르게 적용합니다.")]
    [SerializeField, Min(1.02f)] private float miniJumpingGravitySpeedMultiplier = 2f;
    [InspectorName("Jumping 좌우 비행 기준 거리")]
    [Tooltip("이 거리에서 일정 높이만큼 떨어지도록 궤적을 조절합니다. 수평 이동을 멈추는 거리가 아닙니다.")]
    [SerializeField, Min(0f)] private float jumpingHorizontalDistance = 10f;
    [InspectorName("MiniJumping 좌우 비행 기준 거리")]
    [Tooltip("실제 착지 거리는 바닥 높이에 따라 달라집니다. 이 거리를 지나도 계속 날아갑니다.")]
    [SerializeField, Min(0f)] private float miniJumpingHorizontalDistance = 15f;
    [InspectorName("좌우 비행 낙하 가속도")]
    [SerializeField, Min(0.01f)] private float jumpPadFallAcceleration = 60f;

    [Header("타일/벽 뒷면 이동")]
    [SerializeField] private float tileSearchDistance = 3f;
    [SerializeField] private float tileThickness = 1f;
    [SerializeField] private float surfaceOffset = 0.05f;
    [Tooltip("Time used to move smoothly through a passable tile after pressing K.")]
    [SerializeField, Min(0.02f)] private float sideChangeDuration = 0.12f;

    [Header("Middle long note")]
    [Tooltip("Tiles shorter than this are handled as an ordinary K press.")]
    [SerializeField, Min(2)] private int longNoteMinimumTiles = 2;
    [Tooltip("Prevents very short tile runs from becoming unreadably fast long notes.")]
    [SerializeField, Min(0f)] private float minimumLongNoteDuration = 0.1f;

    [Header("롱노트 충전 / 발사")]
    [Tooltip("롱노트 중 표시할 이미지입니다. 비워두면 기존 이미지에 충전 효과를 적용합니다.")]
    [SerializeField] private Sprite longNoteSprite;
    [SerializeField, Range(1f, 1.5f)] private float longNoteLaunchMultiplier = 1.15f;
    private Vector2 longNoteBoostPreviousPosition;
    private LongNoteChargeVisual chargeVisual;
    private LongNoteSpeed longNoteSpeed;
    private LongNoteFlightBrake flightBrake;
    private float longNoteBoostRemaining;
    private readonly LongNoteTrajectory longNoteTrajectory = new LongNoteTrajectory();
    private bool isProjectileFlight;
    private bool projectileContactPending;
    private readonly RaycastHit2D[] projectileHits = new RaycastHit2D[32];

    [Header("레이어 설정")]
    [Tooltip("넘어갈 수 있는 타일 레이어 (STRIGHT, middle, TOP 등)")]
    [SerializeField] private LayerMask passableTileLayerMask;

    [Tooltip("통과할 수 없는 막힌 장애물/바닥 레이어 (Rock, BOTTOM 등)")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [Tooltip("접촉하면 충돌 MISS를 1회 집계할 대상 레이어입니다. 접촉 유지 중에는 중복 집계하지 않습니다.")]
    [SerializeField] private LayerMask missLayerMask = 1 << 18;

    [InspectorName("점프판 비행 착지 불가 레이어")]
    [Tooltip("점프판 비행 중 이 레이어는 착지로 인정하지 않습니다. 닿아도 J키 잠금과 비행 상태를 유지합니다. 물리 충돌 자체를 끄는 설정은 아닙니다.")]
    [SerializeField] private LayerMask jumpLandingExcludedLayers;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private SpriteRenderer playerSpriteRenderer;

    private int bottomLayer;
    private int bottomLayerMask;
    private int middleLayer;
    private int longLayer;
    private int longLayerMask;
    private readonly RaycastHit2D[] bottomCastHits = new RaycastHit2D[16];

    private bool isReverseGravity;
    private readonly JumpTileGravityLock jumpTileGravityLock = new JumpTileGravityLock();
    private readonly RaycastHit2D[] jumpSupportHits = new RaycastHit2D[32];
    private float activeJumpGravityMultiplier = 1f;
    private Vector2 jumpPadFlightVelocity;
    private bool horizontalJumpPadFlight;
    private readonly JumpPadHorizontalFlight horizontalJumpMotion = new JumpPadHorizontalFlight();
    private bool isChangingSide;
    private bool isProcessingSideChange;
    private bool isRidingLong;
    private bool isMagnetized;
    private float teleportLaunchDistanceRemaining;
    private float teleportLaunchSpeed;
    private int pendingLongNotePenalties;

    /// <summary>
    /// Raised when K is released before a middle long note ends. The crossing still completes.
    /// A future score system can subscribe to this event or consume PendingLongNotePenalties.
    /// </summary>
    public event Action LongNoteReleasedEarly;
    public int PendingLongNotePenalties => pendingLongNotePenalties;
    public bool IsLongNoteCharging => chargeVisual != null && chargeVisual.IsCharging;
    public float LongNoteChargeProgress => chargeVisual != null ? chargeVisual.Progress : 0f;
    public bool IsLongNoteLaunching => chargeVisual != null && chargeVisual.IsLaunching;
    public bool IsLongNoteInFlight => enabled && !HasFinished && (isProjectileFlight || longNoteBoostRemaining > 0f) &&
        !isRidingLong && !isChangingSide && teleportLaunchDistanceRemaining <= 0f;
    public float RunSpeed => runSpeed;
    public float CurrentSpeed { get; private set; }
    public bool HasFinished { get; private set; }

    public int CollisionMisses { get; private set; }
    public bool IsDead { get; private set; }
    private readonly HashSet<Collider2D> missContacts = new HashSet<Collider2D>();

    private void OnCollisionEnter2D(Collision2D collision) => TouchHazard(collision.collider);
    private void OnCollisionExit2D(Collision2D collision) => missContacts.Remove(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TouchHazard(other);
    private void OnTriggerExit2D(Collider2D other) => missContacts.Remove(other);

    private void TouchHazard(Collider2D other)
    {
        if (HasFinished || IsDead) return;
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.name == "DeadZone" || t.name == "DeadZone(1)" || t.name == "DeadZone (1)")
            {
                if (RunGameFlow.Instance != null) RunGameFlow.Instance.Die(this);
                else Die();
                return;
            }
        }
        if ((missLayerMask.value & (1 << other.gameObject.layer)) == 0) return;
        missContacts.RemoveWhere(contact => contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy);
        if (missContacts.Contains(other)) return;
        bool alreadyTouchingObject = false;
        foreach (var contact in missContacts)
            if (contact.gameObject == other.gameObject ||
                (other.attachedRigidbody != null && contact.attachedRigidbody == other.attachedRigidbody))
                alreadyTouchingObject = true;
        missContacts.Add(other);
        if (!alreadyTouchingObject) CollisionMisses++;
    }

    public void Die()
    {
        if (IsDead || HasFinished) return;
        IsDead = true;
        FinishRun();
    }

    public void FinishRun()
    {
        HasFinished = true;
        CurrentSpeed = 0f;
        StopAllCoroutines();
        isProjectileFlight = false;
        longNoteBoostRemaining = 0f;
        if (flightBrake != null) flightBrake.HideBrake();
        if (chargeVisual != null) chargeVisual.ResetVisuals();
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
        enabled = false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        longNoteSpeed = GetComponent<LongNoteSpeed>();
        if (longNoteSpeed == null) longNoteSpeed = gameObject.AddComponent<LongNoteSpeed>();
        flightBrake = GetComponent<LongNoteFlightBrake>();
        if (flightBrake == null) flightBrake = gameObject.AddComponent<LongNoteFlightBrake>();
        CurrentSpeed = Mathf.Abs(runSpeed);
        chargeVisual = GetComponent<LongNoteChargeVisual>();
        if (chargeVisual == null) chargeVisual = gameObject.AddComponent<LongNoteChargeVisual>();
        chargeVisual.Initialize(playerSpriteRenderer, runSpeed);
        if (longNoteSprite == null)
        {
            var settings = Resources.Load<RunGameSettings>("RunGameSettings");
            if (settings != null) longNoteSprite = settings.longNotePlayerSprite;
        }
        bottomLayer = LayerMask.NameToLayer("BOTTOM");
        middleLayer = LayerMask.NameToLayer("middle");
        longLayer = LayerMask.NameToLayer("Long");
        longLayerMask = longLayer >= 0 ? 1 << longLayer : 0;

        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (bottomLayer == -1)
        {
            Debug.LogWarning("BOTTOM 레이어가 프로젝트 설정에 없습니다.");
        }
        else
        {
            bottomLayerMask = 1 << bottomLayer;
        }
    }

    private void Update()
    {
        if (HasFinished || Time.timeScale == 0f)
            return;
        bool flightInputConsumed = flightBrake != null && flightBrake.HandleFlightInput(
            RunKeyBindings.Pressed(RunKeyBindings.Cross), Time.deltaTime);
        if (Keyboard.current == null)
            return;

        // --- J 키: 일반 중력 반전 (천장/바닥 수직 전환) ---
        if (RunKeyBindings.Pressed(RunKeyBindings.Gravity) && !jumpTileGravityLock.IsLocked)
        {
            SetGravityDirection(!isReverseGravity, isWallRotation: false);

            string timeString = DateTime.Now.ToString("HH:mm:ss.fff");
            Vector2 position = rb.position;
            Debug.Log($"[J Pressed] Time: {timeString} | Position: X = {position.x:F2}, Y = {position.y:F2}");
        }

        // --- K 키: 벽 또는 상하 타일 건너편으로 통과 이동 ---
        if (!flightInputConsumed && RunKeyBindings.Pressed(RunKeyBindings.Cross) && !isProcessingSideChange)
        {
            isProcessingSideChange = true;
            StartCoroutine(MoveToTileBackside(!isReverseGravity));
        }
    }

    private void FixedUpdate()
    {
        if (HasFinished)
            return;
        UpdateJumpPadFlight();
        if (jumpTileGravityLock.IsLocked && !isChangingSide && !isRidingLong)
        {
            if (horizontalJumpPadFlight)
            {
                horizontalJumpMotion.Step(Time.fixedDeltaTime, out double vx, out double vy);
                jumpPadFlightVelocity = new Vector2((float)vx, (float)vy);
            }
            else
            {
                // Vertical pad launches retain the current automatic running speed.
                jumpPadFlightVelocity.x = runSpeed;
            }
            rb.linearVelocity = jumpPadFlightVelocity;
            CurrentSpeed = jumpPadFlightVelocity.magnitude;
            return;
        }
        if (isProjectileFlight && StepProjectileFlight()) return;
        bool isTeleportLaunching = teleportLaunchDistanceRemaining > 0f;
        float horizontalSpeed = isRidingLong ? 0f : runSpeed;
        if (!isRidingLong && !isChangingSide && !isTeleportLaunching && longNoteBoostRemaining > 0f)
        {
            longNoteBoostRemaining = Mathf.Max(0f, longNoteBoostRemaining -
                Mathf.Abs(rb.position.x - longNoteBoostPreviousPosition.x));
            longNoteBoostPreviousPosition = rb.position;
            if (longNoteBoostRemaining > 0f) horizontalSpeed *= longNoteLaunchMultiplier;
        }

        if (isTeleportLaunching)
        {
            horizontalSpeed = teleportLaunchSpeed;
            teleportLaunchDistanceRemaining -=
                Mathf.Abs(horizontalSpeed) * Time.fixedDeltaTime;
        }

        // Long-note launch suspends only the custom gravity movement.
        // Check after consuming distance so gravity resumes on the first step after arrival.
        bool isLongNoteLaunching = !isRidingLong && !isChangingSide &&
            !isTeleportLaunching && longNoteBoostRemaining > 0f;
        float verticalSpeed =
            isChangingSide || isTeleportLaunching || isMagnetized || isLongNoteLaunching
                ? 0f
                : GetGravityMoveSpeed();
        verticalSpeed = ClampSurfaceMoveSpeed(verticalSpeed);
        rb.linearVelocity = new Vector2(horizontalSpeed, verticalSpeed);
        if (!isRidingLong) CurrentSpeed = Mathf.Abs(horizontalSpeed);
    }

    public bool StopLongNoteFlight()
    {
        if (!IsLongNoteInFlight) return false;
        isProjectileFlight = false;
        projectileContactPending = false;
        longNoteBoostRemaining = 0f;
        if (chargeVisual != null) chargeVisual.ResetVisuals();
        // Keep the configured base speed (20 by default) and resume normal gravity.
        CurrentSpeed = Mathf.Abs(runSpeed);
        rb.linearVelocity = new Vector2(runSpeed,
            isMagnetized ? 0f : ClampSurfaceMoveSpeed(GetGravityMoveSpeed()));
        return true;
    }

    public void LaunchFromTeleport(float distance, float speed)
    {
        // A teleport replaces the projectile rather than resuming it at the destination.
        if (IsLongNoteInFlight) StopLongNoteFlight();
        teleportLaunchDistanceRemaining = Mathf.Max(0f, distance);
        teleportLaunchSpeed = Mathf.Max(runSpeed, speed);
    }

    private void OnDisable()
    {
        // Warp scripts temporarily disable the runner; do not resume an old arc afterward.
        isProjectileFlight = false;
        projectileContactPending = false;
    }

    public void SetMagnetized(bool magnetized)
    {
        isMagnetized = magnetized;

        if (magnetized && !isProjectileFlight)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    private void BeginProjectileFlight(LongNoteLaunchSettings settings)
    {
        longNoteBoostRemaining = 0f;
        projectileContactPending = false;
        longNoteTrajectory.Begin(settings.launchSpeed, settings.launchAngle,
            settings.gravity, settings.maximumFlightSeconds, isReverseGravity);
        if (flightBrake != null)
            flightBrake.BeginFlight((float)(longNoteTrajectory.VelocityX * longNoteTrajectory.Duration));
        isProjectileFlight = true;
        rb.linearVelocity = new Vector2((float)longNoteTrajectory.VelocityX,
            (float)longNoteTrajectory.VelocityY);
        CurrentSpeed = rb.linearVelocity.magnitude;
    }

    private bool StepProjectileFlight()
    {
        if (projectileContactPending || longNoteTrajectory.Finished)
        {
            StopLongNoteFlight();
            return false;
        }

        float dt = Time.fixedDeltaTime;
        longNoteTrajectory.Step(dt, out double x, out double y);
        Vector2 displacement = new Vector2((float)x, (float)y);
        float distance = displacement.magnitude;
        if (distance > 0f && playerCollider.enabled)
        {
            Vector2 direction = displacement / distance;
            float skin = Mathf.Max(surfaceOffset, Physics2D.defaultContactOffset);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = bottomLayerMask | passableTileLayerMask.value | obstacleLayerMask.value | longLayerMask | missLayerMask.value,
                useTriggers = false
            };
            int count = playerCollider.Cast(direction, filter, projectileHits, distance + skin);
            float allowed = distance;
            Collider2D contact = null;
            for (int i = 0; i < count; i++)
            {
                var hit = projectileHits[i];
                if (hit.collider == null || hit.collider == playerCollider ||
                    Physics2D.GetIgnoreCollision(playerCollider, hit.collider) ||
                    Vector2.Dot(hit.normal, direction) >= -0.001f) continue;
                float travel = Mathf.Max(0f, hit.distance - skin);
                if (travel > allowed) continue;
                allowed = travel;
                contact = hit.collider;
            }
            if (contact != null)
            {
                displacement = direction * allowed;
                projectileContactPending = true;
                TouchHazard(contact);
                if (HasFinished) return true;
            }
        }
        // Use the average velocity over this step for a frame-rate independent parabola.
        rb.linearVelocity = displacement / dt;
        CurrentSpeed = rb.linearVelocity.magnitude;
        return true;
    }

    /// <summary>
    /// 정면 벽 또는 상하 타일을 탐색하여 장애물을 우회하며 반대편으로 건너가는 코루틴입니다.
    /// </summary>
    private IEnumerator MoveToTileBackside(bool reverseGravityAfterMove)
    {
        bool isWallInFront = false;
        Vector2 searchDirection = Vector2.zero;
        RaycastHit2D targetHit = default;

        // Detect Long from the player's center before the ordinary edge ray.
        // This catches a Long tile already overlapping the player; X has priority.
        float closeXDistance = playerCollider.bounds.extents.x + Physics2D.defaultContactOffset * 2f;
        float closeYDistance = playerCollider.bounds.extents.y + Physics2D.defaultContactOffset * 2f;
        RaycastHit2D closeLongHit = Physics2D.Raycast(
            rb.position,
            Vector2.right,
            closeXDistance,
            longLayerMask);

        if (closeLongHit.collider != null && !closeLongHit.collider.isTrigger)
        {
            isWallInFront = true;
            searchDirection = Vector2.right;
            targetHit = closeLongHit;
        }
        else
        {
            Vector2 verticalLongDirection = isReverseGravity ? Vector2.up : Vector2.down;
            closeLongHit = Physics2D.Raycast(
                rb.position,
                verticalLongDirection,
                closeYDistance,
                longLayerMask);

            if (closeLongHit.collider != null && !closeLongHit.collider.isTrigger)
            {
                searchDirection = verticalLongDirection;
                targetHit = closeLongHit;
            }
        }

        // 1. [정면 벽 감지] 플레이어 오른쪽 콜라이더 경계면부터 체크
        Vector2 rayStartPos = new Vector2(playerCollider.bounds.max.x + 0.01f, rb.position.y);
        int crossingLayerMask = passableTileLayerMask.value | longLayerMask;
        RaycastHit2D wallHit = default;
        if (targetHit.collider == null)
        {
            RaycastHit2D[] wallHits = Physics2D.RaycastAll(
                rayStartPos,
                Vector2.right,
                tileSearchDistance,
                crossingLayerMask);
            wallHit = FindPreferredPassableHit(wallHits);
        }

        if (targetHit.collider != null)
        {
            // The close Long probe already selected both the target and direction.
        }
        else if (wallHit.collider != null && !wallHit.collider.isTrigger)
        {
            isWallInFront = true;
            searchDirection = Vector2.right;
            targetHit = wallHit;
        }
        else
        {
            // 2. [상하 타일 감지] 정면에 벽이 없으면 수직 탐색
            searchDirection = isReverseGravity ? Vector2.up : Vector2.down;

            // 통과 가능 타일과 장애물 레이어를 함께 검사하여 Rock이 막고 있는지 체크
            LayerMask combinedMask = crossingLayerMask | obstacleLayerMask;
            RaycastHit2D[] hits = Physics2D.RaycastAll(rb.position, searchDirection, tileSearchDistance, combinedMask);

            targetHit = FindPreferredPassableHit(hits);

            if (targetHit.collider == null)
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.collider == playerCollider || hit.collider.isTrigger)
                    continue;

                // 통과 가능 타일보다 장애물(Rock, BOTTOM 등)이 먼저 가로막고 있다면 이동 금지
                if (((1 << hit.collider.gameObject.layer) & obstacleLayerMask) != 0)
                {
                    Debug.Log($"경로 중간에 장애물({hit.collider.gameObject.name})이 막고 있어 이동을 차단합니다.");
                    isProcessingSideChange = false;
                    yield break;
                }

                // 정상적인 통과 가능 타일을 발견
                if (targetHit.collider == null)
                {
                    targetHit = hit;
                    break;
                }
            }
        }

        // 넘어갈 타일이 없으면 종료
        if (targetHit.collider == null)
        {
            Debug.LogWarning("주변에 넘어갈 수 있는 타일이나 벽이 없습니다.");
            isProcessingSideChange = false;
            yield break;
        }

        if (targetHit.collider.gameObject.layer == longLayer)
        {
            SendLongRendererBehindPlayer(targetHit.collider);
            yield return RideConnectedLong(targetHit);
            isProcessingSideChange = false;
            yield break;
        }

        if (IsMiddleTwoByTwo(targetHit))
        {
            Debug.Log("Middle 2x2 thickness detected. K crossing was blocked.");
            isProcessingSideChange = false;
            yield break;
        }

        MiddleRun longRun = AnalyzeLongRun(targetHit);
        bool isLongTile = targetHit.collider.gameObject.layer == longLayer;
        if (isLongTile)
            SendLongRendererBehindPlayer(targetHit.collider);
        bool isLongNote = isLongTile && longRun.tileCount >= longNoteMinimumTiles;
        float longNoteDuration = 0f;
        if (isLongNote)
        {
            float movementSpeed = longRun.axis == Vector2Int.right
                ? Mathf.Abs(runSpeed)
                : Mathf.Abs(verticalMoveSpeed);
            longNoteDuration = movementSpeed > Mathf.Epsilon
                ? longRun.worldLength / movementSpeed
                : minimumLongNoteDuration;
            longNoteDuration = Mathf.Max(minimumLongNoteDuration, longNoteDuration);
        }

        // --- 3. 이동 거리 및 도착 위치 검사 ---
        float playerHalfExtent = isWallInFront ? playerCollider.bounds.extents.x : playerCollider.bounds.extents.y;
        float crossingClearance = Mathf.Max(surfaceOffset, Physics2D.defaultContactOffset * 2f);
        Vector2 targetPos = GetCrossingPosition(targetHit, searchDirection, playerHalfExtent, crossingClearance);

        // 도착 지점에 Rock 등 장애물이 이미 존재하는지 2차 검사
        Collider2D overlapObstacle = FindBlockingDestinationObstacle(targetPos, targetHit.collider);
        if (overlapObstacle != null)
        {
            Debug.Log($"도착 지점에 장애물({overlapObstacle.name})이 겹쳐있어 이동을 차단합니다.");
            isProcessingSideChange = false;
            yield break;
        }

        // --- 4. 위치 관통 이동 및 회전 적용 ---
        isChangingSide = true;
        Physics2D.IgnoreCollision(playerCollider, targetHit.collider, true);

        rb.linearVelocity = Vector2.zero;
        rb.position = targetPos;
        Physics2D.SyncTransforms();

        // 정면 벽(isWallInFront)을 넘은 경우: 기존 중력 상태 유지 (위로 솟구침 방지) & X축 회전
        // 상하 타일을 넘은 경우: 중력 반전 적용 & Z축(180도) 회전
        bool nextGravityState = isWallInFront ? isReverseGravity : reverseGravityAfterMove;
        if (isLongTile)
        {
            yield return MoveAcrossTileSmoothly(targetPos, nextGravityState, isWallInFront);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = targetPos;
            SetGravityDirection(nextGravityState, isWallInFront);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        // Middle becomes solid again immediately. Long stays passable until its
        // entire run is finished, even if K was released early.
        if (!isLongTile)
            Physics2D.IgnoreCollision(playerCollider, targetHit.collider, false);
        isChangingSide = false;

        // Cross first, then judge the hold while auto-run continues on the new surface.
        // Releasing K never interrupts movement; it only records timing for scoring.
        if (isLongNote)
            yield return HoldLongNote(longNoteDuration);

        if (isLongTile)
        {
            // Restoring collision while still inside Long makes the physics solver
            // eject the runner backward. Wait for the actual shapes to separate.
            while (playerCollider.enabled &&
                   Physics2D.Distance(playerCollider, targetHit.collider).isOverlapped)
            {
                yield return new WaitForFixedUpdate();
            }

            // Give the contact offset one extra physics step of clearance.
            yield return new WaitForFixedUpdate();
            Physics2D.IgnoreCollision(playerCollider, targetHit.collider, false);
            Physics2D.SyncTransforms();
        }

        isProcessingSideChange = false;
    }

    private IEnumerator RideConnectedLong(RaycastHit2D targetHit)
    {
        Tilemap tilemap = targetHit.collider.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogWarning("Long must use a Tilemap and TilemapCollider2D.");
            yield break;
        }

        Vector3Int startCell = GetHitCell(tilemap, targetHit);
        if (!tilemap.HasTile(startCell))
        {
            Debug.LogWarning("Could not find the Long tile cell at the K input position.");
            yield break;
        }

        List<Vector3Int> path = BuildLongPath(tilemap, startCell);
        if (path.Count == 0)
            yield break;

        isChangingSide = true;
        isRidingLong = true;
        rb.linearVelocity = Vector2.zero;
        Physics2D.IgnoreCollision(playerCollider, targetHit.collider, true);
        playerCollider.enabled = false;

        Vector3Int exitDirection = path.Count >= 2
            ? path[path.Count - 1] - path[path.Count - 2]
            : Vector3Int.right;
        Vector3 cellSize = tilemap.layoutGrid.cellSize;
        Vector2 exitOffset = new Vector2(
            exitDirection.x * Mathf.Abs(cellSize.x),
            exitDirection.y * Mathf.Abs(cellSize.y));
        Vector2 exitPosition = (Vector2)tilemap.GetCellCenterWorld(path[path.Count - 1]) + exitOffset;

        bool releasedEarly = false;
        longNoteBoostRemaining = 0f;
        if (flightBrake != null) flightBrake.HideBrake();
        chargeVisual.BeginCharge(longNoteSprite);
        var visualPath = new List<Vector3>(path.Count);
        foreach (var cell in path) visualPath.Add(tilemap.GetCellCenterWorld(cell));
        chargeVisual.SetPath(visualPath);
        var noteSettings = tilemap.GetComponent<LongNoteSettings>();
        var launchSettings = tilemap.GetComponent<LongNoteLaunchSettings>();
        float launchDistance = noteSettings != null ? noteSettings.launchDistance : LongNoteSettings.DefaultDistance(tilemap.name);
        // Give the gauge five times the configured initial charge duration.
        // The default 0.2 seconds now fills the entire gauge in 1 second.
        float fillSeconds = Mathf.Max(0.01f, noteSettings != null ? noteSettings.initialChargeSeconds : 0.2f) * 5f;
        double gaugeProgress = 0;
        longNoteSpeed.BeginRide(runSpeed);
        CurrentSpeed = longNoteSpeed.CurrentSpeed;

        // Carry unused distance across cell boundaries so small tiles do not cap speed.
        int waypoint = 0;
        while (waypoint <= path.Count)
        {
            yield return new WaitForFixedUpdate();
            bool held = RunKeyBindings.Held(RunKeyBindings.Cross);
            RecordLongRelease(ref releasedEarly);
            if (held) CurrentSpeed = longNoteSpeed.Advance(Time.fixedDeltaTime, runSpeed);
            float tilesTravelled = 0f;
            float remaining = CurrentSpeed * Time.fixedDeltaTime;
            while (waypoint <= path.Count)
            {
                Vector2 destination = waypoint < path.Count
                    ? (Vector2)tilemap.GetCellCenterWorld(path[waypoint]) : exitPosition;
                float distance = Vector2.Distance(rb.position, destination);
                if (distance <= 0.001f)
                {
                    waypoint++;
                    continue;
                }
                if (remaining <= 0f) break;
                Vector2 previous = rb.position;
                rb.position = Vector2.MoveTowards(previous, destination, remaining);
                float moved = Vector2.Distance(previous, rb.position);
                // The approach to the first tile is not part of the ridden tile count.
                // Each subsequent segment (including the exit) represents one tile.
                if (waypoint > 0)
                {
                    float tileLength = Vector2.Distance(tilemap.GetCellCenterWorld(path[waypoint - 1]), destination);
                    tilesTravelled += moved / Mathf.Max(0.001f, tileLength);
                }
                remaining = Mathf.Max(0f, remaining - moved);
                if (distance > moved + 0.001f) break;
                waypoint++;
            }
            if (!held) CurrentSpeed = longNoteSpeed.ReleaseOverTiles(tilesTravelled);
            gaugeProgress = LongNoteRideInput.Gauge(gaugeProgress, held, Time.fixedDeltaTime, fillSeconds, tilesTravelled);
            chargeVisual.SetCharge((float)gaugeProgress, false);
        }

        playerCollider.enabled = true;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
        Physics2D.IgnoreCollision(playerCollider, targetHit.collider, false);
        isRidingLong = false;
        isChangingSide = false;
        chargeVisual.EndCharge(!releasedEarly);
        if (!releasedEarly)
        {
            if (launchSettings != null && launchSettings.isActiveAndEnabled)
                BeginProjectileFlight(launchSettings);
            else
            {
                longNoteBoostRemaining = Mathf.Max(0f, launchDistance);
                longNoteBoostPreviousPosition = rb.position;
                if (flightBrake != null) flightBrake.BeginFlight(longNoteBoostRemaining);
            }
        }
        if (!isProjectileFlight)
            CurrentSpeed = Mathf.Abs(runSpeed) * (longNoteBoostRemaining > 0f ? longNoteLaunchMultiplier : 1f);
    }

    private static List<Vector3Int> BuildLongPath(Tilemap tilemap, Vector3Int startCell)
    {
        var path = new List<Vector3Int>();
        var visited = new HashSet<Vector3Int>();
        Vector3Int current = startCell;

        // Preserve cardinal branch priority, but also connect corner-touching
        // slope tiles. Their centers form a straight diagonal riding segment.
        Vector3Int[] priority =
        {
            Vector3Int.up,
            Vector3Int.right,
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            Vector3Int.down,
            Vector3Int.left,
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        while (tilemap.HasTile(current) && visited.Add(current))
        {
            path.Add(current);
            bool foundNext = false;

            foreach (Vector3Int direction in priority)
            {
                // Do not cut across an ordinary L bend or a filled block.
                // Only tiles connected exclusively at a corner are a slope.
                if (direction.x != 0 && direction.y != 0 &&
                    (tilemap.HasTile(current + new Vector3Int(direction.x, 0, 0)) ||
                     tilemap.HasTile(current + new Vector3Int(0, direction.y, 0))))
                    continue;
                Vector3Int next = current + direction;
                if (!visited.Contains(next) && tilemap.HasTile(next))
                {
                    current = next;
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext)
                break;
        }

        return path;
    }

    private void RecordLongRelease(ref bool releasedEarly)
    {
        if (releasedEarly || (RunKeyBindings.Held(RunKeyBindings.Cross)))
            return;

        releasedEarly = true;
        pendingLongNotePenalties++;
        LongNoteReleasedEarly?.Invoke();
        Debug.Log("Long note K released early. A score penalty is pending.");
    }

    private IEnumerator MoveAcrossTileSmoothly(
        Vector2 targetPos,
        bool nextGravityState,
        bool isWallRotation)
    {
        Vector2 startPos = rb.position;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = GetGravityRotation(nextGravityState, isWallRotation);
        float duration = Mathf.Max(0.02f, sideChangeDuration);
        float elapsed = 0f;

        // The middle tile can share an edge with BOTTOM or another solid collider.
        // Disable the player's shape only during traversal so those seams cannot catch it.
        playerCollider.enabled = false;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            Vector2 position = Vector2.Lerp(startPos, targetPos, progress);
            // During an up/down crossing, preserve the runner's normal forward motion.
            if (!isWallRotation)
                position.x += runSpeed * Mathf.Min(elapsed, duration);

            rb.position = position;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);
        }

        SetGravityDirection(nextGravityState, isWallRotation);
        playerCollider.enabled = true;
        Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate();
    }

    private IEnumerator HoldLongNote(float duration)
    {
        float elapsed = 0f;
        bool releasedEarly = false;

        while (elapsed < duration)
        {
            if (Time.timeScale == 0f) { yield return null; continue; }
            if (!releasedEarly &&
                (!RunKeyBindings.Held(RunKeyBindings.Cross)))
            {
                releasedEarly = true;
                pendingLongNotePenalties++;
                LongNoteReleasedEarly?.Invoke();
                Debug.Log("Long note K released early. A score penalty is pending.");
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>Consumes one deferred miss/score penalty when the score system is ready.</summary>
    public bool ConsumeLongNotePenalty()
    {
        if (pendingLongNotePenalties <= 0)
            return false;

        pendingLongNotePenalties--;
        return true;
    }

    private MiddleRun AnalyzeLongRun(RaycastHit2D targetHit)
    {
        if (targetHit.collider == null || targetHit.collider.gameObject.layer != longLayer)
            return default;

        Tilemap tilemap = targetHit.collider.GetComponent<Tilemap>();
        if (tilemap == null)
            return default;

        Vector3Int origin = tilemap.WorldToCell(targetHit.point + targetHit.normal * -0.01f);
        if (!tilemap.HasTile(origin))
            origin = tilemap.WorldToCell(targetHit.point - targetHit.normal * -0.01f);

        if (!tilemap.HasTile(origin))
            return default;

        int xCount = CountLineTiles(tilemap, origin, Vector3Int.right);
        int yCount = CountLineTiles(tilemap, origin, Vector3Int.up);

        // X has priority when both axes continue through the same middle cell.
        bool useX = xCount >= longNoteMinimumTiles;
        int count = useX ? xCount : yCount;
        Vector2Int axis = useX ? Vector2Int.right : Vector2Int.up;
        Vector3 cellSize = tilemap.layoutGrid.cellSize;
        float cellLength = useX ? Mathf.Abs(cellSize.x) : Mathf.Abs(cellSize.y);

        return new MiddleRun
        {
            tileCount = count,
            axis = axis,
            worldLength = count * cellLength
        };
    }

    private bool IsMiddleTwoByTwo(RaycastHit2D targetHit)
    {
        if (targetHit.collider == null || targetHit.collider.gameObject.layer != middleLayer)
            return false;

        Tilemap tilemap = targetHit.collider.GetComponent<Tilemap>();
        if (tilemap == null)
            return false;

        Vector3Int origin = GetHitCell(tilemap, targetHit);
        return tilemap.HasTile(origin) && HasTwoByTwoBlock(tilemap, origin);
    }

    private void SendLongRendererBehindPlayer(Collider2D longCollider)
    {
        if (longCollider == null || playerSpriteRenderer == null)
            return;

        TilemapRenderer longRenderer = longCollider.GetComponent<TilemapRenderer>();
        if (longRenderer == null)
            return;

        longRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        longRenderer.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
    }

    private static Vector3Int GetHitCell(Tilemap tilemap, RaycastHit2D targetHit)
    {
        Vector3Int origin = tilemap.WorldToCell(targetHit.point - targetHit.normal * 0.01f);
        if (!tilemap.HasTile(origin))
            origin = tilemap.WorldToCell(targetHit.point + targetHit.normal * 0.01f);
        return origin;
    }

    private static int CountLineTiles(Tilemap tilemap, Vector3Int origin, Vector3Int axis)
    {
        int count = 1;
        for (Vector3Int cell = origin + axis; tilemap.HasTile(cell); cell += axis)
            count++;
        for (Vector3Int cell = origin - axis; tilemap.HasTile(cell); cell -= axis)
            count++;
        return count;
    }

    private static bool HasTwoByTwoBlock(Tilemap tilemap, Vector3Int origin)
    {
        for (int xOffset = -1; xOffset <= 0; xOffset++)
        {
            for (int yOffset = -1; yOffset <= 0; yOffset++)
            {
                Vector3Int corner = origin + new Vector3Int(xOffset, yOffset, 0);
                if (tilemap.HasTile(corner) &&
                    tilemap.HasTile(corner + Vector3Int.right) &&
                    tilemap.HasTile(corner + Vector3Int.up) &&
                    tilemap.HasTile(corner + Vector3Int.right + Vector3Int.up))
                    return true;
            }
        }

        return false;
    }

    private struct MiddleRun
    {
        public int tileCount;
        public Vector2Int axis;
        public float worldLength;
    }

    private Vector2 GetCrossingPosition(RaycastHit2D hit, Vector2 direction, float halfExtent, float clearance)
    {
        Vector2 farFace = hit.point + direction * tileThickness;
        var map = hit.collider.GetComponent<Tilemap>();
        if (map != null)
        {
            Vector3Int cell = GetHitCell(map, hit);
            if (map.HasTile(cell))
            {
                // Use the actual far edge, including scaled Grid/Tilemap transforms.
                Vector3 localDirection = map.transform.InverseTransformVector(direction);
                Vector3Int step = Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.y)
                    ? new Vector3Int(localDirection.x > 0f ? 1 : -1, 0, 0)
                    : new Vector3Int(0, localDirection.y > 0f ? 1 : -1, 0);
                while (map.HasTile(cell + step)) cell += step;
                float edge = float.NegativeInfinity;
                for (int x = 0; x <= 1; x++)
                    for (int y = 0; y <= 1; y++)
                        edge = Mathf.Max(edge, Vector2.Dot(map.CellToWorld(cell + new Vector3Int(x, y, 0)), direction));
                farFace += direction * (edge - Vector2.Dot(farFace, direction));
            }
        }
        Vector2 center = playerCollider.bounds.center;
        Vector2 destinationCenter = center + direction *
            (Vector2.Dot(farFace - center, direction) + halfExtent + clearance);
        return destinationCenter - (center - rb.position);
    }

    private void UpdateJumpPadFlight()
    {
        if (isChangingSide || isRidingLong || playerCollider == null || !playerCollider.enabled) return;
        // Intended velocity remains available even when physics has stopped at a wall.
        Vector2 incoming = jumpTileGravityLock.IsLocked ? jumpPadFlightVelocity :
            isProjectileFlight ? new Vector2((float)longNoteTrajectory.VelocityX, (float)longNoteTrajectory.VelocityY) :
            new Vector2(runSpeed, GetGravityMoveSpeed());
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer),
            useTriggers = false
        };
        bool touchingJump = false, touchingMiniJump = false, touchingOtherTile = false;
        Vector2 bounceNormal = Vector2.zero;
        float strongestApproach = -1f;
        if (Mathf.Abs(incoming.x) > 0.001f)
            ProbeJumpPadSurface(Vector2.right * Mathf.Sign(incoming.x), incoming, filter,
                ref touchingJump, ref touchingMiniJump, ref touchingOtherTile, ref bounceNormal, ref strongestApproach);
        if (Mathf.Abs(incoming.y) > 0.001f)
            ProbeJumpPadSurface(Vector2.up * Mathf.Sign(incoming.y), incoming, filter,
                ref touchingJump, ref touchingMiniJump, ref touchingOtherTile, ref bounceNormal, ref strongestApproach);
        if (!jumpTileGravityLock.ObserveSupport(touchingJump, touchingOtherTile)) return;
        float jumpingMultiplier = Mathf.Max(1.01f, jumpingGravitySpeedMultiplier);
        activeJumpGravityMultiplier = touchingMiniJump
            ? Mathf.Max(jumpingMultiplier + 0.01f, miniJumpingGravitySpeedMultiplier)
            : jumpingMultiplier;
        if (IsLongNoteInFlight) StopLongNoteFlight();
        teleportLaunchDistanceRemaining = 0f;
        // The outward surface normal handles top, bottom, left, right, and slopes.
        // Keep gravity state and sprite orientation intact.
        jumpPadFlightVelocity = bounceNormal.normalized * Mathf.Abs(verticalMoveSpeed) * activeJumpGravityMultiplier;
        horizontalJumpPadFlight = Mathf.Abs(bounceNormal.x) > Mathf.Abs(bounceNormal.y);
        if (horizontalJumpPadFlight)
        {
            horizontalJumpMotion.Begin(jumpPadFlightVelocity.x,
                touchingMiniJump ? miniJumpingHorizontalDistance : jumpingHorizontalDistance, jumpPadFallAcceleration);
            jumpPadFlightVelocity.y = 0f;
        }
        else
        {
            jumpPadFlightVelocity.x = runSpeed;
        }
        rb.linearVelocity = jumpPadFlightVelocity;
    }

    private void ProbeJumpPadSurface(Vector2 direction, Vector2 incoming, ContactFilter2D filter,
        ref bool touchingJump, ref bool touchingMiniJump, ref bool touchingOtherTile,
        ref Vector2 bounceNormal, ref float strongestApproach)
    {
        float skin = Mathf.Max(Physics2D.defaultContactOffset, surfaceOffset);
        int count = playerCollider.Cast(direction, filter, jumpSupportHits, skin + 0.005f);
        for (int i = 0; i < count; i++)
        {
            var hit = jumpSupportHits[i];
            if (hit.collider == null || hit.collider == playerCollider ||
                Vector2.Dot(hit.normal, -direction) < 0.5f ||
                Physics2D.GetIgnoreCollision(playerCollider, hit.collider)) continue;
            var map = hit.collider.GetComponentInParent<Tilemap>();
            if (map == null || IsBackgroundTile(map.transform)) continue;
            bool jump = map.transform.parent != null && map.transform.parent.name == "Jump" &&
                (map.name == "Jumping" || map.name == "MiniJumping");
            if (jump)
            {
                touchingJump = true;
                float approach = Vector2.Dot(-incoming, hit.normal);
                bool mini = map.name == "MiniJumping";
                if (approach > strongestApproach || (Mathf.Approximately(approach, strongestApproach) && mini))
                {
                    strongestApproach = approach;
                    bounceNormal = hit.normal;
                    touchingMiniJump = mini;
                }
            }
            bool excludedLanding = IsLayerInMask(hit.collider.gameObject.layer, jumpLandingExcludedLayers) ||
                IsLayerInMask(map.gameObject.layer, jumpLandingExcludedLayers);
            touchingOtherTile |= !jump && !excludedLanding;
        }
    }

    private static bool IsBackgroundTile(Transform tile)
    {
        for (var t = tile; t != null; t = t.parent)
            if (string.Equals(t.name, "Background", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(LayerMask.LayerToName(t.gameObject.layer), "Background", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // Use the same support layers as custom gravity, including inverted landings.
    public bool IsOnGravitySurface()
    {
        if (playerCollider == null || !playerCollider.enabled) return false;
        Vector2 direction = isReverseGravity ? Vector2.up : Vector2.down;
        float skin = Mathf.Max(Physics2D.defaultContactOffset, surfaceOffset);
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = bottomLayerMask | passableTileLayerMask.value | obstacleLayerMask.value | longLayerMask,
            useTriggers = false
        };
        int count = playerCollider.Cast(direction, filter, bottomCastHits, skin + 0.005f);
        for (int i = 0; i < count; i++)
        {
            var hit = bottomCastHits[i];
            if (hit.collider != null && Vector2.Dot(hit.normal, -direction) >= 0.5f &&
                !Physics2D.GetIgnoreCollision(playerCollider, hit.collider)) return true;
        }
        return false;
    }

    private float ClampSurfaceMoveSpeed(float verticalSpeed)
    {
        if (!playerCollider.enabled || Mathf.Approximately(verticalSpeed, 0f))
            return verticalSpeed;

        Vector2 direction = verticalSpeed > 0f ? Vector2.up : Vector2.down;
        float skin = Mathf.Max(Physics2D.defaultContactOffset, surfaceOffset);
        float distance = Mathf.Abs(verticalSpeed) * Time.fixedDeltaTime + skin;
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = bottomLayerMask | passableTileLayerMask.value | obstacleLayerMask.value | longLayerMask,
            useTriggers = false
        };
        int count = playerCollider.Cast(direction, filter, bottomCastHits, distance);
        float remaining = Mathf.Abs(verticalSpeed) * Time.fixedDeltaTime;
        for (int i = 0; i < count; i++)
        {
            var hit = bottomCastHits[i];
            // A side wall or an ignored Long tile must not stop vertical travel.
            if (Vector2.Dot(hit.normal, -direction) < 0.5f ||
                Physics2D.GetIgnoreCollision(playerCollider, hit.collider))
                continue;
            remaining = Mathf.Min(remaining, Mathf.Max(0f, hit.distance - skin));
        }
        return Mathf.Sign(verticalSpeed) * remaining / Time.fixedDeltaTime;
    }

    private RaycastHit2D FindPreferredPassableHit(RaycastHit2D[] hits)
    {
        RaycastHit2D nearestPassable = default;
        RaycastHit2D nearestLong = default;
        float nearestObstacleDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider == playerCollider || hit.collider.isTrigger)
                continue;

            int layer = hit.collider.gameObject.layer;
            if (layer == longLayer)
            {
                if (nearestLong.collider == null || hit.distance < nearestLong.distance)
                    nearestLong = hit;
            }
            else if (IsLayerInMask(layer, passableTileLayerMask))
            {
                if (nearestPassable.collider == null || hit.distance < nearestPassable.distance)
                    nearestPassable = hit;
            }
            else if (IsLayerInMask(layer, obstacleLayerMask))
            {
                nearestObstacleDistance = Mathf.Min(nearestObstacleDistance, hit.distance);
            }
        }

        float overlapTolerance = Mathf.Max(surfaceOffset, Physics2D.defaultContactOffset * 2f);
        RaycastHit2D selectedHit = nearestPassable;

        // When middle and Long occupy the same boundary, Long always wins.
        if (nearestLong.collider != null &&
            (selectedHit.collider == null || nearestLong.distance <= selectedHit.distance + overlapTolerance))
        {
            selectedHit = nearestLong;
        }

        if (selectedHit.collider == null)
            return default;

        // MIDDLE and BOTTOM at the same boundary should select MIDDLE first.
        return nearestObstacleDistance + surfaceOffset < selectedHit.distance
            ? default
            : selectedHit;
    }

    private Collider2D FindBlockingDestinationObstacle(Vector2 targetPos, Collider2D targetTile)
    {
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            targetPos,
            playerCollider.bounds.size,
            0f,
            obstacleLayerMask);

        int middleLayer = LayerMask.NameToLayer("middle");
        bool targetIsMiddle = targetTile != null && targetTile.gameObject.layer == middleLayer;

        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == null || overlap == playerCollider || overlap.isTrigger)
                continue;

            // A BOTTOM collider touching MIDDLE must not cancel the MIDDLE crossing.
            if (targetIsMiddle && overlap.gameObject.layer == bottomLayer)
                continue;

            return overlap;
        }

        return null;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private float GetGravityMoveSpeed()
    {
        // 위/아래 이동은 반드시 같은 속력에서 방향만 반대로 사용합니다.
        // Inspector에 음수가 입력되더라도 양방향 이동 시간이 달라지지 않습니다.
        float gravitySpeed = Mathf.Abs(verticalMoveSpeed);
        if (jumpTileGravityLock.IsLocked)
            return jumpPadFlightVelocity.y;
        return isReverseGravity ? gravitySpeed : -gravitySpeed;
    }

    private void SetGravityDirection(bool reverseGravity, bool isWallRotation = false)
    {
        Vector3 colliderCenter = transform.TransformPoint(playerCollider.offset);
        isReverseGravity = reverseGravity;
        transform.rotation = GetGravityRotation(isReverseGravity, isWallRotation);
        // Rotate about the physics shape, including off-centre sprite/collider pivots.
        transform.position += colliderCenter - transform.TransformPoint(playerCollider.offset);
        Physics2D.SyncTransforms();
    }

    private static Quaternion GetGravityRotation(bool reverseGravity, bool isWallRotation)
    {
        if (!reverseGravity)
            return Quaternion.identity;

        // 세로 벽 통과 시: X축 180도 회전
        // 상하 타일 통과 시: Z축 180도 회전
        return isWallRotation
            ? Quaternion.Euler(180f, 0f, 0f)
            : Quaternion.Euler(0f, 0f, 180f);
    }
}
