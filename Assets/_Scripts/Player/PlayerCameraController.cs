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

        }
        else
        {
            // This is another player, disable any camera-related components

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

                    break;
                }
            }
            
            if (virtualCamera == null)
            {
                // Create a new virtual camera if none exists

                GameObject cameraObject = new GameObject("PlayerVirtualCamera");
                virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
                
                // Set priority higher than any existing cameras
                virtualCamera.Priority = 100;
                

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
            
            // Set near clip plane
            virtualCamera.m_Lens.NearClipPlane = 1f;
            
            // Configure third-person follow if not already present
            var thirdPersonFollow = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (thirdPersonFollow == null)
            {
                thirdPersonFollow = virtualCamera.AddCinemachineComponent<Cinemachine3rdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    thirdPersonFollow.CameraDistance = 0f;
                    thirdPersonFollow.ShoulderOffset = new Vector3(0, 0, 0.1f);
                }
            }
            

        }
        else
        {

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
