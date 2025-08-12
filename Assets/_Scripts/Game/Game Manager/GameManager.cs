using UnityEngine;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameObject player; 
    public GameObject playerStartPos;
    public GameObject timer;
    public GameObject chemicalBomb;
    public GameObject throwButton;
    public GameObject shopButton;

    public AudioClip looseSound;
    public AudioClip winSound;


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
        ResetGame();
        // Active Room(Unlocked Door) locked Again
    }

    public void GameWin()
    {
        // Play Game Win Sound

        // (and after some time)
        ResetGame();
        // Active Room(Unlocked Door) locked Again
    }

    public void ResetGame()
    {
        player.transform.position = playerStartPos.transform.position;
        timer.SetActive(false);
        chemicalBomb.SetActive(false);
        throwButton.SetActive(false);
        if(!shopButton.activeSelf)
        {
            shopButton.SetActive(false);
        }
    }


}
