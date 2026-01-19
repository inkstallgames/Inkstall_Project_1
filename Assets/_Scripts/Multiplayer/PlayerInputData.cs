using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
    // Movement input (WASD/Left Stick)
    public Vector2 movement;
    
    // Aim direction (Mouse/Right Stick)
    public Vector2 aimDirection;
    
    // Action inputs
    public bool isShooting;
    public bool isReloading;
    public bool isAiming;  // For ADS (Aim Down Sights)
    
    // Weapon switching
    public int weaponSlot; // 0 = primary, 1 = secondary, etc.
    
    // Utility actions
    public bool isUsingAbility; // For special abilities
    
    // Reset all inputs to default values
    public void Reset()
    {
        movement = Vector2.zero;
        aimDirection = Vector2.zero;
        isShooting = false;
        isReloading = false;
        isAiming = false;
        weaponSlot = 0;
        isUsingAbility = false;
    }
}