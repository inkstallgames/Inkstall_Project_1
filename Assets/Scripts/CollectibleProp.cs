using UnityEngine;

[RequireComponent(typeof(PropIdentity))]
public class CollectibleProp : MonoBehaviour
{
    private bool isCollected = false;

    [Header("Sound Settings")]
    [SerializeField] private AudioClip fakePickupSound;
    [SerializeField] private AudioClip realInteractSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

    private PropIdentity identity;

    void Awake()
    {
        identity = GetComponent<PropIdentity>();
        if (identity == null)
        {
            Debug.LogError("CollectibleProp requires PropIdentity component.");
            enabled = false;
        }
    }

    public void Interact()
    {
        if (identity == null) return;

        if (identity.isFake)
        {
            if (isCollected) return;
            isCollected = true;

            Debug.Log($"✅ Collected fake prop: {gameObject.name}");

            if (fakePickupSound != null)
                AudioSource.PlayClipAtPoint(fakePickupSound, transform.position, soundVolume);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectProp();
            }
            else
            {
                Debug.LogWarning("GameManager instance not found when collecting prop.");
            }

            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"🟤 Interacted with real prop: {gameObject.name}");

            if (realInteractSound != null)
                AudioSource.PlayClipAtPoint(realInteractSound, transform.position, soundVolume);
        }
    }
}
