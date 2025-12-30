using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class CoinsManager : MonoBehaviour
{
    public static CoinsManager Instance;

    // Event that will be triggered whenever coins are updated
    public event System.Action OnCoinsUpdated;

    public TextMeshProUGUI coinText; // Assign in Inspector
    public int currentCoins;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CloudSaveManager.OnCloudDataLoaded += FetchCoins;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            CloudSaveManager.OnCloudDataLoaded -= FetchCoins;
        }
    }

    private void Start()
    {        
        // Data will be loaded by the OnCloudDataLoaded event.
    }

    private void LoadCoins()
    {
        currentCoins = PlayerPrefs.GetInt("CurrentCoins", 0);
        UpdateCoinUI();
    }

    private void SaveCoins()
    {
        PlayerPrefs.SetInt("CurrentCoins", currentCoins);
        PlayerPrefs.Save();
        CloudSaveManager.SaveTimestamp();
    }

    public void FetchCoins()
    {
        LoadCoins();
    }

    public void SpendCoins(int amount, string reason, System.Action<bool> onComplete = null)
    {
        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            SaveCoins();
            UpdateCoinUI();
            OnCoinsUpdated?.Invoke();
            onComplete?.Invoke(true);
        }
        else
        {
            onComplete?.Invoke(false);
        }
    }

    public void UpdateCoinUI()
    {
        if (coinText == null)
        {
            Debug.LogError("[CoinsManager] coinText is not assigned in the Inspector!");
            return;
        }
        
        try
        {
            coinText.text = currentCoins.ToString();
            
            // Force update the canvas
            if (coinText.canvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }
        catch (System.Exception e)
        {
        }
    }



    public void FetchLocalCoinsCount()
    {
        LoadCoins();
    }

    public void AddCoins(int amount, string reason = "Alien Defeated")
    {
        // Update local coins immediately for better UX
        currentCoins += amount;
        SaveCoins();
        UpdateCoinUI();
        OnCoinsUpdated?.Invoke();
    }
}
