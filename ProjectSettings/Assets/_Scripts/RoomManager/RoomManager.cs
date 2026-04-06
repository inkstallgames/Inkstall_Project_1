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
    [SerializeField] private GameTimer roomTimer;  // Reference to the room's timer
    [SerializeField] public bool isFinalRoom = false;          // Is this the final room in the level?
    [SerializeField] public int maxBombs = 6; // Max bombs for this room
    [SerializeField] public float extraTimeAmount = 30f; // Extra time for this specific room

    [SerializeField] private int alienFoundCoins = 10; 
    [SerializeField] private string alienFoundDescription = "Alien Found"; 

    [SerializeField] private int roomCompletionCoins = 200;
    [SerializeField] private string roomCompletionDescription = "Room Completed";

    [Header("Tutorial")]
    [SerializeField] private Tutorial tutorial;
    
    private List<GameObject> alienProps = new List<GameObject>();  // Track all alien props
    private bool isRoomActive = false;  // Track if this room is active

    private void OnEnable()
    {
        // Subscribe to alien found event
        BombPhysics.OnAlienFound += OnAlienFound;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        BombPhysics.OnAlienFound -= OnAlienFound;
    }

    private void OnAlienFound()
    {
        // Award coins when an alien is found
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.AddCoins(alienFoundCoins, alienFoundDescription);
        }
    }

    void Start()
    {
        // Validate door references
        if (thisRoomDoor == null)
        {
            Debug.LogError("RoomManager: No door reference assigned!");
            return;
        }

        // Register with the door to be activated when it's unlocked
        thisRoomDoor.OnDoorUnlocked += ActivateRoom;
        
        // If the door is already unlocked, activate the room immediately
        if (thisRoomDoor.IsUnlocked())
        {
            ActivateRoom();
        }
        
        // Register with BombPhysics to receive notifications
        BombPhysics.OnAlienDestroyed += OnAlienCaught;
    }
    
    // Method to activate the room and set up aliens
    private void ActivateRoom()
    {
        if (isRoomActive) return; // Prevent multiple activations
        isRoomActive = true;

        // Set the extra time for this room
        if (GameTimer.instance != null)
        {
            GameTimer.instance.SetRoomExtraTime(extraTimeAmount);
        }

        if (ChemicalBombManager.Instance != null)
        {
            ChemicalBombManager.Instance.InitializeBombs(maxBombs);
            ChemicalBombManager.Instance.OnRoomEntered();
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
            
            // Debug log which GameObject is selected as alien
            Debug.Log($"[RoomManager] Alien #{i + 1} selected: {obj.name} (Position: {obj.transform.position})");
        }
    }
    
    private void OnDestroy()
    {
        // Unregister from events when destroyed
        BombPhysics.OnAlienDestroyed -= OnAlienCaught;
        
        // Unregister from door event
        if (thisRoomDoor != null)
        {
            thisRoomDoor.OnDoorUnlocked -= ActivateRoom;
        }
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
            
            // Complete tutorial task for finding an alien
            if (tutorial != null)
            {
                tutorial.CompleteTask(1);
            }
            
            // If all aliens are caught, stop the timer
            if (aliensRemaining <= 0)
            {
                if (roomTimer != null)
                {
                    roomTimer.StopTimer();
                }
            }
            
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
        
        // Update spawn point if this is a higher floor
        if (FloorSpawnManager.Instance != null)
        {
            FloorSpawnManager.Instance.OnRoomCompleted(currentDoorID);
        }

        // Add coins for room completion
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.AddCoins(roomCompletionCoins, roomCompletionDescription);
        }

        // Show interstitial ad after a small delay
        if (AdManager.Instance != null)
        {
            StartCoroutine(ShowInterstitialAfterDelay(0.5f));
        }
        else
        {
            Debug.LogWarning("AdManager instance not found. Skipping ad.");
        }

        // Trigger game win
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LevelWin();
        }
    }
    
    // Updates the remaining aliens count UI
    private IEnumerator ShowInterstitialAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AdManager.Instance.ShowInterstitialAd();
    }

    public int GetDoorID()
    {
        if (thisRoomDoor != null)
        {
            return thisRoomDoor.GetDoorID();
        }
        return -1;
    }

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
