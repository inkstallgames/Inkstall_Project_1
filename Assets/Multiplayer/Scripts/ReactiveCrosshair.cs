using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

/// <summary>
/// Reactive crosshair with movement error simulation.
/// Attach this script to a crosshair root UI GameObject that contains four line Images:
///   topLine, bottomLine, leftLine, rightLine.
///
/// The crosshair spreads apart when the player moves and returns to its default
/// (tight) size when the player is standing still. All transitions are smoothly
/// lerped for a professional feel.
///
/// Setup in the Unity Editor:
///   1. Create a Canvas > Panel (CrosshairRoot).
///   2. Inside CrosshairRoot add four Image children named Top, Bottom, Left, Right.
///      Size each one appropriately (e.g. 2x10 for vertical, 10x2 for horizontal).
///   3. Assign those Images to the fields below.
///   4. Assign this component's references via the Inspector.
/// </summary>
public class ReactiveCrosshair : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector Fields — Crosshair Lines
    // ---------------------------------------------------------------

    [Header("Crosshair Lines")]
    [Tooltip("Top line of the crosshair.")]
    [SerializeField] private RectTransform topLine;

    [Tooltip("Bottom line of the crosshair.")]
    [SerializeField] private RectTransform bottomLine;

    [Tooltip("Left line of the crosshair.")]
    [SerializeField] private RectTransform leftLine;

    [Tooltip("Right line of the crosshair.")]
    [SerializeField] private RectTransform rightLine;

    // ---------------------------------------------------------------
    // Inspector Fields — Spread Settings
    // ---------------------------------------------------------------

    [Header("Crosshair Spread Settings")]
    [Tooltip("Gap between the centre and each crosshair line when the player is perfectly still.")]
    [SerializeField] private float defaultSpread = 8f;

    [Tooltip("Maximum gap between the centre and each line when the player is at full sprint speed.")]
    [SerializeField] private float maxSpread = 50f;

    [Tooltip("How fast the crosshair expands when the player starts moving (higher = snappier).")]
    [SerializeField] private float expandSpeed = 10f;

    [Tooltip("How fast the crosshair contracts when the player stops moving (higher = snappier).")]
    [SerializeField] private float contractSpeed = 6f;

    // ---------------------------------------------------------------
    // Inspector Fields — Optional Colour Tint
    // ---------------------------------------------------------------

    [Header("Colour Tint (optional)")]
    [Tooltip("Crosshair colour when standing still.")]
    [SerializeField] private Color defaultColour = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("Crosshair colour when moving at maximum speed.")]
    [SerializeField] private Color movingColour = new Color(1f, 0.45f, 0.05f, 1f);  // warm orange

    [Tooltip("Enable colour transition based on movement.")]
    [SerializeField] private bool enableColourTransition = true;

    // ---------------------------------------------------------------
    // Private State
    // ---------------------------------------------------------------

    private float _currentSpread;
    private Image[] _lineImages;

    // Cached reference to the local player's StarterAssetsInputs
    // (fetched once from the ThirdPersonController that has input authority).
    private StarterAssetsInputs _localInput;
    private bool _inputCached = false;

    // ---------------------------------------------------------------
    // Unity Lifecycle
    // ---------------------------------------------------------------

    private void Awake()
    {
        _currentSpread = defaultSpread;
        CacheLineImages();
    }

    private float _findInputRetryTimer;

    private void Update()
    {
        // Lazily find the local player's input component (it may not exist at Start)
        if (!_inputCached)
        {
            _findInputRetryTimer -= Time.deltaTime;
            if (_findInputRetryTimer <= 0f)
            {
                _findInputRetryTimer = 0.35f;
                TryFindLocalInput();
            }
        }

        float moveMagnitude = GetLocalMoveMagnitude();
        UpdateSpread(moveMagnitude);
        ApplySpread();

        if (enableColourTransition)
        {
            ApplyColour(moveMagnitude);
        }
    }

    // ---------------------------------------------------------------
    // Input Discovery
    // ---------------------------------------------------------------

    /// <summary>
    /// Searches the scene for the local player's StarterAssetsInputs component.
    /// Only the local player's ThirdPersonController has input authority.
    /// </summary>
    private void TryFindLocalInput()
    {
        // ThirdPersonController lives on the player NetworkObject.
        // Only the one with HasInputAuthority has StarterAssetsInputs enabled.
        var allInputs = FindObjectsOfType<StarterAssetsInputs>();
        foreach (var inp in allInputs)
        {
            if (inp.enabled)
            {
                _localInput = inp;
                _inputCached = true;
                return;
            }
        }
    }

    /// <summary>
    /// Returns the current movement magnitude of the local player (0–1).
    /// Checks the virtual joystick first (mobile), then StarterAssetsInputs (PC/keyboard).
    /// Falls back to zero if neither is found.
    /// </summary>
    private float GetLocalMoveMagnitude()
    {
        // Mobile: prefer the network joystick if it is active
        if (NetworkJoystickControl.Instance != null)
        {
            float joystickMag = NetworkJoystickControl.Instance.MovementInput.magnitude;
            if (joystickMag > 0.01f)
                return Mathf.Clamp01(joystickMag);
        }

        // PC / keyboard fallback
        if (_localInput == null) return 0f;
        return Mathf.Clamp01(_localInput.move.magnitude);
    }

    // ---------------------------------------------------------------
    // Spread Logic
    // ---------------------------------------------------------------

    private void UpdateSpread(float moveMagnitude)
    {
        // Target spread is linearly interpolated between default and max
        float targetSpread = Mathf.Lerp(defaultSpread, maxSpread, moveMagnitude);

        // Use different speeds for expanding and contracting
        float speed = (targetSpread > _currentSpread) ? expandSpeed : contractSpeed;
        _currentSpread = Mathf.Lerp(_currentSpread, targetSpread, Time.deltaTime * speed);
    }

    private void ApplySpread()
    {
        if (topLine    != null) SetAnchoredY(topLine,     _currentSpread);
        if (bottomLine != null) SetAnchoredY(bottomLine, -_currentSpread);
        if (leftLine   != null) SetAnchoredX(leftLine,   -_currentSpread);
        if (rightLine  != null) SetAnchoredX(rightLine,   _currentSpread);
    }

    // ---------------------------------------------------------------
    // Colour Logic
    // ---------------------------------------------------------------

    private void ApplyColour(float moveMagnitude)
    {
        if (_lineImages == null) return;
        Color targetColour = Color.Lerp(defaultColour, movingColour, moveMagnitude);
        foreach (var img in _lineImages)
        {
            if (img != null) img.color = targetColour;
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void SetAnchoredY(RectTransform rt, float y)
    {
        var pos = rt.anchoredPosition;
        pos.y = y;
        rt.anchoredPosition = pos;
    }

    private void SetAnchoredX(RectTransform rt, float x)
    {
        var pos = rt.anchoredPosition;
        pos.x = x;
        rt.anchoredPosition = pos;
    }

    private void CacheLineImages()
    {
        _lineImages = new Image[4];
        if (topLine    != null) _lineImages[0] = topLine.GetComponent<Image>();
        if (bottomLine != null) _lineImages[1] = bottomLine.GetComponent<Image>();
        if (leftLine   != null) _lineImages[2] = leftLine.GetComponent<Image>();
        if (rightLine  != null) _lineImages[3] = rightLine.GetComponent<Image>();
    }

    // ---------------------------------------------------------------
    // Public API — call from NetworkUIManager or other scripts
    // ---------------------------------------------------------------

    /// <summary>
    /// Force-reset the cached input reference (e.g. after a respawn when the
    /// local player NetworkObject is destroyed and re-spawned).
    /// </summary>
    public void ResetInputCache()
    {
        _localInput = null;
        _inputCached = false;
    }

    /// <summary>
    /// Instantly snap the crosshair to the default (tight) spread without lerping.
    /// Useful when showing the crosshair for the first time or after a teleport.
    /// </summary>
    public void SnapToDefault()
    {
        _currentSpread = defaultSpread;
        ApplySpread();
    }

    /// <summary>
    /// Show or hide the entire crosshair.
    /// </summary>
    public void SetVisible(bool visible) => gameObject.SetActive(visible);
}
