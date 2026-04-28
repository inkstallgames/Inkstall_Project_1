using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{
    private PlayerInput playerInput;
    private Vector2 moveInput;
    private Vector2 aimInput;
    private bool isShooting;
    
    public override void Spawned()
    {
        Debug.Log($"[PlayerInputHandler] Spawned() - PlayerID: {Object.InputAuthority.PlayerId}, HasInputAuthority: {Object.HasInputAuthority}");
        
        // Enable PlayerInput only for the local player
        if (playerInput != null)
        {
            playerInput.enabled = Object.HasInputAuthority;
            Debug.Log($"[PlayerInputHandler] PlayerInput enabled: {playerInput.enabled} for Player {Object.InputAuthority.PlayerId}");
        }
    }
    
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }
        
        Debug.Log($"[PlayerInputHandler] Awake() called on gameObject: {gameObject.name}");
    }
    
    public override void FixedUpdateNetwork()
    {
        // Only process input for the local player
        if (!Object.HasInputAuthority)
        {
            // Uncomment to see which players are being blocked
            // Debug.Log($"[PlayerInputHandler] FixedUpdateNetwork blocked for Player {Object.InputAuthority.PlayerId} - not local player");
            return;
        }
            
        if (GetInput<PlayerInputData>(out var input))
        {
            // Get movement input (WASD/Joystick)
            moveInput = new Vector2(
                playerInput.actions["Move"].ReadValue<Vector2>().x,
                playerInput.actions["Move"].ReadValue<Vector2>().y
            );
            
            // Get aim input (Mouse/Right Stick)
            if (playerInput.currentControlScheme == "Keyboard&Mouse")
            {
                // Get camera for raycasting
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                    mainCamera = FindObjectOfType<Camera>();
                
                if (mainCamera != null)
                {
                    // Create ray from camera through mouse position
                    Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                    Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                    
                    if (groundPlane.Raycast(ray, out float distance))
                    {
                        Vector3 worldPos = ray.GetPoint(distance);
                        Vector3 direction = worldPos - transform.position;
                        direction.y = 0; // Keep only horizontal direction
                        aimInput = new Vector2(direction.x, direction.z).normalized;
                    }
                    else
                    {
                        aimInput = Vector2.zero;
                    }
                }
                else
                {
                    aimInput = Vector2.zero;
                }
            }
            else
            {
                // Controller right stick
                aimInput = playerInput.actions["Aim"].ReadValue<Vector2>().normalized;
            }
            
            // Get shooting input
            isShooting = playerInput.actions["Fire"].ReadValue<float>() > 0.1f;
            
            // Get jump input
            bool isJumping = playerInput.actions["Jump"].ReadValue<float>() > 0.1f;
            
            // Update network input
            input.movement = moveInput;
            input.aimDirection = aimInput;
            input.isShooting = isShooting;
            input.isJumping = isJumping;
            
            // Debug logs for input (uncomment to see input values)
            // Debug.Log($"[PlayerInputHandler] Input - Move: {moveInput}, Aim: {aimInput}, Shoot: {isShooting}");
        }
    }
    
    // Called by the new Input System
    public void OnMove(InputAction.CallbackContext context)
    {
        if (Object != null && Object.HasInputAuthority)
        {
            moveInput = context.ReadValue<Vector2>();
            Debug.Log($"[PlayerInputHandler] OnMove called for Player {Object.InputAuthority.PlayerId}: {moveInput}");
        }
    }
    
    public void OnAim(InputAction.CallbackContext context)
    {
        aimInput = context.ReadValue<Vector2>();
    }
    
    public void OnFire(InputAction.CallbackContext context)
    {
        isShooting = context.ReadValueAsButton();
    }
    
    public void OnJump(InputAction.CallbackContext context)
    {
        if (Object != null && Object.HasInputAuthority)
        {
            Debug.Log($"[PlayerInputHandler] OnJump called for Player {Object.InputAuthority.PlayerId}");
        }
    }
}
