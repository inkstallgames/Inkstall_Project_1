using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;
using System.Linq;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Lobby Panels")]
    public GameObject lobbyPanel;
    public GameObject heroSelectionPanel;
    public GameObject inGameUIPanel;
    public GameObject loadingScreenPanel;

    [Header("Host Settings")]
    public Button mapButton;
    public TMP_Dropdown modeDropdown;
    public TMP_Dropdown timeDropdown;

    [Header("Player List")]
    public GameObject playerListContent;
    public GameObject playerListItemPrefab;

    [Header("Join Info")]
    public TextMeshProUGUI joinCodeText;
        public TextMeshProUGUI lobbyStatusText; // Used for 'Creating room...' message
    public TextMeshProUGUI notificationText; // Used for general notifications

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
            
        if (mapButton != null)
            mapButton.onClick.AddListener(() => NetworkLobbyManager.Instance?.OnMapSelectionChanged(0));
            
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
        
        // Enable rich text for chat
        if (chatContent != null)
        {
            chatContent.richText = true;
            chatContent.color = Color.white;
        }
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
        if (chatContent != null)
        {
            chatContent.richText = true;
            chatContent.color = Color.white;
        }
        
        string chatLog = "";
        foreach (var msg in messages)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(msg.PlayerColor);
            chatLog += $"<b><color=#{colorHex}>{msg.PlayerName}:</color></b> {msg.Message}\n";
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
        
        // Show ready button only for clients, show start button for host
        if (isHostPlayer)
        {
            // Host always shows start button and is always ready
            if (readyButton != null) readyButton.gameObject.SetActive(false);
            if (startGameButton != null) startGameButton.gameObject.SetActive(true);
            
        }
        else
        {
            // Clients see the ready button
            if (readyButton != null) 
            {
                readyButton.gameObject.SetActive(true);
                readyButton.interactable = true;
                SetReadyButtonState(false); // Start as not ready for clients
            }
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
        }
        
        if (startGameButton != null) 
        {
            startGameButton.gameObject.SetActive(isHost);
        }
        
        // Only host can interact with game settings
        if (mapButton != null) 
        {
            mapButton.interactable = isHost;
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
        
        // Host starts as not ready and must click ready like everyone else
        // The NetworkLobbyManager will handle the initial state
    }

    public void UpdatePlayerList(Dictionary<int, PlayerLobbyData> players)
    {
        // Clear existing player list
        foreach (Transform child in playerListContent.transform)
        {
            Destroy(child.gameObject);
        }

        // Convert dictionary to list and sort: host first, then others by join order
        var sortedPlayers = players
            .OrderByDescending(p => p.Value.IsHost)  // Host comes first
            .ThenBy(p => p.Key)                     // Then order by player ID (join order)
            .Select(p => p.Value);

        // Populate with sorted player data
        foreach (var player in sortedPlayers)
        {
            GameObject item = Instantiate(playerListItemPrefab, playerListContent.transform);
            PlayerListItemUI listItem = item.GetComponent<PlayerListItemUI>();
            if (listItem != null)
            {
                listItem.SetPlayerInfo(player.PlayerName.ToString(), player.IsReady, player.IsHost, player.PlayerColor);
            }
        }
    }

    public void SetJoinCode(string joinCode)
    {
        if (joinCodeText != null)
        {
            joinCodeText.text = $"Join Code: {joinCode}";
        }

        // Once the join code is set, the room is ready, so we can clear the status text.
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = "";
        }
    }

    public void SetLobbyStatusText(string status)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = status;
        }

        // Clear the join code text while a status is being shown.
        if (joinCodeText != null)
        {
            joinCodeText.text = "";
        }
    }

        public void SetReadyButtonState(bool isReady)
    {
        readyButtonText.text = isReady ? "Ready" : "Not Ready";
    }

    public void ShowMessage(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.gameObject.SetActive(true);
            StartCoroutine(HideMessageAfterDelay(3f));
        }
    }

    private System.Collections.IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }
    }

    public void ShowLobby(bool show)
    {
        if(lobbyPanel != null) lobbyPanel.SetActive(show);
        if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
        if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }

    public void ShowHeroSelectionPanel(bool show)
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(!show);
        if (heroSelectionPanel != null) heroSelectionPanel.SetActive(show);
        if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }

    public void ShowLoadingScreen()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
        if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
    }
}
