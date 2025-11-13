using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class FloorAccessRule
{
    public string floorName; // For easier identification in the Inspector
    public List<int> requiredDoorIDs;
    public GameObject accessCollider;
}

public class FloorAccessManager : MonoBehaviour
{
    public static FloorAccessManager Instance { get; private set; }

    [Header("Floor Access Rules")]
    [SerializeField] private List<FloorAccessRule> floorCondition = new List<FloorAccessRule>();


    private void Start()
    {
        // Subscribe to the OnDataLoaded event
        ProgressManager.OnDataLoaded += UpdateFloorAccess;
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        ProgressManager.OnDataLoaded -= UpdateFloorAccess;
    }

    public void UpdateFloorAccess()
    {
        Debug.Log("[FloorAccessManager] UpdateFloorAccess called");
        
        if (ProgressManager.Instance == null)
        {
            Debug.LogError("[FloorAccessManager] ProgressManager Instance is null!");
            return;
        }
        
        if (!ProgressManager.Instance.IsDataLoaded())
        {
            Debug.LogWarning("[FloorAccessManager] ProgressManager data not loaded yet.");
            return;
        }

        Debug.Log($"[FloorAccessManager] Checking {floorCondition.Count} floor rules...");
        
        // Check each floor rule
        for (int i = 0; i < floorCondition.Count; i++)
        {
            var condition = floorCondition[i];
            Debug.Log($"[FloorAccessManager] Checking rule {i}: {condition.floorName} - {condition.requiredDoorIDs.Count} doors");
            CheckFloorRule(condition);
        }
    }

    private void CheckFloorRule(FloorAccessRule rule)
    {
        if (rule.accessCollider == null)
        {
            Debug.LogError($"[FloorAccessManager] Rule '{rule.floorName}' has no collider assigned!");
            return;
        }
        
        if (rule.requiredDoorIDs.Count == 0)
        {
            Debug.LogWarning($"[FloorAccessManager] Rule '{rule.floorName}' has no door IDs specified!");
            return;
        }

        Debug.Log($"[FloorAccessManager] Checking rule for {rule.floorName} - {rule.requiredDoorIDs.Count} doors");
        
        // Check if all required doors for this rule are completed
        bool allDoorsCompleted = true;
        foreach (var doorId in rule.requiredDoorIDs)
        {
            DoorData door = ProgressManager.Instance.GetDoorData(doorId);
            if (door == null)
            {
                Debug.LogError($"[FloorAccessManager] Door with ID {doorId} not found in ProgressManager!");
                allDoorsCompleted = false;
                break;
            }
            
            Debug.Log($"[FloorAccessManager] Door {doorId} completed: {door.isRoomCompleted}");
            if (!door.isRoomCompleted)
            {
                allDoorsCompleted = false;
            }
        }

        if (allDoorsCompleted)
        {
            Debug.Log($"[FloorAccessManager] Unlocking access for {rule.floorName}. Disabling collider: {rule.accessCollider.name}");
            rule.accessCollider.SetActive(false);
        }
        else
        {
            Debug.Log($"[FloorAccessManager] Locking access for {rule.floorName}. Enabling collider: {rule.accessCollider.name}");
            rule.accessCollider.SetActive(true);
        }
    }
}
