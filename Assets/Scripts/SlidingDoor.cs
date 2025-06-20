using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(2f, 0, 0); // How far the door slides
    public float slideSpeed = 2f;

    private Vector3 closedPosition;
    private Vector3 targetPosition;
    private bool isOpen = false;

    void Start()
    {
        closedPosition = transform.localPosition;
        targetPosition = closedPosition;
    }

    void Update()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * slideSpeed);
    }

    public void Interact()
    {
        isOpen = !isOpen;
        targetPosition = isOpen ? closedPosition + openOffset : closedPosition;
    }
}
