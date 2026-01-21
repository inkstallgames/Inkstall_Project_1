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

        // Ensure the content has a VerticalLayoutGroup
        var layoutGroup = playerListContent.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = playerListContent.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = 10f; // Add some space between items
            layoutGroup.padding = new RectOffset(5, 5, 5, 5); // Add some padding
        }

        // Add ContentSizeFitter to handle dynamic resizing
        var contentFitter = playerListContent.GetComponent<ContentSizeFitter>();
        if (contentFitter == null)
        {
            contentFitter = playerListContent.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // Ensure the scroll view's viewport is set up correctly
        var scrollRect = playerListContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.viewport == null)
        {
            // If no viewport is set, try to find it in the hierarchy
            var viewport = scrollRect.transform.Find("Viewport");
            if (viewport != null)
            {
                scrollRect.viewport = viewport.GetComponent<RectTransform>();
            }
        }

        // Populate with new player data
        foreach (var player in players.Values)
        {
            if (playerListItemPrefab == null) continue;
            
            GameObject item = Instantiate(playerListItemPrefab, playerListContent.transform);
            
            // Ensure the item has a LayoutElement for better control
            var layoutElement = item.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = item.AddComponent<LayoutElement>();
                layoutElement.minHeight = 40f; // Set a minimum height for each item
            }
            
            // Set the player info
            PlayerListItemUI listItem = item.GetComponent<PlayerListItemUI>();
            if (listItem != null)
            {
                listItem.SetPlayerInfo(player.PlayerName.ToString(), player.IsReady, player.IsHost);
            }
        }
        
        // Force update the layout
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerListContent.GetComponent<RectTransform>());
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
