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
    [SerializeField] private bool showJumpDebug = false; // Debug jump interpolation
    
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
        
        // Store the current network position before we modify it
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
        // PERFORMANCE: Use moderate thresholds to reduce unnecessary interpolation
        bool positionChanged = Vector3.Distance(networkPosition, _lastNetworkPosition) > 0.005f; // Moderate threshold
        bool rotationChanged = Quaternion.Angle(networkRotation, _lastNetworkRotation) > 0.5f; // Moderate threshold
        
        if (showJumpDebug)
        {
            float posDiff = Vector3.Distance(networkPosition, _lastNetworkPosition);
            if (posDiff > 0.01f)
            {
                Debug.Log($"[NetworkTransformInterpolation] Frame: {Time.frameCount} | PosDiff: {posDiff:F4} | NetworkY: {networkPosition.y:F2} | RenderY: {_renderPosition.y:F2} | Changed: {positionChanged}");
            }
        }
        
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
                if (showJumpDebug) Debug.Log($"[NetworkTransformInterpolation] SNAP - Distance: {distance:F2} > Threshold: {snapDistanceThreshold}");
                _renderPosition = networkPosition;
            }
            else
            {
                // Smooth interpolation for jumps - no frame skipping
                _renderPosition = Vector3.Lerp(_renderPosition, networkPosition, 
                    positionLerpSpeed * Time.deltaTime);
                
                if (showJumpDebug && distance > 0.05f)
                {
                    Debug.Log($"[NetworkTransformInterpolation] LERP - FromY: {_renderPosition.y:F2} ToY: {networkPosition.y:F2} Delta: {distance:F4} LerpFactor: {positionLerpSpeed * Time.deltaTime:F4}");
                }
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
                // Smooth interpolation for jumps - no frame skipping
                _renderRotation = Quaternion.Slerp(_renderRotation, networkRotation, 
                    rotationLerpSpeed * Time.deltaTime);
            }
            
            transform.rotation = _renderRotation;
        }
    }
}
