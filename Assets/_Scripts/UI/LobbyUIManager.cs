using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Lobby Panels")]
    public GameObject lobbyPanel;
    public GameObject inGameUIPanel;

    [Header("Host Settings")]
    public TMP_Dropdown mapDropdown;
    public TMP_Dropdown modeDropdown;
    public TMP_Dropdown timeDropdown;
    public Button startGameButton;

    [Header("Player List")]
    public GameObject playerListContent;
    public GameObject playerListItemPrefab;

    [Header("Join Info")]
    public TextMeshProUGUI joinCodeText;

    [Header("Player Controls")]
    public Button readyButton;
    public Button leaveButton;
    public TextMeshProUGUI readyButtonText;

    [Header("Chat")]
    public TextMeshProUGUI chatContent;
    public TMP_InputField chatInput;
    public Button sendChatButton;

    private void Start()
    {
        // Host controls
        startGameButton.onClick.AddListener(() => NetworkLobbyManager.Instance.StartGame());
        mapDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance.OnMapSelectionChanged(val));
        modeDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance.OnModeSelectionChanged(val));
        timeDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance.OnTimeSelectionChanged(val));

        // Player controls
        readyButton.onClick.AddListener(() => NetworkLobbyManager.Instance.ToggleReadyStatus());
        leaveButton.onClick.AddListener(() => 
        {
            var networkStarter = FindObjectOfType<NetworkStarter>();
            if (networkStarter != null)
            {
                networkStarter.ShutdownRunner();
            }
        });

        // Chat controls
        sendChatButton.onClick.AddListener(SendChatMessage);
        chatInput.onSubmit.AddListener((_) => SendChatMessage());
    }

    private void SendChatMessage()
    {
        if (!string.IsNullOrWhiteSpace(chatInput.text))
        {
            var chatManager = FindObjectOfType<LobbyChatManager>();
            if (chatManager != null)
            {
                chatManager.RPC_SendChatMessage(chatInput.text);
                chatInput.text = "";
            }
        }
    }

    public void UpdateChat(NetworkLinkedList<ChatMessage> messages)
    {
        string chatLog = "";
        foreach (var msg in messages)
        {
            chatLog += $"<b>{msg.PlayerName}:</b> {msg.Message}\n";
        }
        chatContent.text = chatLog;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeLobbyUI(List<string> mapOptions, List<string> modeOptions, List<string> timeOptions)
    {
        mapDropdown.ClearOptions();
        mapDropdown.AddOptions(mapOptions);

        modeDropdown.ClearOptions();
        modeDropdown.AddOptions(modeOptions);

        timeDropdown.ClearOptions();
        timeDropdown.AddOptions(timeOptions);
    }

    public void UpdatePlayerList(Dictionary<int, PlayerLobbyData> players)
    {
        // Clear existing player list
        foreach (Transform child in playerListContent.transform)
        {
            Destroy(child.gameObject);
        }

        // Populate with new player data
        foreach (var player in players.Values)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContent.transform);
            // Assuming the prefab has a script to set player info
            PlayerListItemUI listItem = item.GetComponent<PlayerListItemUI>();
            if (listItem != null)
            {
                listItem.SetPlayerInfo(player.PlayerName.ToString(), player.IsReady, player.IsHost);
            }
        }
    }

    public void SetJoinCode(string joinCode)
    {
        joinCodeText.text = $"Join Code: {joinCode}";
    }

    public void SetStartButtonState(bool interactable)
    {
        startGameButton.interactable = interactable;
    }

    public void SetReadyButtonState(bool isReady)
    {
        readyButtonText.text = isReady ? "Ready" : "Not Ready";
    }

    public void ShowLobby(bool show)
    {
        lobbyPanel.SetActive(show);
        inGameUIPanel.SetActive(!show);
    }
}
