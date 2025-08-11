using System.Collections;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public AudioClip noBulletsSound;
    public Camera mainCamera;
    
    private bool canFire = true;
     
    public void Fire()
    {
        if (BulletsManager.Instance == null)
        {
            Debug.LogError("BulletsManager instance not found!");
            return;
        }

        if (BulletsManager.Instance.currentBullets > 0 && canFire)
        {
            StartCoroutine(FireRoutine());
        }
        else if (audioSource != null)
        {
            audioSource.PlayOneShot(noBulletsSound);
        }
    }

    private IEnumerator FireRoutine()
    {
        canFire = false;

        // Trigger the animation
        gunAnimator.SetTrigger("isFiring");

        // Play fire sound
        audioSource.PlayOneShot(fireSound);

        // Decrease bullet using BulletsManager
        BulletsManager.Instance.DecreaseBullet();

        // Wait for animation duration (adjust if needed)
        float animationLength = gunAnimator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animationLength);

        gunAnimator.SetBool("isFiring", false);
        canFire = true;
    }
}