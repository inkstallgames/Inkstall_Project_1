using Fusion;
using UnityEngine;
using System.Linq;

/// <summary>
/// Handles spawning Mystery Boxes across the network. Only the server spawns them.
/// </summary>
public class MysteryBoxSpawner : NetworkBehaviour
{
    [Header("Spawning Settings")]
    [Tooltip("The NetworkObject prefab of the Mystery Box to spawn.")]
    [SerializeField] private NetworkObject mysteryBoxPrefab;
    
    [Tooltip("How often to spawn a new mystery box (in seconds).")]
    [SerializeField] private float spawnInterval = 15f;

    [Tooltip("Maximum number of mystery boxes that can exist at once.")]
    [SerializeField] private int maxBoxesAtOnce = 3;

    // We use a networked timer so the host reliably manages the spawn intervals
    [Networked] private TickTimer spawnTimer { get; set; }

    [Tooltip("Drag and drop your spawn points from the Hierarchy into this list.")]
    [SerializeField] private MysteryBoxSpawnPoint[] spawnPoints;

    // Track the currently spawned box
    private NetworkObject currentSpawnedBox;

    public override void Spawned()
    {
        // Only the host/server needs to handle spawning logic
        if (!Object.HasStateAuthority) return;

        if (spawnPoints.Length > 0 && mysteryBoxPrefab != null)
        {
            // Start the timer
            spawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
        }
        else
        {
            Debug.LogWarning("[MysteryBoxSpawner] No spawn points found or prefab is missing!");
        }
    }

    private int lastLoggedSecond = -1;

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // If the box is still in the world, pause the countdown.
        // This ensures a new box spawns exactly 15 seconds AFTER the old one is collected/destroyed.
        if (currentSpawnedBox != null)
        {
            spawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            lastLoggedSecond = -1;
            return;
        }

        // Print a countdown every second without spamming the console
        if (spawnTimer.IsRunning)
        {
            float? remaining = spawnTimer.RemainingTime(Runner);
            if (remaining.HasValue)
            {
                int currentSecond = Mathf.CeilToInt(remaining.Value);
                if (currentSecond != lastLoggedSecond && currentSecond > 0 && currentSecond <= 5) 
                {
                    // Only logging the last 5 seconds to prevent spam, or you can remove the "&& currentSecond <= 5" to log every second.
                    Debug.Log($"[MysteryBoxSpawner] Spawning in {currentSecond}...");
                    lastLoggedSecond = currentSecond;
                }
            }
        }

        // When the timer finishes, spawn a box and restart the timer
        if (spawnTimer.Expired(Runner))
        {
            SpawnBox();
            Debug.Log($"[MysteryBoxSpawner] Timer restarted. Next spawn in {spawnInterval} seconds.");
            spawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
            lastLoggedSecond = -1;
        }
    }

    private void SpawnBox()
    {
        if (spawnPoints.Length == 0 || mysteryBoxPrefab == null) return;

        // Pick a random spawn point
        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform spawnTransform = spawnPoints[randomIndex].transform;

        // Spawn it across the network and keep track of it
        currentSpawnedBox = Runner.Spawn(mysteryBoxPrefab, spawnTransform.position, spawnTransform.rotation, null);
        
        Debug.Log($"[MysteryBoxSpawner] Successfully spawned a Mystery Box at: {spawnTransform.position}");
    }
}
