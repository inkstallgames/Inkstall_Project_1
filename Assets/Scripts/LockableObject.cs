using UnityEngine;

public class LockableObject : MonoBehaviour
{
    public enum ContentType { None, Real, Fake, Bonus, Other }

    [Header("Lock Settings")]
    public bool isLocked = false;
    public ContentType contentType = ContentType.None;
    public GameObject containedObject; // Assigned at runtime
    public Animator animator;          // Optional animator for visual open
    public string openTrigger = "Open"; // Animator trigger name

    private bool isOpen = false;

    // Called by TaskManager when task is completed
    public void Unlock()
    {
        if (!isLocked) return;

        isLocked = false;
        isOpen = true;

        if (animator != null)
        {
            animator.SetTrigger(openTrigger);
        }

        if (containedObject != null)
        {
            containedObject.SetActive(true); // Reveal what's inside
        }

        Debug.Log($"{gameObject.name} unlocked!");
    }

    // Optional: for player interaction
    public bool CanInteract()
    {
        return isLocked && !isOpen;
    }
}
