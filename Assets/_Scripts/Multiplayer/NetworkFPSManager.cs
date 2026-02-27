using UnityEngine;
using Fusion;

/// <summary>
/// Manages frame rate settings for multiplayer to ensure consistent 60 FPS
/// for both host and client players locally.
/// </summary>
public class NetworkFPSManager : MonoBehaviour
{
    [Header("Frame Rate Settings")]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private bool disableVSync = true;
    
    [Header("Debug")]
    [SerializeField] private bool showFPSCounter = true;
    [SerializeField] private KeyCode toggleFPSKey = KeyCode.F3;
    
    private float deltaTime = 0.0f;
    private GUIStyle style;
    private bool showFPS = false;
    
    private void Awake()
    {
        InitializeFrameRate();
    }
    
    private void Start()
    {
        if (showFPSCounter)
        {
            showFPS = true;
            InitializeGUIStyle();
        }
    }
    
    private void InitializeFrameRate()
    {
        // Disable VSync to allow manual frame rate control
        if (disableVSync)
        {
            QualitySettings.vSyncCount = 0;
            Debug.Log($"[NetworkFPSManager] VSync disabled");
        }
        
        // Set target frame rate
        Application.targetFrameRate = targetFrameRate;
        Debug.Log($"[NetworkFPSManager] Target frame rate set to {targetFrameRate} FPS");
        
        // Ensure the game runs in background (important for multiplayer)
        Application.runInBackground = true;
        
        // Log current quality settings
        Debug.Log($"[NetworkFPSManager] Current Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
    }
    
    private void InitializeGUIStyle()
    {
        style = new GUIStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 24;
        style.normal.textColor = Color.green;
    }
    
    private void Update()
    {
        // Toggle FPS counter
        if (Input.GetKeyDown(toggleFPSKey))
        {
            showFPS = !showFPS;
        }
        
        // Calculate FPS
        if (showFPS)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        }
    }
    
    private void OnGUI()
    {
        if (!showFPS || style == null) return;
        
        float fps = 1.0f / deltaTime;
        string text = $"FPS: {Mathf.Ceil(fps)}";
        
        // Change color based on performance
        if (fps >= 55f)
            style.normal.textColor = Color.green;
        else if (fps >= 40f)
            style.normal.textColor = Color.yellow;
        else
            style.normal.textColor = Color.red;
        
        GUI.Label(new Rect(10, 10, 200, 50), text, style);
    }
    
    private void OnValidate()
    {
        // Reapply settings when changed in inspector
        if (Application.isPlaying)
        {
            InitializeFrameRate();
        }
    }
}
