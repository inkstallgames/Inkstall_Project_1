using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public GameObject shopCanvas;
    public GameObject mobileControlsCanvas;
    
    public void EnableShopCanvas()
    {
        // Toggle the active state
        shopCanvas.SetActive(true);
            Time.timeScale = 0f; // Pause the game
            mobileControlsCanvas.SetActive(false); // Hide the mobile controls UI
    }

    public void CloseShopCanvas()
    {
        shopCanvas.SetActive(false); // Hide the options UI
        mobileControlsCanvas.SetActive(true); // Hide the mobile controls UI
        Time.timeScale = 1f;                // Resume the game
    }
}
