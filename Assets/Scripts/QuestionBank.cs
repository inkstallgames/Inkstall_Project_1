using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "QuestionBank", menuName = "Quiz/Question Bank", order = 2)]
public class QuestionBank : ScriptableObject
{
    public List<QuestionData> questions;
}
