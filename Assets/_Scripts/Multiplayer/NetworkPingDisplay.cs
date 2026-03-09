using Fusion;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays real-time network ping/RTT (Round Trip Time) for debugging network performance.
/// Shows ping in milliseconds and updates every second.
/// </summary>
public class NetworkPingDisplay : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showPingInConsole = true;
    [SerializeField] private bool showPingOnScreen = true;
    [SerializeField] private float updateInterval = 1f; // Update every second

    [Header("UI (Optional)")]
    [SerializeField] private TextMeshProUGUI pingText;

    private NetworkRunner _runner;
    private float _nextUpdateTime;

    private void Start()
    {
        // Find the NetworkRunner in the scene
        _runner = FindObjectOfType<NetworkRunner>();
        
        if (_runner == null)
        {
            Debug.LogWarning("[NetworkPingDisplay] NetworkRunner not found. Ping display will not work until a session starts.");
        }

        _nextUpdateTime = Time.time + updateInterval;
    }

    private void Update()
    {
        if (Time.time < _nextUpdateTime)
            return;

        _nextUpdateTime = Time.time + updateInterval;

        // Try to find runner if we don't have it yet
        if (_runner == null)
        {
            _runner = FindObjectOfType<NetworkRunner>();
            if (_runner == null || !_runner.IsRunning)
                return;
        }

        // Get ping/RTT from Fusion
        if (_runner.IsRunning)
        {
            // RTT (Round Trip Time) in seconds - convert to milliseconds
            float rttSeconds = (float)_runner.GetPlayerRtt(_runner.LocalPlayer);
            int pingMs = Mathf.RoundToInt(rttSeconds * 1000f);
            
            string role = _runner.IsServer ? "HOST" : "CLIENT";
            
            // Log to console
            if (showPingInConsole)
            {
                Debug.Log($"[NetworkPing] {role} - Ping: {pingMs}ms | RTT: {rttSeconds:F3}s");
            }

            // Update UI text
            if (showPingOnScreen && pingText != null)
            {
                pingText.text = $"Ping: {pingMs}ms\nRole: {role}";
            }
        }
    }

    /// <summary>
    /// Call this to manually get the current ping value
    /// </summary>
    public int GetCurrentPing()
    {
        if (_runner != null && _runner.IsRunning)
        {
            float rttSeconds = (float)_runner.GetPlayerRtt(_runner.LocalPlayer);
            return Mathf.RoundToInt(rttSeconds * 1000f);
        }
        return 0;
    }
}
