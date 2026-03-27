using Fusion;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

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

    [Header("FPS Arm Positioning (Local Space relative to Camera)")]
    [Tooltip("Local position offset of arms relative to the main camera")]
    [SerializeField] private Vector3 armLocalPosition = new Vector3(0.17f, -1.75f, -0.12f);
    
    [Tooltip("Local rotation offset of arms relative to the main camera")]
    [SerializeField] private Vector3 armLocalRotation = Vector3.zero;

    // Track parented arm transforms so we can update them every frame
    private List<Transform> parentedArms = new List<Transform>();
    private bool isLocalPlayer;

    public override void Spawned()
    {
        isLocalPlayer = Object.HasInputAuthority;

        // Handle First Person Arms (Local Only)
        foreach (var arms in firstPersonArms)
        {
            if (arms != null)
            {
                arms.SetActive(isLocalPlayer);
                
                // Parent arms to the main camera so they stay fixed in the camera view.
                // This prevents the "orbiting" issue when looking up/down that happens
                // when parented to CameraTarget (because the arm pivot is offset below it).
                if (isLocalPlayer)
                {
                    StartCoroutine(ParentArmsToCamera(arms));
                }
            }
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

    /// <summary>
    /// Continuously update arm position/rotation so inspector changes apply in real-time.
    /// This lets you tweak armLocalPosition and armLocalRotation during Play mode.
    /// </summary>
    private void LateUpdate()
    {
        if (!isLocalPlayer || parentedArms.Count == 0) return;

        foreach (var armTransform in parentedArms)
        {
            if (armTransform != null)
            {
                armTransform.localPosition = armLocalPosition;
                armTransform.localRotation = Quaternion.Euler(armLocalRotation);
            }
        }
    }

    /// <summary>
    /// Waits for Camera.main to be available, then parents the arms to it.
    /// Parenting to the camera (not CameraTarget) ensures arms stay at a fixed
    /// screen position regardless of look direction.
    /// </summary>
    private IEnumerator ParentArmsToCamera(GameObject arms)
    {
        // Wait until Camera.main is available (Cinemachine may need a frame to set up)
        Camera mainCam = Camera.main;
        while (mainCam == null)
        {
            yield return null;
            mainCam = Camera.main;
        }

        // Parent to the main camera
        arms.transform.SetParent(mainCam.transform, false);
        
        // Set initial local position and rotation
        arms.transform.localPosition = armLocalPosition;
        arms.transform.localRotation = Quaternion.Euler(armLocalRotation);
        
        // Track this arm so LateUpdate can keep updating it
        parentedArms.Add(arms.transform);
        
        Debug.Log($"[PlayerVisualManager] Parented {arms.name} to Main Camera - arms will stay fixed in view");
    }
}

