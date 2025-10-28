using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// This script is responsible for managing the room logic and turning props into aliens
public class RoomManager : MonoBehaviour
{
    private string alienTag = "AlienProp";   // Tag to assign
    [SerializeField] private int numberOfAliens;          // How many props to turn into aliens
    [SerializeField] private int aliensRemaining;                 // Counter for remaining aliens
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI remainingAliensText; // UI Text to display remaining aliens count
    
    [Header("Door & Room Setting")]
    [SerializeField] private DoorInteraction thisRoomDoor;
    [SerializeField] private DoorInteraction nextDoorToUnlock;  // Reference to the next door to unlock
    [SerializeField] private bool isFinalRoom = false;          // Is this the final room in the level?
    public string roomID;                     // Unique identifier for this room
    
    private List<GameObject> alienProps = new List<GameObject>();  // Track all alien props

    void Start()
    {
        // Validate door references
        if (thisRoomDoor == null)
        {
            Debug.LogError($"[RoomManager] {gameObject.name} is missing thisRoomDoor reference!");
            return;
        }

        // Collect all children
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
        {
            children.Add(child);
        }

        // Shuffle the list randomly
        for (int i = 0; i < children.Count; i++)
        {
            Transform temp = children[i];
            int randomIndex = Random.Range(i, children.Count);
            children[i] = children[randomIndex];
            children[randomIndex] = temp;
        }

        // Select first N children as aliens
        int alienCount = Mathf.Min(numberOfAliens, children.Count);  // Avoid overflow
        aliensRemaining = alienCount;  // Initialize counter
        UpdateRemainingAliensUI();
        
        // Generate roomID if not set
        if (string.IsNullOrEmpty(roomID))
        {
            roomID = gameObject.name + "_" + gameObject.GetInstanceID();
            Debug.Log($"[RoomManager] Generated roomID: {roomID}");
        }
        
        // Check if room is already completed in database
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
        {
            // Check if this door is marked as completed in the online database
            int doorID = thisRoomDoor.GetDoorID();
            var doorData = ProgressManager.Instance.GetDoorData(doorID);
            
            if (doorData != null && doorData.isRoomCompleted)
            {
                Debug.Log($"[RoomManager] Room with door ID {doorID} is already completed in database");
                aliensRemaining = 0; // Room is already completed
            }
        }

        for (int i = 0; i < alienCount; i++)
        {
            GameObject obj = children[i].gameObject;

            // Set tag
            obj.tag = alienTag;
            // Add behaviour
            if (obj.GetComponent<AlienPropBehaviour>() == null)
            {
                obj.AddComponent<AlienPropBehaviour>();
            }
            
            // Add to tracking list
            alienProps.Add(obj);
        }
        
        // Register with BombPhysics to receive notifications
        BombPhysics.OnAlienDestroyed += OnAlienCaught;
    }
    
    private void OnDestroy()
    {
        // Unregister from event when destroyed
        BombPhysics.OnAlienDestroyed -= OnAlienCaught;
    }
    
    // Called when an alien is caught
    public void OnAlienCaught(GameObject alienObject)
    {
        // Check if this alien belongs to this room
        if (alienProps.Contains(alienObject))
        {
            alienProps.Remove(alienObject);
            aliensRemaining--;
            
            Debug.Log($"Alien caught! {aliensRemaining} aliens remaining in room {gameObject.name}");
            
            // Update the remaining aliens UI
            UpdateRemainingAliensUI();
            
            // Check if all aliens are caught
            CheckRoomCompletion();
        }
    }
    
    // Check if all aliens in this room have been caught
    private void CheckRoomCompletion()
    {
        Debug.Log($"[ROOM_MANAGER] [ALIEN_COUNT] RoomManager.CheckRoomCompletion() called. Aliens remaining: {aliensRemaining}");

        // Update UI one last time when room is completed
        UpdateRemainingAliensUI();

        if (aliensRemaining <= 0)
        {
            StartCoroutine(CompleteRoomSequence());
        }
    }

    private IEnumerator CompleteRoomSequence()
    {
        int currentDoorID = thisRoomDoor != null ? thisRoomDoor.GetDoorID() : -1;
        bool isValidDoor = currentDoorID >= 1 && currentDoorID <= 24;

        Debug.Log($"[ROOM_MANAGER] [ROOM_COMPLETE] Room {roomID} completed! All aliens caught. Door ID: {currentDoorID}, Valid Door: {isValidDoor}");

        // Wait until ProgressManager has loaded the data
        while (ProgressManager.Instance == null || !ProgressManager.Instance.IsDataLoaded())
        {
            Debug.Log("[ROOM_MANAGER] Waiting for ProgressManager to load data...");
            yield return new WaitForSeconds(0.5f);
        }

        // Save room completion status to online database
        if (ProgressManager.Instance != null && thisRoomDoor != null)
        {
            Debug.Log($"[ROOM_MANAGER] [DB_UPDATE] Starting database update for room {roomID} with door {currentDoorID}");

            // Determine the next door ID to unlock
            int nextDoorID = -1;
            if (currentDoorID < 24 && currentDoorID > 0 && currentDoorID != 6 && currentDoorID != 12 && currentDoorID != 18)
            {
                nextDoorID = currentDoorID + 1;
                Debug.Log($"[ROOM_MANAGER] [NEXT_DOOR] Next door to unlock: {nextDoorID}");
            }
            else
            {
                Debug.Log($"[ROOM_MANAGER] [NEXT_DOOR] No next door to unlock (current door is {currentDoorID})");
            }

            StartCoroutine(UpdateDoorsWithDelay(currentDoorID, nextDoorID));
        }

        // Add 200 coins/points for completing the room
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.AddCoins(200, "Room Completed");
        }

        // If this is the final room, trigger game win
        if (isFinalRoom && GameManager.Instance != null)
        {
            GameManager.Instance.LevelWin();
            Debug.Log("[RoomManager] Final room completed! Level win triggered.");
        }
        else if (GameManager.Instance != null)
        {
            // Call LevelWin for non-final rooms too
            GameManager.Instance.LevelWin();
        }
    }
    
    // Updates the remaining aliens count UI
    private void UpdateRemainingAliensUI()
    {
        if (remainingAliensText != null)
        {
            remainingAliensText.text = aliensRemaining.ToString();
        }
    }
    
    // Refresh all doors in the scene after a delay to ensure database changes are reflected
    private IEnumerator RefreshAllDoorsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log("[RoomManager] Refreshing all doors from database");
        
        // Find all door interactions in the scene
        DoorInteraction[] doors = FindObjectsOfType<DoorInteraction>();
        
        foreach (DoorInteraction door in doors)
        {
            if (door != null)
            {
                int doorId = door.GetDoorID();
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[RoomManager] Refreshing door {doorId} from database");
                    
                    // Update door state from database
                    ProgressManager.Instance.UpdateDoorInteraction(door);
                }
            }
        }
        
        Debug.Log("[RoomManager] Door refresh from database complete");
    }
    
    // Update doors with a small delay to ensure student ID is properly set
    private IEnumerator UpdateDoorsWithDelay(int currentDoorID, int nextDoorID)
    {
        Debug.Log($"[UpdateDoorsWithDelay] Coroutine started for currentDoorID: {currentDoorID} and nextDoorID: {nextDoorID}");
        // Wait a short time to ensure everything is initialized
        yield return new WaitForSeconds(0.2f);

        // Update the current door directly in the database
        Debug.Log($"[ROOM_MANAGER] [DB_DIRECT_UPDATE] Updating current door {currentDoorID} status: isUnlockable=false, isRoomCompleted=true");
        if (ProgressManager.Instance != null)
        {
            StartCoroutine(ProgressManager.Instance.UpdateDoorStatusDirect(currentDoorID, false, true));
        }
        else
        {
            Debug.LogError("[UpdateDoorsWithDelay] ProgressManager.Instance is null. Cannot update current door.");
        }

        // Wait a bit between requests to avoid overwhelming the server
        yield return new WaitForSeconds(0.5f);

        // If there's a next door to unlock, update it directly too
        if (nextDoorID > 0)
        {
            Debug.Log($"[ROOM_MANAGER] [DB_DIRECT_UPDATE] Updating next door {nextDoorID} status: isUnlockable=true, isRoomCompleted=false");
            if (ProgressManager.Instance != null)
            {
                StartCoroutine(ProgressManager.Instance.UpdateDoorStatusDirect(nextDoorID, true, false));
            }
            else
            {
                Debug.LogError("[UpdateDoorsWithDelay] ProgressManager.Instance is null. Cannot update next door.");
            }
        }
        else
        {
            Debug.Log("[UpdateDoorsWithDelay] No valid next door to unlock.");
        }

        // Wait a bit more before calling the backward compatibility method
        yield return new WaitForSeconds(0.5f);

        // Also call MarkRoomAsCompleted for backward compatibility
        Debug.Log($"[ROOM_MANAGER] [DB_UPDATE] Also calling ProgressManager.MarkRoomAsCompleted({currentDoorID}) for backward compatibility");
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.MarkRoomAsCompleted(currentDoorID);
        }
        else
        {
            Debug.LogError("[UpdateDoorsWithDelay] ProgressManager.Instance is null. Cannot call MarkRoomAsCompleted.");
        }
        Debug.Log("[UpdateDoorsWithDelay] Coroutine finished.");
    }
    
    // Update local door visuals without database updates
    private void UpdateLocalDoorVisuals(int currentDoorID)
    {
        // Update the current door visually
        if (thisRoomDoor != null)
        {
            Debug.Log($"[ROOM_MANAGER] [LOCAL_UPDATE] Updating local door {currentDoorID} visuals only (database update failed)");
            thisRoomDoor.isUnlockable = false;
            thisRoomDoor.isRoomCompleted = true;
            thisRoomDoor.UpdateDoorVisuals();
        }
        
        // If we have a next door to unlock, update it visually too
        if (nextDoorToUnlock != null)
        {
            int nextVisualDoorID = nextDoorToUnlock.GetDoorID();
            Debug.Log($"[ROOM_MANAGER] [LOCAL_UPDATE] Updating next door {nextVisualDoorID} visuals only (database update failed)");
            nextDoorToUnlock.isUnlockable = true;
            nextDoorToUnlock.UpdateDoorVisuals();
        }
    }
}
