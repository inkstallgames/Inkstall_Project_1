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
    public TMP_InputField joinCodeInputField;
    public Button joinConfirmButton;
    public Button joinCancelButton;

    private NetworkStarter networkStarter;

    private void Start()
    {
        networkStarter = FindObjectOfType<NetworkStarter>();

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

    private void OnHostClicked()
    {
        if (networkStarter != null)
        {
            networkStarter.StartHost();
            SwitchToLobby();
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
            }
        }
    }
    
    private void HideJoinCodeInput()
    {
        if (joinCodeInputPanel != null)
        {
            joinCodeInputPanel.SetActive(false);
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
    }
}
