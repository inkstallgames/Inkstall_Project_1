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
    public AudioClip winSound;
    
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
        // Level Win effect
        // Play Win Sound
        
        StartCoroutine(ResetGame());
    }

    IEnumerator ResetGame()
    {
        yield return new WaitForSeconds(3f);
        audioSource.PlayOneShot(doorCloseSound);
        yield return new WaitForSeconds(1f);     
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }

}
