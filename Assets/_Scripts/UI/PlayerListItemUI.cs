using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PlayerListItemUI : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerStatusText;
    public GameObject hostIcon;
    
    [Header("Kick Button")]
    public Button kickButton;

    /// <summary>
    /// The player ID this row represents. Set via SetupKickButton.
    /// </summary>
    private int _playerId;

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

    /// <summary>
    /// Configures the kick button for this player row.
    /// Call this after SetPlayerInfo. The button is only shown to the host for non-host players.
    /// </summary>
    /// <param name="playerId">The PlayerRef.PlayerId of this row's player.</param>
    /// <param name="showKick">True if the local user is the host AND this row is NOT the host.</param>
    /// <param name="onKick">Callback invoked with the playerId when the kick button is pressed.</param>
    public void SetupKickButton(int playerId, bool showKick, Action<int> onKick)
    {
        _playerId = playerId;

        if (kickButton == null) return;

        kickButton.gameObject.SetActive(showKick);

        if (showKick)
        {
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(() => onKick?.Invoke(_playerId));
        }
    }
}
