using UnityEngine;

[System.Serializable]
public class HeroData
{
    public string heroName;
    [TextArea(3, 10)]
    public string heroDescription;
    // Add other hero-specific data here if needed, like icons or stats
}
