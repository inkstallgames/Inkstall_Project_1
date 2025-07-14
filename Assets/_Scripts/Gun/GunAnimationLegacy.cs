using UnityEngine;

public class GunAnimationLegacy : MonoBehaviour
{
    private Animation anim;

    void Start()
    {
        anim = GetComponent<Animation>();
        anim.Play("Gun_Idle");
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            // Play fire animation
            anim.Play("GunFire");
        }
        // else if (!anim.isPlaying)
        // {
        //     // Return to idle once fire animation finishes
        //     anim.Play("Gun_Idle");
        // }
    }
}
