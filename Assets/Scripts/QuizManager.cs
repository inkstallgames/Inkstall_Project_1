using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text questionText;
    public Button[] optionButtons; // Assign 4 buttons
    public Button quitButton;      // Assign Quit button

    [Header("Question Source")]
    public QuestionBank questionBank;

    [Header("Audio")]
    public AudioClip correctSFX;
    public AudioClip wrongSFX;

    [Header("Player Control")]
    [SerializeField] private string playerTag = "Player"; // Tag of the player GameObject
    [SerializeField] private bool freezePlayerDuringQuiz = true; // Option to enable/disable player freezing

    private QuestionData currentQuestion;
    private bool hasAnswered = false;
    private Color defaultButtonColor;
    private AudioSource audioSource;
    private QuizTrigger quizTriggerReference;
    private bool quizResult = false;
    
    // Cache references to player components
    private MonoBehaviour[] playerScripts;
    private GameObject playerObject;

    void Start()
    {
        // Get AudioSource component
        audioSource = GetComponent<AudioSource>();

        // Validate question bank
        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("Question Bank is empty or missing!");
            return;
        }

        if (optionButtons.Length != 4)
        {
            Debug.LogError("OptionButtons array must have exactly 4 buttons.");
            return;
        }

        defaultButtonColor = optionButtons[0].image.color;

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitQuiz);

        // This component should start disabled
        gameObject.SetActive(false);
        
        // Find player object
        FindPlayerObject();
    }
    
    // Find and cache the player object and its components
    private void FindPlayerObject()
    {
        playerObject = GameObject.FindWithTag(playerTag);
        if (playerObject != null)
        {
            // Cache all MonoBehaviour scripts on the player that might handle movement or input
            playerScripts = playerObject.GetComponentsInChildren<MonoBehaviour>();
            Debug.Log($"Found player object with {playerScripts.Length} scripts");
        }
        else
        {
            Debug.LogWarning("Could not find player object with tag: " + playerTag);
        }
    }
    
    // Disable player movement and input scripts
    private void FreezePlayer()
    {
        if (!freezePlayerDuringQuiz || playerObject == null) return;
        
        Debug.Log("Freezing player movement for quiz");
        
        // Disable all MonoBehaviour scripts on the player except for essential ones
        if (playerScripts != null)
        {
            foreach (MonoBehaviour script in playerScripts)
            {
                // Skip null references and this script
                if (script == null) continue;
                
                // Skip essential scripts that should remain active
                string scriptName = script.GetType().Name;
                if (scriptName == "AudioListener" || 
                    scriptName == "Camera" || 
                    scriptName == "AudioSource" ||
                    scriptName == "Transform")
                {
                    continue;
                }
                
                // Store the original enabled state and disable the script
                script.enabled = false;
            }
        }
        
        // Lock the cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    // Re-enable player movement and input scripts
    private void UnfreezePlayer()
    {
        if (!freezePlayerDuringQuiz || playerObject == null) return;
        
        Debug.Log("Unfreezing player movement after quiz");
        
        // Re-enable all MonoBehaviour scripts on the player
        if (playerScripts != null)
        {
            foreach (MonoBehaviour script in playerScripts)
            {
                // Skip null references
                if (script == null) continue;
                
                // Skip scripts that should remain in their current state
                string scriptName = script.GetType().Name;
                if (scriptName == "AudioListener" || 
                    scriptName == "Camera" || 
                    scriptName == "AudioSource" ||
                    scriptName == "Transform")
                {
                    continue;
                }
                
                // Re-enable the script
                script.enabled = true;
            }
        }
        
        // Reset cursor state based on game needs
        // This might need to be adjusted based on your game's cursor handling
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        PickRandomQuestion();
        ShowQuestion();
        
        // Make sure cursor is visible for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Freeze player when quiz panel is enabled
        FreezePlayer();
    }
    
    private void OnDisable()
    {
        // Unfreeze player when quiz panel is disabled
        UnfreezePlayer();
    }

    void ShowQuestion()
    {
        hasAnswered = false;
        questionText.text = currentQuestion.questionText;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int optionIndex = i;

            optionButtons[i].interactable = true;
            optionButtons[i].image.color = defaultButtonColor;

            TMP_Text btnText = optionButtons[i].GetComponentInChildren<TMP_Text>();
            btnText.text = currentQuestion.options[i];

            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(optionIndex));
        }
    }

    void OnOptionSelected(int selectedIndex)
    {
        if (hasAnswered) return;
        hasAnswered = true;

        int correctIndex = currentQuestion.correctOptionIndex;

        // Disable all buttons
        foreach (var btn in optionButtons)
            btn.interactable = false;

        // Set colors and play sound
        if (selectedIndex == correctIndex)
        {
            optionButtons[selectedIndex].image.color = Color.green;
            quizResult = true;

            if (correctSFX && audioSource)
                audioSource.PlayOneShot(correctSFX);
        }
        else
        {
            optionButtons[selectedIndex].image.color = Color.red;
            optionButtons[correctIndex].image.color = Color.green;
            quizResult = false;

            if (wrongSFX && audioSource)
                audioSource.PlayOneShot(wrongSFX);
        }

        // Quit after delay
        StartCoroutine(QuitAfterDelay(1.2f));
    }

    void QuitQuiz()
    {
        // Notify the quiz trigger about quiz result before hiding panel
        if (quizTriggerReference != null)
        {
            quizTriggerReference.OnQuizCompleted(quizResult);
        }
        
        // Unfreeze player before hiding the quiz panel
        UnfreezePlayer();
        
        gameObject.SetActive(false);
    }

    IEnumerator QuitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        QuitQuiz();
    }

    void PickRandomQuestion()
    {
        int randomIndex = Random.Range(0, questionBank.questions.Count);
        currentQuestion = questionBank.questions[randomIndex];
    }

    // Set reference to the quiz trigger that triggered this quiz
    public void SetQuizTriggerReference(QuizTrigger quizTrigger)
    {
        quizTriggerReference = quizTrigger;
        quizResult = false; // Reset result for new quiz
        
        // Reset and show a new question
        PickRandomQuestion();
        ShowQuestion();
    }

    // Public method to initialize the quiz when triggered
    public void InitializeQuiz()
    {
        // Reset quiz state
        hasAnswered = false;
        quizResult = false;
        
        // Pick a new random question
        PickRandomQuestion();
        
        // Display the question and options
        ShowQuestion();
        
        // Reset all option buttons to default state
        for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].interactable = true;
            optionButtons[i].image.color = defaultButtonColor;
        }
        
        // Make sure the quiz panel is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Make sure the parent canvas is enabled
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
        }
        
        // Show cursor for quiz interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Freeze player movement
        FreezePlayer();
        
        Debug.Log($"Quiz initialized with question: {currentQuestion.questionText}");
    }
}
