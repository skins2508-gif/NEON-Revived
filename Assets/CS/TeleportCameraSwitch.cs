using UnityEngine;
using Unity.Cinemachine;

public class TeleportCameraSwitch : MonoBehaviour
{
    [SerializeField] private CinemachineCamera normalCamera;
    [SerializeField] private CinemachineCamera teleport2Camera;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        normalCamera.Priority = 0;
        teleport2Camera.Priority = 20;
    }
}