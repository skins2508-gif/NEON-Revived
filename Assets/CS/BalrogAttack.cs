using System.Collections;
using UnityEngine;

public class BalrogAttack : MonoBehaviour
{
    [Header("발사 연결")]
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private GameObject bulletTemplate;
    [SerializeField] private Transform targetPlayer;

    [Header("발록 이미지")]
    [SerializeField] private SpriteRenderer balrogRenderer;
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite attackSprite;

    [Header("공격 시간")]
    [SerializeField, Min(0f)] private float firstAttackDelay = 0.5f;
    [SerializeField, Min(0f)] private float firePointDelay = 0.8f;
    [SerializeField, Min(1)] private int totalShots = 10;
    [Tooltip("플레이어 middle을 기준으로 위/아래 조준점까지의 거리")]
    [SerializeField, Min(0f)] private float verticalAimOffset = 0.32f;

    [Header("마무리 공격")]
    [SerializeField, Min(1f)] private float finalAttackScale = 2f;
    [SerializeField, Min(0f)] private float finalAttackSpeed = 0.02f;
    [SerializeField, Min(0.1f)] private float finalAttackLifeTime = 60f;
    [SerializeField, Min(0f)] private float exitDelayAfterFinalAttack = 3f;

    [Header("입장 / 퇴장")]
    [SerializeField, Min(0f)] private float enterAnimationDuration = 1f;
    [SerializeField, Min(0f)] private float exitAnimationDuration = 1f;

    private Animator visualAnimator;
    private Coroutine attackRoutine;

    private static readonly int EntranceState = Animator.StringToHash("balrog");
    private static readonly int AttackState = Animator.StringToHash("attack");

    private void Awake()
    {
        if (balrogRenderer != null)
            visualAnimator = balrogRenderer.GetComponent<Animator>();

        if (bulletTemplate != null)
            bulletTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        if (visualAnimator != null)
        {
            visualAnimator.enabled = true;
            visualAnimator.speed = 1f;
            visualAnimator.Play(EntranceState, 0, 0f);
        }

        if (balrogRenderer != null && visualAnimator == null)
            balrogRenderer.sprite = idleSprite;

        attackRoutine = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        ShowIdleImage();
    }

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(enterAnimationDuration);
        yield return new WaitForSeconds(firstAttackDelay);

        ShowAttackImage();

        for (int shot = 0; shot < totalShots; shot++)
        {
            int firePointIndex = shot % firePoints.Length;
            bool isFinalAttack = shot == totalShots - 1;
            float aimOffset = isFinalAttack
                ? 0f
                : (firePointIndex % 2 == 0 ? verticalAimOffset : -verticalAimOffset);
            Fire(firePoints[firePointIndex], aimOffset, isFinalAttack);

            if (shot < totalShots - 1)
                yield return new WaitForSeconds(firePointDelay);
        }

        yield return new WaitForSeconds(exitDelayAfterFinalAttack);

        ShowIdleImage();
        yield return PlayExitAnimation();
        attackRoutine = null;
        gameObject.SetActive(false);
    }

    private GameObject Fire(Transform firePoint, float aimOffset, bool isFinalAttack)
    {
        if (firePoint == null || bulletTemplate == null || targetPlayer == null)
        {
            Debug.LogWarning($"{name}: 발록 공격 연결을 확인해 주세요.");
            return null;
        }

        GameObject bulletObject = Instantiate(bulletTemplate, firePoint.position, Quaternion.identity);
        if (isFinalAttack)
            bulletObject.transform.localScale *= finalAttackScale;

        BalrogBullet bullet = bulletObject.GetComponent<BalrogBullet>();
        if (bullet == null)
        {
            Debug.LogWarning("BalBullet에 BalrogBullet 스크립트가 없습니다.");
            Destroy(bulletObject);
            return null;
        }

        if (isFinalAttack)
        {
            bullet.SetSpeed(finalAttackSpeed);
            bullet.SetLifeTime(finalAttackLifeTime);

            Rigidbody2D playerBody = targetPlayer.GetComponent<Rigidbody2D>();
            if (playerBody != null)
                bullet.InheritLaunchVelocity(new Vector2(playerBody.linearVelocity.x, 0f));
        }

        Vector3 aimPosition = targetPlayer.position + Vector3.up * aimOffset;
        bulletObject.SetActive(true);
        bullet.Launch(aimPosition - firePoint.position);
        return bulletObject;
    }

    private IEnumerator PlayExitAnimation()
    {
        if (visualAnimator != null)
        {
            visualAnimator.enabled = true;
            visualAnimator.speed = -1f;
            visualAnimator.Play(EntranceState, 0, 1f);
        }

        yield return new WaitForSeconds(exitAnimationDuration);

        if (visualAnimator != null)
            visualAnimator.speed = 1f;
    }

    private void ShowAttackImage()
    {
        if (visualAnimator != null)
        {
            visualAnimator.enabled = true;
            visualAnimator.speed = 1f;
            visualAnimator.Play(AttackState, 0, 0f);
            return;
        }

        if (balrogRenderer != null && attackSprite != null)
            balrogRenderer.sprite = attackSprite;
    }

    private void ShowIdleImage()
    {
        if (visualAnimator != null)
        {
            visualAnimator.enabled = false;
            visualAnimator.speed = 1f;
        }

        if (balrogRenderer != null && idleSprite != null)
            balrogRenderer.sprite = idleSprite;
    }
}
