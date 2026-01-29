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
        if (button == null) return;
        
        if (selectedButton != null && selectedButton != button)
        {
            selectedButton.GetComponent<Button>().interactable = true;
        }

        selectedButton = button;
        
        // Update UI Text
        if (heroNameText != null) heroNameText.text = button.heroData.heroName;
        if (heroDescriptionText != null) heroDescriptionText.text = button.heroData.heroDescription;
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

        // Check if the hero is already selected by someone else
        if (NetworkLobbyManager.Instance != null && 
            NetworkLobbyManager.Instance.SelectedHeroIds.Contains(selectedHeroId))
        {
            Debug.Log("This hero is already selected by another player!");
            return;
        }

        // Notify the server about the hero selection
        if (NetworkLobbyManager.Instance != null)
        {
            NetworkLobbyManager.Instance.RPC_SetSelectedHero(selectedHeroId);

            // Update the UI to show the selection
            Button selectedUIButton = selectedButton.GetComponent<Button>();
            if (selectedUIButton != null)
            {
                var colors = selectedUIButton.colors;
                colors.disabledColor = colors.pressedColor;
                selectedUIButton.colors = colors;
                selectedUIButton.interactable = false;
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
        if (heroButtons == null || heroButtons.Count == 0)
            return;
            
        for (int i = 0; i < heroButtons.Count; i++)
        {
            if (heroButtons[i] == null) continue;
            
            var button = heroButtons[i].GetComponent<Button>();
            if (button == null) continue;
            
            // If this is the selected button, keep it enabled but mark it as selected
            if (heroButtons[i] == selectedButton)
            {
                button.interactable = true;
                var colors = button.colors;
                colors.disabledColor = colors.pressedColor;
                button.colors = colors;
                button.interactable = false;
            }
            else
            {
                // Otherwise, enable/disable based on whether the hero is taken
                button.interactable = !selectedIds.Contains(i);
            }
        }
    }
}



