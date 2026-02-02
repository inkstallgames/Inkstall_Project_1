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
        // Enable PlayerInput only for the local player
        if (playerInput != null)
        {
            playerInput.enabled = Object.HasInputAuthority;
        }
    }
    
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = gameObject.AddComponent<PlayerInput>();
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        // Only process input for the local player
        if (!Object.HasInputAuthority)
            return;
            
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
                // Mouse position relative to player
                Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3 direction = mousePos - transform.position;
                aimInput = new Vector2(direction.x, direction.y).normalized;
            }
            else
            {
                // Controller right stick
                aimInput = playerInput.actions["Aim"].ReadValue<Vector2>().normalized;
            }
            
            // Get shooting input
            isShooting = playerInput.actions["Fire"].ReadValue<float>() > 0.1f;
            
            // Update network input
            input.movement = moveInput;
            input.aimDirection = aimInput;
            input.isShooting = isShooting;
        }
    }
    
    // Called by the new Input System
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
    
    public void OnAim(InputAction.CallbackContext context)
    {
        aimInput = context.ReadValue<Vector2>();
    }
    
    public void OnFire(InputAction.CallbackContext context)
    {
        isShooting = context.ReadValueAsButton();
    }
}
