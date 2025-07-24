using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public Animator gunAnimator;

    public void Fire()
    {
        gunAnimator.SetTrigger("Fire");
    }
    
}
