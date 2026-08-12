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
    [Tooltip("Button that copies the join code to the clipboard.")]
    public Button copyCodeButton;

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

    [Header("NormalUI")]
    public Button ExitBtn;
    public Button HUDEditButton;

    private bool isHost = false;
    private bool isWaitingScreenActive = false;
    private bool _isLoadingIntoMatch = false;  // true from "Start Game" until the map scene replaces this one

    /// <summary>True while the map is loading, so lobby UI refreshes can be skipped.</summary>
    public bool IsLoadingIntoMatch => _isLoadingIntoMatch;

    private GameObject _autoLoadingScreen;  // built on demand when loadingScreenPanel is unassigned
    private TextMeshProUGUI _loadingLabel;
    private Coroutine _loadingDotsCoroutine;
    private string _rawJoinCode = "";          // Stores the bare join code (no prefix)
    private Coroutine _copyFeedbackCoroutine;  // Tracks the running feedback timer
    
    [Header("Team Headers")]
    [SerializeField] private TextMeshProUGUI teamAHeaderText;
    [SerializeField] private TextMeshProUGUI teamBHeaderText;
    
    [Header("Team Panels")]
    [SerializeField] private Image teamAPanelImage;  // Background image for Team A panel (blue by default)
    [SerializeField] private Image teamBPanelImage;  // Background image for Team B panel (red by default)
    
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
            startGameButton.onClick.AddListener(OnStartGameClicked);
            
        if (mapButton != null)
            mapButton.onClick.AddListener(() => NetworkLobbyManager.Instance?.OnMapSelectionChanged(0));
            
        if (modeDropdown != null)
            modeDropdown.onValueChanged.AddListener((val) =>
            {
                NetworkLobbyManager.Instance?.OnModeSelectionChanged(val);
                UpdateTeamHeaders(val);
            });
            
        if (timeDropdown != null)
            timeDropdown.onValueChanged.AddListener((val) => NetworkLobbyManager.Instance?.OnTimeSelectionChanged(val));

        // Touch-friendly option rows (default TMP item height is only ~20px)
        StyleLobbyDropdown(modeDropdown);
        StyleLobbyDropdown(timeDropdown);

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

    /// <summary>
    /// Hides the lobby the moment the host commits to starting, so the map load
    /// isn't spent staring at the lobby panel. Only hides if the match really started.
    /// </summary>
    private void OnStartGameClicked()
    {
        var lobby = NetworkLobbyManager.Instance;
        if (lobby == null)
        {
            Debug.LogWarning("[LobbyUIManager] Start Game clicked but NetworkLobbyManager.Instance is null.");
            return;
        }

        if (lobby.StartMatch())
        {
            if (startGameButton != null) startGameButton.interactable = false;
            ShowLoadingScreen(true);
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

        // While loading into the map, keep every lobby-scene control hidden.
        if (_isLoadingIntoMatch) return;

        if(lobbyPanel.activeSelf)
        {
            ExitBtn.gameObject.SetActive(false);
        }
        else
        {
            ExitBtn.gameObject.SetActive(true);
        }
        if(HUDEditButton != null)
        {
            if(lobbyPanel.activeSelf)
            {
                HUDEditButton.gameObject.SetActive(false);
            }
            else
            {
                HUDEditButton.gameObject.SetActive(true);
            }
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

        // StringBuilder avoids per-message string reallocations
        var sb = new System.Text.StringBuilder(256);
        foreach (var msg in messages)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(msg.PlayerColor);
            sb.Append("<b><color=#").Append(colorHex).Append('>')
              .Append(msg.PlayerName).Append(":</color></b> ")
              .Append(msg.Message).Append('\n');
        }
        chatContent.text = sb.ToString();
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
            
            // Format mode options: Use "TDM", "FFA" and remove capture area/base option
            List<string> formattedOptions = new List<string>();
            foreach (var opt in modeOptions)
            {
                if (opt.Equals("TeamDeathmatch", StringComparison.OrdinalIgnoreCase) || opt.Equals("TDM", StringComparison.OrdinalIgnoreCase))
                {
                    formattedOptions.Add("TDM");
                }
                else if (opt.Equals("FreeForAll", StringComparison.OrdinalIgnoreCase) || opt.Equals("FFA", StringComparison.OrdinalIgnoreCase))
                {
                    formattedOptions.Add("FFA");
                }
            }
            modeDropdown.AddOptions(formattedOptions);
            StyleLobbyDropdown(modeDropdown);
        }

        if (timeDropdown != null)
        {
            timeDropdown.interactable = isHost;
            timeDropdown.ClearOptions();
            timeDropdown.AddOptions(timeOptions);
            StyleLobbyDropdown(timeDropdown);
        }
        
        // Host starts as not ready and must click ready like everyone else
        // The NetworkLobbyManager will handle the initial state
        
        // Initialize panel colors based on current mode
        if (NetworkLobbyManager.Instance != null)
        {
            UpdateTeamHeaders(NetworkLobbyManager.Instance.SelectedModeIndex);
        }
    }

    /// <summary>
    /// Enlarges TMP dropdown list rows so Mode / Time options are easy to tap on mobile.
    /// </summary>
    public static void StyleLobbyDropdown(TMP_Dropdown dropdown, float itemHeight = 36f, float fontSize = 22f)
    {
        if (dropdown == null || dropdown.template == null) return;

        RectTransform itemRt = null;
        if (dropdown.itemText != null)
            itemRt = dropdown.itemText.transform.parent as RectTransform;
        if (itemRt == null)
        {
            Toggle itemToggle = dropdown.template.GetComponentInChildren<Toggle>(true);
            if (itemToggle != null)
                itemRt = itemToggle.transform as RectTransform;
        }

        if (itemRt != null)
        {
            itemRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, itemHeight);

            LayoutElement layout = itemRt.GetComponent<LayoutElement>();
            if (layout == null) layout = itemRt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = itemHeight;
            layout.preferredHeight = itemHeight;
            layout.flexibleHeight = 0f;
        }

        int optionCount = Mathf.Max(dropdown.options.Count, 2);
        float templateHeight = Mathf.Clamp(itemHeight * optionCount + 8f, itemHeight * 2f, itemHeight * 5f + 8f);
        dropdown.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, templateHeight);

        if (dropdown.itemText != null)
        {
            dropdown.itemText.enableAutoSizing = false;
            dropdown.itemText.fontSize = fontSize;
        }
    }

    /// <summary>
    /// Called immediately when the mode dropdown value changes.
    /// Updates team column headers and panel colors to reflect the selected mode.
    /// </summary>
    private void UpdateTeamHeaders(int modeIndex)
    {
        bool isFFA = (GameMode)modeIndex == GameMode.FreeForAll;

        if (isFFA)
        {
            if (teamAHeaderText != null) teamAHeaderText.text = "FFA";
            if (teamBHeaderText != null) teamBHeaderText.text = "FFA";
            
            // Make both panels blue for FFA mode
            Color blueColor = new Color(0.18f, 0.47f, 1f); // Same blue as game over panel
            if (teamAPanelImage != null) teamAPanelImage.color = blueColor;
            if (teamBPanelImage != null) teamBPanelImage.color = blueColor;
        }
        else
        {
            if (teamAHeaderText != null) teamAHeaderText.text = "Hero's";
            if (teamBHeaderText != null) teamBHeaderText.text = "Aliens";
            
            // Restore original colors for Team Deathmatch
            Color blueColor = new Color(0.18f, 0.47f, 1f); // Blue for Team A (Hero's)
            Color redColor = new Color(1f, 0.22f, 0.22f);   // Red for Team B (Aliens)
            if (teamAPanelImage != null) teamAPanelImage.color = blueColor;
            if (teamBPanelImage != null) teamBPanelImage.color = redColor;
        }

    }

    public void UpdatePlayerList(Dictionary<int, PlayerLobbyData> players)
    {
        bool isFFA = false;
        if (NetworkLobbyManager.Instance != null)
        {
            isFFA = (GameMode)NetworkLobbyManager.Instance.SelectedModeIndex == GameMode.FreeForAll;
        }

        // Update team headers based on mode
        if (isFFA)
        {
            if (teamAHeaderText != null) teamAHeaderText.text = "FFA";
            if (teamBHeaderText != null) teamBHeaderText.text = "FFA";
        }
        else
        {
            if (teamAHeaderText != null) teamAHeaderText.text = "Hero's";
            if (teamBHeaderText != null) teamBHeaderText.text = "Aliens";
        }

        // Clear both team columns
        if (teamAListContent != null)
            foreach (Transform t in teamAListContent.transform) Destroy(t.gameObject);
        if (teamBListContent != null)
            foreach (Transform t in teamBListContent.transform) Destroy(t.gameObject);

        // Sort: host first, then by player ID
        var sortedPlayers = players
            .OrderByDescending(p => p.Value.IsHost)
            .ThenBy(p => p.Key)
            .ToList();

        const int maxPlayersPerColumn = 5;
        int teamASlot = 0;
        int teamBSlot = 0;

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var kvp = sortedPlayers[i];
            int playerId = kvp.Key;
            PlayerLobbyData player = kvp.Value;

            GameObject parent;
            int slotIndex;
            if (isFFA)
            {
                bool leftColumn = i < maxPlayersPerColumn;
                parent = leftColumn ? teamAListContent : teamBListContent;
                slotIndex = leftColumn ? i : i - maxPlayersPerColumn;
            }
            else
            {
                bool heroesTeam = player.TeamID == 0;
                parent = heroesTeam ? teamAListContent : teamBListContent;
                slotIndex = heroesTeam ? teamASlot : teamBSlot;
                if (slotIndex >= maxPlayersPerColumn)
                    continue;
                if (heroesTeam) teamASlot++;
                else teamBSlot++;
            }

            if (parent == null) continue;

            GameObject item = Instantiate(playerListItemPrefab, parent.transform);
            item.transform.SetSiblingIndex(slotIndex);

            PlayerListItemUI listItem = item.GetComponent<PlayerListItemUI>();
            if (listItem != null)
            {
                listItem.SetPlayerInfo(player.PlayerName.ToString(), player.IsReady, player.IsHost, player.PlayerColor, player.TeamID);
                
                // Show kick button only if local user is host AND this row is not the host
                bool showKick = isHost && !player.IsHost;
                listItem.SetupKickButton(playerId, showKick, OnKickButtonPressed);
            }
        }
    }

    /// <summary>
    /// Called when the host presses a kick button on a player row.
    /// </summary>
    private void OnKickButtonPressed(int playerId)
    {
        NetworkLobbyManager.Instance?.KickPlayer(playerId);
    }

    public void SetJoinCode(string joinCode)
    {
        _rawJoinCode = joinCode; // Cache the bare code for clipboard use

        if (joinCodeText != null)
        {
            joinCodeText.text = $"Join Code: {joinCode}";
        }

        // Show the copy button now that we have a code
        if (copyCodeButton != null)
            copyCodeButton.gameObject.SetActive(!string.IsNullOrEmpty(joinCode));

        // Once the join code is set, the room is ready, so we can clear the status text.
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = "";
        }
    }

    /// <summary>
    /// Copies the current join code to the system clipboard.
    /// The button's own label switches from "Copy" → "Copied!" for 2 seconds then reverts.
    /// </summary>
    public void CopyJoinCodeToClipboard()
    {
        if (string.IsNullOrEmpty(_rawJoinCode)) return;

        // Copy to system clipboard
        GUIUtility.systemCopyBuffer = _rawJoinCode;

        // Animate the button label
        if (copyCodeButton != null)
        {
            if (_copyFeedbackCoroutine != null)
                StopCoroutine(_copyFeedbackCoroutine);
            _copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedback());
        }
    }

    private System.Collections.IEnumerator ShowCopyFeedback()
    {
        // Get the TMP label that sits inside the button
        var label = copyCodeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) yield break;

        string original = label.text;   // remember "Copy" (or whatever the label says)
        label.text = "Copied!";
        copyCodeButton.interactable = false; // prevent double-press during feedback

        yield return new WaitForSeconds(2f);

        label.text = original;
        copyCodeButton.interactable = true;
        _copyFeedbackCoroutine = null;
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
        readyButtonText.text = isReady ? "Not Ready" : "Ready";
    }

    public void ShowMessage(string message)
    {
        if (notificationText == null || string.IsNullOrEmpty(message)) return;

        notificationText.text = message;
        // Only enable the label itself — never deactivate lobby/team panels.
        if (!notificationText.gameObject.activeSelf)
            notificationText.gameObject.SetActive(true);

        StartCoroutine(HideMessageAfterDelay(3f));
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
        if (show)
        {
            _isLoadingIntoMatch = false;
            if (startGameButton != null) startGameButton.interactable = true;
        }

        if(lobbyPanel != null) lobbyPanel.SetActive(show);
        if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
        if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
        if (_autoLoadingScreen != null) _autoLoadingScreen.SetActive(false);
        StopLoadingDotsAnimation();
    }

    public void ShowHeroSelectionPanel(bool show)
    {
        if (heroSelectionPanel != null)
        {
            heroSelectionPanel.SetActive(show);
        }
        else
        {
            Debug.LogError("[LobbyUIManager] heroSelectionPanel is NULL!");
        }

        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(!show);
        }
    }

    public void ShowLoadingScreen(bool show = true)
    {
        _isLoadingIntoMatch = show;

        GameObject screen = show ? EnsureLoadingScreen() : (loadingScreenPanel != null ? loadingScreenPanel : _autoLoadingScreen);
        if (screen != null) screen.SetActive(show);

        if (show)
        {
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (heroSelectionPanel != null) heroSelectionPanel.SetActive(false);
            if (inGameUIPanel != null) inGameUIPanel.SetActive(false);
            if (waitingForPlayersPanel != null) waitingForPlayersPanel.SetActive(false);

            // These are toggled from Update() against lobbyPanel, so hide them explicitly.
            if (ExitBtn != null) ExitBtn.gameObject.SetActive(false);
            if (HUDEditButton != null) HUDEditButton.gameObject.SetActive(false);

            StartLoadingDotsAnimation();
            // Loading UI hidden — no console spam
        }
        else
        {
            StopLoadingDotsAnimation();
        }
    }

    /// <summary>
    /// Returns the assigned loading panel, or builds a plain full-screen one when the
    /// scene has none, so the map load is never spent looking at the lobby.
    /// </summary>
    private GameObject EnsureLoadingScreen()
    {
        if (loadingScreenPanel != null)
        {
            // Prefer an assigned panel's label if one exists so dots can animate there too.
            if (_loadingLabel == null)
            {
                _loadingLabel = loadingScreenPanel.GetComponentInChildren<TextMeshProUGUI>(true);
                if (_loadingLabel != null)
                    _loadingLabel.text = "Loading...";
            }
            return loadingScreenPanel;
        }

        if (_autoLoadingScreen != null) return _autoLoadingScreen;

        var root = new GameObject("AutoLoadingScreen", typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(root.transform, false);
        var backgroundRT = background.GetComponent<RectTransform>();
        backgroundRT.anchorMin = Vector2.zero;
        backgroundRT.anchorMax = Vector2.one;
        backgroundRT.offsetMin = Vector2.zero;
        backgroundRT.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Color.black;

        var label = new GameObject("Label", typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        var labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0.5f);
        labelRT.anchorMax = new Vector2(0.5f, 0.5f);
        labelRT.anchoredPosition = Vector2.zero;
        labelRT.sizeDelta = new Vector2(1200f, 160f);
        _loadingLabel = label.GetComponent<TextMeshProUGUI>();
        _loadingLabel.text = "Loading...";
        _loadingLabel.alignment = TextAlignmentOptions.Center;
        _loadingLabel.fontSize = 56f;
        _loadingLabel.color = Color.white;

        _autoLoadingScreen = root;
        return _autoLoadingScreen;
    }

    private void StartLoadingDotsAnimation()
    {
        if (_loadingLabel == null && _autoLoadingScreen != null)
            _loadingLabel = _autoLoadingScreen.GetComponentInChildren<TextMeshProUGUI>(true);

        if (_loadingLabel == null) return;

        StopLoadingDotsAnimation();
        _loadingDotsCoroutine = StartCoroutine(AnimateLoadingDots());
    }

    private void StopLoadingDotsAnimation()
    {
        // Unity fake-null: destroyed LobbyUIManager must not call StopCoroutine
        if (this == null) return;

        if (_loadingDotsCoroutine != null)
        {
            StopCoroutine(_loadingDotsCoroutine);
            _loadingDotsCoroutine = null;
        }
    }

    private System.Collections.IEnumerator AnimateLoadingDots()
    {
        const string baseText = "Loading";
        // First frame: show all three dots, then cycle.
        int dotCount = 3;

        while (_loadingLabel != null)
        {
            _loadingLabel.text = baseText + new string('.', dotCount);
            yield return new WaitForSecondsRealtime(0.4f);
            dotCount = (dotCount % 3) + 1; // "...", ".", "..", then "...", ...
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
            if (_autoLoadingScreen != null) _autoLoadingScreen.SetActive(false);
            StopLoadingDotsAnimation();
        }
    }

    public void ExitToHome(){
        SceneManager.LoadScene(0);
    }
}
