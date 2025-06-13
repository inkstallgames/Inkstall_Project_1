using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;

    private bool isOpen = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (isOpen)
            {
                animator.Play("DoorClose"); // Replace with actual animation name
                audioSource.PlayOneShot(doorCloseSound);
                isOpen = false;
            }
            else
            {
                animator.Play("DoorOpen"); // Replace with actual animation name
                audioSource.PlayOneShot(doorOpenSound);
                isOpen = true;
            }
        }
    }
}
