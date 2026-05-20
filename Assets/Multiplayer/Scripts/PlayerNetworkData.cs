using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class PlayerNetworkData : NetworkBehaviour

{

    [Networked] public int Health { get; set; } = 100;

    // --- Health Regen ---
    [Header("Health Regen")]
    [Tooltip("Seconds after last damage before health regen begins")]
    [SerializeField] private float regenDelay = 10f;
    
    [Tooltip("Health points regenerated per second")]
    [SerializeField] private float regenRate = 5f;
    
    private const int MaxHealth = 100;
    
    /// <summary>The tick at which the player last took damage. Used to calculate regen delay.</summary>
    [Networked] private int LastDamageTick { get; set; }
    
    /// <summary>Accumulated fractional regen (since Health is int, we accumulate sub-integer amounts).</summary>
    [Networked] private float RegenAccumulator { get; set; }

    [Networked] public int TeamId { get; set; } = -1; // -1 means no team
    [Networked] public string LastDamageWeapon { get; set; } = ""; // Tracks which weapon last hit this player

    [Networked] public string PlayerName { get; set; }

    [Networked] public int Kills { get; set; }

    [Networked] public int Deaths { get; set; }

    [Networked] public bool IsReady { get; set; }



    [Header("References")]

    public TextMeshProUGUI nameTag;

    public Slider healthBar;

    public GameObject[] teamIndicators;
    
    [Header("Hit Sound")]
    [Tooltip("Sound to play when this player gets hit by a bullet")]
    public AudioClip bulletBodyHitSound;
    
    [Tooltip("Sound to play when this player gets hit by a laser")]
    public AudioClip laserBodyHitSound;
    
    [SerializeField] private float hitSoundVolume = 1.0f;



    private string _lastPlayerName;

    private int _lastTeamId;



    public override void Spawned()

    {

        if (Object.HasStateAuthority)

        {

            // Initial random name as fallback

            if (string.IsNullOrEmpty(PlayerName))

            {

                PlayerName = $"Player_{Random.Range(1000, 9999)}";

            }

            

            // Register with the game manager

            NetworkGameManager.Instance?.RegisterPlayer(Object.InputAuthority, this);

        }



        // If we are the owner (InputAuthority), send our preferred name

        if (Object.HasInputAuthority)

        {

            string savedName = PlayerPrefs.GetString("PlayerName", "");

            if (!string.IsNullOrEmpty(savedName))

            {

                RPC_SetPlayerName(savedName);

            }

        }



        UpdateVisuals();

    }



    public void UpdateVisuals()

    {

        // Update name tag
        if (nameTag != null)
        {
            nameTag.text = PlayerName;
            bool isFFA = NetworkGameManager.Instance != null && NetworkGameManager.Instance.CurrentGameMode == GameMode.FreeForAll;
            nameTag.color = isFFA ? Color.white : (TeamId == 0 ? Color.blue : (TeamId == 1 ? Color.red : Color.white));
        }

        // Update health bar
        if (healthBar != null)
        {
            healthBar.value = Health / 100f;
        }

        // Update team indicators
        if (teamIndicators != null && teamIndicators.Length > 0)
        {
            foreach (var indicator in teamIndicators)
            {
                indicator.SetActive(false);
            }

            bool isFFA = NetworkGameManager.Instance != null && NetworkGameManager.Instance.CurrentGameMode == GameMode.FreeForAll;
            if (!isFFA && TeamId >= 0 && TeamId < teamIndicators.Length)
            {
                teamIndicators[TeamId].SetActive(true);
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(int damage, PlayerRef sourcePlayer, bool isLaserDamage = false, string weaponName = "Unknown")
    {
        if (Health <= 0) return; // Already dead

        LastDamageWeapon = weaponName;

        // --- Friendly fire protection ---
        // Ignore damage from players on the same team (only if not in Free For All mode)
        bool isFFA = NetworkGameManager.Instance != null && NetworkGameManager.Instance.CurrentGameMode == GameMode.FreeForAll;
        if (!isFFA && sourcePlayer != default && sourcePlayer != Object.InputAuthority)
        {
            var sourceObject = Runner.GetPlayerObject(sourcePlayer);
            if (sourceObject != null)
            {
                var sourceData = sourceObject.GetComponent<PlayerNetworkData>();
                if (sourceData != null && sourceData.TeamId >= 0 && sourceData.TeamId == TeamId)
                {
                    return; // Same team — no friendly fire
                }
            }
        }

        // Shield ability — Hero players are immune to damage while shielded
        var abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController != null && abilityController.IsShielded) return;

        Health = Mathf.Max(0, Health - damage);
        
        // Reset regen timer — any damage resets the countdown
        LastDamageTick = Runner.Tick;
        RegenAccumulator = 0f;
        
        // Play hit sound on all clients (each player will hear it from their perspective)
        RPC_PlayHitSound(isLaserDamage);
        
        // Update visuals on all clients first, so the UI hits 0
        RPC_UpdateHealth(Health);
        
        if (Health <= 0)
        {
            // Debug.Log($"[PlayerNetworkData] *** HEALTH DEPLETED *** Player {PlayerName} (ID:{Object.InputAuthority}) was eliminated by Player {sourcePlayer}!");
            // Player died
            Deaths++;
            
            // Award kill to the source player if it's not a suicide
            if (sourcePlayer != Object.InputAuthority && sourcePlayer != default)
            {
                var sourcePlayerData = Runner.GetPlayerObject(sourcePlayer)?.GetComponent<PlayerNetworkData>();
                if (sourcePlayerData != null)
                {
                    sourcePlayerData.Kills++;
                    // Recharge killer's ability on kill
                    sourcePlayerData.GetComponent<PlayerAbilityController>()?.GrantAbilityCharge();
                }
            }

            // Notify game manager to update team scores and kill feed
            NetworkGameManager.Instance?.OnPlayerKilled(Object, sourcePlayer, LastDamageWeapon);

            if (Object.HasStateAuthority)
            {
                PlayerRef playerRef = Object.InputAuthority;
                int teamId = TeamId;
                string playerName = PlayerName;

                // Let the Game Manager handle a delayed despawn and the respawn UI notification
                if (NetworkGameManager.Instance != null)
                {
                    NetworkGameManager.Instance.ScheduleDeathSequence(Object, playerRef, teamId, playerName, 7f);
                }
            }

            return; // Exit
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateHealth(int newHealth)
    {
        Health = newHealth;
        UpdateVisuals();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayHitSound(bool isLaserDamage = false)
    {
        // Only play hit sound for the local player (the one who got hit)
        if (Object.HasInputAuthority)
        {
            // Select the appropriate hit sound based on weapon type
            AudioClip hitSound = isLaserDamage ? laserBodyHitSound : bulletBodyHitSound;
            
            if (hitSound != null)
            {
                if (NetworkAudioManager.Instance != null)
                {
                    // Industry-standard: Hit sounds are 2D for the player being hit
                    NetworkAudioManager.Instance.PlaySound(hitSound, transform.position, hitSoundVolume, true);
                }
                else
                {
                    // Fallback: 2D centered sound
                    AudioSource.PlayClipAtPoint(hitSound, Camera.main.transform.position, hitSoundVolume);
                }
                
                Debug.Log($"[PlayerNetworkData] *** {(isLaserDamage ? "LASER" : "BULLET")} HIT SOUND PLAYED *** for local player {PlayerName}");
            }
        }
    }









    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_NotifyPowerupReceived(string powerupName)
    {
        if (NetworkUIManager.Instance != null)
        {
            NetworkUIManager.Instance.ShowPowerupNotification(powerupName);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]

    public void RPC_SetPlayerName(string name)

    {

        PlayerName = name;

    }



    // --- Health Regen Logic (runs on State Authority only) ---
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        
        // OPTIMIZATION: Early exit to reduce CPU usage
        if (Health <= 0 || Health >= MaxHealth) return;
        if (LastDamageTick <= 0) return; // Never been hit yet

        // Calculate seconds elapsed since last damage
        float elapsedSinceLastDamage = (Runner.Tick - LastDamageTick) * Runner.DeltaTime;

        // OPTIMIZATION: Exit early if still in cooldown (reduces network traffic)
        if (elapsedSinceLastDamage < regenDelay) return;

        // Accumulate regen
        RegenAccumulator += regenRate * Runner.DeltaTime;

        // OPTIMIZATION: Only send RPC when health actually changes
        if (RegenAccumulator >= 1f)
        {
            int regenAmount = Mathf.FloorToInt(RegenAccumulator);
            RegenAccumulator -= regenAmount;

            int oldHealth = Health;
            Health = Mathf.Min(MaxHealth, Health + regenAmount);

            // Only sync if health actually changed
            if (Health != oldHealth)
            {
                RPC_UpdateHealth(Health);
            }
        }
    }

    public override void Render()
    {
        // OPTIMIZATION: Only update visuals when values actually change
        if (_lastPlayerName != PlayerName || _lastTeamId != TeamId)
        {
            _lastPlayerName = PlayerName;
            _lastTeamId = TeamId;
            UpdateVisuals();
        }
    }

}

