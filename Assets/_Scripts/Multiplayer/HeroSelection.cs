using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        StartCoroutine(Countdown());

        // Select the first hero by default
        if (heroButtons.Count > 0)
        {
            Select(heroButtons[0]);
        }
    }

    void Select(HeroButton heroBtn)
    {
        if (selectedButton != null)
        {
            selectedButton.GetComponent<Button>().interactable = true;
        }

        heroBtn.GetComponent<Button>().interactable = false;
        selectedButton = heroBtn;

        // Update UI Text
        if (heroBtn.heroData != null)
        {
            heroNameText.text = heroBtn.heroData.heroName;
            heroDescriptionText.text = heroBtn.heroData.heroDescription;
        }
    }

    // Update is called once per frame
    public void Lockin()
    {
        if (selectedButton != null)
        {
            Button selectedUIButton = selectedButton.GetComponent<Button>();
            var colors = selectedUIButton.colors;
            colors.disabledColor = colors.pressedColor;
            selectedUIButton.colors = colors;
        }

        foreach (var heroBtn in heroButtons)
        {
            heroBtn.GetComponent<Button>().interactable = false;
        }
    }

    private IEnumerator Countdown()
    {
        float duration = 30f;
        float timer = duration;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            UpdateTimer(timer);
            yield return null;
        }

        UpdateTimer(0);
        Lockin();
    }

    public void UpdateTimer(float time)
    {
        if (time < 0) time = 0;
        Timer.text = time.ToString("00");
    }



}
