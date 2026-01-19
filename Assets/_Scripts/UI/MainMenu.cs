using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField joinCodeInput;
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;

    private NetworkStarter networkStarter;

    private void Start()
    {
        networkStarter = FindObjectOfType<NetworkStarter>();

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnHostClicked()
    {
        if (networkStarter != null)
        {
            networkStarter.StartHost();
            SwitchToLobby();
        }
    }

    private void OnJoinClicked()
    {
        if (networkStarter != null && !string.IsNullOrWhiteSpace(joinCodeInput.text))
        {
            networkStarter.JoinSession(joinCodeInput.text.ToUpper());
            SwitchToLobby();
        }
    }

    private void SwitchToLobby()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }
}
