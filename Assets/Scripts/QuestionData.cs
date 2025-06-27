using UnityEngine;

[CreateAssetMenu(fileName = "NewQuestion", menuName = "Quiz/Question", order = 1)]
public class QuestionData : ScriptableObject
{
    [TextArea(2, 4)]
    public string questionText;

    public string[] options = new string[4];

    [Range(0, 3)]
    public int correctOptionIndex;
}
