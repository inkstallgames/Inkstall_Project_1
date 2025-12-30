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
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
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
        try
        {
            await LoadPlayerData();
        }
        catch (CloudSaveException e)
        {
            Debug.LogWarning($"Failed to load data from cloud. Loading local data instead. Error: {e.Message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"An unexpected error occurred while loading cloud data: {e.Message}");
        }
        finally
        {
            // This will trigger managers to load from PlayerPrefs, using either
            // the fresh cloud data or the existing local data if the cloud failed.
            OnCloudDataLoaded?.Invoke();
        }
    }

    public async Task SaveAllPlayerDataToCloud()
    {
        int coins = PlayerPrefs.GetInt("CurrentCoins", 0);
        int keys = PlayerPrefs.GetInt("KeysCount", 5);
        string studentDoorData = PlayerPrefs.GetString("StudentDoorData", "");

        await SavePlayerData(coins, keys, studentDoorData);
    }

    public async Task LoadPlayerData()
    {
        // Load cloud data with metadata to get server timestamp
        var cloudData = await CloudSaveService.Instance.Data.Player.LoadAllAsync();

        // Get local timestamp
        string localTimestampStr = PlayerPrefs.GetString("LastLocalSaveTimestamp", null);
        System.DateTime.TryParse(localTimestampStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out System.DateTime localTimestamp);

        if (cloudData != null && cloudData.Count > 0)
        {
            // Find the most recent server timestamp from all cloud variables
            var validTimestamps = cloudData.Values.Where(v => v.Modified.HasValue).Select(v => v.Modified.Value);
            System.DateTime cloudTimestamp = validTimestamps.Any() ? validTimestamps.Max() : System.DateTime.MinValue;

            if (localTimestamp > cloudTimestamp)
            {
                // Local data is newer, so upload it to the cloud
                Debug.Log("Local data is newer. Uploading to cloud.");
                await SaveAllPlayerDataToCloud();
            }
            else
            {
                // Cloud data is newer or same, so load it
                Debug.Log("Cloud data is newer. Loading from cloud.");
                int coins = cloudData.ContainsKey("coins") ? cloudData["coins"].Value.GetAs<int>() : 0;
                int keysCount = cloudData.ContainsKey("keys") ? cloudData["keys"].Value.GetAs<int>() : 5;
                string studentDoorData = cloudData.ContainsKey("studentDoorData") ? cloudData["studentDoorData"].Value.GetAsString() : "";

                PlayerPrefs.SetInt("CurrentCoins", coins);
                PlayerPrefs.SetInt("KeysCount", keysCount);
                PlayerPrefs.SetString("StudentDoorData", studentDoorData);
                PlayerPrefs.Save();
                Debug.Log($"✅ Cloud Save Loaded → Coins: {coins}, Keys: {keysCount}");
            }
        }
        else if (!string.IsNullOrEmpty(localTimestampStr))
        {
            // No cloud data, but local data exists. Upload local data.
            Debug.Log("No cloud data found. Uploading local data to cloud.");
            await SaveAllPlayerDataToCloud();
        }
        else
        {
            // No cloud or local data. Game will start with default values.
            Debug.Log("No save data found anywhere. Starting fresh.");
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
