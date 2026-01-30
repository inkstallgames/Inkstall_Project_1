using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class HeroSelection : MonoBehaviour
{
    public List<HeroButton> heroButtons;
    private HeroButton selectedButton;
    public TextMeshProUGUI heroNameText;
    public TextMeshProUGUI heroDescriptionText;
    public TextMeshProUGUI Timer;
    
    private bool isLockedIn = false;
    void Start()
    {
        foreach (HeroButton heroBtn in heroButtons)
        {
            Button button = heroBtn.GetComponent<Button>();
            button.onClick.AddListener(() => Select(heroBtn));
        }

        // Select the first hero by default
        if (heroButtons.Count > 0)
        {
            Select(heroButtons[0]);
        }
    }

    public void Select(HeroButton button)
    {
        if (button == null || isLockedIn) return; // Don't allow selection if locked in
        
        // Reset previous selection to normal color
        if (selectedButton != null && selectedButton != button)
        {
            Button prevButton = selectedButton.GetComponent<Button>();
            if (prevButton != null)
            {
                var colors = prevButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.disabledColor = Color.white;
                prevButton.colors = colors;
                prevButton.interactable = true;
            }
        }

        selectedButton = button;
        
        // Update UI Text
        if (heroNameText != null) heroNameText.text = button.heroData.heroName;
        if (heroDescriptionText != null) heroDescriptionText.text = button.heroData.heroDescription;
        
        // Force update the green color immediately and disable color transitions
        Button currentButton = selectedButton.GetComponent<Button>();
        if (currentButton != null)
        {
            var colors = currentButton.colors;
            colors.normalColor = Color.green;
            colors.highlightedColor = Color.green;
            colors.pressedColor = Color.green;
            colors.disabledColor = Color.green;
            colors.selectedColor = Color.green;
            currentButton.colors = colors;
            currentButton.interactable = true;
            
            // Force the button to update its visual state
            currentButton.targetGraphic = currentButton.targetGraphic;
        }
    }

    // Called when the player clicks the lock in button
    public void Lockin()
    {
        if (selectedButton == null) 
        {
            Debug.Log("No hero selected!");
            return;
        }

        int selectedHeroId = heroButtons.IndexOf(selectedButton);
        if (selectedHeroId == -1)
        {
            Debug.LogError("Invalid hero selection!");
            return;
        }

        // Allow duplicate hero selections - no need to check if hero is already selected

        // Notify the server about the hero selection
        if (NetworkLobbyManager.Instance != null)
        {
            isLockedIn = true; // Set locked in flag
            NetworkLobbyManager.Instance.RPC_SetSelectedHero(selectedHeroId);

            // Update the UI to show locked in state (red color)
            Button selectedUIButton = selectedButton.GetComponent<Button>();
            if (selectedUIButton != null)
            {
                var colors = selectedUIButton.colors;
                colors.normalColor = Color.red;
                colors.disabledColor = Color.red;
                selectedUIButton.colors = colors;
                selectedUIButton.interactable = false;
            }
            
            // Disable all other hero buttons
            foreach (var heroBtn in heroButtons)
            {
                if (heroBtn != selectedButton)
                {
                    Button otherButton = heroBtn.GetComponent<Button>();
                    if (otherButton != null)
                    {
                        otherButton.interactable = false;
                    }
                }
            }
        }
    }

    public void UpdateTimer(float time)
    {
        if (time < 0) time = 0;
        Timer.text = time.ToString("00");
    }

    public void UpdateAvailableHeroes(NetworkLinkedList<int> selectedIds)
    {
        // This method is now simplified since we handle locking in Lockin() method
        // and prevent further selection with isLockedIn flag
        if (isLockedIn)
        {
            // All buttons should be disabled when locked in
            foreach (var heroBtn in heroButtons)
            {
                if (heroBtn == null) continue;
                
                var button = heroBtn.GetComponent<Button>();
                if (button == null) continue;
                
                if (heroBtn == selectedButton)
                {
                    // Selected button stays red
                    var colors = button.colors;
                    colors.normalColor = Color.red;
                    colors.disabledColor = Color.red;
                    button.colors = colors;
                    button.interactable = false;
                }
                else
                {
                    // Other buttons stay disabled but white
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    button.colors = colors;
                    button.interactable = false;
                }
            }
        }
        else
        {
            // Normal selection mode - all buttons interactable except current selection
            for (int i = 0; i < heroButtons.Count; i++)
            {
                if (heroButtons[i] == null) continue;
                
                var button = heroButtons[i].GetComponent<Button>();
                if (button == null) continue;
                
                if (heroButtons[i] == selectedButton)
                {
                    // Selected button is green and fully interactable
                    var colors = button.colors;
                    colors.normalColor = Color.green;
                    colors.highlightedColor = Color.green;
                    colors.pressedColor = Color.green;
                    colors.disabledColor = Color.green;
                    colors.selectedColor = Color.green;
                    button.colors = colors;
                    button.interactable = true;
                }
                else
                {
                    // Other buttons are white and interactable
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = Color.white;
                    colors.pressedColor = Color.white;
                    colors.disabledColor = Color.white;
                    colors.selectedColor = Color.white;
                    button.colors = colors;
                    button.interactable = true;
                }
            }
        }
    }
}



