using UnityEngine;
using StarterAssets; // Needed for FirstPersonController

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int totalPropsToCollect = 0;
    private int propsCollected = 0;
    private bool gameEnded = false;

    [Header("References")]
    public GameTimer gameTimer;        
    public GameObject winUI;           
    public GameObject gameOverUI;      
    public GameObject playerController; // Assigned at runtime when player is spawned

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        totalPropsToCollect = 0;
        propsCollected = 0;
        gameEnded = false;
    }

    public void RegisterCollectible()
    {
        totalPropsToCollect++;
    }

    public void CollectProp()
    {
        propsCollected++;
        Debug.Log($"📦 Collected {propsCollected}/{totalPropsToCollect}");

        if (!gameEnded && propsCollected >= totalPropsToCollect)
        {
            GameWin();
        }
    }

    public void GameWin()
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log("🎉 Game Win! All props collected.");
        if (gameTimer != null) gameTimer.PauseTimer();
        if (winUI != null) winUI.SetActive(true);

        DisablePlayerMovement();
    }

    public void GameOver()
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log("🛑 Game Over! Time's up.");
        if (gameTimer != null) gameTimer.PauseTimer();
        if (gameOverUI != null) gameOverUI.SetActive(true);

        DisablePlayerMovement();
    }

    private void DisablePlayerMovement()
    {
        if (playerController != null)
        {
            FirstPersonController controller = playerController.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("⚠️ PlayerController not assigned in GameManager.");
        }
    }
}
