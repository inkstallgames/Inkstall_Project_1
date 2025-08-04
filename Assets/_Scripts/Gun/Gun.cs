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

    
    public void Fire()
    {
        if(bulletsCount > 0)
            {
                gunAnimator.SetBool("isFiring", true);
                audioSource.PlayOneShot(fireSound);
                bulletsCount--;
            }
            else
            {
                audioSource.PlayOneShot(noBulletsSound);
            }
    }
}
