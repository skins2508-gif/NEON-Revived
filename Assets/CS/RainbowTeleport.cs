using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RainbowTeleport : MonoBehaviour
{
    [Header("레인보우 설정")]
    [SerializeField] private Tilemap targetTilemap;
    [SerializeField] private float rainbowSpeed = 0.8f;
    [SerializeField, Range(0f, 1f)] private float saturation = 1f;
    [SerializeField, Range(0f, 1f)] private float brightness = 1f;

    [Header("텔레포트 설정")]
    [SerializeField] private Transform destination;
    [SerializeField] private float teleportDelay = 0.2f;
    [SerializeField] private float launchDistance = 6f;
    [SerializeField] private float launchSpeed = 15f;

    private bool canTeleport = true;

    private void Awake()
    {
        if (targetTilemap == null)
        {
            targetTilemap = GetComponent<Tilemap>();
        }
    }

    private void Update()
    {
        if (targetTilemap == null)
            return;

        float hue = Mathf.Repeat(Time.time * rainbowSpeed, 1f);

        targetTilemap.color = Color.HSVToRGB(
            hue,
            saturation,
            brightness
        );
    }

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

        RainbowTeleport destinationTeleport =
            destination.GetComponent<RainbowTeleport>();

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
