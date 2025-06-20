using UnityEngine;

public class DrawerMech : MonoBehaviour
{
    public Vector3 OpenPosition;
    public Vector3 ClosePosition;
    public float moveSpeed = 2f;

    private float lerpTimer = 0f;
    private bool drawerOpen = false;

    void Update()
    {
        // Smoothly move the drawer based on state
        float direction = drawerOpen ? 1f : -1f;
        lerpTimer = Mathf.Clamp01(lerpTimer + Time.deltaTime * direction * moveSpeed);
        transform.localPosition = Vector3.Lerp(ClosePosition, OpenPosition, lerpTimer);
    }

    public void Interact()
    {
        drawerOpen = !drawerOpen;
    }
}
