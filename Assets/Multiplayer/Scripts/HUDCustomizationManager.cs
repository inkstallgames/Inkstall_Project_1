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

    public static bool IsEditMode { get; private set; }

    /// <summary>Set to true right before Instantiating the preview clone to prevent Singletons on the prefab from destroying it.</summary>
    public static bool IsCreatingPreview = false;

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

    [Header("Debugging")]
    [Tooltip("Logs every step of the Edit Controls flow. Turn off for release builds.")]
    [SerializeField] private bool logFlow = true;

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
    private UILayoutProfile    _liveEditBackup;  // layout as it was when in-match editing started
    private bool               _isLiveEditing;   // true while editing the real in-game HUD

    // Hierarchy changes made while live editing so the HUD the player is moving
    // draws above the panel's dim backdrop. Undone when editing ends.
    private struct MovedChild
    {
        public Transform child;
        public Transform parent;
        public int       index;
    }

    private readonly List<MovedChild> _movedToolbars = new List<MovedChild>();
    private readonly List<MovedChild> _raisedHudRoots = new List<MovedChild>();
    private Transform _panelOriginalParent;
    private int       _panelOriginalIndex;

    // The dim no longer covers the HUD, so the controls being dragged would still
    // fire (shoot, jump, move). Their raycasts are suppressed while editing.
    private struct BlockedElement
    {
        public CanvasGroup group;
        public bool        previousBlocksRaycasts;
        public bool        groupWasAdded;
    }

    private readonly List<BlockedElement> _blockedElements = new List<BlockedElement>();

    // Lobby / menu canvas siblings hidden while the preview editor is open.
    private readonly List<GameObject> _hiddenCanvasSiblings = new List<GameObject>();

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        if (IsCreatingPreview)
        {
            Destroy(this); // Just remove the script, don't destroy the Canvas!
            return;
        }

        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Wire buttons robustly
        if (openEditorButton != null) 
        {
            openEditorButton.onClick.RemoveAllListeners();
            openEditorButton.onClick.AddListener(OpenEditor);
        }
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(SaveCurrentEditor);
        }
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetCurrentEditor);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseCurrentEditor);
        }

        Flow($"Awake in scene '{gameObject.scene.name}': panel={(customizationPanel != null ? customizationPanel.name : "MISSING")}, " +
             $"save={(saveButton != null ? "ok" : "MISSING")}, reset={(resetButton != null ? "ok" : "MISSING")}, " +
             $"close={(closeButton != null ? "ok" : "MISSING")}, liveRoot={(liveHUDCanvasRoot != null ? liveHUDCanvasRoot.name : "auto")}.");

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

        // HOST / JOIN / EXIT etc. must not stay visible under the editor.
        HideCanvasSiblingsBehindEditor();
        HUDCustomizerPanelChrome.Apply(customizationPanel);

        // Load the saved profile (or build a default from the prefab)
        _workingProfile = UILayoutProfile.Load() ?? new UILayoutProfile();

        // Instantiate preview if not already present
        if (_previewInstance == null && hudCanvasPrefab != null)
        {
            IsCreatingPreview = true;
            _previewInstance = Instantiate(hudCanvasPrefab,
                previewParent != null ? (Transform)previewParent : transform);
            IsCreatingPreview = false;

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

            // Drop lobby/match chrome from the clone (Waiting For Players, menus, etc.)
            // so only draggable controls sit on the dim backdrop.
            HideNonEditablePreviewChrome(_previewInstance);
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

    /// <summary>
    /// Writes the preview layout to PlayerPrefs and keeps the editor open so the
    /// player can carry on adjusting. Only Close leaves the editor.
    /// </summary>
    public void SavePreviewLayout()
    {
        try
        {
            // Snapshot current positions/scales from the preview into _workingProfile
            BakeCurrentLayoutIntoProfile();
            _workingProfile.Save();
            Flow($"Saved preview layout for {_editableElements.Count} elements to PlayerPrefs. Editor stays open.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUDCustomizationManager] Error saving layout: {e}");
        }
    }

    public void SaveAndClose()
    {
        SavePreviewLayout();

        IsEditMode = false;
        if (customizationPanel != null) customizationPanel.SetActive(false);
        RestoreCanvasSiblingsBehindEditor();

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
        RestoreCanvasSiblingsBehindEditor();

        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }
    }

    public void ResetToDefaults()
    {
        try
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
                    if (elem == null || elem.RT == null) continue;

                    UIElementLayout def = factory.GetElement(elem.ElementId);
                    if (def == null) continue;

                    elem.RT.anchoredPosition = def.anchoredPosition;
                    elem.RT.localScale       = def.localScale;
                    elem.gameObject.SetActive(def.isVisible);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUDCustomizationManager] Error resetting to defaults: {e}");
        }
    }

    // ---------------------------------------------------------------
    // In-match live editing (no preview clone — the real HUD is moved)
    // ---------------------------------------------------------------

    public bool IsLiveEditing => _isLiveEditing;

    /// <summary>True while the real in-game HUD (not a preview clone) is being edited.</summary>
    public static bool IsLiveEditModeActive => Instance != null && Instance._isLiveEditing;

    /// <summary>
    /// Starts editing the HUD that is currently on screen during a match.
    /// Unlike <see cref="OpenEditor"/> there is no preview clone: the real
    /// elements are dragged, so the player sees exactly what they will get.
    /// </summary>
    public void OpenLiveEditor()
    {
        if (_isLiveEditing)
        {
            Flow("OpenLiveEditor ignored — already live editing.");
            return;
        }

        GameObject root = ResolveLiveRoot();
        if (root == null)
        {
            Debug.LogWarning("[HUDCustomizationManager] OpenLiveEditor: no live HUD canvas root found.");
            return;
        }

        Flow($"OpenLiveEditor: live root = '{root.name}' (scene '{root.scene.name}').");

        UILayoutProfile saved = UILayoutProfile.Load();
        _workingProfile = saved ?? new UILayoutProfile();
        Flow(saved != null
            ? "Loaded saved layout from PlayerPrefs."
            : "No saved layout yet — starting from current HUD positions.");

        _editableElements.Clear();
        _editableElements.AddRange(
            root.GetComponentsInChildren<DraggableHUDElement>(includeInactive: true));

        if (_editableElements.Count == 0)
        {
            Debug.LogWarning($"[HUDCustomizationManager] No DraggableHUDElement components found under '{root.name}'. Add the component to each HUD element you want the player to move.");
            return;
        }

        Flow($"Found {_editableElements.Count} draggable HUD elements: {string.Join(", ", _editableElements.ConvertAll(e => e != null ? e.ElementId : "null"))}");

        if (customizationPanel == null)
            Debug.LogWarning("[HUDCustomizationManager] 'Customization Panel' is not assigned in this scene, so the Save / Reset / Close UI cannot appear.");

        // Snapshot so Cancel restores the exact pre-edit state.
        _liveEditBackup = CaptureLayoutFromElements(_editableElements);

        // Only trust these positions as factory defaults when the player has
        // never saved a layout — otherwise we would bake their custom layout in.
        if (saved == null)
            UILayoutProfile.SaveFactoryDefaultsOnce(_liveEditBackup);

        _defaultProfile = UILayoutProfile.LoadFactory() ?? _liveEditBackup;

        if (customizationPanel != null)
        {
            customizationPanel.SetActive(true);
            HUDCustomizerPanelChrome.Apply(customizationPanel);

            // Dim the scene, but let the controls being edited sit on top of the dim.
            RaiseLiveEditLayers();
        }

        _isLiveEditing = true;
        IsEditMode     = true;

        Flow($"Live edit mode ON. Panel '{(customizationPanel != null ? customizationPanel.name : "none")}' active = {(customizationPanel != null && customizationPanel.activeInHierarchy)}.");
    }

    /// <summary>
    /// Writes the current on-screen layout to PlayerPrefs and keeps the editor
    /// open so the player can carry on adjusting. Only Close leaves edit mode.
    /// </summary>
    public void SaveLiveEditor()
    {
        if (!_isLiveEditing)
        {
            Flow("SaveLiveEditor ignored — not live editing.");
            return;
        }

        try
        {
            BakeCurrentLayoutIntoProfile();
            _workingProfile.Save();

            // Closing later must keep what was just saved, not revert to the
            // positions the controls had when editing started.
            _liveEditBackup = CaptureLayoutFromElements(_editableElements);

            Flow($"Saved live layout for {_editableElements.Count} elements to PlayerPrefs. Editor stays open.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HUDCustomizationManager] Error saving live layout: {e}");
        }
    }

    /// <summary>
    /// Leaves edit mode, discarding anything moved since the last Save (or since
    /// editing started, if the player never saved).
    /// </summary>
    public void CancelLiveEditor()
    {
        if (!_isLiveEditing)
        {
            Flow("CancelLiveEditor ignored — not live editing.");
            return;
        }

        Flow("Close pressed — restoring the last saved layout and leaving edit mode.");
        ApplyProfileToElements(_liveEditBackup);
        EndLiveEdit();
    }

    /// <summary>
    /// Moves the live elements back to factory defaults without touching
    /// PlayerPrefs — the player still has to press Save to keep it.
    /// </summary>
    public void ResetLiveToDefaults()
    {
        if (!_isLiveEditing)
        {
            Flow("ResetLiveToDefaults ignored — not live editing.");
            return;
        }

        Flow(_defaultProfile != null
            ? "Reset pressed — moving live HUD back to factory defaults (not saved until Save)."
            : "Reset pressed but no factory default layout is available.");
        ApplyProfileToElements(_defaultProfile);
    }

    private void EndLiveEdit()
    {
        IsEditMode     = false;
        _isLiveEditing = false;
        _liveEditBackup = null;
        _editableElements.Clear();

        ClearLiveEditLayers();

        if (customizationPanel != null)
            customizationPanel.SetActive(false);

        Flow("Live edit mode OFF — handing control back to gameplay.");

        NetworkUIManager.Instance?.FinishLiveControlEditing();
    }

    private void SaveCurrentEditor()
    {
        Flow($"Save button pressed (live editing = {_isLiveEditing}).");

        if (_isLiveEditing)
            SaveLiveEditor();
        else
            SavePreviewLayout();
    }

    private void ResetCurrentEditor()
    {
        Flow($"Reset button pressed (live editing = {_isLiveEditing}).");

        if (_isLiveEditing)
            ResetLiveToDefaults();
        else
            ResetToDefaults();
    }

    private void CloseCurrentEditor()
    {
        Flow($"Close button pressed (live editing = {_isLiveEditing}).");

        if (_isLiveEditing)
            CancelLiveEditor();
        else
            CloseWithoutSaving();
    }

    /// <summary>
    /// Stack order while live editing (must stay under the customization panel's
    /// own Canvas — chrome sets overrideSorting + sortingOrder 500):
    ///   dim backdrop  →  editable HUD  →  TopUI toolbar
    /// Reparenting HUD/toolbar onto the root canvas would put them under the dim.
    /// </summary>
    private void RaiseLiveEditLayers()
    {
        ClearLiveEditLayers();

        BlockElementInput();

        if (customizationPanel == null) return;

        Canvas parentCanvas = customizationPanel.GetComponentInParent<Canvas>();
        RectTransform canvasRT = parentCanvas != null ? parentCanvas.rootCanvas.transform as RectTransform : null;
        if (canvasRT == null) return;

        // Dim covers waiting screens / menus / non-editable chrome on this canvas.
        _panelOriginalParent = customizationPanel.transform.parent;
        _panelOriginalIndex  = customizationPanel.transform.GetSiblingIndex();
        customizationPanel.transform.SetParent(canvasRT, false);
        customizationPanel.transform.SetAsLastSibling();

        // Lift each editable control into the customization panel so they share its
        // Canvas sorting (sortingOrder 500). Sibling order on the root canvas cannot beat that.
        var raised = new HashSet<Transform>();
        foreach (DraggableHUDElement elem in _editableElements)
        {
            if (elem == null) continue;

            Transform target = elem.transform;
            if (target.IsChildOf(customizationPanel.transform))
                continue;

            // Raise the highest ancestor that does not contain the editor panel.
            // That keeps joystick groups / button clusters together without swallowing Settings.
            Transform root = target;
            while (root.parent != null &&
                   root.parent != canvasRT &&
                   root.parent != customizationPanel.transform &&
                   !customizationPanel.transform.IsChildOf(root.parent))
            {
                root = root.parent;
            }

            if (root == customizationPanel.transform) continue;
            if (customizationPanel.transform.IsChildOf(root)) continue;
            if (!raised.Add(root)) continue;

            _raisedHudRoots.Add(new MovedChild
            {
                child  = root,
                parent = root.parent,
                index  = root.GetSiblingIndex()
            });
        }

        foreach (MovedChild moved in _raisedHudRoots)
        {
            // worldPositionStays — keep on-screen positions while changing parents
            moved.child.SetParent(customizationPanel.transform, true);
            moved.child.SetAsLastSibling();
        }

        // Toolbar must stay above the raised controls (still inside the panel)
        Transform topUi = customizationPanel.transform.Find("TopUI");
        if (topUi != null)
            topUi.SetAsLastSibling();

        // Drop unused toolbar-move tracking from older layering approach
        _movedToolbars.Clear();

        Flow($"Dim covers non-editable UI; raised {_raisedHudRoots.Count} HUD root(s) into the Edit Controls panel above the dim.");
    }

    /// <summary>
    /// Hides preview canvas children that have no DraggableHUDElement so match/lobby
    /// screens (e.g. Waiting For Players) do not sit on top of the edit backdrop.
    /// </summary>
    private static void HideNonEditablePreviewChrome(GameObject previewRoot)
    {
        if (previewRoot == null) return;

        for (int i = 0; i < previewRoot.transform.childCount; i++)
        {
            Transform child = previewRoot.transform.GetChild(i);
            if (child.GetComponentInChildren<DraggableHUDElement>(true) == null)
                child.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Turns off every canvas sibling of the customization panel (MainMenuPanel with
    /// HOST/JOIN, settings, EXIT, etc.) so only the editor + HUD preview remain.
    /// </summary>
    private void HideCanvasSiblingsBehindEditor()
    {
        RestoreCanvasSiblingsBehindEditor();

        if (customizationPanel == null) return;

        Transform parent = customizationPanel.transform.parent;
        if (parent == null) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform sibling = parent.GetChild(i);
            if (sibling == customizationPanel.transform) continue;
            if (!sibling.gameObject.activeSelf) continue;

            sibling.gameObject.SetActive(false);
            _hiddenCanvasSiblings.Add(sibling.gameObject);
        }

        Flow($"Hid {_hiddenCanvasSiblings.Count} lobby/menu sibling(s) while Edit Controls is open.");
    }

    private void RestoreCanvasSiblingsBehindEditor()
    {
        for (int i = 0; i < _hiddenCanvasSiblings.Count; i++)
        {
            GameObject go = _hiddenCanvasSiblings[i];
            if (go != null) go.SetActive(true);
        }

        if (_hiddenCanvasSiblings.Count > 0)
            Flow($"Restored {_hiddenCanvasSiblings.Count} lobby/menu sibling(s).");

        _hiddenCanvasSiblings.Clear();
    }

    private void BlockElementInput()
    {
        _blockedElements.Clear();

        foreach (DraggableHUDElement elem in _editableElements)
        {
            if (elem == null) continue;

            CanvasGroup group = elem.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = elem.gameObject.AddComponent<CanvasGroup>();

            _blockedElements.Add(new BlockedElement
            {
                group                  = group,
                previousBlocksRaycasts = group.blocksRaycasts,
                groupWasAdded          = added
            });

            group.blocksRaycasts = false;
        }

        Flow($"Suppressed input on {_blockedElements.Count} control(s) so editing cannot fire them.");
    }

    private void RestoreElementInput()
    {
        foreach (BlockedElement blocked in _blockedElements)
        {
            if (blocked.group == null) continue;

            if (blocked.groupWasAdded)
                Destroy(blocked.group);
            else
                blocked.group.blocksRaycasts = blocked.previousBlocksRaycasts;
        }

        if (_blockedElements.Count > 0)
            Flow($"Re-enabled input on {_blockedElements.Count} control(s).");

        _blockedElements.Clear();
    }

    private void ClearLiveEditLayers()
    {
        RestoreElementInput();

        // Older builds moved toolbar out of the panel; put those back if present.
        foreach (MovedChild moved in _movedToolbars)
        {
            if (moved.child == null || moved.parent == null) continue;

            moved.child.SetParent(moved.parent, false);
            moved.child.SetSiblingIndex(moved.index);
        }

        if (_movedToolbars.Count > 0)
            Flow($"Returned {_movedToolbars.Count} toolbar object(s) to the customization panel.");

        _movedToolbars.Clear();

        // Restore HUD to original parents (worldPositionStays keeps layout stable).
        for (int i = _raisedHudRoots.Count - 1; i >= 0; i--)
        {
            MovedChild moved = _raisedHudRoots[i];
            if (moved.child == null || moved.parent == null) continue;
            moved.child.SetParent(moved.parent, true);
            moved.child.SetSiblingIndex(moved.index);
        }

        if (_raisedHudRoots.Count > 0)
            Flow($"Restored {_raisedHudRoots.Count} HUD root(s) to their gameplay parents.");

        _raisedHudRoots.Clear();

        if (customizationPanel != null && _panelOriginalParent != null)
        {
            customizationPanel.transform.SetParent(_panelOriginalParent, false);
            customizationPanel.transform.SetSiblingIndex(_panelOriginalIndex);
            _panelOriginalParent = null;
        }
    }

    private void Flow(string message)
    {
        if (logFlow) Debug.Log($"[HUDCustomize] {message}");
    }

    private GameObject ResolveLiveRoot()
    {
        // A prefab asset dragged into this field would move the asset, not the
        // HUD on screen, so only accept a reference that lives in a loaded scene.
        if (liveHUDCanvasRoot != null)
        {
            if (liveHUDCanvasRoot.scene.IsValid())
                return liveHUDCanvasRoot;

            Debug.LogWarning("[HUDCustomizationManager] liveHUDCanvasRoot points to a prefab asset, not the in-scene HUD. Falling back to the active HUD canvas.");
        }

        if (NetworkUIManager.Instance != null)
        {
            Canvas canvas = NetworkUIManager.Instance.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.gameObject;
            return NetworkUIManager.Instance.gameObject;
        }

        Canvas own = GetComponentInParent<Canvas>();
        return own != null ? own.rootCanvas.gameObject : null;
    }

    private void ApplyProfileToElements(UILayoutProfile profile)
    {
        if (profile == null) return;

        foreach (var elem in _editableElements)
        {
            if (elem == null || elem.RT == null) continue;

            UIElementLayout layout = profile.GetElement(elem.ElementId);
            if (layout == null) continue;

            elem.RT.anchoredPosition = layout.anchoredPosition;
            elem.RT.localScale       = layout.localScale;
            elem.gameObject.SetActive(layout.isVisible);
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
            // Safely skip any elements that were destroyed while the editor was open
            if (elem == null || elem.RT == null) continue;

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
