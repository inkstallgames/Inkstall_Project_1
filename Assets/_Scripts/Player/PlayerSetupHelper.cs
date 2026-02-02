using UnityEngine;
using Fusion;
using UnityEngine.InputSystem;
using StarterAssets;

/// <summary>
/// This component ensures that all required input components are properly set up on the player character
/// It should be placed on the character prefabs
/// </summary>
public class PlayerSetupHelper : NetworkBehaviour
{
    [Header("Components to verify")]
    private StarterAssetsInputs starterAssetsInputs;
    private PlayerInput playerInput;
    private PlayerInputHandler playerInputHandler;
    private ThirdPersonController thirdPersonController;
    private PlayerCameraController playerCameraController;
    
    public override void Spawned()
    {
        Debug.Log($"[PlayerSetupHelper] Spawned() - PlayerID: {Object.InputAuthority.PlayerId}, HasInputAuthority: {Object.HasInputAuthority}");
        
        // Find or add required components
        SetupComponents();
        
        // Configure components based on input authority
        ConfigureForLocalPlayer();
    }
    
    private void SetupComponents()
    {
        // Get or add StarterAssetsInputs
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        if (starterAssetsInputs == null)
        {
            starterAssetsInputs = gameObject.AddComponent<StarterAssetsInputs>();
            Debug.Log("[PlayerSetupHelper] Added StarterAssetsInputs component");
        }
        
        // Get or add PlayerInput
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
            
            // Configure PlayerInput
            playerInput.actions = FindObjectOfType<InputActionAsset>() ?? Resources.Load<InputActionAsset>("InputActions");
            if (playerInput.actions != null)
            {
                playerInput.defaultActionMap = "Player";
            }
            Debug.Log("[PlayerSetupHelper] Added PlayerInput component");
        }
        
        // Get or add PlayerInputHandler
        playerInputHandler = GetComponent<PlayerInputHandler>();
        if (playerInputHandler == null)
        {
            playerInputHandler = gameObject.AddComponent<PlayerInputHandler>();
            Debug.Log("[PlayerSetupHelper] Added PlayerInputHandler component");
        }
        
        // Get ThirdPersonController
        thirdPersonController = GetComponent<ThirdPersonController>();
        if (thirdPersonController == null)
        {
            Debug.LogError("[PlayerSetupHelper] ThirdPersonController component not found!");
        }
        
        // Get PlayerCameraController
        playerCameraController = GetComponent<PlayerCameraController>();
        if (playerCameraController == null)
        {
            Debug.LogError("[PlayerSetupHelper] PlayerCameraController component not found!");
        }
    }
    
    private void ConfigureForLocalPlayer()
    {
        bool isLocalPlayer = Object.HasInputAuthority;
        
        Debug.Log($"[PlayerSetupHelper] Configuring components for local player: {isLocalPlayer}");
        
        // Enable/disable input components based on authority
        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.enabled = isLocalPlayer;
            Debug.Log($"[PlayerSetupHelper] StarterAssetsInputs enabled: {starterAssetsInputs.enabled}");
        }
        
        if (playerInput != null)
        {
            playerInput.enabled = isLocalPlayer;
            Debug.Log($"[PlayerSetupHelper] PlayerInput enabled: {playerInput.enabled}");
        }
        
        // Note: PlayerInputHandler will handle its own enabling/disabling in its Spawned() method
    }
}
