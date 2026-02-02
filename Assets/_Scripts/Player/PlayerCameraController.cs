using Fusion;
using UnityEngine;
using Cinemachine;

public class PlayerCameraController : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private bool autoFindCamera = true;
    [SerializeField] private string cameraTargetName = "CameraTarget";
    
    private CinemachineVirtualCamera virtualCamera;
    private GameObject cameraTarget;
    private bool isLocalPlayer;
    
    public override void Spawned()
    {
        // Check if this is the local player
        isLocalPlayer = Object.HasInputAuthority;
        
        if (isLocalPlayer)
        {
            // This is the local player, set up the camera
            SetupLocalPlayerCamera();
            Debug.Log($"[PlayerCameraController] Set up camera for local player {Object.InputAuthority.PlayerId}");
        }
        else
        {
            // This is another player, disable any camera-related components
            Debug.Log($"[PlayerCameraController] Disabled camera for remote player {Object.InputAuthority.PlayerId}");
        }
    }
    
    private void SetupLocalPlayerCamera()
    {
        // Find or create the camera target
        cameraTarget = FindOrCreateCameraTarget();
        
        // Find the virtual camera
        if (autoFindCamera)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            
            if (virtualCamera == null)
            {
                // Create a new virtual camera if none exists
                GameObject cameraObject = new GameObject("PlayerVirtualCamera");
                cameraObject.tag = "MainCamera";
                virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                
                // Add Camera component if not present
                if (cameraObject.GetComponent<Camera>() == null)
                {
                    cameraObject.AddComponent<Camera>();
                }
            }
        }
        
        // Set the camera to follow this player's target
        if (virtualCamera != null && cameraTarget != null)
        {
            virtualCamera.Follow = cameraTarget.transform;
            virtualCamera.LookAt = cameraTarget.transform;
            virtualCamera.gameObject.SetActive(true);
            
            // Configure third-person follow
            var thirdPersonFollow = virtualCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (thirdPersonFollow != null)
            {
                thirdPersonFollow.CameraDistance = 5f;
                thirdPersonFollow.ShoulderOffset = new Vector3(0, 2f, 0);
            }
            
            Debug.Log("[PlayerCameraController] Camera configured to follow local player");
        }
        else
        {
            Debug.LogError("[PlayerCameraController] Failed to set up camera - missing virtual camera or target");
        }
    }
    
    private GameObject FindOrCreateCameraTarget()
    {
        // Try to find existing camera target
        cameraTarget = transform.Find(cameraTargetName)?.gameObject;
        
        if (cameraTarget == null)
        {
            // Try to find it in children
            cameraTarget = GetComponentInChildren<Transform>(true)?.Find(cameraTargetName)?.gameObject;
        }
        
        if (cameraTarget == null)
        {
            // Create a new camera target
            cameraTarget = new GameObject(cameraTargetName);
            cameraTarget.transform.SetParent(transform);
            cameraTarget.transform.localPosition = Vector3.up * 1.5f; // Position at head height
            Debug.Log("[PlayerCameraController] Created new camera target");
        }
        
        return cameraTarget;
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (isLocalPlayer && virtualCamera != null)
        {
            // Clean up camera when player despawns
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
        }
    }
    
    // Public method to get the camera target for other systems
    public Transform GetCameraTarget()
    {
        if (cameraTarget == null)
        {
            cameraTarget = FindOrCreateCameraTarget();
        }
        return cameraTarget.transform;
    }
}
