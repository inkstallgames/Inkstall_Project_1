using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 openRotation = new Vector3(0, 90, 0);
    public float rotationSpeed = 3f;

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private bool isOpen = false;
    
    // Cache the open rotation quaternion to avoid creating it every time
    private Quaternion openRotationQuaternion;

    void Start()
    {
        closedRotation = transform.rotation;
        targetRotation = closedRotation;
        
        // Pre-calculate the open rotation quaternion
        openRotationQuaternion = Quaternion.Euler(transform.eulerAngles + openRotation);
    }

    void Update()
    {
        // Smooth rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    // Called by the player when interacting
    public void Interact()
    {
        isOpen = !isOpen;
        targetRotation = isOpen
            ? openRotationQuaternion
            : closedRotation;
    }
}
