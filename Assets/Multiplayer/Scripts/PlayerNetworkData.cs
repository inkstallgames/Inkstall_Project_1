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
    public void RPC_TakeDamage(int damage, PlayerRef sourcePlayer)
    {
        if (Health <= 0) return; // Already dead

        Health = Mathf.Max(0, Health - damage);
        // Debug.Log($"[PlayerNetworkData] Player {PlayerName} (ID:{Object.InputAuthority}) took {damage} damage. Health: {Health}/100. Source: {sourcePlayer}");
        
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

                }

            }

            

            RPC_OnDeath();

        }

        

        // Update visuals on all clients

        RPC_UpdateHealth(Health);

    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]

    private void RPC_UpdateHealth(int newHealth)

    {

        Health = newHealth;

        UpdateVisuals();

    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]

    private void RPC_OnDeath()

    {

        // Handle death effects, animations, etc.

        // Debug.Log($"{PlayerName} died!");

        

        // Show respawn UI for the local player who died
        if (Object.HasInputAuthority && NetworkUIManager.Instance != null)
        {
            NetworkUIManager.Instance.ShowRespawnScreen();
        }

        if (Object.HasStateAuthority)

        {

            // Handle respawn after 7-second delay

            StartCoroutine(RespawnAfterDelay(7f));

        }

    }



    private System.Collections.IEnumerator RespawnAfterDelay(float delay)

    {

        // Store player info before destroying

        PlayerRef playerRef = Object.InputAuthority;

        int teamId = TeamId;

        string playerName = PlayerName;

        

        // Wait for respawn delay

        yield return new WaitForSeconds(delay);

        

        // Destroy the current player object

        if (Runner != null && Object != null)

        {

            // Debug.Log($"[PlayerNetworkData] Despawning player {playerName} for respawn");

            Runner.Despawn(Object);

            

            // Request respawn from NetworkGameManager

            if (NetworkGameManager.Instance != null)

            {

                // Debug.Log($"[PlayerNetworkData] Requesting respawn for player {playerRef.PlayerId}");

                NetworkGameManager.Instance.RespawnPlayer(playerRef, teamId, playerName);

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

