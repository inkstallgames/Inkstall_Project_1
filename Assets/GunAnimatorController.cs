using UnityEngine;

public class GunAnimatorControl : MonoBehaviour
{
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // Replace this with your actual fire condition
        if (Input.GetButtonDown("Fire1")) // Default left mouse button
        {
            anim.SetTrigger("Fire");
        }
    }
}
