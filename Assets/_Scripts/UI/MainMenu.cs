using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

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

    private void Start()
    {
        networkStarter = FindObjectOfType<NetworkStarter>();

        // Load or set default player name
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (string.IsNullOrEmpty(savedName))
        {
            savedName = $"Player {UnityEngine.Random.Range(1000, 9999)}";
            PlayerPrefs.SetString("PlayerName", savedName);
        }

        if (playerNameInputField != null)
        {
            playerNameInputField.text = savedName;
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
            // Clear previous input and focus the field
            if (joinCodeInputField != null)
            {
                joinCodeInputField.text = "";
                joinCodeInputField.Select();
                joinCodeInputField.ActivateInputField();
                changeUserNameButton.gameObject.SetActive(false);
            }
        }
    }
    
    private void HideJoinCodeInput()
    {
        if (joinCodeInputPanel != null)
        {
            joinCodeInputPanel.SetActive(false);
            changeUserNameButton.gameObject.SetActive(true);
        }
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
            
            // Show joining status
            if (joinStatusText != null)
            {
                joinStatusText.text = "Joining game...";
                joinStatusText.gameObject.SetActive(true);
            }
            
            // Disable confirm button while joining
            joinConfirmButton.interactable = false;
            
            // Create a callback for join result
            Action<bool> onJoinComplete = (success) => {
                // Run on main thread since this is a Unity UI update
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    joinConfirmButton.interactable = true;
                    
                    if (success)
                    {
                        // Clear status text when joined successfully
                        if (joinStatusText != null)
                            joinStatusText.gameObject.SetActive(false);
                            
                        // Only switch to lobby if join was successful
                        SwitchToLobby();
                        changeUserNameButton.gameObject.SetActive(false);
                    }
                    else
                    {
                        // Show error message to user
                        if (joinStatusText != null)
                        {
                            joinStatusText.text = "Game not found. Please check the code.";
                            joinStatusText.gameObject.SetActive(true);
                            StartCoroutine(HideStatusAfterDelay(3f));
                        }
                        // Keep the join panel open and re-enable input
                        joinCodeInputField.text = "";
                        joinCodeInputField.Select();
                        joinCodeInputField.ActivateInputField();
                        Debug.LogError("Failed to join room. Please check the join code and try again.");
                    }
                });
            };
            
            // Start the join process
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
        changeUserNamePanel.SetActive(true);
    }

    public void HideChangeUserName()
    {
        changeUserNamePanel.SetActive(false);
    }

    public void ConfirmChangeUserName()
    {
        if (playerNameInputField != null)
        {
            string newName = playerNameInputField.text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                PlayerPrefs.SetString("PlayerName", newName);
                HideChangeUserName();
                Debug.Log($"Player name changed to: {newName}");
                
                if (playerNameText != null)
                {
                    playerNameText.text = newName;
                }
            }
        }
    }

    public void ShowMainMenuPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (joinCodeInputPanel != null)
            joinCodeInputPanel.SetActive(false);

        // Re-enable host/join buttons
        if (hostButton != null)
            hostButton.interactable = true;

        if (joinButton != null)
            joinButton.interactable = true;

        if (changeUserNameButton != null)
            changeUserNameButton.gameObject.SetActive(true);
    }
}
