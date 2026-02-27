using UnityEngine;

public class GunLagFix : MonoBehaviour
{
    private Transform gunTransform;
    private Transform cameraTransform;

    void Start()
    {
        // Automatically assign the gun's transform
        gunTransform = transform;

        // Find the main camera at runtime
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
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
            // Sync the gun's position and rotation with the camera
            gunTransform.position = cameraTransform.position;
            gunTransform.rotation = cameraTransform.rotation;
        }
    }
}
