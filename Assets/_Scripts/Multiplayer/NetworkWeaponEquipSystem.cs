using Fusion;
using UnityEngine;

/// <summary>
/// Manages weapon equipping system for pistol and bomb.
/// Only one weapon can be equipped at a time.
/// Attach to player prefab.
/// </summary>
public class NetworkWeaponEquipSystem : NetworkBehaviour
{
    public enum WeaponType
    {
        None = 0,
        Pistol = 1,
        Bomb = 2
    }

    [Header("Visual References")]
    [SerializeField] private GameObject pistolModel;
    [SerializeField] private GameObject bombModel;

    [Networked] public WeaponType CurrentWeapon { get; set; }

    private NetworkPistolBehaviour pistolBehaviour;
    private NetworkBombBehaviour bombBehaviour;
    private bool wantsToEquipPistol;
    private bool wantsToEquipBomb;

    public override void Spawned()
    {
        pistolBehaviour = GetComponent<NetworkPistolBehaviour>();
        bombBehaviour = GetComponent<NetworkBombBehaviour>();

        if (Object.HasStateAuthority)
        {
            CurrentWeapon = WeaponType.Pistol;
        }

        UpdateWeaponVisuals();
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.equipPistol = wantsToEquipPistol;
        inputData.equipBomb = wantsToEquipBomb;
        
        wantsToEquipPistol = false;
        wantsToEquipBomb = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<StarterAssets.NetworkInputData>(out var input))
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            if (input.equipPistol && CurrentWeapon != WeaponType.Pistol)
            {
                EquipWeapon(WeaponType.Pistol);
            }
            else if (input.equipBomb && CurrentWeapon != WeaponType.Bomb)
            {
                EquipWeapon(WeaponType.Bomb);
            }
        }
    }

    public override void Render()
    {
        UpdateWeaponVisuals();
    }

    private void EquipWeapon(WeaponType weapon)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        CurrentWeapon = weapon;
        string weaponName = weapon == WeaponType.Pistol ? "PISTOL" : "BOMB";
        Debug.Log($"[NetworkWeaponEquipSystem] *** Player {Object.InputAuthority.PlayerId} EQUIPPED {weaponName} ***");
    }

    private void UpdateWeaponVisuals()
    {
        if (pistolModel != null)
        {
            pistolModel.SetActive(CurrentWeapon == WeaponType.Pistol);
        }

        if (bombModel != null)
        {
            bombModel.SetActive(CurrentWeapon == WeaponType.Bomb);
        }
    }

    public bool IsPistolEquipped()
    {
        return CurrentWeapon == WeaponType.Pistol;
    }

    public bool IsBombEquipped()
    {
        return CurrentWeapon == WeaponType.Bomb;
    }

    public void RequestEquipPistol()
    {
        wantsToEquipPistol = true;
        Debug.Log("[NetworkWeaponEquipSystem] RequestEquipPistol() called");
    }

    public void RequestEquipBomb()
    {
        wantsToEquipBomb = true;
        Debug.Log("[NetworkWeaponEquipSystem] RequestEquipBomb() called");
    }
}
