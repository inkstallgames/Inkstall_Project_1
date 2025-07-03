using UnityEngine;

public class PropIdentity : MonoBehaviour
{
    public bool isFake = false;

    // Optional visual for debugging
    public void UpdateVisual()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = isFake ? Color.red : Color.green;
        }
    }
}
