using System.Linq;
using Unity.Services.CloudSave;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance;

    void Awake()
    {
        Debug.Log("[CloudSaveManager] Awake() called.");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[CloudSaveManager] Instance set successfully.");
        }
        else
        {
            Debug.LogWarning("[CloudSaveManager] Duplicate found. Destroying this component only.");
            Destroy(this);
        }
    }



    // SAVE PLAYER DATA
    public async Task SavePlayerData(int coins, int keys, string studentDoorData)
    {
        var data = new Dictionary<string, object>
        {
            { "coins", coins },
            { "keys", keys },
            { "studentDoorData", studentDoorData }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log($"✅ Cloud Save: Data Saved:{keys}");
    }

    // LOAD PLAYER DATA
    public static event System.Action OnCloudDataLoaded;

    public async Task LoadAllPlayerDataFromCloud()
    {
        Debug.Log("[CloudSaveManager] LoadAllPlayerDataFromCloud started.");
        try
        {
            await LoadPlayerData();
            Debug.Log("[CloudSaveManager] LoadPlayerData completed successfully.");
        }
        catch (CloudSaveException e)
        {
            Debug.LogWarning($"[CloudSaveManager] CloudSaveException: {e.Message}");
            EnsureDefaultsExist();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CloudSaveManager] Exception during LoadPlayerData: {e.Message}\nStackTrace: {e.StackTrace}");
            EnsureDefaultsExist();
        }
        finally
        {
            Debug.Log("[CloudSaveManager] Invoking OnCloudDataLoaded event.");
            OnCloudDataLoaded?.Invoke();
            Debug.Log("[CloudSaveManager] OnCloudDataLoaded event invoked.");
        }
    }

    private void EnsureDefaultsExist()
    {
        Debug.Log("[CloudSaveManager] EnsureDefaultsExist called.");
        if (!PlayerPrefs.HasKey("KeysCount"))
        {
            Debug.Log("[CloudSaveManager] KeysCount not found in PlayerPrefs. Setting defaults.");
            PlayerPrefs.SetInt("CurrentCoins", 0);
            PlayerPrefs.SetInt("KeysCount", 5);
            PlayerPrefs.SetString("StudentDoorData", "");
            PlayerPrefs.Save();
            SaveTimestamp();
            Debug.Log("[CloudSaveManager] Defaults set: Keys=5, Coins=0");
        }
        else
        {
            int keys = PlayerPrefs.GetInt("KeysCount", 5);
            Debug.Log($"[CloudSaveManager] KeysCount already exists in PlayerPrefs: {keys}");
        }
    }

    public async Task SaveAllPlayerDataToCloud()
    {
        int coins = PlayerPrefs.GetInt("CurrentCoins", 0);
        int keys = PlayerPrefs.GetInt("KeysCount", 5);
        string studentDoorData = PlayerPrefs.GetString("StudentDoorData", "");

        await SavePlayerData(coins, keys, studentDoorData);
    }

    public void LoadLocalDataOnly()
    {
        Debug.Log("[CloudSaveManager] Loading local data only.");
        EnsureDefaultsExist();
        OnCloudDataLoaded?.Invoke();
        Debug.Log("[CloudSaveManager] OnCloudDataLoaded event invoked for local data.");
    }

    public async Task LoadPlayerData()
    {
        Debug.Log("[CloudSaveManager] LoadPlayerData: Fetching cloud data...");
        // Load cloud data with metadata to get server timestamp
        var cloudData = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
        Debug.Log($"[CloudSaveManager] Cloud data fetched. Count: {cloudData?.Count ?? 0}");

        if (cloudData != null && cloudData.Count > 0)
        {
            string rawData = string.Join(", ", cloudData.Select(kvp => $"'{kvp.Key}': '{kvp.Value.Value.GetAsString()}'"));
            Debug.Log($"[CloudSaveManager] RAW CLOUD DATA: {{{rawData}}}");
        }

        // Get local timestamp
        string localTimestampStr = PlayerPrefs.GetString("LastLocalSaveTimestamp", null);
        Debug.Log($"[CloudSaveManager] Local timestamp: {localTimestampStr ?? "null"}");
        System.DateTime.TryParse(localTimestampStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out System.DateTime localTimestamp);

        // REINSTALL CHECK: If no door data exists locally, force a cloud download.
        bool isFreshInstall = !PlayerPrefs.HasKey("StudentDoorData");
        if (isFreshInstall)
        {
            Debug.Log("[CloudSaveManager] Fresh install detected (no local door data). Forcing cloud data load.");
        }

        if (cloudData != null && cloudData.Count > 0 && !isFreshInstall)
        {
            // Find the most recent server timestamp from all cloud variables
            var validTimestamps = cloudData.Values.Where(v => v.Modified.HasValue).Select(v => v.Modified.Value);
            System.DateTime cloudTimestamp = validTimestamps.Any() ? validTimestamps.Max() : System.DateTime.MinValue;

            if (localTimestamp > cloudTimestamp)
            {
                // Local data is newer, so upload it to the cloud
                Debug.Log("[CloudSaveManager] Local data is newer. Uploading to cloud.");
                await SaveAllPlayerDataToCloud();
            }
            else
            {
                // Cloud data is newer or same, so load it
                Debug.Log("[CloudSaveManager] Cloud data is newer or same. Loading from cloud.");
                int coins = cloudData.ContainsKey("coins") ? cloudData["coins"].Value.GetAs<int>() : 0;
                int keysCount = cloudData.ContainsKey("keys") ? cloudData["keys"].Value.GetAs<int>() : 5;
                string studentDoorData = cloudData.ContainsKey("studentDoorData") ? cloudData["studentDoorData"].Value.GetAsString() : "";

                Debug.Log($"[CloudSaveManager] Raw cloud data - Coins: {coins}, Keys: {keysCount}");

                // Validate: keys should never be 0 as default is 5
                if (keysCount == 0)
                {
                    Debug.LogWarning("[CloudSaveManager] Detected 0 keys in cloud data. Resetting to default (5).");
                    keysCount = 5;
                }

                PlayerPrefs.SetInt("CurrentCoins", coins);
                PlayerPrefs.SetInt("KeysCount", keysCount);
                PlayerPrefs.SetString("StudentDoorData", studentDoorData);
                PlayerPrefs.Save();
                Debug.Log($"[CloudSaveManager] ✅ Cloud data saved to PlayerPrefs → Coins: {coins}, Keys: {keysCount}");
            }
        }
        else if (!string.IsNullOrEmpty(localTimestampStr))
        {
            // No cloud data, but local data exists. Upload local data.
            Debug.Log("[CloudSaveManager] No cloud data found, but local timestamp exists. Uploading local data to cloud.");
            await SaveAllPlayerDataToCloud();
        }
        else
        {
            // No cloud or local data. Initialize with defaults.
            Debug.Log("[CloudSaveManager] No save data found anywhere. Initializing with defaults.");
            PlayerPrefs.SetInt("CurrentCoins", 0);
            PlayerPrefs.SetInt("KeysCount", 5);
            PlayerPrefs.SetString("StudentDoorData", "");
            PlayerPrefs.Save();
            SaveTimestamp();
            Debug.Log("[CloudSaveManager] Defaults initialized: Keys=5, Coins=0");
        }
    }

    private void OnApplicationQuit()
    {
        // This is a fire-and-forget call. It's not guaranteed to complete before the app closes,
        // but it's the standard way to handle async saves on quit.
        _ = SaveAllPlayerDataToCloud();
    }

    public static void SaveTimestamp()
    {
        string timestamp = System.DateTime.UtcNow.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PlayerPrefs.SetString("LastLocalSaveTimestamp", timestamp);
    }
}
