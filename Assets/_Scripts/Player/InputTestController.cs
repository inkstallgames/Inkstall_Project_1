using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputTestController : NetworkBehaviour
{
    private Vector2 moveInput;
    private bool jumpInput;
    private bool sprintInput;
    
    public override void Spawned()
    {
        Debug.Log($"[InputTest] Spawned - PlayerID: {Object.InputAuthority.PlayerId}, HasInputAuthority: {Object.HasInputAuthority}");
    }
    
    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;
            
        // Simple input test
        moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        jumpInput = Input.GetKeyDown(KeyCode.Space);
        sprintInput = Input.GetKey(KeyCode.LeftShift);
        
        if (moveInput != Vector2.zero || jumpInput || sprintInput)
        {
            Debug.Log($"[InputTest] Player {Object.InputAuthority.PlayerId} - Move: {moveInput}, Jump: {jumpInput}, Sprint: {sprintInput}");
        }
    }
}
