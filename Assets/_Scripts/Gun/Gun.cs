using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;
    public AudioSource audioSource;
    public AudioClip fireSound;
    public bool isFiring;

    public void Fire()
    {
        if(isFiring)
        {
            gunAnimator.SetBool("isFiring", true);
            audioSource.PlayOneShot(fireSound);
        }
        else
        {
            gunAnimator.SetBool("isFiring", false);
        }
    }
}
