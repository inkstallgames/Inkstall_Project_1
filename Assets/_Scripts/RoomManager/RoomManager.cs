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
        // Update UI one last time when room is completed
        UpdateRemainingAliensUI();
        
        if (aliensRemaining <= 0)
        {
            Debug.Log("[RoomManager] Room completed! All aliens caught.");
            
            // Save room completion status to online database
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
            {
                int currentDoorID = thisRoomDoor.GetDoorID();
                
                // Use the MarkRoomAsCompleted method which handles both current and next door updates
                Debug.Log($"[RoomManager] <color=yellow>STARTING DATABASE UPDATE</color> - Marking room with door {currentDoorID} as completed");
                ProgressManager.Instance.MarkRoomAsCompleted(currentDoorID);
                
                Debug.Log($"[RoomManager] <color=green>DATABASE UPDATE INITIATED</color> - Called MarkRoomAsCompleted for door {currentDoorID}");
                
                // Update the door objects to reflect the new state immediately
                // This ensures visual feedback even before the database update completes
                Debug.Log($"[RoomManager] <color=cyan>LOCAL UPDATE</color> - Setting door {currentDoorID} to: isUnlockable=false, isRoomCompleted=true");
                thisRoomDoor.isUnlockable = false;
                thisRoomDoor.isRoomCompleted = true;
                thisRoomDoor.UpdateDoorVisuals();
                Debug.Log($"[RoomManager] <color=cyan>LOCAL UPDATE</color> - Door {currentDoorID} visuals updated");
                
                // If we have a next door to unlock, update it visually too
                if (nextDoorToUnlock != null)
                {
                    int nextDoorID = nextDoorToUnlock.GetDoorID();
                    Debug.Log($"[RoomManager] <color=cyan>LOCAL UPDATE</color> - Setting next door {nextDoorID} to: isUnlockable=true, isRoomCompleted=false");
                    nextDoorToUnlock.isUnlockable = true;
                    nextDoorToUnlock.isRoomCompleted = false;
                    nextDoorToUnlock.UpdateDoorVisuals();
                    
                    Debug.Log($"[RoomManager] <color=cyan>LOCAL UPDATE</color> - Next door {nextDoorID} visuals updated");
                }
                else
                {
                    Debug.Log("[RoomManager] No next door found to unlock");
                }
                
                // Force a refresh of all doors in the scene to ensure consistency
                StartCoroutine(RefreshAllDoorsAfterDelay(1.5f));
            }
            else
            {
                Debug.LogWarning("[RoomManager] ProgressManager not ready or door reference missing, couldn't save room completion status");
            }
            
            // Add 200 coins/points for completing the room
            if (CoinsManager.Instance != null)
            {
                CoinsManager.Instance.AddCoins(200, "Room Completed");
                Debug.Log("[RoomManager] Added 200 coins for completing room " + roomID);
            }
            else
            {
                Debug.LogWarning("[RoomManager] CoinsManager instance not found, couldn't add coins");
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
                Debug.Log("[RoomManager] Room completed! Level win triggered.");
            }
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
        Debug.Log($"[RoomManager] <color=orange>REFRESH SCHEDULED</color> - Will refresh all doors after {delay} seconds");
        yield return new WaitForSeconds(delay);
        
        if (ProgressManager.Instance != null)
        {
            Debug.Log($"[RoomManager] <color=orange>REFRESH STARTED</color> - Refreshing all doors in scene to ensure consistency");
            
            // Find all door interactions in the scene
            DoorInteraction[] allDoors = FindObjectsOfType<DoorInteraction>();
            Debug.Log($"[RoomManager] <color=orange>REFRESH INFO</color> - Found {allDoors.Length} doors in scene to refresh");
            
            foreach (DoorInteraction door in allDoors)
            {
                if (door != null)
                {
                    // Update door state from database
                    Debug.Log($"[RoomManager] <color=orange>REFRESH DOOR</color> - Refreshing door {door.doorID} from database");
                    ProgressManager.Instance.UpdateDoorInteraction(door);
                }
            }
            
            Debug.Log($"[RoomManager] <color=orange>REFRESH COMPLETE</color> - All doors have been refreshed from database");
        }
    }
}
