using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
    // Movement input (WASD/Left Stick)
    public Vector2 movement;
    
    // Aim direction (Mouse/Right Stick)
    public Vector3 aimDirection;
    
    // Action inputs
    public bool isShooting;
    public bool isReloading;
    public bool isAiming;  // For ADS (Aim Down Sights)
    
    // Bomb throw
    public bool isThrowingBomb;
    public Vector3 throwDirection;
    
    // Weapon switching
    public int weaponSlot; // 0 = primary, 1 = secondary, etc.
    
    // Utility actions
    public bool isUsingAbility; // For special abilities
    
    // Reset all inputs to default values
    public void Reset()
    {
        movement = Vector2.zero;
        aimDirection = Vector3.zero;
        isShooting = false;
        isReloading = false;
        isAiming = false;
        isThrowingBomb = false;
        throwDirection = Vector3.zero;
        weaponSlot = 0;
        isUsingAbility = false;
    }
}