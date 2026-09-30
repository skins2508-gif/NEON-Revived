using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FinishLine : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision) => TryFinish(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => TryFinish(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryFinish(other);
    private void OnTriggerStay2D(Collider2D other) => TryFinish(other);

    private void TryFinish(Collider2D other)
    {
        var runner = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<AutoRunner>()
            : other.GetComponentInParent<AutoRunner>();
        if (runner != null && !runner.HasFinished)
            RunGameFlow.Instance?.Complete(runner);
    }
}
