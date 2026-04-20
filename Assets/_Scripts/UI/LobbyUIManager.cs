using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.SceneManagement;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Lobby Panels")]
    public GameObject lobbyPanel;
    public GameObject heroSelectionPanel;
    public GameObject inGameUIPanel;
    public GameObject loadingScreenPanel;
    public GameObject waitingForPlayersPanel;

    [Header("Host Settings")]
    public Button mapButton;
    public TMP_Dropdown modeDropdown;
    public TMP_Dropdown timeDropdown;

    [Header("Player List")]
    public GameObject teamAListContent;   // TeamAPanel ScrollView > Viewport > Content
    public GameObject teamBListContent;   // TeamBPanel ScrollView > Viewport > Content
    public GameObject playerListItemPrefab;

    [Header("Join Info")]
    public TextMeshProUGUI joinCodeText;
        public TextMeshProUGUI lobbyStatusText; // Used for 'Creating room...' message
    public TextMeshProUGUI notificationText; // Used for general notifications

    [Header("Player Controls")]
    public Button readyButton;
    public Button startGameButton;
    public Button leaveButton;
    public Button switchTeamButton;
    public TextMeshProUGUI readyButtonText;

    [Header("Chat")]
    public TextMeshProUGUI chatContent;
    public TMP_InputField chatInput;
    public Button sendChatButton;

    private bool isHost = false;
    private bool isWaitingScreenActive = false;
    
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

        if (switchTeamButton != null)
            switchTeamButton.onClick.AddListener(() => NetworkLobbyManager.Instance?.SwitchTeam());
            
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

    private void Update()
    {
        // Manually check if the game is ready and hide the waiting screen
        if (isWaitingScreenActive && NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.IsGameReady)
        {
            ShowWaitingForPlayersScreen(false);
            isWaitingScreenActive = false; // Ensure this only runs once
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
        if (chatContent == null) return; // Chat UI removed — skip update
        
        chatContent.richText = true;
        chatContent.color = Color.white;
        
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
        // Clear both team columns
        if (teamAListContent != null)
            foreach (Transform t in teamAListContent.transform) Destroy(t.gameObject);
        if (teamBListContent != null)
            foreach (Transform t in teamBListContent.transform) Destroy(t.gameObject);

        // Sort: host first, then by player ID
        var sortedPlayers = players
            .OrderByDescending(p => p.Value.IsHost)
            .ThenBy(p => p.Key)
            .Select(p => p.Value);

        foreach (var player in sortedPlayers)
        {
            // Route to the correct team column
            GameObject parent = player.TeamID == 1 ? teamBListContent : teamAListContent;
            if (parent == null) continue;

            GameObject item = Instantiate(playerListItemPrefab, parent.transform);
            PlayerListItemUI listItem = item.GetComponent<PlayerListItemUI>();
            if (listItem != null)
                listItem.SetPlayerInfo(player.PlayerName.ToString(), player.IsReady, player.IsHost, player.PlayerColor, player.TeamID);
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
        Debug.Log($"[LobbyUIManager] ShowHeroSelectionPanel called with show={show}");
        if (heroSelectionPanel != null)
        {
            heroSelectionPanel.SetActive(show);
            Debug.Log($"[LobbyUIManager] Set heroSelectionPanel active to {show}, actual state: {heroSelectionPanel.activeSelf}");
        }
        else
        {
            Debug.LogError("[LobbyUIManager] heroSelectionPanel is NULL!");
        }

        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(!show);
            Debug.Log($"[LobbyUIManager] Set lobbyPanel active to {!show}");
        }
    }

    public void ShowLoadingScreen(bool show = true)
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(show);
        if (show)
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
            if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
            if (waitingForPlayersPanel != null) waitingForPlayersPanel.SetActive(false);
        }
    }

    public void ShowWaitingForPlayersScreen(bool show)
    {
        if (waitingForPlayersPanel != null) waitingForPlayersPanel.SetActive(show);
        isWaitingScreenActive = show;

        if (show)
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
            if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
            if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
        }
    }

    public void ExitToHome(){
        SceneManager.LoadScene(0);
    }
}
