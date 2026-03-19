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

