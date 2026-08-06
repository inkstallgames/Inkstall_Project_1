using UnityEngine;
using System;

[Serializable]
public class ButtonSettingsData
{
    public string buttonId;
    public Vector2 anchoredPosition;
    public Vector2 sizeDelta;
    public float scale;
    public bool isVisible;
    
    public ButtonSettingsData(string id, Vector2 position, Vector2 size, float defaultScale = 1f, bool visible = true)
    {
        buttonId = id;
        anchoredPosition = position;
        sizeDelta = size;
        scale = defaultScale;
        isVisible = visible;
    }
    
    public void SaveToPlayerPrefs()
    {
        string prefix = $"ButtonSettings_{buttonId}_";
        PlayerPrefs.SetFloat(prefix + "X", anchoredPosition.x);
        PlayerPrefs.SetFloat(prefix + "Y", anchoredPosition.y);
        PlayerPrefs.SetFloat(prefix + "Width", sizeDelta.x);
        PlayerPrefs.SetFloat(prefix + "Height", sizeDelta.y);
        PlayerPrefs.SetFloat(prefix + "Scale", scale);
        PlayerPrefs.SetInt(prefix + "Visible", isVisible ? 1 : 0);
    }
    
    public void LoadFromPlayerPrefs(Vector2 defaultPosition, Vector2 defaultSize)
    {
        string prefix = $"ButtonSettings_{buttonId}_";
        
        if (PlayerPrefs.HasKey(prefix + "X"))
        {
            anchoredPosition = new Vector2(
                PlayerPrefs.GetFloat(prefix + "X"),
                PlayerPrefs.GetFloat(prefix + "Y")
            );
            sizeDelta = new Vector2(
                PlayerPrefs.GetFloat(prefix + "Width"),
                PlayerPrefs.GetFloat(prefix + "Height")
            );
            scale = PlayerPrefs.GetFloat(prefix + "Scale", 1f);
            isVisible = PlayerPrefs.GetInt(prefix + "Visible", 1) == 1;
        }
        else
        {
            anchoredPosition = defaultPosition;
            sizeDelta = defaultSize;
            scale = 1f;
            isVisible = true;
        }
    }
    
    public void ResetToDefaults(Vector2 defaultPosition, Vector2 defaultSize)
    {
        anchoredPosition = defaultPosition;
        sizeDelta = defaultSize;
        scale = 1f;
        isVisible = true;
        SaveToPlayerPrefs();
    }
}
