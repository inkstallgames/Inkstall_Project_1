using TMPro;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class TeamSelector : NetworkBehaviour
{
    private TMP_Dropdown teamDropdown;
    private NetworkLobbyManager lobbyManager;

    private void Start()
    {
        teamDropdown = GetComponent<TMP_Dropdown>();
        lobbyManager = FindObjectOfType<NetworkLobbyManager>();
        
        // Add listener for when the dropdown value changes
        teamDropdown.onValueChanged.AddListener(OnTeamSelected);
    }

    private void OnTeamSelected(int index)
    {
        // 0 = Blue Team, 1 = Red Team
        int teamId = index;
        if (lobbyManager != null && Runner != null)
        {
            lobbyManager.TryRequestTeamChange(teamId);
        }
    }
}