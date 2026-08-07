using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;


public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public GameObject joinCodeInputPanel;
    public TMP_Text hostStatusText; // For showing status messages like "Creating room..."
    public TMP_Text joinStatusText; // For showing status messages like "Joining room..."
    public TMP_InputField joinCodeInputField;
    public Button joinConfirmButton;
    public Button joinCancelButton;
    public TMP_InputField playerNameInputField; // New Input Field for Player Name

    public Button changeUserNameButton; // NEW: Reference to the button opening the rename panel
    public GameObject changeUserNamePanel;
    public TMP_Text playerNameText; // Display the current player name
    private NetworkStarter networkStarter;

    private IEnumerator hostAnimationCoroutine;
    private readonly List<GameObject> _hiddenWhileRename = new List<GameObject>();
    private readonly List<GameObject> _hiddenWhileJoin = new List<GameObject>();

    private void Start()
    {
        networkStarter = FindObjectOfType<NetworkStarter>();

        // Load or set default player name
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        bool isFirstTime = PlayerPrefs.GetInt("HasSetInitialName", 0) == 0;

        if (string.IsNullOrEmpty(savedName))
        {
            savedName = $"Player {UnityEngine.Random.Range(1000, 9999)}";
            PlayerPrefs.SetString("PlayerName", savedName);
        }

        if (playerNameInputField != null)
        {
            playerNameInputField.text = savedName;
            // Set character limit to 10
            playerNameInputField.characterLimit = 10;
        }

        if (playerNameText != null)
        {
            playerNameText.text = savedName;
        }

        // Set up button listeners
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(ShowJoinCodeInput);
        
        if (joinConfirmButton != null)
            joinConfirmButton.onClick.AddListener(OnJoinConfirmed);
            
        if (joinCancelButton != null)
            joinCancelButton.onClick.AddListener(HideJoinCodeInput);
            
        // Ensure panels are in correct initial state
        if (joinCodeInputPanel != null)
            joinCodeInputPanel.SetActive(false);
            
        // If we are already connected (e.g. returning from a game), skip the main menu
        var runner = FindObjectOfType<Fusion.NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            SwitchToLobby();
        }
        else if (isFirstTime)
        {
            // Only show the rename panel automatically if we're actually staying on the main menu
            ChangeUserName();
        }
    }

    private System.Collections.IEnumerator HideStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (hostStatusText != null && hostStatusText.gameObject.activeSelf)
        {
            hostStatusText.text = "";
            hostStatusText.gameObject.SetActive(false);
        }
        if (joinStatusText != null && joinStatusText.gameObject.activeSelf)
        {
            joinStatusText.text = "";
            joinStatusText.gameObject.SetActive(false);
        }
    }
    
    private IEnumerator AnimateLoadingText(TMP_Text textComponent, string baseText)
    {
        int dotCount = 0;
        while (textComponent != null && textComponent.gameObject.activeInHierarchy)
        {
            string dots = new string('.', (dotCount % 3) + 1);
            textComponent.text = $"{baseText}{dots}";
            dotCount++;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnHostClicked()
    {
        if (!HasInternetConnection())
        {
            if (hostStatusText != null)
            {
                hostStatusText.text = "No internet connection. Please try again.";
                hostStatusText.gameObject.SetActive(true);
                StartCoroutine(HideStatusAfterDelay(3f));
            }
            return;
        }
        
        if (networkStarter != null)
        {
            // Disable button to prevent multiple clicks
            hostButton.interactable = false;
            
            // Show creating message
            if (hostStatusText != null)
            {
                hostStatusText.gameObject.SetActive(true);
                // Stop any existing animation
                if (hostAnimationCoroutine != null)
                    StopCoroutine(hostAnimationCoroutine);
                hostAnimationCoroutine = AnimateLoadingText(hostStatusText, "Creating Room");
                StartCoroutine(hostAnimationCoroutine);
            }
            
            // Start hosting and wait for room to be ready
            networkStarter.StartHost((success) => {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (success)
                    {
                        // Clear status text when room is created
                        if (hostStatusText != null)
                            hostStatusText.gameObject.SetActive(false);
                            
                        // Only switch to lobby after room is created
                        SwitchToLobby();
                    }
                    else
                    {
                        // Re-enable button and update status if failed
                        hostButton.interactable = true;
                        if (hostStatusText != null)
                        {
                            hostStatusText.text = "Failed to create room. Try again.";
                            // Hide the error message after 3 seconds
                            StartCoroutine(HideStatusAfterDelay(3f));
                        }
                    }
                });
            });
        }
    }

    private void ShowJoinCodeInput()
    {
        if (joinCodeInputPanel != null)
        {
            joinCodeInputPanel.SetActive(true);
            HideMenuChromeBehindJoin();
            LobbyInputModalChrome.ApplyJoinCode(joinCodeInputPanel);
            LobbyInputModalChrome.ResetJoinSubtitle(joinStatusText);

            // Clear previous input and focus the field
            if (joinCodeInputField != null)
            {
                joinCodeInputField.text = "";
                joinCodeInputField.Select();
                joinCodeInputField.ActivateInputField();
                if (changeUserNameButton != null)
                    changeUserNameButton.gameObject.SetActive(false);
            }
        }
    }
    
    private void HideJoinCodeInput()
    {
        if (joinCodeInputPanel != null)
            joinCodeInputPanel.SetActive(false);

        RestoreMenuChromeBehindJoin();

        if (changeUserNameButton != null)
            changeUserNameButton.gameObject.SetActive(true);
    }
    
    // Check if the device has an active internet connection
    private bool HasInternetConnection()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }
    
    private void OnJoinConfirmed()
    {
        if (!HasInternetConnection())
        {
            if (joinStatusText != null)
            {
                joinStatusText.text = "No internet connection. Please try again.";
                joinStatusText.gameObject.SetActive(true);
                StartCoroutine(HideStatusAfterDelay(3f));
            }
            return;
        }

        if (networkStarter != null && joinCodeInputField != null && !string.IsNullOrWhiteSpace(joinCodeInputField.text))
        {
            string joinCode = joinCodeInputField.text.Trim().ToUpper();

            if (joinStatusText != null)
            {
                joinStatusText.text = "Joining game...";
                joinStatusText.gameObject.SetActive(true);
            }

            joinConfirmButton.interactable = false;

            Action<bool, string> onJoinComplete = (success, error) => {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    joinConfirmButton.interactable = true;

                    if (success)
                    {
                        if (joinStatusText != null)
                            joinStatusText.gameObject.SetActive(false);

                        SwitchToLobby();
                        changeUserNameButton.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (joinStatusText != null)
                        {
                            joinStatusText.text = error;
                            joinStatusText.gameObject.SetActive(true);
                            StartCoroutine(HideStatusAfterDelay(3f));
                        }
                        joinCodeInputField.text = "";
                        joinCodeInputField.Select();
                        joinCodeInputField.ActivateInputField();
                        Debug.LogError($"Failed to join room: {error}");
                    }
                });
            };

            networkStarter.JoinSession(joinCode, onJoinComplete);
        }
        else
        {
            if (joinStatusText != null)
            {
                joinStatusText.text = "Please enter a valid join code";
                joinStatusText.gameObject.SetActive(true);
                StartCoroutine(HideStatusAfterDelay(3f));
            }
            Debug.LogError("Please enter a valid join code");
        }
    }

    private void SwitchToLobby()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        
        // Hide the join code input panel when successfully joining a lobby
        if (joinCodeInputPanel != null)
        {
            joinCodeInputPanel.SetActive(false);
        }

        // Disable changing name once in a lobby
        if (changeUserNameButton != null)
        {
            changeUserNameButton.gameObject.SetActive(false);
        }
    }

    public void ChangeUserName()
    {
        if (changeUserNamePanel == null) return;

        changeUserNamePanel.SetActive(true);
        HideCanvasSiblingsBehindRename();
        LobbyInputModalChrome.ApplyChangeUsername(changeUserNamePanel);

        if (playerNameInputField != null)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", playerNameInputField.text);
            playerNameInputField.text = savedName;
            playerNameInputField.characterLimit = 10;
            playerNameInputField.Select();
            playerNameInputField.ActivateInputField();
        }
    }

    public void HideChangeUserName()
    {
        // Mark as set even if they just closed the panel without changing
        PlayerPrefs.SetInt("HasSetInitialName", 1);
        PlayerPrefs.Save();

        if (changeUserNamePanel != null)
            changeUserNamePanel.SetActive(false);

        RestoreCanvasSiblingsBehindRename();
    }

    public void ConfirmChangeUserName()
    {
        if (playerNameInputField != null)
        {
            string newName = playerNameInputField.text.Trim();
            
            // Limit player name to 10 characters
            if (newName.Length > 10)
            {
                newName = newName.Substring(0, 10);
                // Update the input field to show the truncated name
                playerNameInputField.text = newName;
            }
            
            if (!string.IsNullOrEmpty(newName))
            {
                PlayerPrefs.SetString("PlayerName", newName);
                PlayerPrefs.SetInt("HasSetInitialName", 1);
                PlayerPrefs.Save();

                HideChangeUserName();
                Debug.Log($"Player name changed to: {newName}");
                
                if (playerNameText != null)
                {
                    playerNameText.text = newName;
                }
            }
        }
    }

    private void HideCanvasSiblingsBehindRename()
    {
        RestoreCanvasSiblingsBehindRename();

        if (changeUserNamePanel == null) return;

        Transform parent = changeUserNamePanel.transform.parent;
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == changeUserNamePanel.transform) continue;

            // Keep lobby artwork so the modal sits on top of it as a layer.
            if (sibling.name == "Background_Image") continue;

            if (sibling.gameObject.activeSelf)
            {
                sibling.gameObject.SetActive(false);
                _hiddenWhileRename.Add(sibling.gameObject);
            }
        }
    }

    private void RestoreCanvasSiblingsBehindRename()
    {
        for (int i = 0; i < _hiddenWhileRename.Count; i++)
        {
            GameObject go = _hiddenWhileRename[i];
            if (go != null) go.SetActive(true);
        }

        _hiddenWhileRename.Clear();
    }

    /// <summary>
    /// Hides HOST / JOIN / other menu buttons under the same parent so only the
    /// scene backdrop shows through the translucent dim — not the lobby buttons.
    /// </summary>
    private void HideMenuChromeBehindJoin()
    {
        RestoreMenuChromeBehindJoin();

        if (joinCodeInputPanel == null) return;

        Transform parent = joinCodeInputPanel.transform.parent;
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == joinCodeInputPanel.transform) continue;
            if (!sibling.gameObject.activeSelf) continue;

            sibling.gameObject.SetActive(false);
            _hiddenWhileJoin.Add(sibling.gameObject);
        }
    }

    private void RestoreMenuChromeBehindJoin()
    {
        for (int i = 0; i < _hiddenWhileJoin.Count; i++)
        {
            GameObject go = _hiddenWhileJoin[i];
            if (go != null) go.SetActive(true);
        }

        _hiddenWhileJoin.Clear();
    }

    public void ShowMainMenuPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (joinCodeInputPanel != null)
            joinCodeInputPanel.SetActive(false);

        RestoreMenuChromeBehindJoin();

        // Re-enable host/join buttons
        if (hostButton != null)
            hostButton.interactable = true;

        if (joinButton != null)
            joinButton.interactable = true;

        if (changeUserNameButton != null)
            changeUserNameButton.gameObject.SetActive(true);
    }

    public void ShowErrorAndReturnToMenu(string message)
    {
        ShowMainMenuPanel();
        if (joinStatusText != null)
        {
            joinStatusText.text = message;
            joinStatusText.gameObject.SetActive(true);
            StartCoroutine(HideStatusAfterDelay(5f)); // Hide after 5 seconds
        }
    }
}
