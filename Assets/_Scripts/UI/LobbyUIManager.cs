using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

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

    [Header("Player List")]
    public GameObject playerListContent;
    public GameObject playerListItemPrefab;

    [Header("Join Info")]
    public TextMeshProUGUI joinCodeText;

    [Header("Player Controls")]
    public Button readyButton;
    public Button startGameButton;
    public Button leaveButton;
    public TextMeshProUGUI readyButtonText;

    [Header("Chat")]
    public TextMeshProUGUI chatContent;
    public TMP_InputField chatInput;
    public Button sendChatButton;

    private bool isHost = false;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Initialize UI event listeners
        InitializeUI();
    }
    
    private void InitializeUI()
    {
        // Host controls
        if (startGameButton != null)
            startGameButton.onClick.AddListener(() => NetworkLobbyManager.Instance?.StartGame());
            
        if (mapDropdown != null)
            mapDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance?.OnMapSelectionChanged(val));
            
        if (modeDropdown != null)
            modeDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance?.OnModeSelectionChanged(val));
            
        if (timeDropdown != null)
            timeDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance?.OnTimeSelectionChanged(val));

        // Player controls
        if (readyButton != null)
            readyButton.onClick.AddListener(() => NetworkLobbyManager.Instance?.ToggleReadyStatus());
            
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(() => 
            {
                var networkStarter = FindObjectOfType<NetworkStarter>();
                networkStarter?.ShutdownRunner();
            });
        }

        // Chat controls
        if (sendChatButton != null)
            sendChatButton.onClick.AddListener(SendChatMessage);
            
        if (chatInput != null)
            chatInput.onSubmit.AddListener((_) => SendChatMessage());
    }

    private void SendChatMessage()
    {
        string message = chatInput?.text?.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            var chatManager = FindObjectOfType<LobbyChatManager>();
            if (chatManager != null)
            {
                try 
                {
                    // The RPC will handle further truncation if needed
                    chatManager.RPC_SendChatMessage(message);
                    chatInput.text = "";
                    chatInput.ActivateInputField(); // Keep focus on the input field
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to send chat message: {e.Message}");
                }
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


    public void InitializeLobbyUI(List<string> mapOptions, List<string> modeOptions, List<string> timeOptions, bool isHostPlayer)
    {
        // Show the lobby panel immediately
        ShowLobby(true);
        
        // Set loading state
        if (joinCodeText != null) joinCodeText.text = isHostPlayer ? "Creating room..." : "Joining room...";
        
        // Initialize UI state
        isHost = isHostPlayer;
        
        // Show/hide buttons based on host/client role
        if (readyButton != null) 
        {
            readyButton.gameObject.SetActive(!isHost);
            readyButton.interactable = !isHost;
        }
        
        if (startGameButton != null) 
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = false; // Disable until room is ready
        }
        
        // Only host can interact with game settings
        if (mapDropdown != null) 
        {
            mapDropdown.interactable = isHost;
            mapDropdown.ClearOptions();
            mapDropdown.AddOptions(mapOptions);
        }

        if (modeDropdown != null)
        {
            modeDropdown.interactable = isHost;
            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(modeOptions);
        }

        if (timeDropdown != null)
        {
            timeDropdown.interactable = isHost;
            timeDropdown.ClearOptions();
            timeDropdown.AddOptions(timeOptions);
        }
        
        // Host is automatically ready once room is created
        if (isHost && NetworkLobbyManager.Instance != null)
        {
            // We'll call ToggleReadyStatus after room is fully created
            // This will be handled by the NetworkLobbyManager
        }
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
        Debug.Log($"[LobbyUIManager] SetJoinCode called with: {joinCode}");
        Debug.Log($"[LobbyUIManager] joinCodeText reference: {joinCodeText != null}");
        
        if (joinCodeText != null)
        {
            Debug.Log($"[LobbyUIManager] Setting join code text to: {joinCode}");
            joinCodeText.text = $"Join Code: {joinCode}";
            Debug.Log($"[LobbyUIManager] Text component value set. Current text: {joinCodeText.text}");
            
            // Force update the canvas to ensure the text is rendered
            Canvas.ForceUpdateCanvases();
            Debug.Log("[LobbyUIManager] Canvas update forced");
        }
        else
        {
            Debug.LogError("[LobbyUIManager] joinCodeText is null! Make sure to assign it in the inspector.");
            Debug.LogError($"[LobbyUIManager] GameObject active: {gameObject.activeInHierarchy}");
            
            // Try to find the text component if not assigned
            var foundText = GetComponentInChildren<TextMeshProUGUI>(true);
            Debug.Log($"[LobbyUIManager] Found TextMeshProUGUI in children: {foundText != null}");
        }
    }

    public void SetStartButtonState(bool interactable)
    {
        if (startGameButton != null)
        {
            startGameButton.interactable = interactable;
        }
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
