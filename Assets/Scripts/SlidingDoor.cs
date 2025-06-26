using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(2f, 0, 0); // How far the door slides
    public float slideSpeed = 2f;

    private Vector3 closedPosition;
    private Vector3 targetPosition;
    private bool isOpen = false;
    private Vector3 currentVelocity; // Added to use SmoothDamp instead of Lerp

    void Start()
    {
        closedPosition = transform.localPosition;
        targetPosition = closedPosition;
    }

    void Update()
    {
        // Using SmoothDamp instead of Lerp to avoid creating new Vector3 objects
        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition, 
            targetPosition, 
            ref currentVelocity, 
            1f / slideSpeed
        );
    }

    public void Interact()
    {
        isOpen = !isOpen;
        targetPosition = isOpen ? closedPosition + openOffset : closedPosition;
    }
}
