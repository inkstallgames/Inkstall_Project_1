using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    [Header("UI References")]
    public GameObject quizPanel;

    private LockableObject currentLockable;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Called by PlayerInteraction when interacting with a locked object.
    /// </summary>
    public void StartTask(LockableObject target)
    {
        currentLockable = target;
        quizPanel.SetActive(true);
        QuizUI.Instance.ShowRandomQuestion();
    }

    /// <summary>
    /// Called by QuizUI after player solves the task.
    /// </summary>
    public void CompleteTask()
    {
        quizPanel.SetActive(false);

        if (currentLockable != null)
        {
            currentLockable.Unlock();
            currentLockable = null;
        }
    }
}
