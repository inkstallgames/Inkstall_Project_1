using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text questionText;
    public Button[] optionButtons; // Assign 4
    public Button quitButton;      // Assign Quit button

    [Header("Question Source")]
    public QuestionBank questionBank;

    private QuestionData currentQuestion;
    private bool hasAnswered = false;
    private Color defaultButtonColor;

    void Start()
    {
        // Fix disappearing cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Validate setup
        if (questionBank == null || questionBank.questions.Count == 0)
        {
            Debug.LogError("❌ Question Bank is empty or missing!");
            return;
        }

        if (optionButtons.Length != 4)
        {
            Debug.LogError("❌ OptionButtons array must have exactly 4 buttons.");
            return;
        }

        // Save default button color (assumes all buttons are same)
        defaultButtonColor = optionButtons[0].image.color;

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitQuiz);

        PickRandomQuestion();
        ShowQuestion();
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

            // Reset visuals
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

        // Set colors
        if (selectedIndex == correctIndex)
        {
            optionButtons[selectedIndex].image.color = Color.green;
            Debug.Log("✅ Correct!");
        }
        else
        {
            optionButtons[selectedIndex].image.color = Color.red;
            optionButtons[correctIndex].image.color = Color.green;
            Debug.Log("❌ Wrong!");
        }

        // Auto-quit after 3 seconds
        StartCoroutine(QuitAfterDelay(3f));
    }

    IEnumerator QuitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        QuitQuiz();
    }

    void QuitQuiz()
    {
        Debug.Log("🛑 Hiding QuizPanel.");
        gameObject.SetActive(false);
    }
}
