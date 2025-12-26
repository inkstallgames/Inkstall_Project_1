using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private Button watchAdButton;
    public GameObject completion;
    public RoomManager roomManager;
    public GameObject MainCanvas;
    public GameObject noAdsPanel;
    public GameObject settingsPanel;
    
    private void Start()
    {
        // Subscribe to key count changes
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.OnKeysChanged += UpdateAdButtonState;
        }

        // Initial update of the ad button state
        UpdateAdButtonState();
        OnfinalRoom();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.OnKeysChanged -= UpdateAdButtonState;
        }
    }
    
    // This method will be called whenever the keys count changes
    private void UpdateAdButtonState()
    {
        if (watchAdButton != null && KeyManager.Instance != null)
        {
            // Deactivate the watch ad button if keys count is maxKeys or more
            int currentKeys = KeyManager.Instance.GetCurrentKeyCount();
            int maxKeys = KeyRefreshTimer.Instance.maxKeys;
            
            bool shouldBeActive = currentKeys < maxKeys;
            
            Debug.Log($"[UIManager] UpdateAdButtonState: Keys={currentKeys}, MaxKeys={maxKeys}, Active={shouldBeActive}");
            
            watchAdButton.gameObject.SetActive(shouldBeActive);
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateAdButtonState: Missing references (watchAdButton or KeyManager)");
        }
    }
    
    // Call this method from other scripts if you need to manually update the button state
    public void RefreshAdButtonState()
    {
        UpdateAdButtonState();
    }

    private bool completionCoroutineStarted = false;

    private void OnfinalRoom()
    {
        
        if (roomManager != null && !completionCoroutineStarted)
        {
            if (ProgressManager.Instance != null && ProgressManager.Instance.IsDataLoaded())
            {
                var doorData = ProgressManager.Instance.GetDoorData(6);

                if (doorData != null)
                {
                    Debug.Log("Door ID: 2, Is Room Completed: " + doorData.isRoomCompleted);
                    if (doorData.isRoomCompleted)
                    {
                        StartCoroutine(CompletionCo());
                        completionCoroutineStarted = true;
                    }
                }
            }
        }
    }



    private IEnumerator CompletionCo()
    {
        completion.SetActive(true);
        yield return new WaitForSeconds(10f);
        completion.SetActive(false);        
    }

    public void OpenNoAdsPanel()
    {
        MainCanvas.SetActive(false);
        noAdsPanel.SetActive(true);
    }

    public void OpenSettingsPanel()
    {
        MainCanvas.SetActive(false);
        settingsPanel.SetActive(true);
    }

}
