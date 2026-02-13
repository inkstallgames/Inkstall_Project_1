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
        
        Debug.Log($"[PlayerCameraController] Spawned() - PlayerID: {Object.InputAuthority.PlayerId}, IsLocalPlayer: {isLocalPlayer}");
        
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
            // Find all virtual cameras and use the first one that's not already assigned to another player
            var allVirtualCameras = FindObjectsOfType<CinemachineVirtualCamera>();
            
            foreach (var cam in allVirtualCameras)
            {
                // Check if this camera is already following another player
                if (cam.Follow == null || cam.Follow == cameraTarget.transform)
                {
                    virtualCamera = cam;
                    Debug.Log($"[PlayerCameraController] Found available virtual camera: {cam.name}");
                    break;
                }
            }
            
            if (virtualCamera == null)
            {
                // Create a new virtual camera if none exists
                Debug.Log("[PlayerCameraController] No available camera found, creating new one");
                GameObject cameraObject = new GameObject("PlayerVirtualCamera");
                virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                
                // Set priority higher than any existing cameras
                virtualCamera.Priority = 100;
                
                Debug.Log("[PlayerCameraController] Created new virtual camera");
            }
        }
        
        // Set the camera to follow this player's target
        if (virtualCamera != null && cameraTarget != null)
        {
            virtualCamera.Follow = cameraTarget.transform;
            virtualCamera.LookAt = cameraTarget.transform;
            virtualCamera.gameObject.SetActive(true);
            
            // Set high priority to ensure this camera is active for the local player
            virtualCamera.Priority = 100;
            
            // Prevent camera from seeing local player's own mesh
            var localPlayerRenderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in localPlayerRenderers)
            {
                // Disable rendering for local player's own mesh
                renderer.enabled = false;
            }
            
            // Configure third-person follow if not already present
            var thirdPersonFollow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (thirdPersonFollow == null)
            {
                thirdPersonFollow = virtualCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    thirdPersonFollow.CameraDistance = 2.5f; // Proper third-person distance
                    thirdPersonFollow.ShoulderOffset = new Vector3(0.5f, 1f, 0f); // Right shoulder offset
                    
                    // Prevent camera clipping through player
                    thirdPersonFollow.CameraCollisionFilter = -1; // Ignore all collisions initially
                    thirdPersonFollow.CameraRadius = 0.2f; // Small collision radius
                    thirdPersonFollow.Damping = new Vector3(0.1f, 0.1f, 0.1f); // Smooth movement
                }
            }
            else
            {
                // Update existing third-person follow component
                thirdPersonFollow.CameraDistance = 2.5f;
                thirdPersonFollow.ShoulderOffset = new Vector3(0.5f, 1f, 0f);
                thirdPersonFollow.CameraRadius = 0.2f;
                thirdPersonFollow.Damping = new Vector3(0.1f, 0.1f, 0.1f);
            }
            
            Debug.Log($"[PlayerCameraController] Camera configured to follow local player. Camera: {virtualCamera.name}, Target: {cameraTarget.name}");
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
        if (isLocalPlayer)
        {
            // Restore player renderers when despawning
            var localPlayerRenderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in localPlayerRenderers)
            {
                renderer.enabled = true;
            }
            
            // Clean up camera when player despawns
            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }
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
