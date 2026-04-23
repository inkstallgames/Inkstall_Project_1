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

        if (isHost)
        {
            playerStatusText.text = "<color=orange>Host</color>";
            hostIcon.SetActive(true);
        }
        else
        {
            string readyStatus = isReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>";
            playerStatusText.text = readyStatus;
            hostIcon.SetActive(false);
        }
    }
}
