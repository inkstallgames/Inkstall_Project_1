using UnityEngine;
using System.Collections;

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
            continue;

            // For building 1, enable by default (no requirements)
            if (portal.buildingNumber == 1)
            {
                portal.portalObject.SetActive(true);
                continue;
            }

            // For other buildings, check if the required door is completed
            DoorData doorData = ProgressManager.Instance.GetDoorData(portal.requiredDoorId);
            bool isUnlocked = doorData != null && doorData.isRoomCompleted;
            
            Debug.Log($"Building {portal.buildingNumber} portal: Required door {portal.requiredDoorId} completed: {isUnlocked}");
            portal.portalObject.SetActive(isUnlocked);
        }
    }

    private void OnDestroy()
    {
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }
}
