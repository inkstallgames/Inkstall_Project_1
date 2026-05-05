using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Master controller for the HUD Customization system.
///
/// ┌─────────────────────────────────────────────────────────────────┐
/// │  LOBBY SCENE                                                    │
/// │  • Attach to a "HUD Customizer" GameObject in MultiplayerLobby │
/// │  • Assign the HUD Canvas Prefab preview reference               │
/// │  • Opening the panel instantiates the HUD preview so players   │
/// │    can drag elements and see changes live                       │
/// │  • On "Save" the layout is written to PlayerPrefs               │
/// │                                                                 │
/// │  RUST GAME SCENE                                                │
/// │  • Attach to the same HUD Canvas (NetworkUIManager's canvas)   │
/// │  • On Awake, ApplySavedLayout() is called automatically        │
/// │  • The player's saved positions/scales are applied instantly    │
/// └─────────────────────────────────────────────────────────────────┘
/// </summary>
public class HUDCustomizationManager : MonoBehaviour
{
    public static HUDCustomizationManager Instance { get; private set; }

    /// <summary>True while the HUD editor is open. DraggableHUDElement reads this.</summary>
    public static bool IsEditMode { get; private set; }

    // ---------------------------------------------------------------
    // Inspector — assign in Lobby scene
    // ---------------------------------------------------------------

    [Header("Lobby — HUD Preview")]
    [Tooltip("The HUD canvas PREFAB to instantiate for the live preview in the lobby.")]
    [SerializeField] private GameObject hudCanvasPrefab;

    [Tooltip("Parent transform where the preview canvas is instantiated (e.g. a dedicated panel).")]
    [SerializeField] private RectTransform previewParent;

    [Header("Editor UI")]
    [Tooltip("The root panel that contains all customization editor UI (shown/hidden as a whole).")]
    [SerializeField] private GameObject customizationPanel;

    [Tooltip("Container where per-element toggle rows are created at runtime.")]
    [SerializeField] private RectTransform elementListContainer;

    [Tooltip("Prefab for a single element row: Toggle (visibility) + Label (name).")]
    [SerializeField] private GameObject elementRowPrefab;

    [Header("Buttons")]
    [SerializeField] private Button openEditorButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;

    [Header("Rust Game Scene — apply on load")]
    [Tooltip("In the Rust scene, assign the live HUD canvas root here so layout is applied on Awake.")]
    [SerializeField] private GameObject liveHUDCanvasRoot;

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------

    private GameObject         _previewInstance;
    private List<DraggableHUDElement> _editableElements = new List<DraggableHUDElement>();
    private UILayoutProfile    _workingProfile;  // edited in-memory; only saved on confirm
    private UILayoutProfile    _defaultProfile;  // prefab positions captured on first open

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Wire buttons
        if (openEditorButton != null) openEditorButton.onClick.AddListener(OpenEditor);
        if (saveButton       != null) saveButton.onClick.AddListener(SaveAndClose);
        if (resetButton      != null) resetButton.onClick.AddListener(ResetToDefaults);
        if (closeButton      != null) closeButton.onClick.AddListener(CloseWithoutSaving);

        // Start with panel hidden
        if (customizationPanel != null)
            customizationPanel.SetActive(false);
    }

    /// <summary>
    /// Apply saved layout in Start (not Awake) so every HUD element is fully
    /// initialized before we move them. This also means it works without
    /// quitting — the layout written in the Lobby is read on the next scene load.
    /// </summary>
    private void Start()
    {
        if (liveHUDCanvasRoot != null)
            ApplySavedLayout(liveHUDCanvasRoot);
    }

    /// <summary>
    /// Call this from NetworkUIManager after the local player spawns to re-apply
    /// the layout in case any HUD elements were enabled post-Start.
    /// </summary>
    public void ApplyNow()
    {
        if (liveHUDCanvasRoot != null)
            ApplySavedLayout(liveHUDCanvasRoot);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsEditMode = false;
    }

    // ---------------------------------------------------------------
    // Open / Close editor
    // ---------------------------------------------------------------

    public void OpenEditor()
    {
        if (customizationPanel != null)
            customizationPanel.SetActive(true);

        // Load the saved profile (or build a default from the prefab)
        _workingProfile = UILayoutProfile.Load() ?? new UILayoutProfile();

        // Instantiate preview if not already present
        if (_previewInstance == null && hudCanvasPrefab != null)
        {
            _previewInstance = Instantiate(hudCanvasPrefab,
                previewParent != null ? (Transform)previewParent : transform);

            // CRITICAL: Use DestroyImmediate (not Destroy) so the Canvas components
            // are fully removed BEFORE we set RectTransform values below.
            // Destroy() is deferred to end-of-frame — the Canvas would still be alive
            // when we set anchors/offsets and would reset them to its own pixel values.
            var nestedScaler    = _previewInstance.GetComponent<UnityEngine.UI.CanvasScaler>();
            var nestedRaycaster = _previewInstance.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            var nestedCanvas    = _previewInstance.GetComponent<UnityEngine.Canvas>();

            if (nestedScaler    != null) DestroyImmediate(nestedScaler);
            if (nestedRaycaster != null) DestroyImmediate(nestedRaycaster);
            if (nestedCanvas    != null) DestroyImmediate(nestedCanvas);

            // Now safely configure the RectTransform to stretch-fill the preview panel.
            var rt = _previewInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.one;
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero; // center on parent
                rt.sizeDelta        = Vector2.zero; // no extra size beyond anchor rect
                rt.localScale       = Vector3.one;
            }
        }

        // Collect all draggable elements in the preview
        _editableElements.Clear();
        if (_previewInstance != null)
        {
            _editableElements.AddRange(
                _previewInstance.GetComponentsInChildren<DraggableHUDElement>(includeInactive: true));
        }

        // Capture current (prefab-fresh) positions as _defaultProfile for this session.
        _defaultProfile = CaptureLayoutFromElements(_editableElements);

        // Write factory defaults to PlayerPrefs exactly ONCE — never overwrite.
        // This is the permanent "Reset" target no matter how many times the player saves.
        UILayoutProfile.SaveFactoryDefaultsOnce(_defaultProfile);

        // Apply saved layout to the preview so the player sees their current setup
        if (_previewInstance != null)
            ApplySavedLayout(_previewInstance);

        // Build the element toggle list
        BuildElementList();

        IsEditMode = true;
    }

    public void SaveAndClose()
    {
        // Snapshot current positions/scales from the preview into _workingProfile
        BakeCurrentLayoutIntoProfile();
        _workingProfile.Save();

        IsEditMode = false;
        if (customizationPanel != null) customizationPanel.SetActive(false);

        // Destroy preview to free memory
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    public void CloseWithoutSaving()
    {
        IsEditMode = false;
        if (customizationPanel != null) customizationPanel.SetActive(false);

        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    public void ResetToDefaults()
    {
        // Restore from the permanent factory defaults (saved once, never overwritten).
        UILayoutProfile factory = UILayoutProfile.LoadFactory();
        if (factory == null) factory = _defaultProfile; // fallback to session snapshot

        _workingProfile = new UILayoutProfile();
        UILayoutProfile.DeleteSaved(); // clear user saves so Rust scene also resets

        if (factory != null)
        {
            foreach (var elem in _editableElements)
            {
                UIElementLayout def = factory.GetElement(elem.ElementId);
                if (def == null) continue;

                elem.RT.anchoredPosition = def.anchoredPosition;
                elem.RT.localScale       = def.localScale;
                elem.gameObject.SetActive(def.isVisible);
            }
        }
    }

    // ---------------------------------------------------------------
    // Apply saved layout to a canvas root
    // ---------------------------------------------------------------

    /// <summary>
    /// Reads PlayerPrefs and moves / scales every DraggableHUDElement found
    /// under <paramref name="canvasRoot"/> to match the saved profile.
    /// Safe to call at any time; silently skips elements with no saved entry.
    /// </summary>
    public static void ApplySavedLayout(GameObject canvasRoot)
    {
        if (canvasRoot == null) return;

        UILayoutProfile profile = UILayoutProfile.Load();
        if (profile == null) return; // nothing saved yet — keep defaults

        var elements = canvasRoot.GetComponentsInChildren<DraggableHUDElement>(includeInactive: true);
        foreach (var elem in elements)
        {
            UIElementLayout saved = profile.GetElement(elem.ElementId);
            if (saved == null) continue;

            RectTransform rt = elem.RT;
            rt.anchoredPosition = saved.anchoredPosition;
            rt.localScale       = saved.localScale;
            elem.gameObject.SetActive(saved.isVisible);
        }
    }

    // ---------------------------------------------------------------
    // Snapshot helper
    // ---------------------------------------------------------------

    private void BakeCurrentLayoutIntoProfile()
    {
        _workingProfile = CaptureLayoutFromElements(_editableElements);
    }

    /// <summary>
    /// Snapshots the current anchoredPosition / localScale / active state
    /// of all elements into a fresh UILayoutProfile.
    /// </summary>
    private UILayoutProfile CaptureLayoutFromElements(List<DraggableHUDElement> elements)
    {
        var profile = new UILayoutProfile();
        foreach (var elem in elements)
        {
            profile.SetElement(new UIElementLayout(
                elem.ElementId,
                elem.RT.anchoredPosition,
                elem.RT.localScale,
                elem.gameObject.activeSelf
            ));
        }
        return profile;
    }

    // ---------------------------------------------------------------
    // Element toggle list builder
    // ---------------------------------------------------------------

    private void BuildElementList()
    {
        if (elementListContainer == null || elementRowPrefab == null) return;

        // Clear old rows
        foreach (Transform child in elementListContainer)
            Destroy(child.gameObject);

        foreach (var elem in _editableElements)
        {
            if (!elem.allowToggleVisibility) continue;

            GameObject row = Instantiate(elementRowPrefab, elementListContainer);

            // Expect the row prefab to have: Toggle + TextMeshProUGUI (label)
            Toggle toggle = row.GetComponentInChildren<Toggle>();
            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();

            if (label  != null) label.text = elem.displayName;
            if (toggle != null)
            {
                toggle.isOn = elem.gameObject.activeSelf;

                // Capture elem in a local variable for the closure
                DraggableHUDElement captured = elem;
                toggle.onValueChanged.AddListener(isOn =>
                {
                    captured.gameObject.SetActive(isOn);
                });
            }
        }
    }
}
