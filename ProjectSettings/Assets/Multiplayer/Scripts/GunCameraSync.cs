using UnityEngine;
using Fusion;

/// <summary>
/// Fixes gun jitter by making the gun follow the camera target rotation in LateUpdate.
/// Attach this to the gun model GameObject (must be child of player).
/// The gun will rotate to match the camera's Y rotation, eliminating jitter.
/// </summary>
public class GunCameraSync : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera target transform (usually CameraTarget on player)")]
    [SerializeField] private Transform cameraTarget;
    
    [Header("Settings")]
    [Tooltip("Offset from camera target position")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0.3f, -0.2f, 0.5f);
    
    [Tooltip("If true, gun rotates with camera in LateUpdate")]
    [SerializeField] private bool followCameraRotation = true;

    private NetworkObject networkObject;
    private bool isLocalPlayer;

    private void Start()
    {
        // Find the camera target if not assigned
        if (cameraTarget == null)
        {
            var player = GetComponentInParent<NetworkObject>();
            if (player != null)
            {
                isLocalPlayer = player.HasInputAuthority;
                
                // Find CameraTarget in player hierarchy
                var cameraController = player.GetComponent<PlayerCameraController>();
                if (cameraController != null)
                {
                    cameraTarget = cameraController.GetCameraTarget();
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (cameraTarget == null || !followCameraRotation)
        {
            return;
        }

        if (isLocalPlayer)
        {
            // Local player: Update gun rotation to match camera target's Y rotation
            // This happens in LateUpdate AFTER camera movement, eliminating jitter
            Vector3 cameraEuler = cameraTarget.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
        }
        else
        {
            // Remote player: Force gun to match camera target rotation directly
            // This bypasses parent's interpolated rotation to reduce jitter
            Vector3 cameraEuler = cameraTarget.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
        }
    }
}
