using UnityEngine;

/// <summary>
/// Applies multiplayer frame-rate settings (target FPS / VSync).
/// </summary>
public class NetworkFPSManager : MonoBehaviour
{
    [Header("Frame Rate Settings")]
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private bool disableVSync = true;

    private void Awake()
    {
        ApplyFrameRate();
    }

    private void ApplyFrameRate()
    {
        if (disableVSync)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = targetFrameRate;
        Application.runInBackground = true;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyFrameRate();
    }
}
