using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuizUI : MonoBehaviour
{
    public static QuizUI Instance;

    [Header("UI Elements")]
    public TMP_Text questionText;
    public Button[] optionButtons;

    [Header("Task Source")]
    public TaskDatabase taskDatabase;

    private int correctAnswerIndex;

    void Awake()
    {
        Instance = this;
    }

    public void ShowRandomQuestion()
    {
        int index = Random.Range(0, taskDatabase.allTasks.Length);
        Task task = taskDatabase.allTasks[index];

        questionText.text = task.question;
        correctAnswerIndex = task.correctIndex;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int capturedIndex = i;

            optionButtons[i].GetComponentInChildren<TMP_Text>().text = task.options[i];
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionClicked(capturedIndex));
        }
    }

    void OnOptionClicked(int selected)
    {
        if (selected == correctAnswerIndex)
        {
            Debug.Log("Correct Answer!");
            TaskManager.Instance.CompleteTask();
        }
        else
        {
            Debug.Log("Wrong Answer!");
            // Optional: feedback effect
        }
    }
}
