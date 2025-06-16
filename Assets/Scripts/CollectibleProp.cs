using UnityEngine;

public class CollectibleProp : MonoBehaviour
{
    private bool isCollected = false;
    private AudioClip pickupSound;
    private float pickupVolume = 1f;

    public void SetPickupSound(AudioClip clip, float volume)
    {
        pickupSound = clip;
        pickupVolume = volume;
    }

    public void Interact()
    {
        if (isCollected) return;
        isCollected = true;

        Debug.Log("✅ Collected: " + gameObject.name);

        // ✅ Skip pickup sound if this is the last prop
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
