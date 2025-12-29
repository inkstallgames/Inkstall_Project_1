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
        await LoadPlayerData();
        OnCloudDataLoaded?.Invoke();
    }

    public async Task SaveAllPlayerDataToCloud()
    {
        int coins = PlayerPrefs.GetInt("CurrentCoins", 0);
        int keys = PlayerPrefs.GetInt("KeysCount", 3);
        string studentDoorData = PlayerPrefs.GetString("StudentDoorData", "");

        await SavePlayerData(coins, keys, studentDoorData);
    }

    public async Task LoadPlayerData()
    {
        var keys = new HashSet<string> { "coins", "keys", "studentDoorData" };
        var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        int coins = result.ContainsKey("coins")
            ? result["coins"].Value.GetAs<int>()
            : 0;

        int keysCount = result.ContainsKey("keys")
            ? result["keys"].Value.GetAs<int>()
            : 3;

        string studentDoorData = result.ContainsKey("studentDoorData")
            ? result["studentDoorData"].Value.GetAsString()
            : "";

        PlayerPrefs.SetInt("CurrentCoins", coins);
        PlayerPrefs.SetInt("KeysCount", keysCount);
        PlayerPrefs.SetString("StudentDoorData", studentDoorData);
        PlayerPrefs.Save();

        Debug.Log($"✅ Cloud Save Loaded → Coins: {coins}, Keys: {keysCount}");
    }
}
