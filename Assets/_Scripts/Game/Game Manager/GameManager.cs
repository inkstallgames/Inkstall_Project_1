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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

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
        // Level Win effect
        
        // Play Win Sound
        if (levelCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(levelCompleteSound);
        }
        StartCoroutine(ResetGame());
    }

    IEnumerator ResetGame()
    {
        yield return new WaitForSeconds(3f);
        
        // Play door close sound
        if (doorCloseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(doorCloseSound);
        }
        
        yield return new WaitForSeconds(1f);
        
        // Check if ProgressManager has completed saving door data
        if (ProgressManager.Instance != null)
        {
            // Force one final data reload to ensure latest data is saved
            bool reloadSuccess = true;
            try
            {
                ProgressManager.Instance.LoadStudentDoorData();
            }
            catch (System.Exception)
            {
                reloadSuccess = false;
            }
            
            // Small delay to allow data reload to start
            if (reloadSuccess)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        // Reload the current scene
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

}
