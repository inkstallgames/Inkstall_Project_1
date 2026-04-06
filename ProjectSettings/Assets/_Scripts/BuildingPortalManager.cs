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

        // Sort portals by building number in descending order to find the highest unlocked one first.
        var sortedPortals = buildingPortals.OrderByDescending(p => p.buildingNumber).ToArray();

        GameObject portalToActivate = null;

        // Find the highest-level portal that is unlocked.
        foreach (var portal in sortedPortals)
        {
            if (portal.portalObject == null)
            {
                Debug.LogError($"Portal object for Building {portal.buildingNumber} is not assigned!");
                continue;
            }

            bool isUnlocked;
            if (portal.buildingNumber == 1)
            {
                // Building 1 is the default fallback.
                isUnlocked = true;
            }
            else
            {
                // For other buildings, check if the required door is completed.
                DoorData requiredDoor = ProgressManager.Instance?.GetDoorData(portal.requiredDoorId);
                isUnlocked = requiredDoor?.isRoomCompleted ?? false;
            }

            if (isUnlocked)
            {
                // We found the highest unlocked portal. This is the one to activate.
                portalToActivate = portal.portalObject;
                Debug.Log($"Highest unlocked portal is for Building {portal.buildingNumber}. This will be activated.");
                break; // Exit the loop as we've found our target.
            }
        }

        // Now, iterate through all portals to set their active state.
        foreach (var portal in buildingPortals)
        {
            if (portal.portalObject != null)
            {
                // Activate the portal if it's the one we identified, otherwise deactivate it.
                bool shouldBeActive = (portal.portalObject == portalToActivate);
                portal.portalObject.SetActive(shouldBeActive);
                Debug.Log($"Building {portal.buildingNumber} portal active: {shouldBeActive}");
            }
        }
    }

    private void OnDestroy()
    {
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }
}
