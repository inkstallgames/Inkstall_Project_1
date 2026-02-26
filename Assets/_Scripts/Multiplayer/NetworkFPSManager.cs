using UnityEngine;
using Fusion;

/// <summary>
/// Manages frame rate settings for multiplayer to use device native FPS
/// for optimal performance on both host and client devices.
/// </summary>
public class NetworkFPSManager : MonoBehaviour
{
    [Header("Frame Rate Settings")]
    [SerializeField] private bool useDeviceNativeFPS = true;
    [SerializeField] private int customFrameRate = 60; // Fallback if not using native
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
        
        // Set frame rate based on settings
        if (useDeviceNativeFPS)
        {
            // Use device native refresh rate (uncapped)
            Application.targetFrameRate = -1; // -1 = device native
            Debug.Log($"[NetworkFPSManager] Using device native FPS (uncapped)");
        }
        else
        {
            // Use custom frame rate
            Application.targetFrameRate = customFrameRate;
            Debug.Log($"[NetworkFPSManager] Target frame rate set to {customFrameRate} FPS");
        }
        
        // Ensure the game runs in background (important for multiplayer)
        Application.runInBackground = true;
        
        // Log current quality settings and device refresh rate
        Debug.Log($"[NetworkFPSManager] Current Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        Debug.Log($"[NetworkFPSManager] Device Refresh Rate: {Screen.currentResolution.refreshRate} Hz");
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
        
        // Toggle between native and custom FPS (Ctrl + F3)
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F3))
        {
            useDeviceNativeFPS = !useDeviceNativeFPS;
            InitializeFrameRate();
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
        string fpsMode = useDeviceNativeFPS ? "Native" : $"{customFrameRate}FPS";
        string text = $"FPS: {Mathf.Ceil(fps)} ({fpsMode})";
        
        // Change color based on performance (adjusted for higher FPS)
        if (fps >= 120f)
            style.normal.textColor = Color.cyan; // Excellent performance
        else if (fps >= 60f)
            style.normal.textColor = Color.green; // Good performance
        else if (fps >= 30f)
            style.normal.textColor = Color.yellow; // Acceptable performance
        else
            style.normal.textColor = Color.red; // Poor performance
        
        GUI.Label(new Rect(10, 10, 250, 50), text, style);
        
        // Show device refresh rate info
        string refreshInfo = $"Device: {Screen.currentResolution.refreshRate}Hz";
        GUI.Label(new Rect(10, 40, 200, 30), refreshInfo, style);
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
