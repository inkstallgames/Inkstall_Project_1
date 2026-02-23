using UnityEngine;
using Fusion;

/// <summary>
/// Provides smooth visual interpolation for networked objects on clients.
/// Fusion's NetworkTransform handles the network sync, but this adds
/// additional smoothing for 60 FPS rendering between network ticks.
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
    
    private Vector3 _renderPosition;
    private Quaternion _renderRotation;
    private bool _initialized = false;
    
    public override void Spawned()
    {
        // Initialize render position/rotation to current transform values
        _renderPosition = transform.position;
        _renderRotation = transform.rotation;
        _initialized = true;
    }
    
    public override void Render()
    {
        if (!_initialized) return;
        
        // Only interpolate for objects we don't have input authority over
        // (i.e., other players' characters, not our own)
        if (Object.HasInputAuthority) return;
        
        // Get the networked position (updated in FixedUpdateNetwork)
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
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
