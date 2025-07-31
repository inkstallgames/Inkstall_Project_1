using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private Camera playerMainCamera;
    [SerializeField] private GameObject openCloseButton;

    [SerializeField] private float interactDistance = 3f;

    private DoorInteraction currentDoor;   // Track which door we're looking at

    private void Start()
    {
        if (playerMainCamera == null)
        {
            playerMainCamera = Camera.main;
            if (playerMainCamera == null)
            {
                enabled = false;
                return;
            }
        }

    }

    private void Update()
    {
        CheckRaycastInteraction();
    }

    private void CheckRaycastInteraction()
    {
        Ray ray = new Ray(playerMainCamera.transform.position, playerMainCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            GameObject hitObject = hit.collider.gameObject;

            //Check for door
            if (hitObject.tag == "Door")
            {
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (hitObject.tag == "Door" && distanceToObject <= interactDistance)
                {
                    openCloseButton.SetActive(true);
                }
                else if (distanceToObject > interactDistance || hitObject.tag != "Door")
                {
                    openCloseButton.SetActive(false);
                }
            }
            
            // Check for drawer
            if (hitObject.tag == "Drawer")
            {
                float distanceToObject = Vector3.Distance(transform.position, hitObject.transform.position);
                if (distanceToObject <= interactDistance)
                {
                    openCloseButton.SetActive(true);
                }
                else if (distanceToObject > interactDistance || hitObject.tag != "Drawer")
                {
                    openCloseButton.SetActive(false);
                }
            }     
            
        }
    }
}
