using UnityEngine;

public class GunLagFix : MonoBehaviour
{
    private Transform gunTransform;
    private Transform cameraTransform;
    private Vector3 localOffset;
    private Quaternion localRotationOffset;

    void Start()
    {
        // Automatically assign the gun's transform
        gunTransform = transform;

        // Find the main camera at runtime
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            
            // Store the initial local offset and rotation relative to camera
            localOffset = cameraTransform.InverseTransformPoint(gunTransform.position);
            localRotationOffset = Quaternion.Inverse(cameraTransform.rotation) * gunTransform.rotation;
        }
        else
        {
            Debug.LogError("GunLagFix: Main camera not found. Make sure your camera is tagged as 'MainCamera'.");
        }
    }

    void LateUpdate()
    {
        if (gunTransform != null && cameraTransform != null)
        {
            // Apply the gun's position and rotation relative to camera, maintaining the offset
            gunTransform.position = cameraTransform.TransformPoint(localOffset);
            gunTransform.rotation = cameraTransform.rotation * localRotationOffset;
        }
    }
}
