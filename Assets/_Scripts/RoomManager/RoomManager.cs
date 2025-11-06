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
    [SerializeField] private TextMeshProUGUI remainingAliensTextCount; // UI Text to display remaining aliens count
    
    [Header("Door & Room Setting")]
    [SerializeField] private DoorInteraction thisRoomDoor;
    [SerializeField] private DoorInteraction nextDoorToUnlock;  // Reference to the next door to unlock
    [SerializeField] private bool isFinalRoom = false;          // Is this the final room in the level?
    
    private List<GameObject> alienProps = new List<GameObject>();  // Track all alien props

    void Start()
    {
        // Validate door references
        if (thisRoomDoor == null)
        {
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
        
        // Check if room is already completed in database
        if (ProgressManager.Instance != null && ProgressManager.Instance.isDataLoaded && thisRoomDoor != null)
        {
            // Check if this door is marked as completed in the online database
            int doorID = thisRoomDoor.GetDoorID();
            var doorData = ProgressManager.Instance.GetDoorData(doorID);
            
            if (doorData != null && doorData.isRoomCompleted)
            {
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
            
            UpdateRemainingAliensUI();
            
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
            StartCoroutine(CompleteRoomSequence());
        }
    }

    private IEnumerator CompleteRoomSequence()
    {
        int currentDoorID = thisRoomDoor != null ? thisRoomDoor.GetDoorID() : -1;
        if (currentDoorID < 1 || currentDoorID > 24) yield break;

        // Wait for ProgressManager to be ready
        while (ProgressManager.Instance == null || !ProgressManager.Instance.IsDataLoaded())
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Mark room as completed - this will handle all door updates
        ProgressManager.Instance.MarkRoomAsCompleted(currentDoorID);

        // Add coins for room completion
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.AddCoins(200, "Room Completed");
        }

        // Trigger game win
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LevelWin();
        }
    }
    
    // Updates the remaining aliens count UI
    private void UpdateRemainingAliensUI()
    {
        if (remainingAliensTextCount != null)
        {
            remainingAliensTextCount.text = aliensRemaining.ToString();
        }
    }
    
    // Refresh all doors in the scene after a delay to ensure database changes are reflected
    private IEnumerator RefreshAllDoorsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Find all door interactions in the scene
        DoorInteraction[] doors = FindObjectsOfType<DoorInteraction>();
        
        foreach (DoorInteraction door in doors)
        {
            if (door != null)
            {
                int doorId = door.GetDoorID();
                if (doorId >= 1 && doorId <= 24)
                {
                    ProgressManager.Instance.UpdateDoorInteraction(door);
                }
            }
        }
        
    }
    
}
