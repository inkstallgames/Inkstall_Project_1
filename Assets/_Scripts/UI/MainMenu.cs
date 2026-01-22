using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public GameObject joinCodeInputPanel;
    public TMP_Text statusText; // For showing status messages like "Creating room..."
    public TMP_InputField joinCodeInputField;
    public Button joinConfirmButton;
    public Button joinCancelButton;
    public TMP_InputField playerNameInputField; // New Input Field for Player Name

    public Button changeUserNameButton; // NEW: Reference to the button opening the rename panel
    public GameObject changeUserNamePanel;
    public TMP_Text playerNameText; // Display the current player name
    private NetworkStarter networkStarter;

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
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void OnHostClicked()
    {
        if (networkStarter != null)
        {
            // Disable button to prevent multiple clicks
            hostButton.interactable = false;
            
            // Show creating message
            if (statusText != null)
            {
                statusText.text = "Creating Room...";
                statusText.gameObject.SetActive(true);
            }
            
            // Start hosting and wait for room to be ready
            networkStarter.StartHost((success) => {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    if (success)
                    {
                        // Clear status text when room is created
                        if (statusText != null)
                            statusText.gameObject.SetActive(false);
                            
                        // Only switch to lobby after room is created
                        SwitchToLobby();
                    }
                    else
                    {
                        // Re-enable button and update status if failed
                        hostButton.interactable = true;
                        if (statusText != null)
                        {
                            statusText.text = "Failed to create room. Try again.";
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
    
    private void OnJoinConfirmed()
    {
        if (networkStarter != null && joinCodeInputField != null && !string.IsNullOrWhiteSpace(joinCodeInputField.text))
        {
            string joinCode = joinCodeInputField.text.Trim().ToUpper();
            // Show loading state
            joinConfirmButton.interactable = false;
            
            // Create a callback for join result
            Action<bool> onJoinComplete = (success) => {
                // Run on main thread since this is a Unity UI update
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    joinConfirmButton.interactable = true;
                    
                    if (success)
                    {
                        // Only switch to lobby if join was successful
                        SwitchToLobby();
                        changeUserNameButton.gameObject.SetActive(false);
                    }
                    else
                    {
                        // Show error message to user
                        Debug.LogError("Failed to join room. Please check the join code and try again.");
                        // You could show an error message to the user here
                    }
                });
            };
            
            // Start the join process
            networkStarter.JoinSession(joinCode, onJoinComplete);
        }
        else
        {
            Debug.LogError("Please enter a valid join code");
        }
    }

    private void SwitchToLobby()
    {
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);

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

    
}
