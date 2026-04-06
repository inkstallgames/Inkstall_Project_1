using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }
    
    [Header("UI Elements")]
    public Text healthText;
    public Text killsText;
    public Text deathsText;
    public Text gameModeText;
    public Text teamScoreText;
    
    private NetworkRunner runner;
    private NetworkGameManager gameManager;
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        gameManager = FindObjectOfType<NetworkGameManager>();
    }
    
    void Update()
    {
        if (runner == null || !runner.IsRunning) return;
        
        // Update local player stats
        var localPlayer = runner.LocalPlayer;
        var playerObject = runner.GetPlayerObject(localPlayer);
        
        if (playerObject != null)
        {
            var networkData = playerObject.GetComponent<PlayerNetworkData>();
            
            if (networkData != null)
            {
                healthText.text = $"Health: {networkData.Health}";
                killsText.text = $"Kills: {networkData.Kills}";
                deathsText.text = $"Deaths: {networkData.Deaths}";
            }
        }
        
        // Update game info
        if (gameManager != null)
        {
            gameModeText.text = $"Mode: {gameManager.CurrentGameMode}";
            
            if (gameManager.CurrentGameMode == GameMode.TeamDeathmatch)
            {
                teamScoreText.text = $"Blue: {gameManager.BlueTeamScore} - Red: {gameManager.RedTeamScore}";
            }
            else
            {
                teamScoreText.text = "Free For All";
            }
        }
    }
}
