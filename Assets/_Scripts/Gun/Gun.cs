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
        if(bulletsCount > 0 && canFire)
            {
                gunAnimator.SetBool("isFiring", true);
                StartCoroutine(ResetFireBool());
                audioSource.PlayOneShot(fireSound);
                bulletsCount--;
                
            }
            else if(bulletsCount <= 0 || !canFire)
            {
                audioSource.PlayOneShot(noBulletsSound);
            }
    }


    private IEnumerator ResetFireBool()
    {
        canFire = false;
        yield return new WaitForSeconds(0.1f); // short delay
        gunAnimator.SetBool("isFiring", false);
        yield return new WaitForSeconds(0.3f); // cooldown (based on your animation length)
        canFire = true;
    }
}
