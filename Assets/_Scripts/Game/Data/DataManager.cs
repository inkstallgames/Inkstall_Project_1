using UnityEngine;
using System.Collections.Generic;

public class DataManager : MonoBehaviour
{
    // Singleton instance
    public static DataManager Instance { get; private set; }

    // Keys for PlayerPrefs
    private const string DOOR_UNLOCKABLE_KEY = "DoorUnlockable_";
    private const string ROOM_COMPLETED_KEY = "RoomCompleted_";

    private void Awake()
    {
        // Singleton pattern
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

    // Save door unlockable state
    public void SaveDoorUnlockableState(string doorID, bool isUnlockable)
    {
        PlayerPrefs.SetInt(DOOR_UNLOCKABLE_KEY + doorID, isUnlockable ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Load door unlockable state
    public bool LoadDoorUnlockableState(string doorID)
    {
        return PlayerPrefs.GetInt(DOOR_UNLOCKABLE_KEY + doorID, 0) == 1;
    }

    // Save room completion state
    public void SaveRoomCompletionState(string roomID, bool isCompleted)
    {
        PlayerPrefs.SetInt(ROOM_COMPLETED_KEY + roomID, isCompleted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Load room completion state
    public bool LoadRoomCompletionState(string roomID)
    {
        return PlayerPrefs.GetInt(ROOM_COMPLETED_KEY + roomID, 0) == 1;
    }

    // Reset all saved data (for testing or new game)
    public void ResetAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
