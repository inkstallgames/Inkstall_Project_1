using UnityEngine;
using System.Collections.Generic;

public class DisableOnPropSpawn : MonoBehaviour
{
    [Header("Spawn Points to Monitor")]
    public Transform[] spawnPoints;

    [Header("Interaction Components to Disable")]
    public Behaviour[] interactionComponents; // Support multiple components (e.g. both DoorInteraction and QuizTrigger)

    [Header("Check Settings")]
    public float checkInterval = 1f; // How often to check (seconds)
    public bool disableIfAllSpawned = false; // Disable only if ALL spawn points are filled
    public bool checkOnStart = true; // Check immediately on start
    public bool stopCheckingAfterQuiz = true; // Stop checking spawn points after quiz is completed

    [Header("Audio")]
    [SerializeField] private AudioClip lockedSound;
    private AudioSource audioSource;

    private bool hasDisabled = false;
    private QuizTrigger quizTrigger;
    private bool quizCompleted = false; // Track if quiz has been completed

    void Start()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Find QuizTrigger component if it exists
        quizTrigger = GetComponent<QuizTrigger>();
        
        // Validate interaction components
        if (interactionComponents == null || interactionComponents.Length == 0)
        {
            // Try to auto-populate with common interaction components
            List<Behaviour> components = new List<Behaviour>();
            
            DoorInteraction door = GetComponent<DoorInteraction>();
            if (door != null) components.Add(door);
            
            DrawerMech drawer = GetComponent<DrawerMech>();
            if (drawer != null) components.Add(drawer);
            
            interactionComponents = components.ToArray();
            
            if (interactionComponents.Length == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] No interaction components found or assigned.");
            }
        }

        if (checkOnStart)
        {
            CheckSpawnPoints();
        }
        
        // Start periodic checking
        InvokeRepeating(nameof(CheckSpawnPoints), checkInterval, checkInterval);
    }

    void CheckSpawnPoints()
    {
        // Skip checking if quiz has been completed and we're configured to stop checking
        if (quizCompleted && stopCheckingAfterQuiz)
        {
            Debug.Log($"[{gameObject.name}] Skipping spawn point check because quiz was completed");
            return;
        }
        
        // Skip if already disabled or no components to disable
        if (hasDisabled || interactionComponents == null || interactionComponents.Length == 0) return;

        bool shouldDisable = false;
        
        if (disableIfAllSpawned)
        {
            // Disable only if ALL spawn points have at least one child
            shouldDisable = true;
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] Null spawn point reference!");
                    continue;
                }
                
                if (point.childCount == 0)
                {
                    shouldDisable = false; // At least one spawn point is empty — don't disable yet
                    break;
                }
            }
        }
        else
        {
            // Disable if ANY spawn point has a prop
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] Null spawn point reference!");
                    continue;
                }
                
                if (point.childCount > 0)
                {
                    shouldDisable = true;
                    break;
                }
            }
        }
        
        if (shouldDisable)
        {
            DisableInteraction();
        }
    }

    void DisableInteraction()
    {
        // Don't disable if quiz has been completed
        if (quizCompleted && stopCheckingAfterQuiz) return;
        
        if (interactionComponents != null)
        {
            foreach (Behaviour component in interactionComponents)
            {
                if (component != null)
                {
                    component.enabled = false;
                }
            }
        }
        
        hasDisabled = true;
        Debug.Log($"[{gameObject.name}] Interaction disabled due to spawned prop.");
    }
    
    // Public method to play locked sound when interaction is attempted
    public void PlayLockedSound()
    {
        if (lockedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(lockedSound);
            Debug.Log($"[{gameObject.name}] Played locked sound.");
        }
    }
    
    // Public method to enable interaction after quiz is completed correctly
    public void EnableInteraction()
    {
        Debug.Log($"[{gameObject.name}] EnableInteraction called - enabling all interaction components");
        
        // Mark quiz as completed to prevent further disabling
        quizCompleted = true;
        
        // First try to enable components in our array
        if (interactionComponents != null && interactionComponents.Length > 0)
        {
            foreach (Behaviour component in interactionComponents)
            {
                if (component != null)
                {
                    component.enabled = true;
                    Debug.Log($"[{gameObject.name}] Enabled component: {component.GetType().Name}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No interaction components in array to enable");
        }
        
        // Explicitly check for DoorInteraction and DrawerMech components
        DoorInteraction door = GetComponent<DoorInteraction>();
        if (door != null)
        {
            door.enabled = true;
            Debug.Log($"[{gameObject.name}] Explicitly enabled DoorInteraction");
        }
        
        DrawerMech drawer = GetComponent<DrawerMech>();
        if (drawer != null)
        {
            drawer.enabled = true;
            Debug.Log($"[{gameObject.name}] Explicitly enabled DrawerMech");
        }
        
        hasDisabled = false;
        Debug.Log($"[{gameObject.name}] Interaction enabled after quiz completion.");
        
        // Optional: Cancel the InvokeRepeating to completely stop checking
        if (stopCheckingAfterQuiz)
        {
            CancelInvoke(nameof(CheckSpawnPoints));
            Debug.Log($"[{gameObject.name}] Stopped checking spawn points after quiz completion");
        }
    }
    
    // Check if this object is locked (has disabled interaction)
    public bool IsLocked()
    {
        return hasDisabled;
    }
}
