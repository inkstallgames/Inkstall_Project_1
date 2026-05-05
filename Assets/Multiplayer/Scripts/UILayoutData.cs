using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds the layout profile for one HUD element.
/// Everything here must be JSON-serializable by Unity's JsonUtility.
/// </summary>
[Serializable]
public class UIElementLayout
{
    /// <summary>Unique name matching the GameObject name in the HUD canvas.</summary>
    public string elementId;

    /// <summary>Anchored position in the Canvas coordinate system.</summary>
    public Vector2 anchoredPosition;

    /// <summary>Local scale of the RectTransform.</summary>
    public Vector3 localScale;

    /// <summary>Whether this element is visible.</summary>
    public bool isVisible;

    public UIElementLayout() { }

    public UIElementLayout(string id, Vector2 pos, Vector3 scale, bool visible)
    {
        elementId        = id;
        anchoredPosition = pos;
        localScale       = scale;
        isVisible        = visible;
    }
}

/// <summary>
/// Full HUD layout profile — a list of element layouts.
/// Serialized to JSON and stored in PlayerPrefs under the key "HUDLayout".
/// </summary>
[Serializable]
public class UILayoutProfile
{
    public List<UIElementLayout> elements = new List<UIElementLayout>();

    // ---------------------------------------------------------------
    // Persistence helpers
    // ---------------------------------------------------------------

    private const string PREFS_KEY    = "HUDLayout_v1";
    private const string FACTORY_KEY  = "HUDLayout_factory_v1"; // written once, never overwritten

    /// <summary>Save this profile to PlayerPrefs (the user's current layout).</summary>
    public void Save()
    {
        string json = JsonUtility.ToJson(this, prettyPrint: false);
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Saves <paramref name="profile"/> as the permanent factory defaults
    /// ONLY if factory defaults have not been written before.
    /// Call this on the first editor open — subsequent calls are no-ops.
    /// </summary>
    public static void SaveFactoryDefaultsOnce(UILayoutProfile profile)
    {
        if (PlayerPrefs.HasKey(FACTORY_KEY)) return; // already saved — never overwrite
        string json = JsonUtility.ToJson(profile, prettyPrint: false);
        PlayerPrefs.SetString(FACTORY_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>Load factory defaults. Returns null if never saved yet.</summary>
    public static UILayoutProfile LoadFactory()
    {
        if (!PlayerPrefs.HasKey(FACTORY_KEY)) return null;
        try
        {
            return JsonUtility.FromJson<UILayoutProfile>(PlayerPrefs.GetString(FACTORY_KEY, ""));
        }
        catch { return null; }
    }

    /// <summary>
    /// Load a saved profile from PlayerPrefs.
    /// Returns null if no profile has been saved yet.
    /// </summary>
    public static UILayoutProfile Load()
    {
        if (!PlayerPrefs.HasKey(PREFS_KEY)) return null;
        string json = PlayerPrefs.GetString(PREFS_KEY, "");
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var profile = JsonUtility.FromJson<UILayoutProfile>(json);
            return profile;
        }
        catch
        {
            Debug.LogWarning("[UILayoutProfile] Failed to parse saved layout. Resetting.");
            PlayerPrefs.DeleteKey(PREFS_KEY);
            return null;
        }
    }

    /// <summary>Delete the user's saved layout. Factory defaults are untouched.</summary>
    public static void DeleteSaved() => PlayerPrefs.DeleteKey(PREFS_KEY);


    // ---------------------------------------------------------------
    // Element helpers
    // ---------------------------------------------------------------

    public UIElementLayout GetElement(string id)
        => elements.Find(e => e.elementId == id);

    public void SetElement(UIElementLayout layout)
    {
        int idx = elements.FindIndex(e => e.elementId == layout.elementId);
        if (idx >= 0) elements[idx] = layout;
        else          elements.Add(layout);
    }
}
