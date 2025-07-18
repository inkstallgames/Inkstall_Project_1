using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadBuildingScene : MonoBehaviour
{
    [Tooltip("The build index of the scene to load")]
    [SerializeField] private int targetSceneIndex;
    
    [Tooltip("Is this an entrance to a building (true) or an exit back to the city (false)")]
    [SerializeField] private bool isEntrance = true;
    
    [Tooltip("Optional delay before loading the scene")]
    [SerializeField] private float loadDelay = 0.2f;
    
    [Tooltip("The main city scene index (used when exiting buildings)")]
    [SerializeField] private int mainCitySceneIndex = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Invoke("LoadTargetScene", loadDelay);
        }
    }

    private void LoadTargetScene()
    {
        int sceneToLoad = isEntrance ? targetSceneIndex : mainCitySceneIndex;
        SceneManager.LoadScene(sceneToLoad);
    }
}