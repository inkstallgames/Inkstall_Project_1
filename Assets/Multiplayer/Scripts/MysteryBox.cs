using Fusion;
using UnityEngine;

public class MysteryBox : NetworkBehaviour
{
    [Header("Proximity Pickup")]
    [SerializeField, Min(0.25f)]
    [Tooltip("A player entering this radius claims the box without shooting it.")]
    private float pickupRadius = 1.2f;

    [Networked] private NetworkBool IsClaimed { get; set; }

    private bool _localPickupRequested;

    public override void Spawned()
    {
        // The prefab keeps its solid collider for bullets/lasers. This separate
        // trigger detects a player walking over or close to the box.
        SphereCollider pickupTrigger = gameObject.AddComponent<SphereCollider>();
        pickupTrigger.isTrigger = true;
        pickupTrigger.radius = pickupRadius;

        // Trigger callbacks require a physics body on one of the participants.
        Rigidbody body = gameObject.GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryClaimFromPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Also handles a box spawning while a player is already inside its radius.
        TryClaimFromPlayer(other);
    }

    private void TryClaimFromPlayer(Collider other)
    {
        if (_localPickupRequested || Runner == null || !Runner.IsRunning)
            return;

        NetworkObject playerObject = other.GetComponentInParent<NetworkObject>();
        if (playerObject == null || playerObject == Object)
            return;

        // Every peer simulates the trigger. Only the owner of the player who
        // entered may request the pickup, preventing duplicate RPCs from proxies.
        if (playerObject.InputAuthority != Runner.LocalPlayer)
            return;

        if (playerObject.GetComponent<PlayerNetworkData>() == null)
            return;

        _localPickupRequested = true;
        RPC_OnShot(playerObject.InputAuthority);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_OnShot(PlayerRef sourcePlayer)
    {
        // A shot and a proximity pickup can arrive in the same tick. Award once.
        if (IsClaimed)
            return;

        var playerObj = Runner.GetPlayerObject(sourcePlayer);
        if (playerObj == null)
            return;

        var playerData = playerObj.GetComponent<PlayerNetworkData>();
        if (playerData == null)
            return;

        IsClaimed = true;

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
            }
            else
            {
                int randomPower = Random.Range(0, 2);

                // If the randomly chosen powerup is unavailable/already owned, give the other one
                if (randomPower == 0 && (pistol == null || hasAutoFire)) randomPower = 1;
                else if (randomPower == 1 && (bomb == null || hasGrenadeAmmo)) randomPower = 0;

                if (randomPower == 0 && pistol != null)
                {
                    pistol.HasAutoFirePowerup = true;
                    playerData.RPC_NotifyPowerupReceived("Pistol Auto Fire");
                }
                else if (bomb != null)
                {
                    bomb.GrantGrenadeAmmoPowerup();
                    playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                }
            }
        }
        else // Alien
        {
            var laser = playerObj.GetComponent<NetworkLaserBehaviour>();
            var bomb = playerObj.GetComponent<NetworkBombBehaviour>();

            bool hasDamageIncrease = laser != null && laser.HasDamageIncreasePowerup;
            bool hasGrenadeAmmo = bomb != null && bomb.HasGrenadeAmmoPowerup;

            if (hasDamageIncrease && hasGrenadeAmmo)
            {
                playerData.RPC_NotifyPowerupReceived("No New Power-Up");
            }
            else
            {
                int randomPower = Random.Range(0, 2);

                if (randomPower == 0 && (laser == null || hasDamageIncrease)) randomPower = 1;
                else if (randomPower == 1 && (bomb == null || hasGrenadeAmmo)) randomPower = 0;

                if (randomPower == 0 && laser != null)
                {
                    laser.HasDamageIncreasePowerup = true;
                    playerData.RPC_NotifyPowerupReceived("Laser Damage Increase");
                }
                else if (bomb != null)
                {
                    bomb.GrantGrenadeAmmoPowerup();
                    playerData.RPC_NotifyPowerupReceived("Grenade Ammo");
                }
            }
        }

        // Spawn some destruction effect here if you want
        Runner.Despawn(Object);
    }
}
