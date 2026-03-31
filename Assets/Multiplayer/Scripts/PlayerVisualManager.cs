using Fusion;

using UnityEngine;

using UnityEngine.Rendering;



public class PlayerVisualManager : NetworkBehaviour

{

    [Header("Local Player Elements (FPS Arms)")]

    [Tooltip("These GameObjects will ONLY be active for the local player. (e.g. First Person Arms)")]

    [SerializeField] private GameObject[] firstPersonArms;



    [Header("Remote Player Elements (Full Body)")]

    [Tooltip("These GameObjects will ONLY be fully rendered for remote players. (e.g. Third Person Body)")]

    [SerializeField] private GameObject[] thirdPersonBody;



    [Tooltip("If true, the full body will cast shadows for the local player but won't be visible to them. This is the standard for First Person games like Valorant.")]
    [SerializeField] private bool fullBodyCastsShadowsForLocal = true;

    [Header("Arm Positioning")]
    [Tooltip("Offset position for the first person arms relative to the camera")]
    [SerializeField] private Vector3 armPositionOffset = new Vector3(0, -1.5f, 0.5f);
    
    [Tooltip("Offset rotation for the first person arms relative to the camera")]
    [SerializeField] private Vector3 armRotationOffset = Vector3.zero;

    private bool _armsParented;

    public override void Spawned()
    {
        bool isLocalPlayer = Object.HasInputAuthority;



        // Handle First Person Arms (Local Only)
        foreach (var arms in firstPersonArms)
        {
            if (arms != null)
            {
                arms.SetActive(isLocalPlayer);
            }
        }

        if (isLocalPlayer)
        {
            StartCoroutine(ParentArmsToCameraRoutine());
        }



        // Handle Third Person Body

        foreach (var body in thirdPersonBody)

        {

            if (body != null)

            {

                if (isLocalPlayer)

                {

                    if (fullBodyCastsShadowsForLocal)

                    {

                        // Ensure it's active so it can cast shadows

                        body.SetActive(true);

                        

                        // Change shadow casting mode to ShadowsOnly for the local view

                        Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);

                        foreach (var ren in renderers)

                        {

                            ren.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

                        }

                    }

                    else

                    {

                        // Just disable the third person body completely for the local player

                        body.SetActive(false);

                    }

                }

                else

                {

                    // For remote players (proxies), ensure the body is active and visible

                    body.SetActive(true);

                    

                    Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);

                    foreach (var ren in renderers)

                    {

                        ren.shadowCastingMode = ShadowCastingMode.On;

                    }

                }

            }

        }

    }

    private System.Collections.IEnumerator ParentArmsToCameraRoutine()
    {
        // Wait until MainCamera is found
        Camera mainCam = null;
        while (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }
            yield return null;
        }

        foreach (var arms in firstPersonArms)
        {
            if (arms != null)
            {
                arms.transform.SetParent(mainCam.transform, false);
                arms.transform.localPosition = armPositionOffset;
                arms.transform.localRotation = Quaternion.Euler(armRotationOffset);
                Debug.Log($"[PlayerVisualManager] Parented '{arms.name}' to MainCamera successfully.");
            }
        }
        _armsParented = true;
    }

    private void LateUpdate()
    {
        // Allow real-time tweaking of arm offsets in the editor
        if (Object.HasInputAuthority && _armsParented && Application.isEditor)
        {
            foreach (var arms in firstPersonArms)
            {
                if (arms != null)
                {
                    arms.transform.localPosition = armPositionOffset;
                    arms.transform.localRotation = Quaternion.Euler(armRotationOffset);
                }
            }
        }
    }
}

