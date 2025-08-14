using UnityEngine;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public Transform player; 
    public Transform playerResetPos;
    public GameObject timer;
    public GameObject chemicalBomb;
    public GameObject throwButton;
    public GameObject shopButton;

    public AudioClip looseSound;
    public AudioClip winSound;

    public DoorInteraction[] allDoors;


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
        // Level Loose effect
        // Play Loose Sound
        // Disable Timer, Disable throw button, Disable shop button

        // (and after some time)

        // Player Position to Start Position
        // Door will be locked Again
    }

    public void GameWin()
    {
        // Level Win effect
        // Play Win Sound
        // Disable Timer, Disable throw button, Disable shop button
        
        // (and after some time)
        
        // Player Position to Start Position
        // Door will be locked Again       
    }

    public void ResetGame()
    {
        Debug.Log("Reset Game");
        // player.position = playerResetPos.position;
        // player.rotation = playerResetPos.rotation;
        timer.SetActive(false);
        // chemicalBomb.SetActive(false);
        throwButton.SetActive(false);
        if(!shopButton.activeSelf)
        {
            shopButton.SetActive(false);
        }
        
        // Reset all doors in the scene
    }
    
    private void ResetAllDoors()
    {
        // Find all door interaction components in the scene
        
        // Reset each door to closed and locked state
        foreach (DoorInteraction door in allDoors)
        {
            // Close the door if it's open
            door.ResetDoor();
            
            // We need to add a method to relock the door in DoorInteraction class
            // For now, we'll call a method that we'll implement next
            RelockDoor(door);
        }
        
        Debug.Log("All doors have been reset and locked");
    }
    
    private void RelockDoor(DoorInteraction door)
    {
        // Access the door's isLocked field using reflection since it's private
        // This is a workaround - ideally, DoorInteraction should have a public LockDoor() method
        var field = typeof(DoorInteraction).GetField("isLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(door, true);
        }
    }
}
