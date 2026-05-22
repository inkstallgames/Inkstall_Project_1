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
    private Camera _mainCam;

    private Camera _handsCamera; // Found at runtime by tag "HandCamera" (scene object, not prefab)

    private bool _wasLocalPlayer;



    public override void Spawned()

    {

        base.Spawned();

        _wasLocalPlayer = Object.HasInputAuthority;

        SetupVisuals(_wasLocalPlayer);

    }



    public void SetupVisuals(bool isLocalPlayer)

    {

        // Handle First Person Arms (Local Only) - ACTIVATE IMMEDIATELY

        foreach (var arms in firstPersonArms)
        {
            if (arms != null)
            {
                arms.SetActive(isLocalPlayer);
                
                // Force hands visible immediately if local player, 
                // or force them completely hidden if remote player.
                if (isLocalPlayer)
                {
                    EnsureArmsVisible(arms);
                }
                else
                {
                    EnsureArmsHidden(arms);
                }
            }
        }
        
        // FOOLPROOF FALLBACK FOR REMOTE PLAYERS:
        // If the user forgot to assign the FPS arms to the 'firstPersonArms' array in the inspector
        // (e.g., on the Hero prefab), those arms will still render because they are likely on the "FPS_Hands" layer,
        // and the local player's HandCamera will render them floating in the air.
        // We do a sweep of the entire player hierarchy to forcefully disable any renderer on the FPS_Hands layer.
        if (!isLocalPlayer)
        {
            int fpsLayer = LayerMask.NameToLayer("FPS_Hands");
            if (fpsLayer != -1)
            {
                Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in allRenderers)
                {
                    if (renderer.gameObject.layer == fpsLayer)
                    {
                        renderer.enabled = false;
                        
                        // Also disable any animators on that specific game object
                        var animator = renderer.GetComponent<Animator>();
                        if (animator != null) animator.enabled = false;
                    }
                }
                Debug.Log("[PlayerVisualManager] Performed foolproof FPS_Hands layer sweep to hide floating arms for remote player.");
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

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Cleanup: Because we parented the FPS arms to the Main Camera (which lives outside the prefab),
        // we MUST manually destroy them when the player dies/despawns, otherwise you'll get 
        // infinitely stacking floating ghost arms with each respawn!
        if (firstPersonArms != null)
        {
            foreach (var arms in firstPersonArms)
            {
                if (arms != null)
                {
                    Destroy(arms);
                }
            }
        }
    }
    
    private void EnsureArmsHidden(GameObject arms)
    {
        // Force GameObject inactive
        if (arms.activeSelf)
        {
            arms.SetActive(false);
        }
        
        // CRITICAL: Disable all Renderers so even if another script (like NetworkWeaponEquipSystem) 
        // turns the GameObject back on, the meshes will stay invisible for remote players.
        var renderers = arms.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }
        
        // Also disable animators to save performance
        var animators = arms.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            animator.enabled = false;
        }
        
        Debug.Log($"[PlayerVisualManager] Forced arms '{arms.name}' to be completely hidden for remote player.");
    }
    
    private void EnsureArmsVisible(GameObject arms)
    {
        // Force GameObject active
        if (!arms.activeSelf)
        {
            arms.SetActive(true);
        }
        
        // Enable all renderers
        var renderers = arms.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled)
            {
                renderer.enabled = true;
            }
        }
        
        // Enable all SkinnedMeshRenderers
        var skinnedRenderers = arms.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var skinnedRenderer in skinnedRenderers)
        {
            if (!skinnedRenderer.enabled)
            {
                skinnedRenderer.enabled = true;
            }
        }
        
        // Enable all animators - EXCEPT those controlled by PistolRecoilAnimation
        var animators = arms.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            // Check if this animator is controlled by PistolRecoilAnimation
            var recoilAnimation = animator.GetComponent<PistolRecoilAnimation>();
            if (recoilAnimation != null)
            {
                // Don't enable this animator - let PistolRecoilAnimation control it
            }
            else if (!animator.enabled)
            {
                animator.enabled = true;
            }
        }
        
    }

    private System.Collections.IEnumerator ParentArmsToCameraRoutine()
    {
        // Wait until MainCamera is found
        while (_mainCam == null)
        {
            _mainCam = Camera.main;
            if (_mainCam == null)
            {
                var camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null) _mainCam = camObj.GetComponent<Camera>();
            }
            yield return null;
        }

        // Optimized camera operations - batch transform changes
        foreach (var arms in firstPersonArms)
        {
            if (arms != null)
            {
                arms.transform.SetParent(_mainCam.transform, false);
                arms.transform.localPosition = armPositionOffset;
                arms.transform.localRotation = Quaternion.Euler(armRotationOffset);
            }
        }
        _armsParented = true;

        // Cache HandsCamera lookup to avoid repeated FindWithTag calls
        if (_handsCamera == null)
        {
            var handsCamObj = GameObject.FindWithTag("HandCamera");
            if (handsCamObj != null)
            {
                _handsCamera = handsCamObj.GetComponent<Camera>();
            }
        }
    }

    private void LateUpdate()
    {
        if (!Object.HasInputAuthority) return;

        // CRITICAL FIX: If authority arrives late (after Spawned), trigger a visual reset
        // so the local player does not get stuck rendering as a remote player (Full Body visible).
        if (!_wasLocalPlayer)
        {
            _wasLocalPlayer = true;
            SetupVisuals(true);
        }

        // Allow real-time tweaking of arm offsets in the editor
        if (_armsParented && Application.isEditor)
        {
            foreach (var arms in firstPersonArms)
            {
                if (arms != null)
                {
                    // Check if PistolRecoilAnimation is present and don't override transform
                    var recoilAnimation = arms.GetComponentInChildren<PistolRecoilAnimation>();
                    if (recoilAnimation != null)
                    {
                        // Don't override transform during reload animation or recoil animation
                        if (!recoilAnimation.IsReloading() && recoilAnimation.IsReadyToFire())
                        {
                            // Only set position when no animations are playing
                            arms.transform.localPosition = armPositionOffset;
                            // Don't set localRotation - let PistolRecoilAnimation control it
                        }
                        // If any animation is playing, don't touch the transform at all
                    }
                    else
                    {
                        // Normal behavior when no recoil animation
                        arms.transform.localPosition = armPositionOffset;
                        arms.transform.localRotation = Quaternion.Euler(armRotationOffset);
                    }
                }
            }
        }

        // Sync HandsCamera FOV to MainCamera every frame.
        // Cinemachine (CinemachineBrain) can change MainCamera's FOV at runtime when
        // virtual cameras activate/blend. If HandsCamera FOV differs from MainCamera FOV,
        // the FPS arms and the laser beam are projected differently, causing a visual offset.
        if (_handsCamera != null && _mainCam != null)
        {
            if (!Mathf.Approximately(_handsCamera.fieldOfView, _mainCam.fieldOfView))
            {
                _handsCamera.fieldOfView = _mainCam.fieldOfView;
            }
        }
    }
    
    private System.Collections.IEnumerator DelayedIdleAnimation()
    {
        // DISABLED - Performance killer: Prevent additional animator errors
        // The delayed animation is causing more Animator.GotoState errors
        yield break;
        
        // Original code commented out:
        /*
        yield return new WaitForSeconds(0.1f);
        ForceIdleAnimation();
        */
    }
    
    private void ForceIdleAnimation()
    {
        // For local player, focus specifically on FPS hands animators to prevent T-pose
        if (Object.HasInputAuthority)
        {
            // Target FPS hands specifically
            foreach (var arms in firstPersonArms)
            {
                if (arms != null && arms.activeInHierarchy)
                {
                    var armsAnimators = arms.GetComponentsInChildren<Animator>(true);
                    foreach (var animator in armsAnimators)
                    {
                        if (animator != null && animator.enabled)
                        {
                            ForceAnimatorToIdle(animator);
                        }
                    }
                }
            }
        }
        else
        {
            // For remote players, handle full body animators
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator != null && animator.enabled)
                {
                    ForceAnimatorToIdle(animator);
                }
            }
        }
    }
    
    private void ForceAnimatorToIdle(Animator animator)
    {
        // DISABLED - Performance killer: This method is causing 1030ms frame spikes
        // due to Animator.GotoState errors. Let the animator handle its own states.
        return;
        
        // Original code commented out to prevent massive performance issues:
        /*
        if (animator == null) return;
        
        // Check if animator has any parameters and states
        if (animator.parameters.Length == 0) return;
        
        // Try to set the animator to the default state (usually idle)
        // Only play if the state exists to avoid errors
        if (HasAnimatorState(animator, "Idle"))
        {
            animator.Play("Idle", 0, 0f);
        }
        else if (HasAnimatorState(animator, "Pistol Idle"))
        {
            animator.Play("Pistol Idle", 0, 0f);
        }
        else if (HasAnimatorState(animator, "Base Layer.Idle"))
        {
            animator.Play("Base Layer.Idle", 0, 0f);
        }
        else
        {
            // Fallback: try to play the first available state
            var controller = animator.runtimeAnimatorController;
            if (controller != null && controller.animationClips.Length > 0)
            {
                animator.Play(controller.animationClips[0].name, 0, 0f);
            }
        }
        */
    }
    
    private bool HasAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        
        int stateHash = Animator.StringToHash(stateName);
        
        // Check if the state exists in any layer
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, stateHash))
            {
                return true;
            }
        }
        return false;
    }
}

