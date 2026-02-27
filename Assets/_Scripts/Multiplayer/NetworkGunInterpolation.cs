using Fusion;
using UnityEngine;

/// <summary>
/// Fixes gun model jitter for remote players by smoothly interpolating the visual position.
/// Attach this to the gun model GameObject (child of player).
/// </summary>
public class NetworkGunInterpolation : NetworkBehaviour
{
    [Header("Interpolation Settings")]
    [SerializeField] private float interpolationSpeed = 20f;
    
    private Vector3 _networkPosition;
    private Quaternion _networkRotation;
    private Vector3 _renderPosition;
    private Quaternion _renderRotation;
    
    private bool _initialized = false;

    public override void Spawned()
    {
        // Initialize positions
        _networkPosition = transform.localPosition;
        _networkRotation = transform.localRotation;
        _renderPosition = _networkPosition;
        _renderRotation = _networkRotation;
        _initialized = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (!_initialized) return;
        
        // Store the authoritative network position/rotation
        _networkPosition = transform.localPosition;
        _networkRotation = transform.localRotation;
    }

    public override void Render()
    {
        if (!_initialized) return;
        
        // Only interpolate for remote players (non-input-authority)
        if (!Object.HasInputAuthority)
        {
            // Smoothly interpolate towards the network position
            _renderPosition = Vector3.Lerp(_renderPosition, _networkPosition, interpolationSpeed * Time.deltaTime);
            _renderRotation = Quaternion.Slerp(_renderRotation, _networkRotation, interpolationSpeed * Time.deltaTime);
            
            // Apply the interpolated position
            transform.localPosition = _renderPosition;
            transform.localRotation = _renderRotation;
        }
        else
        {
            // For local player, just sync render position with network position
            _renderPosition = _networkPosition;
            _renderRotation = _networkRotation;
        }
    }
}
