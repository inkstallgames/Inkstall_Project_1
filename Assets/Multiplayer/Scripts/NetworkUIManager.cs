using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    [Header("Disconnect Notification UI")]
    [SerializeField] private TextMeshProUGUI disconnectNotificationText; // Shows "[PlayerName] disconnected" to all players
    [SerializeField] private float disconnectNotificationDuration = 3f;
    [SerializeField] private float disconnectSlideInDuration = 0.35f;
    [SerializeField] private float disconnectSlideDistance = 800f;

    [Header("Kill Feed UI (Global)")]
    [SerializeField] private RectTransform killFeedContainer; // Container with VerticalLayoutGroup
    [SerializeField] private GameObject killFeedItemPrefab; // Prefab with KillFeedItem script
    [SerializeField] private int maxKillFeedItems = 5;
    
    [System.Serializable]
    public struct WeaponIconEntry
    {
        public string weaponName;
        public Sprite weaponIcon;
    }
    [SerializeField] private List<WeaponIconEntry> weaponIcons = new List<WeaponIconEntry>();

    [Header("Powerup Notification UI")]
    [SerializeField] private TextMeshProUGUI powerupNotificationText;
    [SerializeField] private float powerupNotificationDuration = 3f;
    private Coroutine powerupNotificationCoroutine;

    [Header("Game Info UI")]
    [SerializeField] private TextMeshProUGUI gameStateText;      // Shows current game state
    [SerializeField] private TextMeshProUGUI timerText;          // Round timer
    [SerializeField] private TextMeshProUGUI pingText;           // Shows current ping in ms

    [Header("Team Score UI")]
    [SerializeField] private TextMeshProUGUI blueTeamScoreText;  // Blue team score display
    [SerializeField] private TextMeshProUGUI redTeamScoreText;   // Red team score display
    [SerializeField] private GameObject teamAScorePanel;          // GameObject representing Team A Score UI (hidden in FFA)
    [SerializeField] private GameObject teamBScorePanel;          // GameObject representing Team B Score UI (hidden in FFA)
    [SerializeField] private GameObject ffaLocalPlayerPanel;            // Parent container for FFA local player rank & kills (enabled in FFA only)
    [SerializeField] private TextMeshProUGUI ffaLocalPlayerKillsText;  // Text showing local player's kills in FFA (e.g. 5 Kills)
    [SerializeField] private TextMeshProUGUI ffaLocalPlayerRankText;   // Text showing local player's rank in FFA (e.g. #2)

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

    // Treat live HUD editing like the settings menu so gameplay input remains blocked.
    public bool IsSettingsPanelActive =>
        (settingsPanel != null && settingsPanel.activeSelf) ||
        HUDCustomizationManager.IsEditMode;

    /// <summary>Alias used by input scripts to suppress movement/look while a menu or the HUD editor is open.</summary>
    public bool IsGameplayInputBlocked => IsSettingsPanelActive;

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
    private float leaderboardUpdateTimer = 0f;
    private const float LEADERBOARD_UPDATE_INTERVAL = 0.5f;
    private float ffaRankUpdateTimer = 0f;
    private const float FFA_RANK_UPDATE_INTERVAL = 0.5f;
    private int _lastKnownHealth = -1;
    private int _lastDisplayedHealth = -1;
    private int _lastKnownKills = -1;
    private int _lastDisplayedKills = -1;
    private int _lastKnownDeaths = -1;
    private int _lastDisplayedBombs = -1;
    private int _lastDisplayedPistolAmmo = -1;
    private int _lastDisplayedPistolReserve = -1;
    private int _lastDisplayedLaserEnergy = -1;
    private int _lastDisplayedLaserReserve = -1;
    private string _lastDisplayedPlayerName;
    private int _lastDisplayedBlueScore = -1;
    private int _lastDisplayedRedScore = -1;
    private int _lastDisplayedFfaKills = -1;
    private int _lastDisplayedFfaRank = -1;
    private string _lastDisplayedGameState;
    private int _lastDisplayedTimerSeconds = -1;
    private int _lastDisplayedWaitingSeconds = -1;
    private string _lastWaitingStatus;
    private GameMode? _lastDisplayedScoreMode;
    private bool? _lastAbilityReady;
    private readonly List<KeyValuePair<PlayerRef, int>> _ffaKillSortBuffer = new List<KeyValuePair<PlayerRef, int>>(10);
    private float _damageIndicatorTimer = 0f;
    private const float DAMAGE_INDICATOR_DURATION = 0.3f;
    private float _healIndicatorTimer = 0f;
    private const float HEAL_INDICATOR_DURATION = 0.3f;

    // Kill notification system
    private float killNotificationTimer = 0f;
    private Coroutine killNotificationCoroutine;
    private Coroutine disconnectNotificationCoroutine;
    private RectTransform _disconnectNotificationRect;
    private Vector2 _disconnectNotificationRestPos;
    private bool _disconnectRestPosCached;

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
        if (HUDCustomizationManager.IsCreatingPreview)
        {
            Destroy(this); // Just remove the script, don't destroy the Canvas!
            return;
        }

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

        if (disconnectNotificationText == null)
        {
            // Use the existing scene object "Player Disconnted Warning" if not assigned in Inspector
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == "Player Disconnted Warning")
                {
                    disconnectNotificationText = texts[i];
                    break;
                }
            }
        }

        if (disconnectNotificationText != null)
        {
            _disconnectNotificationRect = disconnectNotificationText.rectTransform;
            _disconnectNotificationRestPos = _disconnectNotificationRect.anchoredPosition;
            _disconnectRestPosCached = true;
            disconnectNotificationText.gameObject.SetActive(false);
            disconnectNotificationText.text = "";
        }

        // Apply any saved HUD layout immediately at Start to prevent snapping later
        if (throwButton != null)
        {
            Canvas rootCanvas = throwButton.GetComponentInParent<Canvas>(true);
            if (rootCanvas != null)
            {
                HUDCustomizationManager.ApplySavedLayout(rootCanvas.gameObject);
            }
            else
            {
                HUDCustomizationManager.ApplySavedLayout(gameObject);
            }
        }
        else
        {
            HUDCustomizationManager.ApplySavedLayout(gameObject);
        }
    }

    private void Update()
    {
        // Try to find runner and local player if not cached yet
        // Also invalidate stale runner (destroyed or no longer running)
        if (runner == null || !runner)
        {
            if (NetworkStarter.Instance != null && NetworkStarter.Instance.Runner != null)
                runner = NetworkStarter.Instance.Runner;
            if (runner == null)
                runner = FindObjectOfType<NetworkRunner>();
        }
        else if (!runner.IsRunning)
        {
            // Runner exists but stopped running (e.g. after shutdown)
            runner = null;
            ClearCachedPlayerReferences();
            return; // Skip this frame, re-find runner next frame
        }

        // Invalidate cached references if the underlying object was destroyed
        // (happens when a player disconnects and reconnects — old object is despawned)
        if (localPlayerObject != null && (!localPlayerObject || !localPlayerObject.IsValid))
        {
            ClearCachedPlayerReferences();
        }

        if (runner != null && localPlayerObject == null)
        {
            TryFindLocalPlayer();
            
            if (localPlayerObject != null)
            {
                // Re-apply layout now that the local player has spawned
                if (throwButton != null)
                {
                    Canvas rootCanvas = throwButton.GetComponentInParent<Canvas>(true);
                    if (rootCanvas != null)
                    {
                        HUDCustomizationManager.ApplySavedLayout(rootCanvas.gameObject);
                    }
                    else
                    {
                        HUDCustomizationManager.ApplySavedLayout(gameObject);
                    }
                }
                else
                {
                    HUDCustomizationManager.ApplySavedLayout(gameObject);
                }
            }
        }

        // Update all UI elements
        if (localPlayerObject != null)
        {
            UpdateBombUI();
            UpdateBulletAmmoUI();
            UpdatePlayerStatsUI();

            // Leaderboard is only shown at game-over; throttle scene scans to avoid per-frame cost.
            leaderboardUpdateTimer -= Time.deltaTime;
            if (leaderboardUpdateTimer <= 0f)
            {
                leaderboardUpdateTimer = LEADERBOARD_UPDATE_INTERVAL;
                UpdateLeaderboardCache();
            }

#if UNITY_EDITOR
            // TEST KILL FEED + KILL NOTIFICATION (editor only)
            if (Input.GetKeyDown(KeyCode.K))
            {
                int randomKillerTeam = Random.Range(0, 2);
                int randomVictimTeam = randomKillerTeam == 0 ? 1 : 0;
                string[] testWeapons = { "Pistol", "Laser", "Bomb" };
                string randomWeapon = testWeapons[Random.Range(0, testWeapons.Length)];
                AddKillFeedEntry("Test Killer", randomKillerTeam, "Test Victim", randomVictimTeam, randomWeapon);
                OnPlayerKilled("Test Victim");
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                if (NetworkGameManager.Instance != null && runner != null && runner.IsRunning)
                {
                    NetworkGameManager.Instance.RPC_DebugSimulateKill(runner.LocalPlayer);
                }
            }
#endif

            // Keyboard shortcut: press T to throw/shoot
            // For laser: use GetKey (held) for continuous fire, GetKeyDown for others
            bool isLaserEquipped = localEquipSystem != null && localEquipSystem.IsLaserEquipped();
            bool isPistolEquipped = localEquipSystem != null && localEquipSystem.IsPistolEquipped();
            bool hasPistolAutoFire = isPistolEquipped && localPistolBehaviour != null && localPistolBehaviour.HasAutoFirePowerup;
            
            if (isLaserEquipped || hasPistolAutoFire)
            {
                if (Input.GetKey(KeyCode.T))
                {
                    if (isLaserEquipped && localLaserBehaviour != null)
                        localLaserBehaviour.RequestShoot();
                    else if (hasPistolAutoFire && localPistolBehaviour != null)
                        localPistolBehaviour.RequestShoot();
                }
                else if (Input.GetKeyUp(KeyCode.T))
                {
                    if (isLaserEquipped && localLaserBehaviour != null)
                        localLaserBehaviour.StopShooting();
                }
            }
            else if (Input.GetKeyDown(KeyCode.T))
            {
                OnThrowButtonPressed();
            }
            
            // Continuous fire while EITHER throw button is held (via HoldableButton)
            bool isThrowHeld = (throwHoldable != null && throwHoldable.IsHeld)
                            || (throwHoldableLeft != null && throwHoldableLeft.IsHeld);
            if (isThrowHeld)
            {
                if (isLaserEquipped && localLaserBehaviour != null)
                {
                    localLaserBehaviour.RequestShoot();
                }
                else if (hasPistolAutoFire && localPistolBehaviour != null)
                {
                    localPistolBehaviour.RequestShoot();
                }
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
                    int secondsLeft = Mathf.CeilToInt(remainingTime);
                    if (secondsLeft != _lastDisplayedWaitingSeconds)
                    {
                        _lastDisplayedWaitingSeconds = secondsLeft;
                        _lastWaitingStatus = $"Game starting in {secondsLeft}";
                        waitingStatusText.text = _lastWaitingStatus;
                    }

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
                    int joined = lobbyManager.PlayersLoadedCount;
                    int total = lobbyManager.LobbyPlayers.Count;
                    string status = $"{joined}/{total} players joined";
                    if (_lastWaitingStatus != status)
                    {
                        _lastWaitingStatus = status;
                        _lastDisplayedWaitingSeconds = -1;
                        waitingStatusText.text = _lastWaitingStatus;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // Find Local Player
    // ---------------------------------------------------------------

    private void TryFindLocalPlayer()
    {
        if (runner == null || !runner || !runner.IsRunning) return;

        localPlayerObject = runner.GetPlayerObject(runner.LocalPlayer);

        // Fallback: if SetPlayerObject was never called or hasn't synced yet,
        // find the local player manually by scanning multiple component types.
        // The Alien (Team B) prefab may not have NetworkBombBehaviour, so we
        // also check NetworkLaserBehaviour and PlayerNetworkData.
        if (localPlayerObject == null)
        {
            // Try NetworkBombBehaviour first
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

        if (localPlayerObject == null)
        {
            // Try PlayerNetworkData as a universal fallback (all prefabs have this)
            var allPlayers = FindObjectsOfType<PlayerNetworkData>();
            foreach (var pnd in allPlayers)
            {
                if (pnd.Object != null && pnd.Object.HasInputAuthority)
                {
                    localPlayerObject = pnd.Object;
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
            // DISABLED - Performance killer: Debug.Log($"[NetworkUIManager] Local player found. Pistol: {localPistolBehaviour != null}, Laser: {localLaserBehaviour != null}, Equip: {localEquipSystem != null}");
        }
    }

    /// <summary>
    /// Clears all cached player references so TryFindLocalPlayer can re-discover them.
    /// Called when the cached object is destroyed (e.g. player disconnect/reconnect).
    /// </summary>
    private void ClearCachedPlayerReferences()
    {
        localPlayerObject = null;
        localBombBehaviour = null;
        localPistolBehaviour = null;
        localLaserBehaviour = null;
        localEquipSystem = null;
        localPlayerData = null;
        localAbilityController = null;
        _lastKnownHealth = -1;
        _lastDisplayedHealth = -1;
        _lastKnownKills = -1;
        _lastDisplayedKills = -1;
        _lastKnownDeaths = -1;
        _lastDisplayedBombs = -1;
        _lastDisplayedPistolAmmo = -1;
        _lastDisplayedPistolReserve = -1;
        _lastDisplayedLaserEnergy = -1;
        _lastDisplayedLaserReserve = -1;
        _lastDisplayedPlayerName = null;
        _lastDisplayedBlueScore = -1;
        _lastDisplayedRedScore = -1;
        _lastDisplayedFfaKills = -1;
        _lastDisplayedFfaRank = -1;
        _lastDisplayedGameState = null;
        _lastDisplayedTimerSeconds = -1;
        _lastDisplayedWaitingSeconds = -1;
        _lastWaitingStatus = null;
        _lastDisplayedScoreMode = null;
        _lastAbilityReady = null;
        leaderboardUpdateTimer = 0f;
        ffaRankUpdateTimer = 0f;
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
                // Unlimited ammo - always allow reload
                localPistolBehaviour.RequestReload();
            }
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null && !localLaserBehaviour.IsReloading)
            {
                // Unlimited energy - always allow reload
                localLaserBehaviour.RequestReload();
            }
        }
        else if (localPistolBehaviour != null && !localPistolBehaviour.IsReloading)
        {
            // Unlimited ammo - always allow reload
            localPistolBehaviour.RequestReload();
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
        if (_lastAbilityReady == ready) return;
        _lastAbilityReady = ready;

        // Radial fill overlay: fully filled (blocked) when no charge, hidden when ready
        if (abilityCooldownOverlay != null)
        {
            abilityCooldownOverlay.fillAmount = ready ? 0f : 1f;
            abilityCooldownOverlay.gameObject.SetActive(!ready);
        }

        // Text: "Q" when ready, "KILL" when waiting for a kill
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
        if (currentBombs == _lastDisplayedBombs) return;
        _lastDisplayedBombs = currentBombs;

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

    private int _lastEquippedWeaponType = -1; // 0=pistol, 1=laser

    private void UpdateBulletAmmoUI()
    {
        int equippedType = -1;
        if (localEquipSystem != null)
        {
            if (localEquipSystem.IsPistolEquipped() && localPistolBehaviour != null)
                equippedType = 0;
            else if (localEquipSystem.IsLaserEquipped() && localLaserBehaviour != null)
                equippedType = 1;
        }
        else if (localPistolBehaviour != null)
        {
            equippedType = 0;
        }

        if (equippedType != _lastEquippedWeaponType)
        {
            _lastEquippedWeaponType = equippedType;
            _lastDisplayedPistolAmmo = -1;
            _lastDisplayedPistolReserve = -1;
            _lastDisplayedLaserEnergy = -1;
            _lastDisplayedLaserReserve = -1;
        }

        if (equippedType == 0)
            UpdatePistolAmmoUI();
        else if (equippedType == 1)
            UpdateLaserAmmoUI();
    }

    private void UpdatePistolAmmoUI()
    {
        // Auto-reload when current ammo is zero (unlimited ammo)
        if (localPistolBehaviour.CurrentAmmo == 0 && !localPistolBehaviour.IsReloading)
        {
            localPistolBehaviour.RequestReload();
        }

        int currentBullets = localPistolBehaviour.CurrentAmmo;
        int maxCapacity = localPistolBehaviour.MaxAmmo;
        bool isReloading = localPistolBehaviour.IsReloading;

        // Update bullet ammo text when values change (show current/maxCapacity)
        if (bulletAmmoText != null &&
            (currentBullets != _lastDisplayedPistolAmmo || maxCapacity != _lastDisplayedPistolReserve))
        {
            _lastDisplayedPistolAmmo = currentBullets;
            _lastDisplayedPistolReserve = maxCapacity;
            bulletAmmoText.text = $"{currentBullets:D2}/{maxCapacity:D2}";
        }

        // Update reload button visual feedback (unlimited ammo - always allow reload if not full)
        if (reloadButton != null)
        {
            bool canReload = !isReloading && currentBullets < maxCapacity;
            reloadButton.interactable = canReload;
        }
    }

    private void UpdateLaserAmmoUI()
    {
        // Auto-reload when energy reaches zero (unlimited system)
        if (localLaserBehaviour.CurrentEnergy == 0 && !localLaserBehaviour.IsReloading)
        {
            // Laser auto-reloads when energy reaches zero (handled in NetworkLaserBehaviour)
        }

        int currentEnergy = localLaserBehaviour.CurrentEnergy;
        int maxCapacity = localLaserBehaviour.MaxEnergy;

        // Update laser energy text when values change (show current/maxCapacity)
        if (bulletAmmoText != null &&
            (currentEnergy != _lastDisplayedLaserEnergy || maxCapacity != _lastDisplayedLaserReserve))
        {
            _lastDisplayedLaserEnergy = currentEnergy;
            _lastDisplayedLaserReserve = maxCapacity;
            bulletAmmoText.text = $"{currentEnergy:D2}/{maxCapacity:D2}";
        }

        // Update reload button visual feedback (unlimited energy - always allow reload if not full)
        if (reloadButton != null)
        {
            bool canReload = !localLaserBehaviour.IsReloading && currentEnergy < maxCapacity;
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
            float healthNormalized = currentHealth / 100f;
            if (!Mathf.Approximately(healthBar.value, healthNormalized))
                healthBar.value = healthNormalized;
        }

        if (healthText != null && currentHealth != _lastDisplayedHealth)
        {
            _lastDisplayedHealth = currentHealth;
            healthText.text = $"{currentHealth}";
        }

        // Kills & Deaths
        if (killsText != null && currentKills != _lastDisplayedKills)
        {
            _lastDisplayedKills = currentKills;
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

        int currentDeaths = localPlayerData.Deaths;
        if (deathsText != null && currentDeaths != _lastKnownDeaths)
        {
            _lastKnownDeaths = currentDeaths;
            deathsText.text = $"Deaths: {currentDeaths}";
        }

        // Player name
        string playerName = localPlayerData.PlayerName;
        if (playerNameText != null && playerName != _lastDisplayedPlayerName)
        {
            _lastDisplayedPlayerName = playerName;
            playerNameText.text = playerName;
        }
    }

    // ---------------------------------------------------------------
    // Game Info UI
    // ---------------------------------------------------------------

    private void UpdateGameInfoUI()
    {
        if (NetworkGameManager.Instance == null) return;
        
        // Guard against accessing networked properties on a despawned object
        // (NetworkGameManager uses DontDestroyOnLoad so Instance persists,
        //  but its NetworkObject may be invalid after runner shutdown)
        var gm = NetworkGameManager.Instance;
        if (gm.Object == null || !gm.Object.IsValid) return;

        // Game state
        if (gameStateText != null)
        {
            string stateLabel = gm.CurrentGameState.ToString();
            if (stateLabel != _lastDisplayedGameState)
            {
                _lastDisplayedGameState = stateLabel;
                gameStateText.text = stateLabel;
            }
        }

        // Timer — only rewrite TMP when the displayed second changes
        if (timerText != null && runner != null && runner.IsRunning && gm.CurrentGameState == GameState.InProgress)
        {
            float elapsed = runner.SimulationTime - gm.RoundStartTime;
            float remaining = Mathf.Max(0f, gm.RoundTime - elapsed);
            int totalSeconds = Mathf.FloorToInt(remaining);

            if (totalSeconds != _lastDisplayedTimerSeconds)
            {
                _lastDisplayedTimerSeconds = totalSeconds;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        // Team & FFA scores — only toggle panel active state when mode changes
        if (gm.CurrentGameMode == GameMode.TeamDeathmatch)
        {
            if (_lastDisplayedScoreMode != GameMode.TeamDeathmatch)
            {
                _lastDisplayedScoreMode = GameMode.TeamDeathmatch;
                if (teamAScorePanel != null) teamAScorePanel.SetActive(true);
                if (teamBScorePanel != null) teamBScorePanel.SetActive(true);
                if (ffaLocalPlayerPanel != null) ffaLocalPlayerPanel.SetActive(false);
                if (blueTeamScoreText != null) blueTeamScoreText.gameObject.SetActive(true);
                if (redTeamScoreText != null) redTeamScoreText.gameObject.SetActive(true);
            }

            if (blueTeamScoreText != null && gm.BlueTeamScore != _lastDisplayedBlueScore)
            {
                _lastDisplayedBlueScore = gm.BlueTeamScore;
                blueTeamScoreText.text = $"{gm.BlueTeamScore}";
            }

            if (redTeamScoreText != null && gm.RedTeamScore != _lastDisplayedRedScore)
            {
                _lastDisplayedRedScore = gm.RedTeamScore;
                redTeamScoreText.text = $"{gm.RedTeamScore}";
            }
        }
        else if (gm.CurrentGameMode == GameMode.FreeForAll)
        {
            if (_lastDisplayedScoreMode != GameMode.FreeForAll)
            {
                _lastDisplayedScoreMode = GameMode.FreeForAll;
                if (teamAScorePanel != null) teamAScorePanel.SetActive(false);
                if (teamBScorePanel != null) teamBScorePanel.SetActive(false);
                if (ffaLocalPlayerPanel != null) ffaLocalPlayerPanel.SetActive(true);
            }

            // Find and display local player's rank & kills (throttled, no LINQ allocations)
            if (runner != null && runner.IsRunning)
            {
                ffaRankUpdateTimer -= Time.deltaTime;
                if (ffaRankUpdateTimer <= 0f)
                {
                    ffaRankUpdateTimer = FFA_RANK_UPDATE_INTERVAL;

                    _ffaKillSortBuffer.Clear();
                    foreach (var entry in gm.PlayerKills)
                        _ffaKillSortBuffer.Add(new KeyValuePair<PlayerRef, int>(entry.Key, entry.Value));
                    _ffaKillSortBuffer.Sort((a, b) => b.Value.CompareTo(a.Value));

                    int localRank = -1;
                    int localKills = 0;

                    for (int i = 0; i < _ffaKillSortBuffer.Count; i++)
                    {
                        if (_ffaKillSortBuffer[i].Key == runner.LocalPlayer)
                        {
                            localRank = i + 1;
                            localKills = _ffaKillSortBuffer[i].Value;
                            break;
                        }
                    }

                    if (localKills != _lastDisplayedFfaKills && ffaLocalPlayerKillsText != null)
                    {
                        _lastDisplayedFfaKills = localKills;
                        ffaLocalPlayerKillsText.text = localRank != -1 ? $"{localKills} Kills" : "0 Kills";
                    }

                    if (localRank != _lastDisplayedFfaRank && ffaLocalPlayerRankText != null)
                    {
                        _lastDisplayedFfaRank = localRank;
                        ffaLocalPlayerRankText.text = localRank != -1 ? $"#{localRank}" : "#-";
                    }
                }
            }
            else
            {
                if (ffaLocalPlayerKillsText != null) ffaLocalPlayerKillsText.text = "0 Kills";
                if (ffaLocalPlayerRankText != null) ffaLocalPlayerRankText.text = "#-";
            }
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

    public void ShowPowerupNotification(string powerupName)
    {
        if (powerupNotificationText != null)
        {
            powerupNotificationText.text = $"Power-Up Acquired: {powerupName}!";
            powerupNotificationText.gameObject.SetActive(true);
            
            if (powerupNotificationCoroutine != null)
            {
                StopCoroutine(powerupNotificationCoroutine);
            }
            powerupNotificationCoroutine = StartCoroutine(HidePowerupNotificationAfterDelay());
        }
    }

    /// <summary>
    /// Shows which player disconnected mid-game. Visible to all players still in the match.
    /// Slides in from left to right, then hides after the display duration.
    /// </summary>
    public void ShowPlayerDisconnected(string playerName)
    {
        if (disconnectNotificationText == null) return;

        if (string.IsNullOrEmpty(playerName))
            playerName = "A player";

        if (disconnectNotificationCoroutine != null)
        {
            StopCoroutine(disconnectNotificationCoroutine);
        }

        if (_disconnectNotificationRect == null)
            _disconnectNotificationRect = disconnectNotificationText.rectTransform;

        if (!_disconnectRestPosCached && _disconnectNotificationRect != null)
        {
            _disconnectNotificationRestPos = _disconnectNotificationRect.anchoredPosition;
            _disconnectRestPosCached = true;
        }

        disconnectNotificationText.text = $"{playerName} disconnected";
        disconnectNotificationText.gameObject.SetActive(true);
        disconnectNotificationCoroutine = StartCoroutine(AnimateDisconnectNotification());
    }

    private System.Collections.IEnumerator AnimateDisconnectNotification()
    {
        if (_disconnectNotificationRect != null)
        {
            // Start off-screen to the left, then slide to the rest position
            Vector2 startPos = _disconnectNotificationRestPos + Vector2.left * disconnectSlideDistance;
            _disconnectNotificationRect.anchoredPosition = startPos;

            float time = 0f;
            while (time < disconnectSlideInDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / disconnectSlideInDuration);
                // Smoothstep ease-out
                float smoothT = t * t * (3f - 2f * t);
                _disconnectNotificationRect.anchoredPosition = Vector2.Lerp(startPos, _disconnectNotificationRestPos, smoothT);
                yield return null;
            }

            _disconnectNotificationRect.anchoredPosition = _disconnectNotificationRestPos;
        }

        yield return new WaitForSeconds(disconnectNotificationDuration);

        if (disconnectNotificationText != null)
        {
            disconnectNotificationText.gameObject.SetActive(false);
            disconnectNotificationText.text = "";
            if (_disconnectNotificationRect != null && _disconnectRestPosCached)
                _disconnectNotificationRect.anchoredPosition = _disconnectNotificationRestPos;
        }

        disconnectNotificationCoroutine = null;
    }

    private System.Collections.IEnumerator HidePowerupNotificationAfterDelay()
    {
        yield return new WaitForSeconds(powerupNotificationDuration);
        if (powerupNotificationText != null)
        {
            powerupNotificationText.gameObject.SetActive(false);
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

        ShutdownAndReturnToLobby();
    }

    /// <summary>
    /// Async helper — called at the end of the leaderboard sequence to cleanly
    /// return to the lobby scene without shutting down the connection.
    /// </summary>
    private void ShutdownAndReturnToLobby()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (runner != null && runner.IsServer)
        {
            // Reopen the session so new players can join for the next match
            if (runner.SessionInfo != null)
            {
                runner.SessionInfo.IsOpen = true;
                Debug.Log("[NetworkUIManager] Session reopened — players can join again.");
            }
            
            runner.LoadScene("MultiplayerLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else if (runner == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MultiplayerLobby");
        }
    }

    // ---------------------------------------------------------------
    // Leaderboard
    // ---------------------------------------------------------------

    private readonly List<PlayerRef> _leaderboardKeysToRemove = new List<PlayerRef>(4);

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
            
            // Clean up any old duplicate entries for this player name that had a different PlayerRef (due to reconnecting)
            _leaderboardKeysToRemove.Clear();
            foreach (var kvp in allTimeLeaderboard)
            {
                if (kvp.Value.PlayerName == pName && kvp.Key != pref)
                {
                    _leaderboardKeysToRemove.Add(kvp.Key);
                }
            }
            for (int i = 0; i < _leaderboardKeysToRemove.Count; i++)
            {
                allTimeLeaderboard.Remove(_leaderboardKeysToRemove[i]);
            }
            
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
        
        // Final fallback: filter out duplicate names to prevent showing reconnected players multiple times
        System.Collections.Generic.Dictionary<string, LeaderboardEntry> uniqueEntries = new System.Collections.Generic.Dictionary<string, LeaderboardEntry>();
        foreach (var entry in allTimeLeaderboard.Values)
        {
            if (string.IsNullOrEmpty(entry.PlayerName)) continue;
            
            if (uniqueEntries.TryGetValue(entry.PlayerName, out var existing))
            {
                // Keep the one with more kills/deaths or the active one
                if (entry.Kills > existing.Kills || (entry.Kills == existing.Kills && entry.Deaths > existing.Deaths))
                {
                    uniqueEntries[entry.PlayerName] = entry;
                }
            }
            else
            {
                uniqueEntries[entry.PlayerName] = entry;
            }
        }
        
        cachedLeaderboardData.AddRange(uniqueEntries.Values);

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
    public void AddKillFeedEntry(string killerName, int killerTeam, string victimName, int victimTeam, string weaponName)
    {
        if (killFeedContainer == null || killFeedItemPrefab == null) return;
        
        // Find matching sprite for the weapon
        Sprite weaponSprite = null;
        if (!string.IsNullOrEmpty(weaponName))
        {
            var entry = weaponIcons.Find(x => x.weaponName.ToLower() == weaponName.ToLower());
            weaponSprite = entry.weaponIcon;
        }

        // Clean up old entries if we exceed the limit
        if (killFeedContainer.childCount >= maxKillFeedItems)
        {
            // Destroy the oldest entry (the first child)
            Destroy(killFeedContainer.GetChild(0).gameObject);
        }

        // Instantiate new kill feed item (appears at the bottom by default)
        GameObject itemGO = Instantiate(killFeedItemPrefab, killFeedContainer);
        KillFeedItem feedItem = itemGO.GetComponent<KillFeedItem>();

        if (feedItem != null)
        {
            // Pass the looked-up weapon sprite
            feedItem.Setup(killerName, victimName, weaponSprite, killerTeam, victimTeam);
        }
    }

    public void OnPlayerKilled(string victimName)
    {
        // Fallback: plain text show/hide (original behaviour)
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
            
            // If it has a canvas group (maybe added by the user manually), ensure it's visible
            var cg = killNotificationText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            
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
#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
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
#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
#endif
    }

    /// <summary>
    /// Opens the live HUD editor from the in-match Settings panel.
    /// Wire the Settings > Edit Controls button to this method.
    /// </summary>
    public void EditControlsBtn()
    {
        Debug.Log("[HUDCustomize] Edit Controls button clicked.");

        var customizer = HUDCustomizationManager.Instance;
        if (customizer == null)
        {
            Debug.LogWarning("[HUDCustomize] HUDCustomizationManager.Instance is null — add the HUD Customizer object to this scene.");
            return;
        }

        customizer.OpenLiveEditor();
        if (!customizer.IsLiveEditing)
        {
            Debug.LogWarning("[HUDCustomize] Live editing did not start — see the warning above for the reason.");
            return;
        }

        // The same customization panel used in the lobby is now visible.
        settingsPanel.SetActive(false);
        Debug.Log("[HUDCustomize] Settings panel hidden; customization panel is now driving the HUD edit.");

#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#endif
    }

    /// <summary>Called by the shared customization panel after Save or Close.</summary>
    public void FinishLiveControlEditing()
    {
        Debug.Log("[HUDCustomize] Editing finished — closing menus and resuming gameplay.");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
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
