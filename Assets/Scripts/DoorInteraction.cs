using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 openRotation = new Vector3(0, 90, 0);
    public float rotationSpeed = 3f;

    private Quaternion closedRotation;
    private Quaternion targetRotation;
    private bool isOpen = false;

    void Start()
    {
        closedRotation = transform.rotation;
        targetRotation = closedRotation;
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
            ? Quaternion.Euler(transform.eulerAngles + openRotation)
            : closedRotation;
    }
}
