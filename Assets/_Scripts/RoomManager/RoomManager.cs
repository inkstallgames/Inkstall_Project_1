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
            // Removed all debug logs
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
            // Removed all debug logs
        }
        
        // Check if room is already completed in database
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
        {
            // Check if this door is marked as completed in the online database
            int doorID = thisRoomDoor.GetDoorID();
            var doorData = ProgressManager.Instance.GetDoorData(doorID);
            
            if (doorData != null && doorData.isRoomCompleted)
            {
                // Removed all debug logs
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
            
            // Removed all debug logs
            
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
            int currentDoorID = thisRoomDoor != null ? thisRoomDoor.GetDoorID() : -1;
            bool isValidDoor = currentDoorID >= 1 && currentDoorID <= 24;
            
            if (isValidDoor)
            {
                // Removed all debug logs
            }
            
            // Save room completion status to online database
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
            {
                // Use the MarkRoomAsCompleted method which handles both current and next door updates
                if (isValidDoor)
                {
                    // Removed all debug logs
                }
                
                ProgressManager.Instance.MarkRoomAsCompleted(currentDoorID);
                
                // Update the door objects to reflect the new state immediately
                // This ensures visual feedback even before the database update completes
                thisRoomDoor.isUnlockable = false;
                thisRoomDoor.isRoomCompleted = true;
                thisRoomDoor.UpdateDoorVisuals();
                
                // If we have a next door to unlock, update it visually too
                if (nextDoorToUnlock != null)
                {
                    int nextDoorID = nextDoorToUnlock.GetDoorID();
                    bool isNextDoorValid = nextDoorID >= 1 && nextDoorID <= 24;
                    
                    if (isNextDoorValid)
                    {
                        // Removed all debug logs
                    }
                    
                    nextDoorToUnlock.isUnlockable = true;
                    nextDoorToUnlock.isRoomCompleted = false;
                    nextDoorToUnlock.UpdateDoorVisuals();
                }
                
                // Force a refresh of all doors in the scene to ensure consistency
                StartCoroutine(RefreshAllDoorsAfterDelay(1.5f));
            }
            else if (isValidDoor)
            {
                // Removed all debug logs
                
                // Try to find ProgressManager if it's not available
                if (ProgressManager.Instance == null)
                {
                    // Removed all debug logs
                    // This will create the instance if it doesn't exist
                    var progressManager = ProgressManager.Instance;
                    
                    // Try again after ensuring the instance exists
                    if (progressManager != null && thisRoomDoor != null)
                    {
                        // Removed all debug logs
                        progressManager.MarkRoomAsCompleted(currentDoorID);
                    }
                }
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
                // Removed all debug logs
            }
            else if (GameManager.Instance != null)
            {
                // Call LevelWin for non-final rooms too
                GameManager.Instance.LevelWin();
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
        // Removed all debug logs
        yield return new WaitForSeconds(delay);
        
        if (ProgressManager.Instance != null)
        {
            // Find all door interactions in the scene
            DoorInteraction[] allDoors = FindObjectsOfType<DoorInteraction>();
            // Removed all debug logs
            
            foreach (DoorInteraction door in allDoors)
            {
                if (door != null)
                {
                    // Update door state from database
                    ProgressManager.Instance.UpdateDoorInteraction(door);
                }
            }
            
            // Removed all debug logs
        }
    }
}
