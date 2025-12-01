using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class Transition : MonoBehaviour
{

    public Animator transition;


    public void LoadLevel()
    {
        StartCoroutine(TriggerTransition(SceneManager.GetActiveScene().buildIndex + 1));
    }


    IEnumerator TriggerTransition(int levelIndex)
    {
        transition.SetTrigger("Start");
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(levelIndex);
    }   
}



