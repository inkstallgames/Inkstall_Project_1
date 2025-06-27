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

    private QuestionData currentQuestion;
    private bool hasAnswered = false;
    private Color defaultButtonColor;
    private AudioSource audioSource;
    private QuizTrigger quizTriggerReference;
    private bool quizResult = false;

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
    }
    
    private void OnEnable()
    {
        PickRandomQuestion();
        ShowQuestion();
        
        // Make sure cursor is visible for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void PickRandomQuestion()
    {
        int randomIndex = Random.Range(0, questionBank.questions.Count);
        currentQuestion = questionBank.questions[randomIndex];
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
        StartCoroutine(QuitAfterDelay(3f));
    }

    IEnumerator QuitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        QuitQuiz();
    }

    void QuitQuiz()
    {
        // Notify the quiz trigger about quiz result before hiding panel
        if (quizTriggerReference != null)
        {
            quizTriggerReference.OnQuizCompleted(quizResult);
        }
        
        gameObject.SetActive(false);
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
        
        Debug.Log($"Quiz initialized with question: {currentQuestion.questionText}");
    }
}
