using UnityEngine;

public class LockableObject : MonoBehaviour
{
    public enum ContentType { None, Real, Fake, Bonus, Other }

    [Header("Lock Settings")]
    public bool isLocked = false;
    public ContentType contentType = ContentType.None;

    [Tooltip("This gets assigned at runtime when a prop is inserted into the container.")]
    public GameObject containedObject;

    [Header("Optional Animation")]
    public Animator animator;
    public string openTrigger = "Open"; // Animator trigger to play when unlocked

    private bool isOpen = false;

    /// <summary>
    /// Called (e.g. by TaskManager) when the unlocking condition is satisfied.
    /// Unlocks this object, plays animation, and reveals contained object.
    /// </summary>
    public void Unlock()
    {
        if (!isLocked) return;

        isLocked = false;
        isOpen = true;

        if (animator != null && !string.IsNullOrEmpty(openTrigger))
        {
            animator.SetTrigger(openTrigger);
        }

        if (containedObject != null)
        {
            containedObject.SetActive(true);
        }

        Debug.Log($"[{name}] Unlocked!");
    }

    /// <summary>
    /// Used by interaction systems to check if this lockable object can be opened.
    /// </summary>
    /// <returns>True if locked and not already open.</returns>
    public bool CanInteract()
    {
        return isLocked && !isOpen;
    }
}
