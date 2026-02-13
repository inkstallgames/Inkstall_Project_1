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
    
    [Header("Player Stats UI")]
    [SerializeField] private Slider healthBar;                   // Local player health bar
    [SerializeField] private TextMeshProUGUI healthText;         // Health number text
    [SerializeField] private TextMeshProUGUI killsText;          // Kill count
    [SerializeField] private TextMeshProUGUI deathsText;         // Death count
    [SerializeField] private TextMeshProUGUI playerNameText;     // Local player name

    [Header("Game Info UI")]
    [SerializeField] private TextMeshProUGUI gameStateText;      // Shows current game state
    [SerializeField] private TextMeshProUGUI timerText;          // Round timer

    // Cached references
    private NetworkRunner runner;
    private NetworkObject localPlayerObject;
    private NetworkBombBehaviour localBombBehaviour;
    private PlayerNetworkData localPlayerData;

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
            UpdatePlayerStatsUI();
        }

        UpdateGameInfoUI();
    }

    // ---------------------------------------------------------------
    // Find Local Player
    // ---------------------------------------------------------------

    private void TryFindLocalPlayer()
    {
        if (runner == null || !runner.IsRunning) return;

        localPlayerObject = runner.GetPlayerObject(runner.LocalPlayer);

        if (localPlayerObject != null)
        {
            localBombBehaviour = localPlayerObject.GetComponent<NetworkBombBehaviour>();
            localPlayerData = localPlayerObject.GetComponent<PlayerNetworkData>();

            Debug.Log("[NetworkUIManager] Local player found and cached.");
        }
    }

    // ---------------------------------------------------------------
    // Throw Button
    // ---------------------------------------------------------------

    /// <summary>
    /// Called when the throw button is pressed.
    /// Assign this to your UI Button's OnClick in the Inspector,
    /// or it will be auto-wired if throwButton is assigned.
    /// </summary>
    public void OnThrowButtonPressed()
    {
        if (localBombBehaviour != null)
        {
            localBombBehaviour.RequestThrow();
        }
        else
        {
            Debug.LogWarning("[NetworkUIManager] Cannot throw — local player's NetworkBombBehaviour not found.");
            TryFindLocalPlayer(); // Try to re-find
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
        if (timerText != null && NetworkGameManager.Instance.CurrentGameState == GameState.InProgress)
        {
            float elapsed = Time.time - NetworkGameManager.Instance.RoundStartTime;
            float remaining = NetworkGameManager.Instance.RoundTime - elapsed;
            remaining = Mathf.Max(0, remaining);

            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // ---------------------------------------------------------------
    // Cleanup
    // ---------------------------------------------------------------

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
}
