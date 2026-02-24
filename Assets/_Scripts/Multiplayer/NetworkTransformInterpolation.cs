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
            if (_isLocalPlayer)
            {
                Debug.Log($"[NetworkTransformInterpolation] LOCAL player - Position: NOT interpolated (direct), Rotation: Interpolated");
            }
            else
            {
                Debug.Log($"[NetworkTransformInterpolation] REMOTE player - Position: Interpolated, Rotation: Interpolated");
            }
        }
    }
    
    public override void Render()
    {
        // Safety check - skip for local player entirely (they use client-side prediction)
        if (!_initialized) return;
        
        // Store the current network position before we modify it
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
        // Detect if network position/rotation changed this tick
        bool positionChanged = Vector3.Distance(transform.position, _lastNetworkPosition) > 0.001f;
        bool rotationChanged = Quaternion.Angle(transform.rotation, _lastNetworkRotation) > 0.1f;

        if (positionChanged)
        {
            _lastNetworkPosition = transform.position;
            _renderPosition = transform.position; // Snap render position to new network position
        }

        if (rotationChanged)
        {
            _lastNetworkRotation = transform.rotation;
            _renderRotation = transform.rotation; // Snap render rotation to new network rotation
        }

        // Local player: Direct position (no lag), interpolated rotation (smooth camera)
        // Remote players: Interpolate both for smoothness
        if (_isLocalPlayer)
        {
            // Position: Direct from server (no interpolation to avoid input lag)
            _renderPosition = transform.position;
            
            // Rotation: Interpolate for smooth camera movement
            if (interpolateRotation)
            {
                _renderRotation = Quaternion.Slerp(_renderRotation, transform.rotation, rotationLerpSpeed * Time.deltaTime);
                if (Quaternion.Angle(_renderRotation, transform.rotation) < 0.5f)
                {
                    _renderRotation = transform.rotation;
                }
            }
            else
            {
                _renderRotation = transform.rotation;
            }
        }
        else
        {
            // Remote players: Full interpolation for smooth visuals
            if (interpolatePosition)
            {
                _renderPosition = Vector3.Lerp(_renderPosition, transform.position, positionLerpSpeed * Time.deltaTime);
                if (Vector3.Distance(_renderPosition, transform.position) < 0.01f)
                {
                    _renderPosition = transform.position;
                }
            }
            else
            {
                _renderPosition = transform.position;
            }

            if (interpolateRotation)
            {
                _renderRotation = Quaternion.Slerp(_renderRotation, transform.rotation, rotationLerpSpeed * Time.deltaTime);
                if (Quaternion.Angle(_renderRotation, transform.rotation) < 0.5f)
                {
                    _renderRotation = transform.rotation;
                }
            }
            else
            {
                _renderRotation = transform.rotation;
            }
        }

        // Apply the interpolated values to the visual transform
        transform.position = _renderPosition;
        transform.rotation = _renderRotation;
    }
}
