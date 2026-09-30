using UnityEngine;

public enum EnemyTriggerAction
{
    Show,
    Hide
}

[RequireComponent(typeof(Collider2D))]
public class EnemySpawnTrigger : MonoBehaviour
{
    [Header("등장시킬 적")]
    [SerializeField] private GameObject enemy;

    [Header("감지할 플레이어")]
    [Tooltip("비워두면 Player 태그를 가진 모든 플레이어를 감지합니다.")]
    [SerializeField] private Transform targetPlayer;

    [Header("실행 설정")]
    [SerializeField] private EnemyTriggerAction action = EnemyTriggerAction.Show;
    [SerializeField] private bool hideEnemyOnStart = true;
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;
    private Collider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (action == EnemyTriggerAction.Show && hideEnemyOnStart && enemy != null)
            enemy.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || !IsTargetPlayer(other))
            return;

        if (enemy == null)
        {
            Debug.LogWarning($"{gameObject.name}에 등장시킬 Enemy가 연결되지 않았습니다.");
            return;
        }

        enemy.SetActive(action == EnemyTriggerAction.Show);
        hasTriggered = true;

        if (triggerOnlyOnce)
            triggerCollider.enabled = false;
    }

    private bool IsTargetPlayer(Collider2D other)
    {
        Transform detectedPlayer = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform.root;

        if (targetPlayer != null)
            return detectedPlayer == targetPlayer;

        return other.CompareTag("Player") || detectedPlayer.CompareTag("Player");
    }
}
