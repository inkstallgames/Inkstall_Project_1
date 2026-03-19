using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles all network game UI interactions — throw button, ammo display,
/// health bar, kill feed, etc. Attach to a Canvas in the multiplayer game scene.
/// </summary>
public class NetworkUIManager : MonoBehaviour
{
    public static NetworkUIManager Instance { get; private set; }

    [Header("Bomb UI")]
    [SerializeField] private Button throwButton;                // Throw bomb button
    [SerializeField] private TextMeshProUGUI ammoText;          // Shows current bomb count
    [SerializeField] private GameObject[] bombUIElements;        // Individual bomb icons (like offline mode)
    [SerializeField] private Image throwCooldownOverlay;         // Optional overlay on throw button to show cooldown
    
    [Header("Movement UI")]
    [Tooltip("UI Button that players hold down to jump.")]
    [SerializeField] private HoldableButton jumpButton;
    
    public bool IsJumpHeld => jumpButton != null && jumpButton.IsHeld;

    [Header("Player Stats UI")]
    [SerializeField] private Slider healthBar;                   // Local player health bar
    [SerializeField] private TextMeshProUGUI healthText;         // Health number text
    [SerializeField] private TextMeshProUGUI killsText;          // Kill count
    [SerializeField] private TextMeshProUGUI deathsText;         // Death count
    [SerializeField] private TextMeshProUGUI playerNameText;     // Local player name

    [Header("Game Info UI")]
    [SerializeField] private TextMeshProUGUI gameStateText;      // Shows current game state
    [SerializeField] private TextMeshProUGUI timerText;          // Round timer

    [Header("Team Score UI")]
    [SerializeField] private TextMeshProUGUI blueTeamScoreText;  // Blue team score display
    [SerializeField] private TextMeshProUGUI redTeamScoreText;   // Red team score display

    [Header("State Panels")]
    [SerializeField] private GameObject waitingForPlayersPanel; // Panel shown while waiting for others
    [SerializeField] private TextMeshProUGUI waitingStatusText;  // Text inside the waiting panel

    [Header("Respawn UI")]
    [SerializeField] private GameObject respawnPanel;             // Panel shown when player dies
    [SerializeField] private TextMeshProUGUI respawnTimerText;    // Timer text inside the respawn panel

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;            // Panel shown when settings button is clicked
    
    public bool IsSettingsPanelActive => settingsPanel != null && settingsPanel.activeSelf;

    // Cached references
    private NetworkRunner runner;
    private NetworkObject localPlayerObject;
    private NetworkBombBehaviour localBombBehaviour;
    private NetworkPistolBehaviour localPistolBehaviour;
    private bool isWaitingScreenActive = false;
    private NetworkLaserBehaviour localLaserBehaviour;
    private NetworkWeaponEquipSystem localEquipSystem;
    private PlayerNetworkData localPlayerData;
    private HoldableButton throwHoldable; // Tracks throw button hold state for continuous laser fire
    private bool wasThrowHeld = false; // Previous frame's held state for detecting release

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Wire up button listeners
        if (throwButton != null)
        {
            throwButton.onClick.AddListener(OnThrowButtonPressed);
            
            // Add HoldableButton for hold-to-fire support (laser gun)
            throwHoldable = throwButton.gameObject.GetComponent<HoldableButton>();
            if (throwHoldable == null)
                throwHoldable = throwButton.gameObject.AddComponent<HoldableButton>();
        }
    }

    private void Update()
    {
        // Try to find runner and local player if not cached yet
        if (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
            if (runner != null)
            {
                // Debug.Log("[NetworkUIManager] NetworkRunner found.");
            }
        }

        if (runner != null && localPlayerObject == null)
        {
            TryFindLocalPlayer();
        }

        // Update all UI elements
        if (localPlayerObject != null)
        {
            UpdateBombUI();
            UpdatePlayerStatsUI();

            // Keyboard shortcut: press T to throw/shoot
            // For laser: use GetKey (held) for continuous fire, GetKeyDown for others
            bool isLaserEquipped = localEquipSystem != null && localEquipSystem.IsLaserEquipped();
            
            if (isLaserEquipped)
            {
                if (Input.GetKey(KeyCode.T))
                {
                    if (localLaserBehaviour != null)
                        localLaserBehaviour.RequestShoot();
                }
                else if (Input.GetKeyUp(KeyCode.T))
                {
                    if (localLaserBehaviour != null)
                        localLaserBehaviour.StopShooting();
                }
            }
            else if (Input.GetKeyDown(KeyCode.T))
            {
                OnThrowButtonPressed();
            }
            
            // Continuous laser fire while throw button is held (via HoldableButton)
            bool isThrowHeld = throwHoldable != null && throwHoldable.IsHeld;
            if (isThrowHeld && isLaserEquipped && localLaserBehaviour != null)
            {
                localLaserBehaviour.RequestShoot();
            }
            // Detect release: was held last frame, not held this frame -> stop laser
            else if (wasThrowHeld && !isThrowHeld && isLaserEquipped && localLaserBehaviour != null)
            {
                localLaserBehaviour.StopShooting();
            }
            wasThrowHeld = isThrowHeld;
        }
        else
        {
            // Log periodically to show we're still looking (every 2 seconds)
            if (Time.frameCount % 120 == 0)
            {
                // Debug.LogWarning($"[NetworkUIManager] Local player NOT found yet. Runner: {(runner != null ? "exists" : "null")}, Runner.IsRunning: {(runner != null ? runner.IsRunning.ToString() : "N/A")}");
            }
        }

        UpdateGameInfoUI();

        // Handle the waiting screen and countdown
        if (isWaitingScreenActive)
        {
            var lobbyManager = NetworkLobbyManager.Instance;
            if (lobbyManager != null && waitingStatusText != null)
            {
                if (lobbyManager.IsGameReady && lobbyManager.GameStartTimer.IsRunning)
                {
                    // Countdown is active
                    float remainingTime = lobbyManager.GameStartTimer.RemainingTime(runner) ?? 0;
                    waitingStatusText.text = $"Game starting in {Mathf.CeilToInt(remainingTime)}";

                    if (lobbyManager.GameStartTimer.Expired(runner))
                    {
                        // Countdown finished — transition to InProgress and start the game timer
                        if (NetworkGameManager.Instance != null && runner.IsServer)
                        {
                            NetworkGameManager.Instance.StartRoundAfterCountdown();
                        }

                        // Enable the game timer text (deactivated by default in the scene)
                        if (timerText != null)
                        {
                            timerText.gameObject.SetActive(true);
                        }

                        ShowWaitingForPlayersScreen(false);
                    }
                }
                else
                {
                    // Waiting for players, before countdown starts
                    waitingStatusText.text = "Waiting for other players...";
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // Find Local Player
    // ---------------------------------------------------------------

    private void TryFindLocalPlayer()
    {
        if (runner == null || !runner.IsRunning) return;

        localPlayerObject = runner.GetPlayerObject(runner.LocalPlayer);

        // Fallback: if SetPlayerObject was never called, find the local player manually
        if (localPlayerObject == null)
        {
            var allBombs = FindObjectsOfType<NetworkBombBehaviour>();
            foreach (var bomb in allBombs)
            {
                if (bomb.Object != null && bomb.Object.HasInputAuthority)
                {
                    localPlayerObject = bomb.Object;
                    // Debug.Log($"[NetworkUIManager] Found local player via fallback scan: {localPlayerObject.name}");
                    break;
                }
            }
        }

        if (localPlayerObject != null)
        {
            localBombBehaviour = localPlayerObject.GetComponent<NetworkBombBehaviour>();
            localPistolBehaviour = localPlayerObject.GetComponent<NetworkPistolBehaviour>();
            localLaserBehaviour = localPlayerObject.GetComponent<NetworkLaserBehaviour>();
            localEquipSystem = localPlayerObject.GetComponent<NetworkWeaponEquipSystem>();
            localPlayerData = localPlayerObject.GetComponent<PlayerNetworkData>();

            // Debug.Log($"[NetworkUIManager] Local player found! Object: {localPlayerObject.name}");
            // Debug.Log($"[NetworkUIManager]   - NetworkBombBehaviour: {(localBombBehaviour != null ? "FOUND" : "MISSING")}");
            // Debug.Log($"[NetworkUIManager]   - NetworkPistolBehaviour: {(localPistolBehaviour != null ? "FOUND" : "MISSING")}");
            // Debug.Log($"[NetworkUIManager]   - NetworkLaserBehaviour: {(localLaserBehaviour != null ? "FOUND" : "MISSING")}");
            // Debug.Log($"[NetworkUIManager]   - NetworkWeaponEquipSystem: {(localEquipSystem != null ? "FOUND" : "MISSING")}");
            // Debug.Log($"[NetworkUIManager]   - PlayerNetworkData: {(localPlayerData != null ? "FOUND" : "MISSING")}");
        }
    }

    // ---------------------------------------------------------------
    // Throw Button
    // ---------------------------------------------------------------

    /// <summary>
    /// Called when the throw button is pressed.
    /// Fires the currently equipped weapon (bomb, pistol, or laser).
    /// </summary>
    public void OnThrowButtonPressed()
    {
        if (localEquipSystem != null)
        {
            if (localEquipSystem.IsBombEquipped() && localBombBehaviour != null)
            {
                // Debug.Log("[NetworkUIManager] Throw button pressed - Bomb is equipped, throwing bomb");
                localBombBehaviour.RequestThrow();
            }
            else if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null)
            {
                // Debug.Log("[NetworkUIManager] Throw button pressed - Pistol is equipped, shooting pistol");
                localPistolBehaviour.RequestShoot();
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null)
            {
                // We deliberately ignore single-taps for Laser because it's a Hold-weapon.
                // The HoldableButton logic in Update() natively handles the Laser instead!
            }
            else
            {
                // Debug.LogWarning("[NetworkUIManager] Throw button pressed but no weapon equipped or behaviour missing");
            }
        }
        else
        {
            // Fallback to old behavior if equip system not found
            if (localBombBehaviour != null)
            {
                localBombBehaviour.RequestThrow();
            }
            else
            {
                // Debug.LogWarning("[NetworkUIManager] Cannot throw — local player components not found.");
                TryFindLocalPlayer(); // Try to re-find
            }
        }
    }

    // ---------------------------------------------------------------
    // Bomb / Ammo UI
    // ---------------------------------------------------------------

    private void UpdateBombUI()
    {
        if (localBombBehaviour == null) return;

        int currentBombs = localBombBehaviour.CurrentBombs;
        int maxBombs = localBombBehaviour.MaxBombs;

        // Update ammo text
        if (ammoText != null)
        {
            ammoText.text = $"{currentBombs} / {maxBombs}";
        }

        // Update individual bomb icons (like the offline ChemicalBombManager)
        if (bombUIElements != null)
        {
            for (int i = 0; i < bombUIElements.Length; i++)
            {
                if (bombUIElements[i] != null)
                {
                    bombUIElements[i].SetActive(i < currentBombs);
                }
            }
        }

        // Disable throw button when out of ammo
        if (throwButton != null)
        {
            throwButton.interactable = currentBombs > 0;
        }
    }

    // ---------------------------------------------------------------
    // Player Stats UI
    // ---------------------------------------------------------------

    private void UpdatePlayerStatsUI()
    {
        if (localPlayerData == null) return;

        // Health
        if (healthBar != null)
        {
            healthBar.value = localPlayerData.Health / 100f;
        }

        if (healthText != null)
        {
            healthText.text = $"{localPlayerData.Health}";
        }

        // Kills & Deaths
        if (killsText != null)
        {
            killsText.text = $"Kills: {localPlayerData.Kills}";
        }

        if (deathsText != null)
        {
            deathsText.text = $"Deaths: {localPlayerData.Deaths}";
        }

        // Player name
        if (playerNameText != null)
        {
            playerNameText.text = localPlayerData.PlayerName;
        }
    }

    // ---------------------------------------------------------------
    // Game Info UI
    // ---------------------------------------------------------------

    private void UpdateGameInfoUI()
    {
        if (NetworkGameManager.Instance == null) return;

        // Game state
        if (gameStateText != null)
        {
            gameStateText.text = NetworkGameManager.Instance.CurrentGameState.ToString();
        }

        // Timer
        if (timerText != null && runner != null && runner.IsRunning && NetworkGameManager.Instance.CurrentGameState == GameState.InProgress)
        {
            float elapsed = runner.SimulationTime - NetworkGameManager.Instance.RoundStartTime;
            float remaining = NetworkGameManager.Instance.RoundTime - elapsed;
            remaining = Mathf.Max(0, remaining);

            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        // Team scores
        if (blueTeamScoreText != null)
        {
            blueTeamScoreText.text = $"{NetworkGameManager.Instance.BlueTeamScore}";
        }

        if (redTeamScoreText != null)
        {
            redTeamScoreText.text = $"{NetworkGameManager.Instance.RedTeamScore}";
        }
    }

    // ---------------------------------------------------------------
    // Cleanup
    // ---------------------------------------------------------------

    public void ShowWaitingForPlayersScreen(bool show)
    {
        if (waitingForPlayersPanel != null)
        {
            waitingForPlayersPanel.SetActive(show);
            isWaitingScreenActive = show;
        }
    }

    // ---------------------------------------------------------------
    // Respawn UI
    // ---------------------------------------------------------------

    /// <summary>
    /// Called when the local player dies. Shows the respawn panel with a 7-second countdown.
    /// </summary>
    public void ShowRespawnScreen(float duration = 7f)
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(true);
            StartCoroutine(RespawnCountdown(duration));
        }

        // Force health bar to zero on death
        if (healthBar != null)
        {
            healthBar.value = 0f;
        }
        if (healthText != null)
        {
            healthText.text = "0";
        }
    }

    private System.Collections.IEnumerator RespawnCountdown(float duration)
    {
        float timer = duration;
        while (timer > 0f)
        {
            if (respawnTimerText != null)
            {
                respawnTimerText.text = $"Respawning in {Mathf.CeilToInt(timer)}";
            }
            yield return null;
            timer -= Time.deltaTime;
        }

        // Hide the respawn panel when countdown finishes
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (throwButton != null)
        {
            throwButton.onClick.RemoveListener(OnThrowButtonPressed);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SettingBTn()
    {
        settingsPanel.SetActive(true);
        
        // Unlock and show cursor so the player can interact with UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettingBTn()
    {
        settingsPanel.SetActive(false);
        
        // Re-lock the cursor when closing settings (assuming gameplay requires it)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
