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


    /// Called by PlayerInteraction when interacting with a locked object.
    public void StartTask(LockableObject target)
    {
        currentLockable = target;
        quizPanel.SetActive(true);
        QuizUI.Instance.ShowRandomQuestion();
    }


    /// Called by QuizUI after player solves the task.
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
