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
    private bool isFinalRoom = false;          // Is this the final room in the level?
    private string roomID;                     // Unique identifier for this room
    private int doorId;                        // Door ID for ProgressManager
    
    private List<GameObject> alienProps = new List<GameObject>();  // Track all alien props

    void Start()
    {
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
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
        {
            // Check if this door is marked as completed in the online database
            var doorData = ProgressManager.Instance.GetDoorData(doorId);
            if (doorData != null && doorData.isRoomCompleted)
            {
                Debug.Log($"[RoomManager] Room with door ID {doorId} already completed. Unlocking next door.");
                if (nextDoorToUnlock != null)
                {
                    thisRoomDoor.SetRoomCompleted(true);
                    nextDoorToUnlock.SetUnlockable(true);    
                }
                return;
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
            Debug.Log($"All aliens caught in room {gameObject.name}! Room completed!");
            
            // Save room completion status to online database
            if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded)
            {
                // Mark the room as completed in the online database
                ProgressManager.Instance.MarkRoomAsCompleted(doorId);
                Debug.Log($"[RoomManager] Saved room completion status for door ID {doorId} to online database");
            }
            else
            {
                Debug.LogWarning("[RoomManager] ProgressManager not ready, couldn't save room completion status");
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
            
            // Unlock the next door if specified
            if (nextDoorToUnlock != null)
            {
                nextDoorToUnlock.SetUnlockable(true);
                nextDoorToUnlock.SetRoomCompleted(true);
                Debug.Log($"Next door {nextDoorToUnlock.gameObject.name} is now unlockable!");
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
