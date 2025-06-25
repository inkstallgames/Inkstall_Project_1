using UnityEngine;

public class DisableOnPropSpawn : MonoBehaviour
{
    [Header("Spawn Points to Monitor")]
    public Transform[] spawnPoints;

    [Header("Interaction Script to Disable")]
    public Behaviour interactionComponent; // e.g. DoorInteraction, DrawerOpen, etc.

    [Header("Check Settings")]
    public float checkInterval = 1f; // How often to check (seconds)
    public bool disableIfAllSpawned = false; // Disable only if ALL spawn points are filled

    private bool hasDisabled = false;

    void Start()
    {
        if (interactionComponent == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No interaction script assigned.");
            return;
        }

        InvokeRepeating(nameof(CheckSpawnPoints), 0f, checkInterval);
    }

    void CheckSpawnPoints()
    {
        if (hasDisabled || interactionComponent == null) return;

        if (disableIfAllSpawned)
        {
            // Disable only if ALL spawn points have at least one child
            foreach (Transform point in spawnPoints)
            {
                if (point.childCount == 0)
                    return; // At least one spawn point is empty — don't disable yet
            }
        }
        else
        {
            // Disable if ANY spawn point has a prop
            foreach (Transform point in spawnPoints)
            {
                if (point.childCount > 0)
                {
                    DisableInteraction();
                    return;
                }
            }
            return;
        }

        DisableInteraction();
    }

    void DisableInteraction()
    {
        interactionComponent.enabled = false;
        hasDisabled = true;
        Debug.Log($"[{gameObject.name}] Interaction disabled due to spawned prop.");
    }
}
