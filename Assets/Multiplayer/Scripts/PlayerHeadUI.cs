using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Player head UI — shows name and health bar above each remote player's head.
/// Reads the same [Networked] Health value that RPC_UpdateHealth sets on all clients,
/// so it updates at exactly the same time as the screen UI health bar.
/// Hidden for the local player (they use the screen HUD).
/// </summary>
public class PlayerHeadUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("UI References")]
    public Canvas          uiCanvas;
    public TextMeshProUGUI playerNameText;
    public Slider          healthBarSlider;
    public Image           healthBarFill;   // the Fill image inside the Slider

    [Header("Billboard Settings")]
    public Vector3 headOffset = new Vector3(0f, 2.2f, 0f);
    public bool    faceCamera = true;

    [Header("Health Bar Colors")]
    public Color highHealthColor = new Color(0.18f, 0.85f, 0.18f);  // green
    public Color midHealthColor  = new Color(1.00f, 0.70f, 0.00f);  // yellow-orange
    public Color lowHealthColor  = new Color(0.90f, 0.10f, 0.10f);  // red

    [Header("Smoothing")]
    [Tooltip("How fast the health bar slides to the new value (higher = faster)")]
    [SerializeField] private float healthLerpSpeed = 8f;

    // ── Private state ──────────────────────────────────────────────────
    private PlayerNetworkData _playerData;
    private Camera            _cam;
    private float             _targetHealth  = 100f;
    private float             _displayHealth = 100f;
    private string            _cachedName;

    // ── Debug toggle ───────────────────────────────────────────────────
    [Header("Debug")]
    [Tooltip("Enable to print diagnostic logs to the Console")]
    public bool debugLogs = true;

    // ── Unity lifecycle ────────────────────────────────────────────────

    private void Awake()
    {
        // Use Canvas.enabled instead of SetActive(false)!
        // SetActive(false) was deactivating THIS script's own GameObject (or its parent),
        // which prevented Start() from ever being called — breaking everything.
        // Canvas.enabled = false hides all rendering without touching the GameObject state.
        if (uiCanvas != null)
        {
            uiCanvas.enabled = false;
            if (debugLogs) Debug.Log("[PlayerHeadUI] Awake — canvas DISABLED (rendering hidden, GameObject stays active so Start() will fire).");
        }
        else
        {
            if (debugLogs) Debug.LogWarning("[PlayerHeadUI] Awake — uiCanvas is NULL! Assign it in the Inspector.");
        }
    }

    private void Start()
    {
        // Start() is always called by Unity after the object is active —
        // reliable for getting references even on child GameObjects.
        _playerData = GetComponentInParent<PlayerNetworkData>();
        _cam        = Camera.main;

        if (uiCanvas != null)
        {
            uiCanvas.renderMode  = RenderMode.WorldSpace;
            uiCanvas.worldCamera = _cam;
        }

        // ── Auto-detect healthBarFill from Slider's fillRect if not set in Inspector ──
        if (healthBarFill == null && healthBarSlider != null && healthBarSlider.fillRect != null)
        {
            healthBarFill = healthBarSlider.fillRect.GetComponent<Image>();
            if (debugLogs) Debug.Log($"[PlayerHeadUI] Start — Auto-detected healthBarFill from Slider.fillRect: {(healthBarFill != null ? "SUCCESS" : "FAILED — Fill image not found on fillRect GameObject")}");
        }

        // Force-initialize fill color so it's never invisible (white/transparent default)
        if (healthBarFill != null)
        {
            healthBarFill.color = highHealthColor;
            if (debugLogs) Debug.Log($"[PlayerHeadUI] Start — Fill color initialized to highHealthColor ({highHealthColor}).");
        }

        // Snap the bar to the current health on spawn (no lerp slide-in)
        if (_playerData != null)
        {
            _targetHealth  = _playerData.Health;
            _displayHealth = _playerData.Health;
            SetHealthBarInstant(_playerData.Health / 100f);
            if (debugLogs) Debug.Log($"[PlayerHeadUI] Start — Found PlayerNetworkData. PlayerName='{_playerData.PlayerName}' Health={_playerData.Health} HasInputAuthority={_playerData.Object?.HasInputAuthority}");
        }

        // Validation errors
        if (_playerData     == null) Debug.LogError("[PlayerHeadUI] Start — PlayerNetworkData NOT FOUND in parent! Head UI will not work.");
        if (uiCanvas        == null) Debug.LogError("[PlayerHeadUI] Start — UICanvas is not assigned in the Inspector!");
        if (healthBarSlider == null) Debug.LogError("[PlayerHeadUI] Start — HealthBarSlider is not assigned in the Inspector!");
        if (playerNameText  == null) Debug.LogError("[PlayerHeadUI] Start — PlayerNameText is not assigned in the Inspector!");
        if (_cam            == null) Debug.LogError("[PlayerHeadUI] Start — Camera.main is NULL! Make sure the camera is tagged 'MainCamera'.");
        if (healthBarFill   == null) Debug.LogError("[PlayerHeadUI] Start — HealthBarFill Image is NULL! Assign the Fill image from inside the Slider hierarchy in the Inspector, OR make sure the Slider has a fillRect set up.");
    }

    // Track frames so we don't spam logs every frame
    private int  _debugFrameCounter;
    private int  _lastLoggedHealth = -999;
    private bool _lastLoggedVisibility;

    private void LateUpdate()
    {
        _debugFrameCounter++;

        // If references aren't ready, keep the canvas hidden and wait
        if (_playerData == null || _cam == null)
        {
            if (uiCanvas != null) uiCanvas.enabled = false;
            // Log once every 120 frames so the console doesn't spam
            if (debugLogs && _debugFrameCounter % 120 == 0)
                Debug.LogWarning("[PlayerHeadUI] LateUpdate — _playerData or _cam is null. Canvas is hidden. Check Start() logs above.");
            return;
        }

        // Read live health every frame — _playerData.Health is the [Networked] value
        // that RPC_UpdateHealth sets on ALL clients, so this updates at the same
        // time as the screen UI health bar.
        _targetHealth = _playerData.Health;

        // Log when health value changes
        if (debugLogs && _lastLoggedHealth != _playerData.Health)
        {
            _lastLoggedHealth = _playerData.Health;
            Debug.Log($"[PlayerHeadUI] Health changed → {_playerData.Health} for player '{_playerData.PlayerName}' (IsLocalPlayer={_playerData.Object?.HasInputAuthority})");
        }

        // Only rebuild the TextMesh when the name actually changes
        if (_cachedName != _playerData.PlayerName)
        {
            _cachedName = _playerData.PlayerName;
            if (playerNameText != null)
                playerNameText.text = _cachedName;
            if (debugLogs) Debug.Log($"[PlayerHeadUI] Name updated → '{_cachedName}'");
        }

        UpdateBillboard();
        UpdateVisibility();
        SmoothHealthBar();
    }

    // ── Private helpers ────────────────────────────────────────────────

    private void UpdateBillboard()
    {
        transform.position = transform.parent.position + headOffset;
        if (faceCamera)
            transform.rotation = _cam.transform.rotation;
    }

    private void UpdateVisibility()
    {
        if (uiCanvas == null) return;

        // Guard: NetworkObject.Object can be null on the very first frame
        if (_playerData.Object == null)
        {
            uiCanvas.enabled = false;
            if (debugLogs && _debugFrameCounter % 120 == 0)
                Debug.LogWarning("[PlayerHeadUI] UpdateVisibility — _playerData.Object is null (Fusion not ready yet). Canvas hidden.");
            return;
        }

        bool isLocalPlayer = _playerData.Object.HasInputAuthority;
        bool shouldShow    = !isLocalPlayer;

        // Only log when visibility actually changes
        if (debugLogs && _lastLoggedVisibility != shouldShow)
        {
            _lastLoggedVisibility = shouldShow;
            Debug.Log($"[PlayerHeadUI] Visibility changed → {(shouldShow ? "SHOWN" : "HIDDEN")} | Player='{_playerData.PlayerName}' IsLocalPlayer={isLocalPlayer}");
        }

        // Local player  → HIDE  (they use the screen HUD)
        // Remote players → SHOW
        uiCanvas.enabled = shouldShow;
    }

    private void SmoothHealthBar()
    {
        if (healthBarSlider == null) return;

        // Smoothly lerp the visual bar toward the real health value
        _displayHealth = Mathf.Lerp(_displayHealth, _targetHealth, Time.deltaTime * healthLerpSpeed);

        float normalized = Mathf.Clamp01(_displayHealth / 100f);
        healthBarSlider.value = normalized;

        // Color: green (100 %) → yellow-orange (50 %) → red (0 %)
        if (healthBarFill != null)
        {
            Color barColor = normalized > 0.5f
                ? Color.Lerp(midHealthColor, highHealthColor, (normalized - 0.5f) * 2f)
                : Color.Lerp(lowHealthColor, midHealthColor,   normalized         * 2f);
            healthBarFill.color = barColor;
        }
    }

    private void SetHealthBarInstant(float normalizedValue)
    {
        if (healthBarSlider != null)
            healthBarSlider.value = normalizedValue;

        if (healthBarFill != null)
            healthBarFill.color = normalizedValue > 0.5f
                ? Color.Lerp(midHealthColor, highHealthColor, (normalizedValue - 0.5f) * 2f)
                : Color.Lerp(lowHealthColor, midHealthColor,   normalizedValue         * 2f);
    }
}
