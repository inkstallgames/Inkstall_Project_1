using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public AudioClip noBulletsSound;


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
