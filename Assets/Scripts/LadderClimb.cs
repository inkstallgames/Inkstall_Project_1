using UnityEngine;

public class LadderClimb : MonoBehaviour
{
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private string ladderTag = "Ladder";

    private CharacterController controller;
    private bool isClimbing = false;
    private bool isOnLadder = false;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (isClimbing)
        {
            float verticalInput = Input.GetAxis("Vertical");
            Vector3 climbDirection = Vector3.up * verticalInput;

            controller.Move(climbDirection * climbSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(ladderTag))
        {
            isOnLadder = true;
            controller.enabled = false; // disable CC to allow manual climbing
            isClimbing = true;
            controller.enabled = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(ladderTag))
        {
            isClimbing = false;
        }
    }
}
