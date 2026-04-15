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
    [SerializeField] private Button throwButton;                // Throw/Shoot button (right)
    [SerializeField] private Button throwButtonLeft;            // Duplicate Throw/Shoot button (left)
    [SerializeField] private TextMeshProUGUI ammoText;          // Shows current bomb count
    [SerializeField] private GameObject[] bombUIElements;        // Individual bomb icons (like offline mode)
    [SerializeField] private Image throwCooldownOverlay;         // Optional overlay on throw button to show cooldown
    
    [Header("Bullet UI")]
    [SerializeField] private TextMeshProUGUI bulletAmmoText;    // Shows current bullets in 00:00 format or RELOADING text
    [SerializeField] private Button reloadButton;               // Manual reload button
    
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
    [SerializeField] private GameObject damageIndicatorImage;    // Flashes on screen when taking damage

    [Header("Game Info UI")]
    [SerializeField] private TextMeshProUGUI gameStateText;      // Shows current game state
    [SerializeField] private TextMeshProUGUI timerText;          // Round timer
    [SerializeField] private TextMeshProUGUI pingText;           // Shows current ping in ms

    [Header("Team Score UI")]
    [SerializeField] private TextMeshProUGUI blueTeamScoreText;  // Blue team score display
    [SerializeField] private TextMeshProUGUI redTeamScoreText;   // Red team score display

    [Header("State Panels")]
    [SerializeField] private GameObject waitingForPlayersPanel; // Panel shown while waiting for others
    [SerializeField] private TextMeshProUGUI waitingStatusText;  // Text inside the waiting panel

    [Header("Respawn UI")]
    [SerializeField] private GameObject respawnPanel;             // Panel shown when player dies
    [SerializeField] private TextMeshProUGUI respawnTimerText;    // Timer text inside the respawn panel

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;            // Panel shown when the match ends
    [SerializeField] private TextMeshProUGUI gameOverText;        // "Blue Team Wins!" / "Red Team Wins!" / "It's a Draw!"
    [SerializeField] private UnityEngine.UI.Image gameOverImage;  // Tinted Blue / Red / Grey based on winner

    [Header("Ability UI")]
    [SerializeField] private UnityEngine.UI.Button abilityButton;          // On-screen ability button
    [SerializeField] private UnityEngine.UI.Image  abilityCooldownOverlay; // Radial/fill overlay (fill amount = cooldown %)
    [SerializeField] private TextMeshProUGUI        abilityCooldownText;    // Optional: "Q  5s" countdown
    [SerializeField] private Slider                abilityDurationSlider;  // Shows active ability remaining duration

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
    private HoldableButton throwHoldableLeft; // Tracks left throw button hold state
    private bool wasThrowHeld = false; // Previous frame's held state for detecting release
    private PlayerAbilityController localAbilityController;
    private bool wasAbilityActive = false;
    private float abilityActiveEndTime = 0f;
    private float pingUpdateTimer = 0f;
    private const float PING_UPDATE_INTERVAL = 0.5f;
    private int _lastKnownHealth = -1;
    private float _damageIndicatorTimer = 0f;
    private const float DAMAGE_INDICATOR_DURATION = 0.3f;

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

        // Wire up left throw button identically
        if (throwButtonLeft != null)
        {
            throwButtonLeft.onClick.AddListener(OnThrowButtonPressed);
            
            throwHoldableLeft = throwButtonLeft.gameObject.GetComponent<HoldableButton>();
            if (throwHoldableLeft == null)
                throwHoldableLeft = throwButtonLeft.gameObject.AddComponent<HoldableButton>();
        }

        if (abilityButton != null)
            abilityButton.onClick.AddListener(OnAbilityButtonPressed);

        if (reloadButton != null)
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
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
            UpdateBulletAmmoUI();
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
            
            // Continuous laser fire while EITHER throw button is held (via HoldableButton)
            bool isThrowHeld = (throwHoldable != null && throwHoldable.IsHeld)
                            || (throwHoldableLeft != null && throwHoldableLeft.IsHeld);
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

            // --- Reload (R key) ---
            if (Input.GetKeyDown(KeyCode.R))
                OnReloadButtonPressed();

            // --- Ability (Q key) ---
            if (Input.GetKeyDown(KeyCode.Q))
                OnAbilityButtonPressed();

            // --- Ability cooldown UI ---
            UpdateAbilityCooldownUI();
            UpdateAbilityDurationUI();
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
            localAbilityController = localPlayerObject.GetComponent<PlayerAbilityController>();

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

    public void OnThrowButtonPressed()
    {
        // Now acts exclusively as the main Attack/Shoot button (since Bomb is thrown directly by its own button)
        if (localEquipSystem != null)
        {
            if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null)
            {
                localPistolBehaviour.RequestShoot();
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null)
            {
                // We deliberately ignore single-taps for Laser because it's a Hold-weapon.
                // The HoldableButton logic in Update() natively handles the Laser instead!
            }
        }
        else
        {
            // Fallback if equip system is removed completely: default to standard pistol
            if (localPistolBehaviour != null)
            {
                localPistolBehaviour.RequestShoot();
            }
        }
    }

    // ---------------------------------------------------------------
    // Reload Button
    // ---------------------------------------------------------------

    /// <summary>Called when the reload button is pressed (UI button or R key).</summary>
    public void OnReloadButtonPressed()
    {
        if (localEquipSystem != null)
        {
            if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null && !localPistolBehaviour.IsReloading)
            {
                localPistolBehaviour.RequestReload();
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null && !localLaserBehaviour.IsReloading)
            {
                localLaserBehaviour.RequestReload();
            }
        }
        else if (localPistolBehaviour != null && !localPistolBehaviour.IsReloading)
        {
            localPistolBehaviour.RequestReload();
        }
    }

    // ---------------------------------------------------------------
    // Ability Button
    // ---------------------------------------------------------------

    /// <summary>Called when the ability button is pressed (UI button or Q key).</summary>
    public void OnAbilityButtonPressed()
    {
        if (localAbilityController == null) return;
        if (localAbilityController.IsOnCooldown()) return;
        localAbilityController.RPC_UseAbility();
    }

    private void UpdateAbilityCooldownUI()
    {
        if (localAbilityController == null) return;

        bool ready = !localAbilityController.IsOnCooldown(); // true = has charge

        // Radial fill overlay: fully filled (blocked) when no charge, hidden when ready
        if (abilityCooldownOverlay != null)
        {
            abilityCooldownOverlay.fillAmount = ready ? 0f : 1f;
            abilityCooldownOverlay.gameObject.SetActive(!ready);
        }

        // Text: "Q" when ready, "🔒" (or "—") when waiting for a kill
        if (abilityCooldownText != null)
        {
            abilityCooldownText.text = ready ? "Q" : "KILL";
            abilityCooldownText.gameObject.SetActive(true);
        }

        // Grey out button when no charge
        if (abilityButton != null)
            abilityButton.interactable = ready;
    }

    private void UpdateAbilityDurationUI()
    {
        if (localAbilityController == null) return;

        bool isAbilityActive = localAbilityController.IsAbilityActive;
        
        if (isAbilityActive && !wasAbilityActive)
        {
            // Ability just activated locally (or via network sync)
            abilityActiveEndTime = Time.time + localAbilityController.GetAbilityDuration();
        }
        
        wasAbilityActive = isAbilityActive;

        if (abilityDurationSlider != null)
        {
            if (isAbilityActive)
            {
                if (!abilityDurationSlider.gameObject.activeSelf)
                    abilityDurationSlider.gameObject.SetActive(true);

                float remaining = abilityActiveEndTime - Time.time;
                float duration = localAbilityController.GetAbilityDuration();
                
                // Slider value goes from 1 to 0 mapping the duration left
                abilityDurationSlider.value = Mathf.Clamp01(remaining / duration);
            }
            else
            {
                if (abilityDurationSlider.gameObject.activeSelf)
                    abilityDurationSlider.gameObject.SetActive(false);
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
            ammoText.text = $"{currentBombs}";
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

        // NOTE: throwButton and throwButtonLeft are the primary attack/shoot buttons.
        // They must NEVER be disabled — they are used for pistol/laser fire, not just bombs.
    }

    // ---------------------------------------------------------------
    // Bullet Ammo UI
    // ---------------------------------------------------------------

    private void UpdateBulletAmmoUI()
    {
        // Check which weapon is equipped
        if (localEquipSystem != null)
        {
            if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null)
            {
                UpdatePistolAmmoUI();
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null)
            {
                UpdateLaserAmmoUI();
            }
        }
        // Fallback to pistol if equip system not available
        else if (localPistolBehaviour != null)
        {
            UpdatePistolAmmoUI();
        }
    }

    private void UpdatePistolAmmoUI()
    {
        // Auto-reload when current ammo is zero
        if (localPistolBehaviour.CurrentAmmo == 0 && !localPistolBehaviour.IsReloading && localPistolBehaviour.ReserveAmmo > 0)
        {
            localPistolBehaviour.RequestReload();
        }

        // Update bullet ammo text in 00/00 format or show reload dots
        if (bulletAmmoText != null)
        {
            if (localPistolBehaviour.IsReloading)
            {
                bulletAmmoText.text = "...";
            }
            else
            {
                int currentBullets = localPistolBehaviour.CurrentAmmo;
                int reserveBullets = localPistolBehaviour.ReserveAmmo;
                bulletAmmoText.text = $"{currentBullets:D2}/{reserveBullets:D2}";
            }
        }
    }

    private void UpdateLaserAmmoUI()
    {
        // Auto-reload when energy reaches zero
        if (localLaserBehaviour.CurrentEnergy == 0 && !localLaserBehaviour.IsReloading)
        {
            // Laser auto-reloads when energy reaches zero (handled in NetworkLaserBehaviour)
        }

        // Update laser energy text in 00/00 format or show reload dots
        if (bulletAmmoText != null)
        {
            if (localLaserBehaviour.IsReloading)
            {
                bulletAmmoText.text = "...";
            }
            else
            {
                int currentEnergy = localLaserBehaviour.CurrentEnergy;
                int reserveEnergy = localLaserBehaviour.ReserveEnergy;
                
                // Show 0 if reserve drops to 0, ensuring format is maintained
                bulletAmmoText.text = $"{currentEnergy:D2}/{reserveEnergy:D2}";
            }
        }
    }

    // ---------------------------------------------------------------
    // Player Stats UI
    // ---------------------------------------------------------------

    private void UpdatePlayerStatsUI()
    {
        if (localPlayerData == null) return;

        int currentHealth = localPlayerData.Health;

        // Damage indicator — show when health drops
        if (_lastKnownHealth >= 0 && currentHealth < _lastKnownHealth)
        {
            _damageIndicatorTimer = DAMAGE_INDICATOR_DURATION;
            if (damageIndicatorImage != null && !damageIndicatorImage.activeSelf)
                damageIndicatorImage.SetActive(true);
        }
        _lastKnownHealth = currentHealth;

        // Count down and hide the indicator when no new damage arrives
        if (_damageIndicatorTimer > 0f)
        {
            _damageIndicatorTimer -= Time.deltaTime;
            if (_damageIndicatorTimer <= 0f && damageIndicatorImage != null)
                damageIndicatorImage.SetActive(false);
        }

        // Health
        if (healthBar != null)
        {
            healthBar.value = currentHealth / 100f;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}";
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

        // Ping — update every 0.5s to avoid per-frame overhead
        if (pingText != null && runner != null && runner.IsRunning)
        {
            pingUpdateTimer -= Time.deltaTime;
            if (pingUpdateTimer <= 0f)
            {
                pingUpdateTimer = PING_UPDATE_INTERVAL;
                int pingMs = Mathf.RoundToInt((float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000));
                pingText.text = $"Ping: {pingMs}ms";
            }
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

    // ---------------------------------------------------------------
    // Game Over UI
    // ---------------------------------------------------------------

    /// <summary>
    /// Called on all clients when the match ends. Shows the game-over panel,
    /// sets the winner text, and tints the panel image:
    ///   winningTeam 0 = Blue (Team A), 1 = Red (Team B), -1 = Grey (Draw).
    /// </summary>
    public void ShowGameOverScreen(string winnerText, int winningTeam)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (gameOverText != null)
        {
            gameOverText.text = winnerText;
        }

        if (gameOverImage != null)
        {
            if (winningTeam == 0)       gameOverImage.color = new Color(0.18f, 0.47f, 1f);   // Blue
            else if (winningTeam == 1)  gameOverImage.color = new Color(1f, 0.22f, 0.22f);   // Red
            else                        gameOverImage.color = new Color(0.55f, 0.55f, 0.55f); // Grey
        }
    }

    private void OnDestroy()
    {
        if (throwButton != null)
            throwButton.onClick.RemoveListener(OnThrowButtonPressed);

        if (throwButtonLeft != null)
            throwButtonLeft.onClick.RemoveListener(OnThrowButtonPressed);

        if (abilityButton != null)
            abilityButton.onClick.RemoveListener(OnAbilityButtonPressed);

        if (reloadButton != null)
            reloadButton.onClick.RemoveListener(OnReloadButtonPressed);

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

    public async void ExitToLobbyBtn()
    {
        // Close the settings panel
        settingsPanel.SetActive(false);
        
        // Unlock cursor for the lobby scene
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (NetworkStarter.Instance != null)
        {
            await NetworkStarter.Instance.ShutdownRunner();
        }
        else if (runner != null)
        {
            await runner.Shutdown();
        }
        else
        {
            // Fallback
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }
}
