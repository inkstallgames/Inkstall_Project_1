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
                    int randomPower = Random.Range(0, 2);
                    if (randomPower == 0)
                    {
                        var pistol = playerObj.GetComponent<NetworkPistolBehaviour>();
                        if (pistol != null)
                        {
                            pistol.HasAutoFirePowerup = true;
                            playerData.RPC_NotifyPowerupReceived("Pistol Auto Fire");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Hero) was given the power-up: Pistol Auto Fire");
                        }
                    }
                    else
                    {
                        var bomb = playerObj.GetComponent<NetworkBombBehaviour>();
                        if (bomb != null)
                        {
                            bomb.GrantGrenadeAmmoPowerup();
                            playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Hero) was given the power-up: Grenade Ammo");
                        }
                    }
                }
                else // Alien
                {
                    int randomPower = Random.Range(0, 2);
                    if (randomPower == 0)
                    {
                        var laser = playerObj.GetComponent<NetworkLaserBehaviour>();
                        if (laser != null)
                        {
                            laser.HasDamageIncreasePowerup = true;
                            playerData.RPC_NotifyPowerupReceived("Laser Damage Increase");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Laser Damage Increase");
                        }
                    }
                    else
                    {
                        var bomb = playerObj.GetComponent<NetworkBombBehaviour>();
                        if (bomb != null)
                        {
                            bomb.GrantGrenadeAmmoPowerup();
                            playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                            // DISABLED - Performance killer: Debug.Log($"[MysteryBox] Mystery box was hit! Player {sourcePlayer.PlayerId} (Alien) was given the power-up: Grenade Ammo");
                        }
                    }
                }
            }
        }

        // Spawn some destruction effect here if you want

        Runner.Despawn(Object);
    }
}
