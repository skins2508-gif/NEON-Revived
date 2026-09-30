using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BalrogBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 5f;
    [SerializeField, Min(0.1f)] private float lifeTime = 20f;

    private Rigidbody2D body;
    private Vector2 direction = Vector2.left;
    private Vector2 inheritedVelocity;

    private void Awake()
    {
        // Every spawned attack uses the player's configurable MISS layer mask.
        int missLayer = LayerMask.NameToLayer("MISS");
        if (missLayer >= 0) gameObject.layer = missLayer;
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        ApplyVelocity();
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector2 newDirection)
    {
        direction = newDirection.sqrMagnitude > 0f ? newDirection.normalized : Vector2.left;
        ApplyVelocity();
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);
        ApplyVelocity();
    }

    public void SetLifeTime(float newLifeTime)
    {
        lifeTime = Mathf.Max(0.1f, newLifeTime);
    }

    public void InheritLaunchVelocity(Vector2 velocity)
    {
        inheritedVelocity = velocity;
        ApplyVelocity();
    }

    private void ApplyVelocity()
    {
        if (body != null)
            body.linearVelocity = inheritedVelocity + direction * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform root = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform.root;

        if (other.CompareTag("Player") || root.CompareTag("Player"))
            Destroy(gameObject);
    }
}
