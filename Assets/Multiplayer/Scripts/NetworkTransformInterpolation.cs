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
        // If this object has a CharacterController, it's a player character.
        // ThirdPersonController + CharacterController handle movement and client prediction.
        // This interpolation script must NOT touch transform.position on players
        // or it will fight with CharacterController causing rubberbanding.
        if (GetComponent<CharacterController>() != null)
        {
            // Debug.Log($"[NetworkTransformInterpolation] Disabled on {gameObject.name} - CharacterController handles movement");
            enabled = false;
            return;
        }
        
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
            // Debug.Log($"[NetworkTransformInterpolation] Spawned on {gameObject.name}. IsLocalPlayer: {_isLocalPlayer}");
            if (_isLocalPlayer)
            {
                // Debug.Log($"[NetworkTransformInterpolation] LOCAL player - Position: Direct, Rotation: Interpolated");
            }
            else
            {
                // Debug.Log($"[NetworkTransformInterpolation] REMOTE player - Position: Interpolated, Rotation: Interpolated");
            }
        }
    }
    
    public override void Render()
    {
        // Safety check
        if (!_initialized) return;
        
        // CRITICAL FIX: Skip ALL interpolation for local player to prevent rubberbanding
        // Local player movement is already smooth via direct input in FixedUpdateNetwork
        if (_isLocalPlayer) return;
        
        // PERFORMANCE: Only interpolate every other frame for remote players (FPS optimization)
        if (Time.frameCount % 2 == 0) return;
        
        // Store the current network position before we modify it
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
        // PERFORMANCE: Use larger thresholds to reduce interpolation frequency
        bool positionChanged = Vector3.Distance(networkPosition, _lastNetworkPosition) > 0.01f; // Increased from 0.001f
        bool rotationChanged = Quaternion.Angle(networkRotation, _lastNetworkRotation) > 1.0f; // Increased from 0.1f
        
        if (positionChanged || rotationChanged)
        {
            _lastNetworkPosition = networkPosition;
            _lastNetworkRotation = networkRotation;
        }
        else
        {
            // PERFORMANCE: Skip interpolation if nothing changed
            return;
        }
        
        // Only interpolate for remote players
        if (interpolatePosition && positionChanged)
        {
            float distance = Vector3.Distance(_renderPosition, networkPosition);
            
            // Snap if too far away (teleport/respawn)
            if (distance > snapDistanceThreshold)
            {
                _renderPosition = networkPosition;
            }
            else
            {
                // PERFORMANCE: Faster interpolation for FPS games
                _renderPosition = Vector3.Lerp(_renderPosition, networkPosition, 
                    positionLerpSpeed * Time.deltaTime * 2f); // Doubled speed
            }
            
            transform.position = _renderPosition;
        }
        
        // Interpolate rotation for remote players (only if changed)
        if (interpolateRotation && rotationChanged)
        {
            float angle = Quaternion.Angle(_renderRotation, networkRotation);
            
            // Snap if too far (teleport/respawn)
            if (angle > snapAngleThreshold)
            {
                _renderRotation = networkRotation;
            }
            else
            {
                // PERFORMANCE: Faster interpolation for FPS games
                _renderRotation = Quaternion.Slerp(_renderRotation, networkRotation, 
                    rotationLerpSpeed * Time.deltaTime * 2f); // Doubled speed
            }
            
            transform.rotation = _renderRotation;
        }
    }
}
