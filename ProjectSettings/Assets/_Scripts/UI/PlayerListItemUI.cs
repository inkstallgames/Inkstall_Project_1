using UnityEngine;
using TMPro;

public class PlayerListItemUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText;
    public GameObject hostIcon;

    public void SetPlayerInfo(string playerName, bool isReady, bool isHost, Color playerColor, int teamId = -1)
    {
        playerNameText.text = playerName;
        playerNameText.color = playerColor;

        // Build team label
        string teamLabel = teamId == 0
            ? " <color=#4FC3F7>[Team A]</color>"
            : teamId == 1
                ? " <color=#EF9A9A>[Team B]</color>"
                : "";

        if (isHost)
        {
            playerStatusText.text = $"<color=orange>Host</color>{teamLabel}";
            hostIcon.SetActive(true);
        }
        else
        {
            string readyStatus = isReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>";
            playerStatusText.text = $"{readyStatus}{teamLabel}";
            hostIcon.SetActive(false);
        }
    }
}
