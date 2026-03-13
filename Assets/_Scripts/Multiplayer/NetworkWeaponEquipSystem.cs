using Fusion;
using UnityEngine;

/// <summary>
/// Manages weapon equipping system with team-based weapons.
/// Team A (TeamId 0): Pistol and Bomb
/// Team B (TeamId 1): Laser and Bomb
/// Only one weapon can be equipped at a time.
/// Attach to player prefab.
/// </summary>
public class NetworkWeaponEquipSystem : NetworkBehaviour
{
    public enum WeaponType
    {
        None = 0,
        Pistol = 1,
        Laser = 2,
        Bomb = 3
    }

    [Header("Visual References")]
    [SerializeField] private GameObject pistolModel;
    [SerializeField] private GameObject laserModel;
    [SerializeField] private GameObject bombModel;

    [Networked] public WeaponType CurrentWeapon { get; set; }

    private NetworkPistolBehaviour pistolBehaviour;
    private NetworkLaserBehaviour laserBehaviour;
    private NetworkBombBehaviour bombBehaviour;
    private PlayerNetworkData playerData;
    private bool wantsToEquipPrimary;
    private bool wantsToEquipBomb;

    public override void Spawned()
    {
        pistolBehaviour = GetComponent<NetworkPistolBehaviour>();
        laserBehaviour = GetComponent<NetworkLaserBehaviour>();
        bombBehaviour = GetComponent<NetworkBombBehaviour>();
        playerData = GetComponent<PlayerNetworkData>();

        // Debug.Log($"[NetworkWeaponEquipSystem] Components found - Pistol: {pistolBehaviour != null}, Laser: {laserBehaviour != null}, Bomb: {bombBehaviour != null}, PlayerData: {playerData != null}");

        if (Object.HasStateAuthority)
        {
            // Set default weapon based on team - but wait for team data to sync
            StartCoroutine(SetDefaultWeaponWhenTeamReady());
        }

        UpdateWeaponVisuals();
    }

    private System.Collections.IEnumerator SetDefaultWeaponWhenTeamReady()
    {
        // Wait for team data to sync (TeamId will be -1 initially)
        while (playerData != null && playerData.TeamId == -1)
        {
            // Debug.Log("[NetworkWeaponEquipSystem] Waiting for team data to sync...");
            yield return new WaitForSeconds(0.1f);
        }

        if (playerData != null && playerData.TeamId == 1)
        {
            CurrentWeapon = WeaponType.Laser; // Team B starts with laser
            // Debug.Log("[NetworkWeaponEquipSystem] Team B detected - setting default weapon to LASER");
        }
        else
        {
            CurrentWeapon = WeaponType.Pistol; // Team A starts with pistol
            // Debug.Log("[NetworkWeaponEquipSystem] Team A detected - setting default weapon to PISTOL");
        }

        UpdateWeaponVisuals();
    }

    public void CollectNetworkInput(ref StarterAssets.NetworkInputData inputData)
    {
        inputData.equipPrimary = wantsToEquipPrimary;
        inputData.equipBomb = wantsToEquipBomb;
        
        wantsToEquipPrimary = false;
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
            if (input.equipPrimary)
            {
                EquipPrimaryWeapon();
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
        
        switch (weapon)
        {
            case WeaponType.Pistol:
                // Debug.Log("[NetworkWeaponEquipSystem] *** Player 1 EQUIPPED PISTOL ***");
                break;
            case WeaponType.Laser:
                // Debug.Log("[NetworkWeaponEquipSystem] *** Player 1 EQUIPPED LASER ***");
                break;
            case WeaponType.Bomb:
                // Debug.Log("[NetworkWeaponEquipSystem] *** Player 1 EQUIPPED BOMB ***");
                break;
        }
    }

    private void EquipPrimaryWeapon()
    {
        WeaponType primaryWeapon = playerData != null && playerData.TeamId == 1 
            ? WeaponType.Laser 
            : WeaponType.Pistol;
        
        if (CurrentWeapon != primaryWeapon)
        {
            EquipWeapon(primaryWeapon);
        }
    }

    private void UpdateWeaponVisuals()
    {
        if (pistolModel != null)
        {
            pistolModel.SetActive(CurrentWeapon == WeaponType.Pistol);
        }

        if (laserModel != null)
        {
            laserModel.SetActive(CurrentWeapon == WeaponType.Laser);
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

    public bool IsLaserEquipped()
    {
        return CurrentWeapon == WeaponType.Laser;
    }

    public bool IsBombEquipped()
    {
        return CurrentWeapon == WeaponType.Bomb;
    }

    public bool IsPrimaryWeaponEquipped()
    {
        return CurrentWeapon == WeaponType.Pistol || CurrentWeapon == WeaponType.Laser;
    }

    public void RequestEquipPrimary()
    {
        wantsToEquipPrimary = true;
        // Debug.Log("[NetworkWeaponEquipSystem] RequestEquipPrimary() called");
    }

    public void RequestEquipBomb()
    {
        wantsToEquipBomb = true;
        // Debug.Log("[NetworkWeaponEquipSystem] RequestEquipBomb() called");
    }
}
