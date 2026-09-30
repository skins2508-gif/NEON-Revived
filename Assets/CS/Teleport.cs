using System.Collections;
using UnityEngine;

public class Teleport : MonoBehaviour
{
    [Header("텔레포트 설정")]
    [SerializeField] private Transform destination;
    [SerializeField] private float teleportDelay = 0.2f;
    [SerializeField] private float launchDistance = 6f;
    [SerializeField] private float launchSpeed = 15f;

    private bool canTeleport = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canTeleport)
            return;

        if (!other.CompareTag("Player"))
            return;

        if (destination == null)
        {
            Debug.LogWarning($"{gameObject.name}의 Destination이 비어 있습니다.");
            return;
        }

        Teleport destinationTeleport =
            destination.GetComponent<Teleport>();

        if (destinationTeleport != null)
        {
            destinationTeleport.BlockTemporarily();
        }

        Rigidbody2D playerRigidbody =
            other.GetComponent<Rigidbody2D>();

        if (playerRigidbody != null)
        {
            playerRigidbody.position = destination.position;

            AutoRunner autoRunner = other.GetComponent<AutoRunner>();
            if (autoRunner != null)
            {
                autoRunner.LaunchFromTeleport(launchDistance, launchSpeed);
            }
            else
            {
                playerRigidbody.linearVelocity =
                    new Vector2(launchSpeed, playerRigidbody.linearVelocity.y);
            }
        }
        else
        {
            other.transform.position = destination.position;
        }
    }

    public void BlockTemporarily()
    {
        StopAllCoroutines();
        StartCoroutine(TeleportCooldown());
    }

    private IEnumerator TeleportCooldown()
    {
        canTeleport = false;

        yield return new WaitForSeconds(teleportDelay);

        canTeleport = true;
    }
}
