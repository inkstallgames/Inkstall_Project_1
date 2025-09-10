using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This script is responsible for managing the room logic and turning props into aliens
public class RoomManager : MonoBehaviour
{
    private string alienTag = "AlienProp";   // Tag to assign
    [SerializeField] private int numberOfAliens;          // How many props to turn into aliens
    [SerializeField] private int aliensRemaining;                 // Counter for remaining aliens
    
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
            
            // Check if all aliens are caught
            CheckRoomCompletion();
        }
    }
    
    // Check if all aliens in this room have been caught
    private void CheckRoomCompletion()
    {
        if (aliensRemaining <= 0)
        {
            // Save room completion status to online database
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
            {
                int currentDoorID = thisRoomDoor.GetDoorID();
                
                // Mark the room as completed in the online database
                thisRoomDoor.SetRoomCompleted(true);
                
                // If we have a next door to unlock, set it as unlockable
                if (nextDoorToUnlock != null)
                {
                    int nextDoorID = nextDoorToUnlock.GetDoorID();
                    nextDoorToUnlock.SetUnlockable(true);
                    Debug.Log($"[RoomManager] Next door {nextDoorToUnlock.gameObject.name} (ID: {nextDoorID}) is now unlockable!");
                    
                    // Use ProgressManager to update both doors in the database
                    ProgressManager.Instance.MarkRoomAsCompleted(currentDoorID);
                    Debug.Log($"[RoomManager] Saved room completion status for door ID {currentDoorID} to online database");
                }
                else
                {
                    // Just update the current door if there's no next door
                    ProgressManager.Instance.StartCoroutine(
                        ProgressManager.Instance.UpdateDoorStatus(currentDoorID, thisRoomDoor.isUnlockable, true)
                    );
                    Debug.Log($"[RoomManager] Updated completion status for door ID {currentDoorID} (no next door found)");
                }
            }
            else
            {
                Debug.LogWarning("[RoomManager] ProgressManager not ready or door reference missing, couldn't save room completion status");
            }
            
            // Add 200 coins/points for completing the room
            if (CoinsManager.Instance != null)
            {
                CoinsManager.Instance.AddCoins(200, "Room Completed");
                Debug.Log($"[RoomManager] Added 200 coins for completing room {roomID}");
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
}
