using UnityEngine;
using Fusion;

/// <summary>
/// Provides smooth visual interpolation for networked rigidbody objects (like bombs).
/// This ensures physics objects appear smooth on clients even though physics
/// simulation only runs on the server.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class NetworkRigidbodyInterpolation : NetworkBehaviour
{
    [Header("Interpolation Settings")]
    [SerializeField] private float positionLerpSpeed = 20f;
    [SerializeField] private float rotationLerpSpeed = 20f;
    
    [Header("Snap Thresholds")]
    [Tooltip("If position difference exceeds this, snap instead of lerp")]
    [SerializeField] private float snapDistanceThreshold = 10f;
    
    private Vector3 _renderPosition;
    private Quaternion _renderRotation;
    private Rigidbody _rigidbody;
    private bool _initialized = false;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }
    
    public override void Spawned()
    {
        _renderPosition = transform.position;
        _renderRotation = transform.rotation;
        _initialized = true;
        
        // On clients, make rigidbody kinematic since server handles physics
        if (!Object.HasStateAuthority && _rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
    }
    
    public override void Render()
    {
        if (!_initialized) return;
        
        // Only interpolate on clients (server runs actual physics)
        if (Object.HasStateAuthority) return;
        
        // Get the networked position from the transform
        Vector3 networkPosition = transform.position;
        Quaternion networkRotation = transform.rotation;
        
        float distance = Vector3.Distance(_renderPosition, networkPosition);
        
        // Snap if too far (initial spawn or teleport)
        if (distance > snapDistanceThreshold)
        {
            _renderPosition = networkPosition;
            _renderRotation = networkRotation;
        }
        else
        {
            // Smooth interpolation for visual rendering
            _renderPosition = Vector3.Lerp(_renderPosition, networkPosition, 
                positionLerpSpeed * Time.deltaTime);
            _renderRotation = Quaternion.Slerp(_renderRotation, networkRotation, 
                rotationLerpSpeed * Time.deltaTime);
        }
        
        // Apply interpolated values
        transform.position = _renderPosition;
        transform.rotation = _renderRotation;
    }
}
