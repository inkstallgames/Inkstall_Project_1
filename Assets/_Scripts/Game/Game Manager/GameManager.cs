using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private AudioSource audioSource;
    [SerializeField] private AudioClip doorCloseSound;
    
    public AudioClip looseSound;
    public AudioClip levelCompleteSound;
    
    private string studentId; // Added missing variable declaration

    // Play Loose effect
    // Play Win effect

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Get Student ID from StudentIdManager
        studentId = StudentIdManager.Instance.StudentId;
        Debug.Log($"[GameManager] Got student ID from StudentIdManager: {studentId}");
        
        // Initialize ProgressManager if it doesn't exist
        if (ProgressManager.Instance != null)
        {
            // Set student ID if needed
            if (!string.IsNullOrEmpty(studentId))
            {
                ProgressManager.Instance.SetStudentId(studentId);
                
                // Load door data from the server
                ProgressManager.Instance.LoadStudentDoorData();
            }
            else
            {
                Debug.LogWarning("[GameManager] No student ID available, cannot load door data");
            }
        }
    }

    public void GameOver()
    {
        // Level Loose effect
        // Play Loose Sound        
        
        StartCoroutine(ResetGame());
    }
    
    public void LevelWin()
    {
        Debug.Log("[LEVEL_WIN_START] Level completed, starting win sequence");
        
        // Level Win effect
        
        // Play Win Sound
        if (levelCompleteSound != null && audioSource != null)
        {
            Debug.Log("[LEVEL_WIN_AUDIO] Playing level complete sound");
            audioSource.PlayOneShot(levelCompleteSound);
        }
        else
        {
            Debug.LogWarning("[LEVEL_WIN_AUDIO_ERROR] Cannot play level complete sound: " + 
                           (levelCompleteSound == null ? "Sound is null" : "AudioSource is null"));
        }
        
        Debug.Log("[LEVEL_WIN_RESET] Starting ResetGame coroutine for scene reload");
        StartCoroutine(ResetGame());
    }

    IEnumerator ResetGame()
    {
        Debug.Log("[RESET_GAME_START] Reset game sequence started");
        Debug.Log("[RESET_GAME_WAIT] Waiting 3 seconds before door close sound");
        yield return new WaitForSeconds(3f);
        
        // Play door close sound
        if (doorCloseSound != null && audioSource != null)
        {
            Debug.Log("[RESET_GAME_AUDIO] Playing door close sound");
            audioSource.PlayOneShot(doorCloseSound);
        }
        else
        {
            Debug.LogWarning("[RESET_GAME_AUDIO_ERROR] Cannot play door close sound: " + 
                           (doorCloseSound == null ? "Sound is null" : "AudioSource is null"));
        }
        
        Debug.Log("[RESET_GAME_WAIT] Waiting 1 second before scene reload");
        yield return new WaitForSeconds(1f);
        
        // Check if ProgressManager has completed saving door data
        if (ProgressManager.Instance != null)
        {
            Debug.Log("[RESET_GAME_PROGRESS_CHECK] ProgressManager status before scene reload:");
            Debug.Log($"[RESET_GAME_PROGRESS_CHECK] isDataLoaded: {ProgressManager.Instance.isDataLoaded}");
            Debug.Log("[RESET_GAME_PROGRESS_CHECK] Forcing one final data reload before scene change");
            
            // Force one final data reload to ensure latest data is saved
            bool reloadSuccess = true;
            try
            {
                ProgressManager.Instance.LoadStudentDoorData();
                Debug.Log("[RESET_GAME_PROGRESS_CHECK] Final data reload initiated");
            }
            catch (System.Exception ex)
            {
                reloadSuccess = false;
                Debug.LogError($"[RESET_GAME_PROGRESS_ERROR] Error during final data reload: {ex.Message}");
            }
            
            // Small delay to allow data reload to start (moved outside try-catch)
            if (reloadSuccess)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Get current scene info for logging
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        Debug.Log($"[RESET_GAME_SCENE_RELOAD] Reloading scene: {currentSceneName} (index: {currentSceneIndex})");
        
        // Reload the scene
        SceneManager.LoadScene(currentSceneIndex);
        Debug.Log("[RESET_GAME_SCENE_RELOAD] Scene reload initiated");
    }

}
