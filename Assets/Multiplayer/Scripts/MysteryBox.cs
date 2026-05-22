using Fusion;
using UnityEngine;

public class MysteryBox : NetworkBehaviour
{
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_OnShot(PlayerRef sourcePlayer)
    {
        var playerObj = Runner.GetPlayerObject(sourcePlayer);
        if (playerObj != null)
        {
            var playerData = playerObj.GetComponent<PlayerNetworkData>();
            if (playerData != null)
            {
                int teamId = playerData.TeamId;
                
                if (teamId == 0) // Hero
                {
                    var pistol = playerObj.GetComponent<NetworkPistolBehaviour>();
                    var bomb = playerObj.GetComponent<NetworkBombBehaviour>();

                    bool hasAutoFire = pistol != null && pistol.HasAutoFirePowerup;
                    bool hasGrenadeAmmo = bomb != null && bomb.HasGrenadeAmmoPowerup;

                    // If player already has both powerups, give nothing extra
                    if (hasAutoFire && hasGrenadeAmmo)
                    {
                        playerData.RPC_NotifyPowerupReceived("No New Power-Up");
                        Debug.Log($"[MysteryBox] Player {sourcePlayer.PlayerId} (Hero) already has all power-ups.");
                    }
                    else
                    {
                        int randomPower = Random.Range(0, 2);

                        // If the randomly chosen powerup is already owned, give the other one
                        if (randomPower == 0 && hasAutoFire) randomPower = 1;
                        else if (randomPower == 1 && hasGrenadeAmmo) randomPower = 0;

                        if (randomPower == 0)
                        {
                            if (pistol != null)
                            {
                                pistol.HasAutoFirePowerup = true;
                                playerData.RPC_NotifyPowerupReceived("Pistol Auto Fire");
                                // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Hero) was given the power-up: Pistol Auto Fire");
                            }
                        }
                        else
                        {
                            if (bomb != null)
                            {
                                bomb.GrantGrenadeAmmoPowerup();
                                playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                                // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Hero) was given the power-up: Grenade Ammo");
                            }
                        }
                    }
                }
                else // Alien
                {
                    var laser = playerObj.GetComponent<NetworkLaserBehaviour>();
                    var bomb = playerObj.GetComponent<NetworkBombBehaviour>();

                    bool hasDamageIncrease = laser != null && laser.HasDamageIncreasePowerup;
                    bool hasGrenadeAmmo = bomb != null && bomb.HasGrenadeAmmoPowerup;

                    // If player already has both powerups, give nothing extra
                    if (hasDamageIncrease && hasGrenadeAmmo)
                    {
                        var laser = playerObj.GetComponent<NetworkLaserBehaviour>();
                        if (laser != null)
                        {
                            laser.HasDamageIncreasePowerup = true;
                            playerData.RPC_NotifyPowerupReceived("Laser Damage Increase");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Laser Damage Increase");
                        }
                        playerData.RPC_NotifyPowerupReceived("No New Power-Up");
                        Debug.Log($"[MysteryBox] Player {sourcePlayer.PlayerId} (Alien) already has all power-ups.");
                    }
                    else
                    {
                        int randomPower = Random.Range(0, 2);

                        // If the randomly chosen powerup is already owned, give the other one
                        if (randomPower == 0 && hasDamageIncrease) randomPower = 1;
                        else if (randomPower == 1 && hasGrenadeAmmo) randomPower = 0;

                        if (randomPower == 0)
                        {
                            bomb.GrantGrenadeAmmoPowerup();
                            playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Grenade Ammo");
                            if (laser != null)
                            {
                                laser.HasDamageIncreasePowerup = true;
                                playerData.RPC_NotifyPowerupReceived("Laser Damage Increase");
                                Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Laser Damage Increase");
                            }
                        }
                        else
                        {
                            if (bomb != null)
                            {
                                bomb.GrantGrenadeAmmoPowerup();
                                playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                                Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Grenade Ammo");
                            }
                        }
                    }
                }
            }
        }

        // Spawn some destruction effect here if you want

        Runner.Despawn(Object);
    }
}
