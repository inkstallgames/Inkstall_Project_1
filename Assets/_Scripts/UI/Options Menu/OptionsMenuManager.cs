using UnityEngine;

public class OptionsMenuManager : MonoBehaviour
{
    public GameObject optionsMenuCanvas; // Assign your Settings Canvas here
    public GameObject mobileControlsCanvas; // Assign your Mobile Controls Canvas here
    
    // Call this function when button is clicked
    public void EnableOptionsMenu()
    {
        // Toggle the active state
        optionsMenuCanvas.SetActive(true);
            Time.timeScale = 0f; // Pause the game
            mobileControlsCanvas.SetActive(false); // Hide the mobile controls UI
    }

    public void ResumeGame()
{
    optionsMenuCanvas.SetActive(false); // Hide the options UI
    mobileControlsCanvas.SetActive(true); // Hide the mobile controls UI
    Time.timeScale = 1f;                // Resume the game
}

}
