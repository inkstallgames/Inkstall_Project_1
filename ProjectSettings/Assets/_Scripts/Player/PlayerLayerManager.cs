using UnityEngine;
using Fusion;

/// <summary>
/// Manages layer assignments for multiplayer FPS visibility
/// Local player: LocalPlayer layer (hidden from own camera)
/// Remote players: RemotePlayer layer (visible to all cameras)
/// </summary>
public class PlayerLayerManager : NetworkBehaviour
{
    [Header("Layer Settings")]
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Transform handsTransform;
    
    // Layer indices (cached for performance)
    private static int localPlayerLayer;
    private static int remotePlayerLayer;
    private static int fpsHandsLayer;
    
    private void Awake()
    {
        // Cache layer indices
        localPlayerLayer = LayerMask.NameToLayer("LocalPlayer");
        remotePlayerLayer = LayerMask.NameToLayer("RemotePlayer");
        fpsHandsLayer = LayerMask.NameToLayer("FPS_Hands");
        
        // Find body and hands transforms if not assigned
        if (bodyTransform == null)
            bodyTransform = transform.Find("FullPlayerBody");
            
        if (handsTransform == null)
            handsTransform = transform.Find("FPS_Hands");
    }
    
    public override void Spawned()
    {
        // Set layers based on whether this is the local player
        bool isLocalPlayer = Object.HasInputAuthority;
        
        if (isLocalPlayer)
        {
            SetLocalPlayerLayers();
        }
        else
        {
            SetRemotePlayerLayers();
        }
    }
    
    private void SetLocalPlayerLayers()
    {
        Debug.Log($"[PlayerLayerManager] Setting layers for local player: {Object.InputAuthority.PlayerId}");
        
        // Hide local player's body from their own camera
        if (bodyTransform != null)
        {
            SetLayerRecursively(bodyTransform.gameObject, localPlayerLayer);
        }
        
        // Show hands to local player
        if (handsTransform != null)
        {
            SetLayerRecursively(handsTransform.gameObject, fpsHandsLayer);
        }
        
        // Configure camera culling mask
        ConfigureLocalPlayerCamera();
    }
    
    private void SetRemotePlayerLayers()
    {
        Debug.Log($"[PlayerLayerManager] Setting layers for remote player: {Object.InputAuthority.PlayerId}");
        
        // Show remote player's full body
        if (bodyTransform != null)
        {
            SetLayerRecursively(bodyTransform.gameObject, remotePlayerLayer);
        }
        
        // Hide hands from other players (they shouldn't see floating hands)
        if (handsTransform != null)
        {
            SetLayerRecursively(handsTransform.gameObject, remotePlayerLayer);
        }
    }
    
    private void ConfigureLocalPlayerCamera()
    {
        // Find the main camera - more robust search
        Camera mainCamera = null;
        
        // Try Camera.main first
        mainCamera = Camera.main;
        
        // If not found, try to find camera tagged as MainCamera
        if (mainCamera == null)
        {
            GameObject camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null)
                mainCamera = camObj.GetComponent<Camera>();
        }
        
        // If still not found, try any camera
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
            
        if (mainCamera != null)
        {
            // Parent camera to player's CameraRoot so it follows the player
            Transform cameraRoot = transform.Find("CameraRoot");
            if (cameraRoot != null)
            {
                mainCamera.transform.SetParent(cameraRoot);
                mainCamera.transform.localPosition = Vector3.zero;
                mainCamera.transform.localRotation = Quaternion.identity;
                Debug.Log($"[PlayerLayerManager] Parented Main Camera to CameraRoot");
            }
            
            // Create culling mask for Main Camera
            // Main Camera should NOT render FPS_Hands (separate hands camera handles that)
            int cullingMask = mainCamera.cullingMask;
            cullingMask &= ~(1 << localPlayerLayer); // Exclude LocalPlayer layer
            cullingMask &= ~(1 << fpsHandsLayer);    // Exclude FPS_Hands layer (handled by separate camera)
            cullingMask |= (1 << remotePlayerLayer); // Include RemotePlayer layer
            
            mainCamera.cullingMask = cullingMask;
            
            Debug.Log($"[PlayerLayerManager] Configured Main Camera '{mainCamera.name}' culling mask. Excluding LocalPlayer and FPS_Hands layers.");
            
            // Configure the separate hands camera if it exists
            ConfigureHandsCamera(cameraRoot);
        }
        else
        {
            Debug.LogError("[PlayerLayerManager] No camera found! Camera culling mask not configured.");
        }
    }
    
    private void ConfigureHandsCamera(Transform cameraRoot)
    {
        // Look for a separate hands camera (commonly named "HandsCamera" or similar)
        Camera handsCamera = null;
        
        // Try to find camera by common names
        string[] handsCameraNames = { "HandsCamera", "FPS_Hands_Camera", "PlayerHandsCamera" };
        foreach (string camName in handsCameraNames)
        {
            GameObject camObj = GameObject.Find(camName);
            if (camObj != null)
            {
                handsCamera = camObj.GetComponent<Camera>();
                if (handsCamera != null)
                    break;
            }
        }
        
        // If not found by name, try to find camera that renders FPS_Hands layer
        if (handsCamera == null)
        {
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in allCameras)
            {
                if (cam != Camera.main && (cam.cullingMask & (1 << fpsHandsLayer)) != 0)
                {
                    handsCamera = cam;
                    break;
                }
            }
        }
        
        if (handsCamera != null)
        {
            // Parent hands camera to player's CameraRoot so it follows the player
            if (cameraRoot != null)
            {
                handsCamera.transform.SetParent(cameraRoot);
                handsCamera.transform.localPosition = Vector3.zero;
                handsCamera.transform.localRotation = Quaternion.identity;
                Debug.Log($"[PlayerLayerManager] Parented Hands Camera to CameraRoot");
            }
            
            // Configure hands camera to ONLY render FPS_Hands layer
            int handsCullingMask = 0;
            handsCullingMask |= (1 << fpsHandsLayer); // Only render FPS_Hands layer
            
            handsCamera.cullingMask = handsCullingMask;
            
            Debug.Log($"[PlayerLayerManager] Configured Hands Camera '{handsCamera.name}' to render only FPS_Hands layer.");
        }
        else
        {
            Debug.LogWarning("[PlayerLayerManager] No separate hands camera found. FPS_Hands will not be rendered.");
        }
    }
    
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        
        obj.layer = layer;
        
        // Set layer for all children
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
        }
    }
    
    private void OnValidate()
    {
        // Auto-assign transforms in editor
        if (bodyTransform == null && transform.Find("FullPlayerBody") != null)
            bodyTransform = transform.Find("FullPlayerBody");
            
        if (handsTransform == null && transform.Find("FPS_Hands") != null)
            handsTransform = transform.Find("FPS_Hands");
    }
}
