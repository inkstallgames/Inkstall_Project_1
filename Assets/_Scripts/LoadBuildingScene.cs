using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadBuildingScene : MonoBehaviour
{
     private void OnTriggerEnter(Collider other)
     {
          if (other.tag == "Player")
          {
               Invoke("LoadNextScene", 0.2f);
          }
     }

     private void LoadNextScene()
     {
          SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
     }
}
