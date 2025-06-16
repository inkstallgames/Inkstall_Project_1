using UnityEngine;

public class PropInteractor : MonoBehaviour
{
    public Camera playerCam;
    public float range = 10f;
    public LayerMask propLayer;
    public int score = 0;

    void Update()
    {
        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) // For mouse click (PC)
        {
            TryInteract();
        }
        #endif

        #if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) // For tap (mobile)
        {
            TryInteract();
        }
        #endif
    }

    void TryInteract()
    {
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, propLayer))
        {
            PropBehavior pb = hit.collider.GetComponent<PropBehavior>();

            if (pb != null)
            {
                if (pb.IsFake())
                {
                    Destroy(hit.collider.gameObject);
                    score++;
                    Debug.Log("✅ Fake prop removed! Score: " + score);
                }
                else
                {
                    Debug.Log("❌ Wrong! This is a real prop.");
                }
            }
        }
    }
}