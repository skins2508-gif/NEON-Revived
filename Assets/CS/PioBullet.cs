using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PioBullet : MonoBehaviour
{
    [Header("총알 설정")]
    [SerializeField, Min(0f)] private float speed = 25f;
    [SerializeField, Min(0f)] private float lifeTime = 3f;
    [SerializeField] private Vector2 moveDirection = Vector2.left;

    private Rigidbody2D bulletRigidbody;

    private void Awake()
    {
        // Every spawned attack uses the player's configurable MISS layer mask.
        int missLayer = LayerMask.NameToLayer("MISS");
        if (missLayer >= 0) gameObject.layer = missLayer;
        bulletRigidbody = GetComponent<Rigidbody2D>();
        bulletRigidbody.gravityScale = 0f;
        bulletRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        SetVelocity(moveDirection);
        Destroy(gameObject, lifeTime);
    }

    public void SetDirection(Vector2 direction)
    {
        moveDirection = direction;

        if (bulletRigidbody != null)
            SetVelocity(moveDirection);
    }

    private void SetVelocity(Vector2 direction)
    {
        Vector2 normalizedDirection = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector2.left;

        bulletRigidbody.linearVelocity = normalizedDirection * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        Destroy(gameObject);
    }
}
