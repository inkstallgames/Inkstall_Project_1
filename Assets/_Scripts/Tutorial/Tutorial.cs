using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Tutorial : MonoBehaviour
{
    public static Tutorial Instance { get; private set; }
    
    public GameObject tutorialText1;
    public GameObject tutorialText2;
    public List<GameObject> buildingsTasks;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[Tutorial] Tutorial instance created");
        }
        else
        {
            Debug.LogWarning("[Tutorial] Duplicate Tutorial instance found, destroying this one");
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        Debug.Log($"[Tutorial] Start() called. Checking PlayerPrefs...");
        
        if(!PlayerPrefs.HasKey("TutorialCompleted"))
        {
            if (tutorialText1 != null) tutorialText1.SetActive(true);
            if (tutorialText2 != null) tutorialText2.SetActive(true);
        }

        if(PlayerPrefs.HasKey("TutorialCompleted"))
        {
            if (tutorialText1 != null) tutorialText1.SetActive(false);
            if (tutorialText2 != null) tutorialText2.SetActive(false);
        }

        Debug.Log($"[Tutorial] buildingsTasks count: {buildingsTasks.Count}");
        
        for (int i = 0; i < buildingsTasks.Count; i++)
        {
            string taskKey = "Task_" + i + "_Completed";
            bool hasKey = PlayerPrefs.HasKey(taskKey);
            int value = PlayerPrefs.GetInt(taskKey, 0);
            
            Debug.Log($"[Tutorial] Checking Task {i}: HasKey={hasKey}, Value={value}, GameObject={(buildingsTasks[i] != null ? buildingsTasks[i].name : "null")}");

            if (hasKey && value == 1)
            {
                Debug.Log($"[Tutorial] Task {i} is already completed, hiding UI");
                if (buildingsTasks[i] != null)
                {
                    buildingsTasks[i].SetActive(false);
                }
            }
            else
            {
                Debug.Log($"[Tutorial] Task {i} is NOT completed, showing UI");
            }
        }
    }

    public void CompleteTask(int taskIndex)
    {   
       string taskKey = "Task_" + taskIndex + "_Completed";
       PlayerPrefs.SetInt(taskKey, 1);
       PlayerPrefs.Save();
       Debug.Log($"[Tutorial] Task {taskIndex} completed and saved to PlayerPrefs with key: {taskKey}");
       
       // Disable task UI instantly
        if (taskIndex >= 0 && taskIndex < buildingsTasks.Count)
        {
            buildingsTasks[taskIndex].SetActive(false);
            Debug.Log($"[Tutorial] Task {taskIndex} UI disabled");
        }
        else
        {
            Debug.LogWarning($"[Tutorial] Task index {taskIndex} is out of range (0-{buildingsTasks.Count - 1})");
        }
    }




}
