using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParentalGate : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("The TextMeshProUGUI component to display the math question.")]
    public TextMeshProUGUI questionText;

    [Tooltip("The TextMeshPro input field for the user's answer.")]
    public TMP_InputField answerInput;

    [Tooltip("The GameObject to show when the answer is incorrect.")]
    public GameObject notificationGameObject;

    [Tooltip("The button that submits the answer.")]
    public Button submitButton;

    // Event to notify listeners that the purchase can continue.
    public static event System.Action OnPurchaseApproved;

    private int correctAnswer;

    private void OnEnable()
    {
        GenerateNewQuestion();

        // Clear the input field every time the panel is enabled.
        if (answerInput != null)
        {
            answerInput.text = "";
        }

        // Hide the notification panel initially.
        if (notificationGameObject != null)
        {
            notificationGameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Generates a new random multiplication question.
    /// </summary>
    private void GenerateNewQuestion()
    {
        int num1 = Random.Range(1, 11); // Random number between 1 and 10
        int num2 = Random.Range(1, 11); // Random number between 1 and 10

        correctAnswer = num1 * num2;

        if (questionText != null)
        {
            questionText.text = $"What is {num1} x {num2}?";
        }
        else
        {
            Debug.LogError("Question Text is not assigned in the ParentalGate.");
        }
    }

    private void Start()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(CheckAnswer);
        }
        else
        {
            Debug.LogError("Submit button is not assigned in the ParentalGate.");
        }
    }

    /// <summary>
    /// Checks the answer provided by the user.
    /// </summary>
    public void CheckAnswer()
    {
        if (answerInput != null && int.TryParse(answerInput.text, out int userAnswer) && userAnswer == correctAnswer)
        {
            // Correct answer: continue with the purchase and close the panel.
            OnPurchaseApproved?.Invoke();
            gameObject.SetActive(false);
        }
        else
        {
            // Incorrect answer: show notification and generate a new question.
            StartCoroutine(ShowNotification());
            GenerateNewQuestion();
        }
    }

    /// <summary>
    /// Coroutine to show the notification GameObject for a short duration.
    /// </summary>
    private IEnumerator ShowNotification()
    {
        if (notificationGameObject != null)
        {
            notificationGameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            notificationGameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // Clean up the listener when the object is destroyed.
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(CheckAnswer);
        }
    }
}
