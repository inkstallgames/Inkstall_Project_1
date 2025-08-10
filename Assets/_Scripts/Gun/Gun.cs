using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public AudioClip noBulletsSound;
    public Camera mainCamera;

    
    public int coinCount = 100;
    public int bulletsCount = 0;
    private int bulletsToBuy = 1;
    public int maxBulletsToBuy = 3;
    public int costPerBullet = 10;

    [Header("UI Elements")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI bulletAmountText;
    public TextMeshProUGUI totalCostText;

    private bool canFire = true;
     
    public void Fire()
{
        RaycastHit hit;
        Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit);



        if (bulletsCount > 0 && audioSource != null)
        {
            if (canFire)
            {
                StartCoroutine(FireRoutine());
            }
        }
        else
        {
            if (audioSource != null)
                audioSource.PlayOneShot(noBulletsSound);
        }
}

private IEnumerator FireRoutine()
{
    canFire = false;

    // 🔥 Trigger the animation (not a bool!)
    gunAnimator.SetBool("isFiring", true);

    // 🔊 Play fire sound
    audioSource.PlayOneShot(fireSound);

    // 🔫 Decrease bullet
    bulletsCount--;

    // 🕒 Wait for animation duration (adjust if needed)
    yield return new WaitForSeconds(0.1f);

    gunAnimator.SetBool("isFiring", false);
    canFire = true;
}

}