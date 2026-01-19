using UnityEngine;
using TMPro;

public class PlayerListItemUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText;
    public GameObject hostIcon;

    public void SetPlayerInfo(string playerName, bool isReady, bool isHost)
    {
        playerNameText.text = playerName;
        if (isHost)
        {
            playerStatusText.text = "<color=orange>Host</color>";
            hostIcon.SetActive(true);
        }
        else
        {
            playerStatusText.text = isReady ? "<color=green>Ready</color>" : "<color=red>Not Ready</color>";
            hostIcon.SetActive(false);
        }
    }
}
