using UnityEngine;
using Fusion;

/// <summary>
/// Provides smooth visual interpolation for networked objects on clients.
/// Fusion's NetworkTransform handles the network sync, but this adds
/// additional smoothing for 60 FPS rendering between network ticks.
/// Works for ALL players - local and remote - to smooth server position updates.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkTransformInterpolation : NetworkBehaviour
{
    [Header("Interpolation Settings")]
    [SerializeField] private bool interpolatePosition = true;
    [SerializeField] private bool interpolateRotation = true;
    [SerializeField] private float positionLerpSpeed = 15f;
    [SerializeField] private float rotationLerpSpeed = 15f;
    
    [Header("Snap Thresholds")]
    [Tooltip("If position difference exceeds this, snap instead of lerp")]
    [SerializeField] private float snapDistanceThreshold = 5f;
    [Tooltip("If rotation difference exceeds this, snap instead of lerp")]
    [SerializeField] private float snapAngleThreshold = 45f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private Vector3 _renderPosition;
    private Quaternion _renderRotation;
    private Vector3 _lastNetworkPosition;
    private Quaternion _lastNetworkRotation;
    private bool _initialized = false;
    private bool _isLocalPlayer = false;
    
    public override void Spawned()
    {
        // Determine if this is the local player
        _isLocalPlayer = Object.HasInputAuthority;
        
        // Initialize render position/rotation to current transform values
        _renderPosition = transform.position;
        _renderRotation = transform.rotation;
        _lastNetworkPosition = transform.position;
        _lastNetworkRotation = transform.rotation;
        _initialized = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"[NetworkTransformInterpolation] Spawned on {gameObject.name}. IsLocalPlayer: {_isLocalPlayer}");
            Debug.Log($"[NetworkTransformInterpolation] Interpolation ENABLED for {(_isLocalPlayer ? "LOCAL" : "REMOTE")} player");
        }
    }
    
    public override void Render()
    {
        // Safety check
        if (!_initialized) return;
        
        // Store the current network position before we modify it
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
        // Check if we received a new network update
        bool positionChanged = Vector3.Distance(networkPosition, _lastNetworkPosition) > 0.001f;
        bool rotationChanged = Quaternion.Angle(networkRotation, _lastNetworkRotation) > 0.1f;
        
        if (positionChanged || rotationChanged)
        {
            _lastNetworkPosition = networkPosition;
            _lastNetworkRotation = networkRotation;
        }
        
        if (interpolatePosition)
        {
            float distance = Vector3.Distance(_renderPosition, networkPosition);
            
            // Snap if too far away (teleport/respawn)
            if (distance > snapDistanceThreshold)
            {
                _renderPosition = networkPosition;
            }
            else
            {
                // Smooth interpolation
                _renderPosition = Vector3.Lerp(_renderPosition, networkPosition, 
                    positionLerpSpeed * Time.deltaTime);
            }
            
            transform.position = _renderPosition;
        }
        
        if (interpolateRotation)
        {
            float angle = Quaternion.Angle(_renderRotation, networkRotation);
            
            // Snap if rotation difference is too large
            if (angle > snapAngleThreshold)
            {
                _renderRotation = networkRotation;
            }
            else
            {
                // Smooth interpolation
                _renderRotation = Quaternion.Slerp(_renderRotation, networkRotation, 
                    rotationLerpSpeed * Time.deltaTime);
            }
            
            transform.rotation = _renderRotation;
        }
    }
}
