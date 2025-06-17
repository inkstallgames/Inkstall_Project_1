using UnityEngine;

[RequireComponent(typeof(PropIdentity))]
public class CollectibleProp : MonoBehaviour
{
    private bool isCollected = false;
    private AudioClip pickupSound;
    private float pickupVolume = 1f;
    private PropIdentity identity;

    void Awake()
    {
        identity = GetComponent<PropIdentity>();
        if (identity == null)
        {
            Debug.LogError("❌ CollectibleProp requires PropIdentity component.");
            enabled = false;
        }
    }

    public void SetPickupSound(AudioClip clip, float volume)
    {
        pickupSound = clip;
        pickupVolume = volume;
    }

    public void Interact()
    {
        // Only fake props can be collected
        if (!identity.isFake)
        {
            Debug.LogWarning($"❌ Tried to collect real prop: {gameObject.name}");
            return;
        }

        if (isCollected) return;
        isCollected = true;

        Debug.Log($"✅ Collected fake prop: {gameObject.name}");

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.PropsLeft() > 1 && pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);
            }

            GameManager.Instance.CollectProp();
        }
        else
        {
            Debug.LogWarning("⚠️ GameManager instance not found when collecting prop.");
        }

        Destroy(gameObject);
    }
}
