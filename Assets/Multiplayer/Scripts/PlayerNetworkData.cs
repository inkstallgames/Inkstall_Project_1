using Fusion;

using UnityEngine;

using UnityEngine.UI;



public class PlayerNetworkData : NetworkBehaviour

{

    [Networked] public int Health { get; set; } = 100;

    [Networked] public int TeamId { get; set; } = -1; // -1 means no team

    [Networked] public string PlayerName { get; set; }

    [Networked] public int Kills { get; set; }

    [Networked] public int Deaths { get; set; }

    [Networked] public bool IsReady { get; set; }



    [Header("References")]

    public TextMesh nameTag;

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

            nameTag.color = TeamId == 0 ? Color.blue : (TeamId == 1 ? Color.red : Color.white);

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



            if (TeamId >= 0 && TeamId < teamIndicators.Length)

            {

                teamIndicators[TeamId].SetActive(true);

            }

        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(int damage, PlayerRef sourcePlayer, bool isLaserDamage = false)
    {
        if (Health <= 0) return; // Already dead

        // Shield ability — Hero players are immune to damage while shielded
        var abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController != null && abilityController.IsShielded) return;

        Health = Mathf.Max(0, Health - damage);
        
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

            // Notify game manager to update team scores
            NetworkGameManager.Instance?.OnPlayerKilled(Object.InputAuthority, sourcePlayer);

            if (Object.HasStateAuthority)
            {
                PlayerRef playerRef = Object.InputAuthority;
                int teamId = TeamId;
                string playerName = PlayerName;

                // Let the Game Manager handle a delayed despawn and the respawn UI notification
                if (NetworkGameManager.Instance != null)
                {
                    NetworkGameManager.Instance.ScheduleDeathSequence(playerRef, teamId, playerName, 7f);
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
                // Play 2D sound for local player so it's clear and centered
                GameObject tempAudioObject = new GameObject(isLaserDamage ? "TempLaserHitSound" : "TempBulletHitSound");
                AudioSource tempAudioSource = tempAudioObject.AddComponent<AudioSource>();
                
                // Configure for 2D sound (equal in both ears)
                tempAudioSource.clip = hitSound;
                tempAudioSource.volume = hitSoundVolume;
                tempAudioSource.spatialBlend = 0f; // 0 = 2D sound, 1 = 3D sound
                tempAudioSource.playOnAwake = false;
                
                // Play the sound
                tempAudioSource.Play();
                
                // Destroy the temporary object after sound finishes
                Destroy(tempAudioObject, hitSound.length + 0.1f);
                
                Debug.Log($"[PlayerNetworkData] *** {(isLaserDamage ? "LASER" : "BULLET")} HIT SOUND PLAYED *** for local player {PlayerName}");
            }
        }
    }









    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]

    public void RPC_SetPlayerName(string name)

    {

        PlayerName = name;

    }



    public override void Render()

    {

        if (_lastPlayerName != PlayerName || _lastTeamId != TeamId)

        {

            _lastPlayerName = PlayerName;

            _lastTeamId = TeamId;

            UpdateVisuals();

        }

    }

}

