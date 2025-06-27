using UnityEngine;
using System.Collections;

public class QuizTrigger : MonoBehaviour
{
    [Header("Quiz Settings")]
    [SerializeField] private GameObject quizPanel;
    
    [Header("Target Interaction Component")]
    [SerializeField] private Behaviour targetInteractionComponent;
    
    [Header("Audio")]
    [SerializeField] private AudioClip lockedSound;
    [SerializeField] private AudioClip unlockedSound;
    [SerializeField] private float unlockSoundDelay = 2f; // Delay before playing unlock sound
    
    private AudioSource audioSource;
    private bool quizCompleted = false;
    
    private void Start()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // If no target component is assigned, try to find one on this GameObject
        if (targetInteractionComponent == null)
        {
            // Try to find DoorInteraction first
            targetInteractionComponent = GetComponent<DoorInteraction>();
            
            // If no DoorInteraction, try DrawerMech
            if (targetInteractionComponent == null)
            {
                targetInteractionComponent = GetComponent<DrawerMech>();
            }
        }
    }
    
    // Called by PlayerInteraction when interaction is attempted on a locked object
    public void TriggerQuiz()
    {
        // Don't show quiz if it's already been completed
        if (quizCompleted)
        {
            Debug.Log($"[{gameObject.name}] Quiz already completed, not showing again");
            return;
        }
        
        // Play locked sound
        if (audioSource != null && lockedSound != null)
        {
            audioSource.PlayOneShot(lockedSound);
        }
        
        // Show quiz panel
        if (quizPanel != null)
        {
            // Make sure the panel is active
            quizPanel.SetActive(true);
            
            // Get QuizManager component to set up callback
            QuizManager quizManager = quizPanel.GetComponent<QuizManager>();
            if (quizManager != null)
            {
                quizManager.SetQuizTriggerReference(this);
                
                // Force the quiz manager to initialize the quiz
                quizManager.InitializeQuiz();
            }
        }
    }
    
    // Called by QuizManager when quiz is completed
    public void OnQuizCompleted(bool isCorrect)
    {
        Debug.Log($"[{gameObject.name}] Quiz completed with result: {(isCorrect ? "Correct" : "Incorrect")}");
        
        if (isCorrect)
        {
            // Mark as completed to prevent showing quiz again
            quizCompleted = true;
            
            // Enable the interaction component if answer was correct
            if (targetInteractionComponent != null)
            {
                targetInteractionComponent.enabled = true;
                Debug.Log($"[{gameObject.name}] Enabled {targetInteractionComponent.GetType().Name}");
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] No target interaction component assigned!");
                
                // Try to find and enable DoorInteraction
                DoorInteraction door = GetComponent<DoorInteraction>();
                if (door != null)
                {
                    door.enabled = true;
                    Debug.Log($"[{gameObject.name}] Found and enabled DoorInteraction");
                }
                
                // Try to find and enable DrawerMech
                DrawerMech drawer = GetComponent<DrawerMech>();
                if (drawer != null)
                {
                    drawer.enabled = true;
                    Debug.Log($"[{gameObject.name}] Found and enabled DrawerMech");
                }
            }
            
            // Also notify DisableOnPropSpawn component if it exists
            DisableOnPropSpawn disabler = GetComponent<DisableOnPropSpawn>();
            if (disabler != null)
            {
                disabler.EnableInteraction();
                Debug.Log($"[{gameObject.name}] Called EnableInteraction on DisableOnPropSpawn");
            }
            
            // Play unlocked sound after delay
            if (audioSource != null && unlockedSound != null)
            {
                StartCoroutine(PlayUnlockSoundAfterDelay());
            }
        }
        else
        {
            Debug.Log($"[{gameObject.name}] Quiz answered incorrectly. Object remains locked.");
        }
    }
    
    // Coroutine to play unlock sound after a delay
    private IEnumerator PlayUnlockSoundAfterDelay()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(unlockSoundDelay);
        
        // Play the unlock sound
        if (audioSource != null && unlockedSound != null)
        {
            audioSource.PlayOneShot(unlockedSound);
            Debug.Log($"[{gameObject.name}] Played unlock sound after {unlockSoundDelay} seconds");
        }
    }
}