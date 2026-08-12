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
    
    [Header("Name Colors")]
    public Color defaultNameColor = Color.white;
    public Color enemyNameColor = new Color(0.90f, 0.10f, 0.10f);  // Red for enemies in FFA
    public Color heroTeamColor = new Color(0.18f, 0.47f, 1f);        // Blue for Hero team (Team A)
    public Color alienTeamColor = new Color(1f, 0.22f, 0.22f);       // Red for Alien team (Team B)

    [Header("Smoothing")]
    [Tooltip("How fast the health bar slides to the new value (higher = faster)")]
    [SerializeField] private float healthLerpSpeed = 8f;

    // ── Private state ──────────────────────────────────────────────────
    private PlayerNetworkData _playerData;
    private Camera            _cam;
    private float             _targetHealth  = 100f;
    private float             _displayHealth = 100f;
    private float             _lastWrittenHealth = -999f;
    private Color             _lastWrittenBarColor = new Color(0f, 0f, 0f, 0f);
    private string            _cachedName;
    private Color             _cachedNameColor;
    private int               _cachedLocalTeamId = -1;
    private Vector3           _lastBillboardPos;
    private bool              _hasBillboardPos;

    // ── Debug toggle ───────────────────────────────────────────────────
    [Header("Debug")]
    [Tooltip("Enable to print diagnostic logs to the Console")]
    public bool debugLogs = false;

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
            
            // Initialize name color
            _cachedNameColor = defaultNameColor;
            if (playerNameText != null)
                playerNameText.color = _cachedNameColor;
                
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

        if (_playerData == null || _cam == null)
        {
            if (uiCanvas != null) uiCanvas.enabled = false;
            if (debugLogs && _debugFrameCounter % 120 == 0)
                Debug.LogWarning("[PlayerHeadUI] LateUpdate — _playerData or _cam is null. Canvas is hidden. Check Start() logs above.");
            return;
        }

        // Local player uses screen HUD — skip all head-UI work
        if (_playerData.Object != null && _playerData.Object.HasInputAuthority)
        {
            if (uiCanvas != null && uiCanvas.enabled)
                uiCanvas.enabled = false;
            return;
        }

        if (uiCanvas != null && !uiCanvas.enabled)
            uiCanvas.enabled = true;

        _targetHealth = _playerData.Health;

        if (_cachedName != _playerData.PlayerName)
        {
            _cachedName = _playerData.PlayerName;
            if (playerNameText != null)
                playerNameText.text = _cachedName;
        }

        UpdateNameColor();
        UpdateBillboard();
        SmoothHealthBar();
    }

    // ── Private helpers ────────────────────────────────────────────────

    private void UpdateBillboard()
    {
        Vector3 targetPos = transform.parent != null
            ? transform.parent.position + headOffset
            : transform.position;

        // Avoid rewriting transform when nothing moved
        if (!_hasBillboardPos || (targetPos - _lastBillboardPos).sqrMagnitude > 0.0001f)
        {
            transform.position = targetPos;
            _lastBillboardPos = targetPos;
            _hasBillboardPos = true;
        }

        if (faceCamera && _cam != null)
            transform.rotation = _cam.transform.rotation;
    }

    private void UpdateVisibility()
    {
        if (uiCanvas == null) return;

        if (_playerData.Object == null)
        {
            uiCanvas.enabled = false;
            return;
        }

        bool isLocalPlayer = _playerData.Object.HasInputAuthority;
        uiCanvas.enabled = !isLocalPlayer;
    }

    private void SmoothHealthBar()
    {
        if (healthBarSlider == null) return;

        _displayHealth = Mathf.Lerp(_displayHealth, _targetHealth, Time.deltaTime * healthLerpSpeed);
        if (Mathf.Abs(_displayHealth - _targetHealth) < 0.05f)
            _displayHealth = _targetHealth;

        // Only dirty UI graphics when the visible value actually changes
        if (Mathf.Abs(_displayHealth - _lastWrittenHealth) < 0.2f)
            return;

        _lastWrittenHealth = _displayHealth;
        float normalized = Mathf.Clamp01(_displayHealth / 100f);
        healthBarSlider.value = normalized;

        if (healthBarFill != null)
        {
            Color barColor = normalized > 0.5f
                ? Color.Lerp(midHealthColor, highHealthColor, (normalized - 0.5f) * 2f)
                : Color.Lerp(lowHealthColor, midHealthColor, normalized * 2f);

            if (_lastWrittenBarColor != barColor)
            {
                _lastWrittenBarColor = barColor;
                healthBarFill.color = barColor;
            }
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
    
    private void UpdateNameColor()
    {
        if (playerNameText == null || _playerData == null) return;
        
        bool isLocalPlayer = _playerData.Object?.HasInputAuthority == true;
        Color targetColor = defaultNameColor;
        
        // Don't change color for local player (their own head UI is hidden anyway)
        if (!isLocalPlayer && NetworkGameManager.Instance != null)
        {
            GameMode currentMode = NetworkGameManager.Instance.CurrentGameMode;
            
            if (currentMode == GameMode.FreeForAll)
            {
                // FFA: All other players are enemies (red)
                targetColor = enemyNameColor;
                
                if (debugLogs && _cachedNameColor != targetColor)
                    Debug.Log($"[PlayerHeadUI] FFA Mode: Player '{_playerData.PlayerName}' name set to enemy color (red)");
            }
            else if (currentMode == GameMode.TeamDeathmatch)
            {
                // TDM: Show team colors
                int playerTeamId = _playerData.TeamId;
                int localTeamId = GetLocalPlayerTeamId();
                
                if (playerTeamId == 0) // Hero team (Team A)
                {
                    targetColor = heroTeamColor; // Blue
                }
                else if (playerTeamId == 1) // Alien team (Team B)
                {
                    targetColor = alienTeamColor; // Red
                }
                
                if (debugLogs && _cachedNameColor != targetColor)
                    Debug.Log($"[PlayerHeadUI] TDM Mode: Player '{_playerData.PlayerName}' (Team {playerTeamId}) name set to team color");
            }
        }
        
        // Only update if color actually changed (performance optimization)
        if (_cachedNameColor != targetColor)
        {
            _cachedNameColor = targetColor;
            playerNameText.color = targetColor;
        }
    }
    
    private int GetLocalPlayerTeamId()
    {
        // Return cached value if already found
        if (_cachedLocalTeamId != -1)
            return _cachedLocalTeamId;
            
        // Find local player's team ID and cache it
        var localPlayerData = FindObjectsOfType<PlayerNetworkData>();
        foreach (var playerData in localPlayerData)
        {
            if (playerData.Object != null && playerData.Object.HasInputAuthority)
            {
                _cachedLocalTeamId = playerData.TeamId;
                return _cachedLocalTeamId;
            }
        }
        _cachedLocalTeamId = 0; // Default to Hero team if not found
        return _cachedLocalTeamId;
    }
}
