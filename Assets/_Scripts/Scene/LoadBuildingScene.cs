using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadBuildingScene : MonoBehaviour
{
    [Tooltip("The build index of the scene to load")]
    [SerializeField] private int targetSceneIndex;

    [Tooltip("Is this an entrance to a building (true) or an exit back to the city (false)")]
    [SerializeField] private bool isEntrance = true;

    [Tooltip("Optional delay before loading the scene")]
    [SerializeField] private float loadDelay = 0.5f;

    [Tooltip("The main city scene index (used when exiting buildings)")]
    [SerializeField] private int mainCitySceneIndex = 0;

    [Tooltip("Sound to play when player enters the portal")]
    [SerializeField] private AudioClip portalEnterSound;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Play the portal entry sound if one is assigned
            if (portalEnterSound != null)
            {
                audioSource.PlayOneShot(portalEnterSound);
            }
            Invoke("LoadNextScene", loadDelay);
        }
    }

    private void LoadNextScene()
    {
        int sceneToLoad = isEntrance ? targetSceneIndex : mainCitySceneIndex;
        SceneManager.LoadScene(sceneToLoad);
    }
}