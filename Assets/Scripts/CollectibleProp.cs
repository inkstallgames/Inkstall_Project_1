using UnityEngine;

public class CollectibleProp : MonoBehaviour
{
    private bool isCollected = false;

    public void Interact()
    {
        if (isCollected) return; // Prevent double collection
        isCollected = true;

        Debug.Log("✅ Collected: " + gameObject.name);

        // Notify GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CollectProp();
        }
        else
        {
            Debug.LogWarning("⚠️ GameManager instance not found when collecting prop.");
        }

        // Optionally add a delay or animation before destroying
        Destroy(gameObject); // Remove from scene
    }

    // Optional: You could also add OnTriggerEnter or OnMouseDown here
    // for direct interaction if not using a separate interaction script.
}
