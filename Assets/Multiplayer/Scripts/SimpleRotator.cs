using UnityEngine;

/// <summary>
/// A simple script to constantly rotate a GameObject along its X-axis.
/// Attach this to any cube or object you want to spin.
/// </summary>
public class SimpleRotator : MonoBehaviour
{
    [Tooltip("The speed at which the object spins on the X-axis (degrees per second).")]
    public float spinSpeed = 90f;

    void Update()
    {
        // Rotate the object around its local X-axis at a constant speed, regardless of framerate
        transform.Rotate(0f,spinSpeed * Time.deltaTime ,0f, Space.Self);
    }
}
