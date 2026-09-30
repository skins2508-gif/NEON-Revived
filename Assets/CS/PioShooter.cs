using System.Collections;
using UnityEngine;

public class PioShooter : MonoBehaviour
{
    [Header("발사 설정")]
    [SerializeField] private Transform firepoint;
    [SerializeField] private GameObject bulletTemplate;
    [SerializeField] private Transform targetPlayer;

    [Header("애니메이션 시간")]
    [SerializeField, Min(0f)] private float appearAnimationDuration = 1.5f;
    [SerializeField, Min(0f)] private float shootAnimationDuration = 0.5f;
    [SerializeField, Min(0f)] private float exitAnimationDuration = 1f;

    [Header("공격 설정")]
    [SerializeField, Min(0.1f)] private float fireInterval = 3f;
    [SerializeField, Min(1)] private int burstCount = 5;
    [SerializeField, Min(0.1f)] private float burstInterval = 0.15f;

    private Animator animator;
    private Coroutine firingRoutine;

    private static readonly int AppearAndIdleState = Animator.StringToHash("Pio1");
    private static readonly int ShootState = Animator.StringToHash("pio shoot");
    private static readonly int ExitState = Animator.StringToHash("pio exit");

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (bulletTemplate != null)
            bulletTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        if (animator != null)
            animator.Play(AppearAndIdleState, 0, 0f);

        firingRoutine = StartCoroutine(AttackSequence());
    }

    private void OnDisable()
    {
        if (firingRoutine != null)
        {
            StopCoroutine(firingRoutine);
            firingRoutine = null;
        }
    }

    private IEnumerator AttackSequence()
    {
        // 최초 활성화 때만 아래에서 올라오는 등장 애니메이션을 재생합니다.
        yield return new WaitForSeconds(appearAnimationDuration);
        ShowIdlePose();

        // 등장 완료 후 3초 뒤 첫 번째 단발을 발사합니다.
        yield return new WaitForSeconds(fireInterval);

        // 첫 번째와 두 번째 공격은 단발입니다.
        for (int attackNumber = 1; attackNumber <= 2; attackNumber++)
        {
            PlayShootAnimation();
            yield return new WaitForSeconds(shootAnimationDuration);
            ShowIdlePose();

            float remainingInterval = Mathf.Max(0f, fireInterval - shootAnimationDuration);
            yield return new WaitForSeconds(remainingInterval);
        }

        // 세 번째 공격은 짧은 간격의 5연발입니다.
        for (int shot = 0; shot < burstCount; shot++)
        {
            PlayShootAnimation();

            bool isLastShot = shot == burstCount - 1;
            float waitTime = isLastShot ? shootAnimationDuration : burstInterval;
            yield return new WaitForSeconds(waitTime);
        }

        // 5연발이 끝나면 퇴장 애니메이션을 한 번 재생합니다.
        if (animator != null)
            animator.Play(ExitState, 0, 0f);

        yield return new WaitForSeconds(exitAnimationDuration);

        // 퇴장 애니메이션이 모두 보인 다음 Pio 전체를 비활성화합니다.
        firingRoutine = null;
        gameObject.SetActive(false);
    }

    private void PlayShootAnimation()
    {
        if (animator != null)
            animator.Play(ShootState, 0, 0f);
        else
            FireBullet();
    }

    private void ShowIdlePose()
    {
        if (animator != null)
            animator.Play(AppearAndIdleState, 0, 1f);
    }

    // pio shoot 애니메이션의 Animation Event에서 호출됩니다.
    public void FireBullet()
    {
        if (firepoint == null || bulletTemplate == null || targetPlayer == null)
        {
            Debug.LogWarning($"{gameObject.name}의 총알 발사 설정이 연결되지 않았습니다.");
            return;
        }

        GameObject bulletObject = Instantiate(
            bulletTemplate,
            firepoint.position,
            Quaternion.identity
        );

        PioBullet bullet = bulletObject.GetComponent<PioBullet>();
        if (bullet == null)
        {
            Debug.LogWarning("총알 원본에 PioBullet 스크립트가 없습니다.");
            Destroy(bulletObject);
            return;
        }

        Vector2 direction = targetPlayer.position - firepoint.position;
        bullet.SetDirection(direction);
        bulletObject.SetActive(true);
    }
}
