using UnityEngine;

[System.Serializable]
public class Task
{
    public string question;
    public string[] options;
    public int correctIndex;
}

[CreateAssetMenu(fileName = "New Task Database", menuName = "Tasks/Task Database")]
public class TaskDatabase : ScriptableObject
{
    public Task[] allTasks;
    
}
