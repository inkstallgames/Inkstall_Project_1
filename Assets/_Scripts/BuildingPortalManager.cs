using UnityEngine;
using System.Collections;
using System.Linq;

public class BuildingPortalManager : MonoBehaviour
{
    [System.Serializable]
    public class BuildingPortal
    {
        public int buildingNumber;  // 1, 2, 3, or 4
        public GameObject portalObject;  // The portal GameObject to enable/disable
        public int requiredDoorId;  // The door ID that needs to be completed to unlock this building
    }

    [SerializeField] private BuildingPortal[] buildingPortals;
    
    private void Start()
    {
        // If ProgressManager is already loaded, check doors immediately
        if (ProgressManager.Instance != null && ProgressManager.Instance.IsDataLoaded())
        {
            CheckBuildingPortals();
        }
        else
        {
            // Otherwise, wait for ProgressManager to load data
            ProgressManager.OnDataLoaded += OnProgressDataLoaded;
        }
    }

    private void OnProgressDataLoaded()
    {
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
        CheckBuildingPortals();
    }

    private void CheckBuildingPortals()
    {
        if (buildingPortals == null || buildingPortals.Length == 0)
        {
            Debug.LogError("No building portals assigned in BuildingPortalManager!");
            return;
        }

        foreach (var portal in buildingPortals)
        {
            if (portal.portalObject == null)
            {
                Debug.LogError($"Portal object for Building {portal.buildingNumber} is not assigned!");
                continue;
            }

            if (portal.buildingNumber == 1)
            {
                // Building 1 is always active
                portal.portalObject.SetActive(true);
                Debug.Log($"Building 1 portal: Always enabled");
            }
            else
            {
                // For buildings 2-4, check if the required door is completed
                DoorData requiredDoor = ProgressManager.Instance?.GetDoorData(portal.requiredDoorId);
                bool isUnlocked = requiredDoor?.isRoomCompleted ?? false;
                
                // Enable the portal if its required door is completed
                portal.portalObject.SetActive(isUnlocked);
                Debug.Log($"Building {portal.buildingNumber} portal: Required door {portal.requiredDoorId} completed: {isUnlocked} | Portal active: {isUnlocked}");
            }
        }
    }

    private void OnDestroy()
    {
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }
}
