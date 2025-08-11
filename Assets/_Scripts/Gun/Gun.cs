using System.Collections;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public AudioClip noBulletsSound;
    public Camera mainCamera;
    private bool canFire;
    
    public void Fire()
    {
        if (BulletsManager.Instance.currentBullets > 0)
        {
            canFire = true;
            if (canFire)
            {
                StartCoroutine(FireRoutine());
            }
        }
        else if (BulletsManager.Instance.currentBullets <= 0)
        {
            audioSource.PlayOneShot(noBulletsSound);
        }
    }

    private IEnumerator FireRoutine()
    {
        gunAnimator.SetBool("isFiring", true);
        audioSource.PlayOneShot(fireSound);
        BulletsManager.Instance.DecreaseBullet();

        // Cast ray from camera center to detect hits
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                // Check if the hit object has the Alien tag
                if (hit.collider.CompareTag("Alien"))
                {
                    Debug.Log("Alien found! Hit at: " + hit.point);
                    
                    // You can add more alien interaction logic here later
                    // For example: hit.collider.GetComponent<Alien>().TakeDamage();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        gunAnimator.SetBool("isFiring", false);
        canFire = true;
    }
}