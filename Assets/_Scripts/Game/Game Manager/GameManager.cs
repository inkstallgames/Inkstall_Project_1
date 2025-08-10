using UnityEngine;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }


    public void GameOver()
    {
        // Play Loose Sound

        // (and after some time)
        // Player Position = Start Position
        // Disable Timer
        // Disable Gun Model
        // Disable Fire Button
        // Disable Shop Button
        // Active Room(Unlocked Door) locked Again
    }

    public void GameWin()
    {
        // Play Game Win Sound

        // (and after some time)
        // Player Position = Start Position
        // Disable Timer
        // Disable Gun Model
        // Disable Fire Button
        // Disable Shop Button
        // Active Room(Unlocked Door) locked Again
    }

}
