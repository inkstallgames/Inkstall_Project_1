using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    public GameObject buybombsPanel;
    public GameObject Notification;
    public TextMeshProUGUI notificationText; // Assign this in the Inspector
    public GameObject exitTextOBJ;
    public GameObject appleSigninBtn;
    public GameObject GooglePlayGamesBtn;
    private bool exitText;
    public int lastDoorid;

    private bool completionCoroutineStarted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

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

        // Re-assign button listeners to ensure they are connected to the AuthManager singleton
#if UNITY_ANDROID
        if (GooglePlayGamesBtn != null)
        {
            var googleButton = GooglePlayGamesBtn.GetComponent<Button>();
            if (googleButton != null) 
            {
                googleButton.onClick.RemoveAllListeners();
                googleButton.onClick.AddListener(() =>
                {
                    DisplayNotification("Signing you in...", 0); // Display indefinitely
                    AuthManager.Instance.SignInWithGoogle();
                });
            }
        }
#elif UNITY_IOS
        if (appleSigninBtn != null)
        {
            var appleButton = appleSigninBtn.GetComponent<Button>();
            var appleSignInHandler = appleSigninBtn.GetComponent<AppleSignInHandler>();
            if (appleSignInHandler == null) 
            {
                appleSignInHandler = appleSigninBtn.AddComponent<AppleSignInHandler>();
            }

            if (appleButton != null) 
            {
                appleButton.onClick.RemoveAllListeners();
                appleButton.onClick.AddListener(() =>
                {
                    DisplayNotification("Signing you in...", 0); // Display indefinitely
                    appleSignInHandler.SignIn();
                });
            }
        }
#endif

        #if UNITY_EDITOR
        appleSigninBtn.SetActive(true);
        GooglePlayGamesBtn.SetActive(true);
#elif UNITY_IOS
        appleSigninBtn.SetActive(true);
        GooglePlayGamesBtn.SetActive(false);
#elif UNITY_ANDROID
        appleSigninBtn.SetActive(false);
        GooglePlayGamesBtn.SetActive(true);
#else
        // For any other platform, disable both
        appleSigninBtn.SetActive(false);
        GooglePlayGamesBtn.SetActive(false);
#endif
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


    private void OnfinalRoom()
    {
        
        if (roomManager != null && !completionCoroutineStarted)
        {
            if (ProgressManager.Instance != null && ProgressManager.Instance.IsDataLoaded())
            {
                var doorData = ProgressManager.Instance.GetDoorData(lastDoorid);

                if (doorData != null)
                {
                    if (doorData.isRoomCompleted)
                    {
                        StartCoroutine(CompletionCo());
                        if( exitText == true )
                        {
                            exitTextOBJ.SetActive(true);
                        }
                        completionCoroutineStarted = true;
                    }
                }
            }
        }
    }



    private IEnumerator CompletionCo()
    {   
        exitText = true;
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

    public void OpenShopPanel()
    {
        MainCanvas.SetActive(false);
        buybombsPanel.SetActive(true);
    }

    public void OnWatchAdClicked()
    {
        Debug.Log("[WatchAdButton] 'Watch Ad' button clicked. Attempting to show a rewarded ad.");

        if (AdManager.Instance != null)
        {
            // Call the singleton method to show the ad and check if it was successful
            bool adShown = AdManager.Instance.ShowRewardedAdForExtraKey();

            if (!adShown)
            {
                // If the ad wasn't shown, show a notification
                DisplayNotification("Ad not found", 3f);
            }
        }
        else
        {
            // Log an error if the AdManager is not available
            Debug.LogError("[WatchAdButton] AdManager.Instance is not found in the scene! Cannot show rewarded ad.");
            DisplayNotification("Ad service not available", 3f);
        }
    }

    
    public void ShowNotificationMessage(string message)
    {
        DisplayNotification(message, 3f); // Display for 3 seconds
    }

    private Coroutine notificationCoroutine;

    public void DisplayNotification(string message, float duration)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        notificationCoroutine = StartCoroutine(ShowNotification(message, duration));
    }

    public void HideNotification()
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }
    }

    private IEnumerator ShowNotification(string message, float duration)
    {
        if (Notification != null)
        {
            if (notificationText != null)
            {
                notificationText.text = message;
            }
            else
            {
                Debug.LogWarning("[UIManager] Notification TextMeshProUGUI component is not assigned in the Inspector.");
            }

            notificationText.gameObject.SetActive(true);
            if (duration > 0)
            {
                yield return new WaitForSeconds(duration);
                notificationText.gameObject.SetActive(false);
            }
        }
    }




}
