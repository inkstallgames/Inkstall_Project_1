using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroSelection : MonoBehaviour
{
    public List<Button> heroButtons;
    private Button selectedButton;

    void Start()
    {
        foreach (Button button in heroButtons)
        {
            button.onClick.AddListener(() => Select(button));
        }
    }

    void Select(Button button)
    {
        if (selectedButton != null)
        {
            selectedButton.interactable = true;
        }

        button.interactable = false;
        selectedButton = button;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
