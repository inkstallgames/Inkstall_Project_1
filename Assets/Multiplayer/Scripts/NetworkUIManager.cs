using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private GameObject healIndicatorImage;      // Flashes on screen when healing/regenerating
    
    [Header("Kill Notification UI")]
    [SerializeField] private TextMeshProUGUI killNotificationText; // Shows "You killed [PlayerName]" message
    [SerializeField] private float killNotificationDuration = 3f;     // How long to show kill message

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

    [Header("Leaderboard UI")]
    [SerializeField] private GameObject leaderboardPanel;          // Separate panel for the leaderboard (shown after game over panel)
    [SerializeField] private float gameOverDisplayTime = 5f;      // How long to show the game over panel before switching
    [SerializeField] private float leaderboardDisplayTime = 10f;  // How long to show the leaderboard before exiting

    [Tooltip("Row GameObjects — disabled for unused slots. Create 10 in the editor.")]
    [SerializeField] private GameObject[] leaderboardRows = new GameObject[10];
    [Tooltip("Player name texts — one per row.")]
    [SerializeField] private TextMeshProUGUI[] leaderboardNameTexts = new TextMeshProUGUI[10];
    [Tooltip("Kill count texts — one per row.")]
    [SerializeField] private TextMeshProUGUI[] leaderboardKillTexts = new TextMeshProUGUI[10];
    [Tooltip("Death count texts — one per row.")]
    [SerializeField] private TextMeshProUGUI[] leaderboardDeathTexts = new TextMeshProUGUI[10];
    [Tooltip("Countdown text showing seconds until exit.")]
    [SerializeField] private TextMeshProUGUI leaderboardCountdownText;

    [Header("Ability UI")]
    [SerializeField] private UnityEngine.UI.Button abilityButton;          // On-screen ability button
    [SerializeField] private UnityEngine.UI.Image  abilityCooldownOverlay; // Radial/fill overlay (fill amount = cooldown %)
    [SerializeField] private TextMeshProUGUI        abilityCooldownText;    // Optional: "Q  5s" countdown
    [SerializeField] private Slider                abilityDurationSlider;  // Shows active ability remaining duration

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;            // Panel shown when settings button is clicked
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip killSoundEffect;

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
    private int _lastKnownKills = -1;
    private float _damageIndicatorTimer = 0f;
    private const float DAMAGE_INDICATOR_DURATION = 0.3f;
    private float _healIndicatorTimer = 0f;
    private const float HEAL_INDICATOR_DURATION = 0.3f;

    // Kill notification system
    private float killNotificationTimer = 0f;
    private Coroutine killNotificationCoroutine;

    // Cached leaderboard data (snapshotted at game-over so it survives despawns)
    private struct LeaderboardEntry
    {
        public string PlayerName;
        public int Kills;
        public int Deaths;
        public int TeamId;
        public bool IsLocalPlayer;
    }
    private List<LeaderboardEntry> cachedLeaderboardData = new List<LeaderboardEntry>();
    private Dictionary<PlayerRef, LeaderboardEntry> allTimeLeaderboard = new Dictionary<PlayerRef, LeaderboardEntry>();

    //awake
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
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
        
        // Debug check for kill notification text
        if (killNotificationText == null)
        {
            // Kill notification text not assigned - will be handled in editor
        }
    }

    private void Update()
    {
        // Try to find runner and local player if not cached yet
        if (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
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
            UpdateLeaderboardCache();

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
            // Local player not found yet
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
                // Check if we have reserve ammo before allowing reload
                if (localPistolBehaviour.ReserveAmmo > 0)
                {
                    localPistolBehaviour.RequestReload();
                }
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null && !localLaserBehaviour.IsReloading)
            {
                // Check if we have reserve energy before allowing reload
                if (localLaserBehaviour.ReserveEnergy > 0)
                {
                    localLaserBehaviour.RequestReload();
                }
            }
        }
        else if (localPistolBehaviour != null && !localPistolBehaviour.IsReloading)
        {
            // Check if we have reserve ammo before allowing reload
            if (localPistolBehaviour.ReserveAmmo > 0)
            {
                localPistolBehaviour.RequestReload();
            }
        }
    }

    // ---------------------------------------------------------------
    // Ability Button
    // ---------------------------------------------------------------

    /// <summary>Called when the ability button is pressed (UI button or Q key).</summary>
    private void OnAbilityButtonPressed()
    {
        if (localAbilityController == null) return;
        if (localAbilityController.IsOnCooldown()) return;
        localAbilityController.RequestAbility();
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

        // Update bullet ammo text - keep showing current ammo during reload
        if (bulletAmmoText != null)
        {
            int currentBullets = localPistolBehaviour.CurrentAmmo;
            int reserveBullets = localPistolBehaviour.ReserveAmmo;
            
            // Always show ammo count, even during reload
            // The ammo count will automatically update when reload completes
            bulletAmmoText.text = $"{currentBullets:D2}/{reserveBullets:D2}";
        }
        
        // Update reload button visual feedback
        if (reloadButton != null)
        {
            bool canReload = !localPistolBehaviour.IsReloading && 
                            localPistolBehaviour.ReserveAmmo > 0 && 
                            localPistolBehaviour.CurrentAmmo < localPistolBehaviour.MaxAmmo;
            reloadButton.interactable = canReload;
        }
    }

    private void UpdateLaserAmmoUI()
    {
        // Auto-reload when energy reaches zero
        if (localLaserBehaviour.CurrentEnergy == 0 && !localLaserBehaviour.IsReloading)
        {
            // Laser auto-reloads when energy reaches zero (handled in NetworkLaserBehaviour)
        }

        // Update laser energy text - keep showing current energy during reload
        if (bulletAmmoText != null)
        {
            int currentEnergy = localLaserBehaviour.CurrentEnergy;
            int reserveEnergy = localLaserBehaviour.ReserveEnergy;
            
            // Always show energy count, even during reload
            // The energy count will automatically update when reload completes
            bulletAmmoText.text = $"{currentEnergy:D2}/{reserveEnergy:D2}";
        }
        
        // Update reload button visual feedback
        if (reloadButton != null)
        {
            bool canReload = !localLaserBehaviour.IsReloading && 
                            localLaserBehaviour.ReserveEnergy > 0 && 
                            localLaserBehaviour.CurrentEnergy < localLaserBehaviour.MaxEnergy;
            reloadButton.interactable = canReload;
        }
    }

    // ---------------------------------------------------------------
    // Player Stats UI
    // ---------------------------------------------------------------

    private void UpdatePlayerStatsUI()
    {
        if (localPlayerData == null) return;

        int currentHealth = localPlayerData.Health;
        int currentKills = localPlayerData.Kills;

        // Damage indicator — show when health drops
        if (_lastKnownHealth >= 0 && currentHealth < _lastKnownHealth)
        {
            _damageIndicatorTimer = DAMAGE_INDICATOR_DURATION;
            if (damageIndicatorImage != null && !damageIndicatorImage.activeSelf)
                damageIndicatorImage.SetActive(true);
        }

        // Heal indicator — show when health increases (regenerating)
        if (_lastKnownHealth > 0 && currentHealth > _lastKnownHealth)
        {
            _healIndicatorTimer = HEAL_INDICATOR_DURATION;
            if (healIndicatorImage != null && !healIndicatorImage.activeSelf)
                healIndicatorImage.SetActive(true);
        }

        _lastKnownHealth = currentHealth;

        // Count down and hide the damage indicator
        if (_damageIndicatorTimer > 0f)
        {
            _damageIndicatorTimer -= Time.deltaTime;
            if (_damageIndicatorTimer <= 0f && damageIndicatorImage != null)
                damageIndicatorImage.SetActive(false);
        }

        // Count down and hide the heal indicator
        if (_healIndicatorTimer > 0f)
        {
            _healIndicatorTimer -= Time.deltaTime;
            if (_healIndicatorTimer <= 0f && healIndicatorImage != null)
                healIndicatorImage.SetActive(false);
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
            killsText.text = $"Kills: {currentKills}";
        }

        if (_lastKnownKills >= 0 && currentKills > _lastKnownKills)
        {
            if (audioSource != null && killSoundEffect != null)
            {
                audioSource.PlayOneShot(killSoundEffect);
            }
        }
        
        _lastKnownKills = currentKills;

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
                
                // Update ping text with color coding
                string quality = GetPingQuality(pingMs);
                Color pingColor = GetPingColor(pingMs);
                pingText.text = $"Ping: {pingMs}ms ({quality})";
                pingText.color = pingColor;
                
                // Show warnings for high ping
                if (pingMs > 200)
                {
                    // High ping detected - gameplay may be affected
                }
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

        // Hide leaderboard initially — it will show after the game over panel
        if (leaderboardPanel != null)
        {
            leaderboardPanel.SetActive(false);
        }

        // Snapshot player data NOW while objects still exist
        CacheLeaderboardData();

        // Start the transition: Game Over → Leaderboard → Exit
        StartCoroutine(GameOverToLeaderboardSequence());
    }

    /// <summary>
    /// Handles the timed transition:
    ///   1. Show Game Over panel for gameOverDisplayTime seconds
    ///   2. Hide Game Over panel, populate & show Leaderboard panel
    ///   3. After leaderboardDisplayTime seconds, exit to lobby
    /// </summary>
    private System.Collections.IEnumerator GameOverToLeaderboardSequence()
    {
        // Phase 1: Game Over panel is already visible — wait
        yield return new WaitForSeconds(gameOverDisplayTime);

        // Phase 2: Hide game over, show leaderboard
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        PopulateLeaderboard();

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);

        // Unlock cursor so players can see the leaderboard comfortably
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Phase 3: Countdown, then exit
        float countdown = leaderboardDisplayTime;
        while (countdown > 0f)
        {
            if (leaderboardCountdownText != null)
                leaderboardCountdownText.text = Mathf.CeilToInt(countdown).ToString();

            yield return null;
            countdown -= Time.deltaTime;
        }

        if (leaderboardCountdownText != null)
            leaderboardCountdownText.text = "0";

        // Shut down and return to lobby
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);

        // If we are the server (host), wait a few extra seconds to ensure all clients have 
        // finished their sequence and successfully disconnected themselves, to avoid 
        // interrupting their leaderboard display with an abrupt server shutdown.
        if (runner != null && runner.IsServer)
        {
            yield return new WaitForSeconds(3.0f);
        }

        ShutdownAndReturnToLobby();
    }

    /// <summary>
    /// Async helper — called at the end of the leaderboard sequence to cleanly
    /// shut down the NetworkRunner and return to the lobby scene.
    /// </summary>
    private async void ShutdownAndReturnToLobby()
    {
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
            UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerLobby");
        }
    }

    // ---------------------------------------------------------------
    // Leaderboard
    // ---------------------------------------------------------------

    private void UpdateLeaderboardCache()
    {
        var allPlayers = FindObjectsOfType<PlayerNetworkData>();
        foreach (var pData in allPlayers)
        {
            if (pData.Object == null) continue;
            
            PlayerRef pref = pData.Object.InputAuthority;
            string pName = pData.PlayerName;
            if (string.IsNullOrEmpty(pName)) pName = "Player";
            
            bool isLocal = pData.Object.HasInputAuthority;
            
            // Only update if we have meaningful data, avoiding resetting to 0 momentarily
            // Though since we fixed NetworkPlayerSpawner, Kills should never be incorrectly 0.
            allTimeLeaderboard[pref] = new LeaderboardEntry
            {
                PlayerName = pName,
                Kills = pData.Kills,
                Deaths = pData.Deaths,
                TeamId = pData.TeamId,
                IsLocalPlayer = isLocal
            };
        }
    }

    /// <summary>
    /// Snapshots all PlayerNetworkData in the scene right now, before any despawns.
    /// Uses the continuously updated allTimeLeaderboard to include dead/despawned players.
    /// </summary>
    private void CacheLeaderboardData()
    {
        // Do one last update just in case
        UpdateLeaderboardCache();
        
        cachedLeaderboardData.Clear();
        cachedLeaderboardData.AddRange(allTimeLeaderboard.Values);

        Debug.Log($"[NetworkUIManager] CacheLeaderboardData — preparing leaderboard with {cachedLeaderboardData.Count} entries");

        // Sort: most kills first, then fewest deaths
        cachedLeaderboardData.Sort((a, b) =>
        {
            int cmp = b.Kills.CompareTo(a.Kills);
            return cmp != 0 ? cmp : a.Deaths.CompareTo(b.Deaths);
        });
    }

    /// <summary>
    /// Updates the pre-made leaderboard row texts with cached player data.
    /// Unused rows are hidden.
    /// </summary>
    private void PopulateLeaderboard()
    {
        int rowCount = Mathf.Min(leaderboardRows.Length, 10);

        for (int i = 0; i < rowCount; i++)
        {
            if (i < cachedLeaderboardData.Count)
            {
                var entry = cachedLeaderboardData[i];

                // Show the row
                if (leaderboardRows[i] != null)
                    leaderboardRows[i].SetActive(true);

                // Update texts
                if (leaderboardNameTexts[i] != null)
                {
                    leaderboardNameTexts[i].text = entry.PlayerName;
                    leaderboardNameTexts[i].color = entry.IsLocalPlayer
                        ? Color.yellow // Yellow for local player
                        : Color.white;
                }

                if (leaderboardKillTexts[i] != null)
                    leaderboardKillTexts[i].text = entry.Kills.ToString();

                if (leaderboardDeathTexts[i] != null)
                    leaderboardDeathTexts[i].text = entry.Deaths.ToString();
            }
            else
            {
                // Hide unused rows
                if (leaderboardRows[i] != null)
                    leaderboardRows[i].SetActive(false);
            }
        }

        if (cachedLeaderboardData.Count == 0)
        {
            Debug.LogWarning("[NetworkUIManager] No cached player data for leaderboard.");
        }
    }

    /// <summary>
    /// Called when local player kills another player
    /// Shows kill notification to local player only
    /// </summary>
    public void OnPlayerKilled(string victimName)
    {
        if (killNotificationText != null)
        {
            // Stop any existing kill notification
            if (killNotificationCoroutine != null)
            {
                StopCoroutine(killNotificationCoroutine);
            }
            
            // Show new kill notification
            killNotificationText.text = $"You killed {victimName}";
            killNotificationText.gameObject.SetActive(true);
            
            killNotificationCoroutine = StartCoroutine(ShowKillNotification(victimName));
        }
    }
    
    /// <summary>
    /// Shows kill notification for specified duration
    /// </summary>
    private System.Collections.IEnumerator ShowKillNotification(string victimName)
    {
        // Show for specified duration
        yield return new WaitForSeconds(killNotificationDuration);
        
        // Hide notification
        killNotificationText.gameObject.SetActive(false);
        killNotificationText.text = "";
    }

    
    /// <summary>
    /// Get ping quality description
    /// </summary>
    private string GetPingQuality(int pingMs)
    {
        if (pingMs < 50) return "Excellent";
        if (pingMs < 100) return "Good";
        if (pingMs < 150) return "Fair";
        if (pingMs < 200) return "Poor";
        return "Very Poor";
    }
    
    /// <summary>
    /// Get color based on ping quality
    /// </summary>
    private Color GetPingColor(int pingMs)
    {
        if (pingMs < 50) return Color.green;      // Excellent
        if (pingMs < 100) return Color.yellow;    // Good  
        if (pingMs < 150) return new Color(1f, 0.5f, 0f); // Orange - Fair
        if (pingMs < 200) return Color.red;       // Poor
        return new Color(0.5f, 0f, 0f);          // Dark Red - Very Poor
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
        
        // Unlock and show cursor so the player can interact with UI (PC only)
        // On mobile, cursor lock is not applicable and can cause touch input spikes
#if !UNITY_ANDROID || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#endif
    }

    public void CloseSettingBTn()
    {
        settingsPanel.SetActive(false);
        
        // Re-lock cursor when closing settings (PC only)
        // On mobile, changing cursor lock state causes touch delta spikes
        // that corrupt camera rotation and invert movement direction
#if !UNITY_ANDROID || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
#endif
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
            UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerLobby");
        }
    }
}
