using Fusion;
using UnityEngine;

/// <summary>
/// Lightweight network movement helper.
/// The actual movement is handled by ThirdPersonController using CharacterController.
/// This script only exposes networked properties (e.g. AimDirection) that other systems need.
/// DO NOT add movement logic here — it will conflict with ThirdPersonController.
/// </summary>
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Networked] public Vector3 AimDirection { get; set; }
    
    private PlayerCameraController cameraController;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            cameraController = GetComponent<PlayerCameraController>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only update aim direction from input — movement is handled by ThirdPersonController
        if (GetInput<PlayerInputData>(out var input))
        {
            if (input.aimDirection != Vector3.zero)
            {
                AimDirection = input.aimDirection;
            }
        }
    }
}
