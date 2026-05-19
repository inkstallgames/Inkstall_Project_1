using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles laser shooting input from UI buttons or keyboard for Team B players.
/// This component automatically detects if the player is Team B and enables laser controls.
/// </summary>
public class LaserInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    private NetworkLaserBehaviour laserBehaviour;
    private bool isLocalPlayer = false;
    private bool hasInitialized = false;

    private bool IsLocalPlayer
    {
        get
        {
            if (laserBehaviour != null && laserBehaviour.Object != null)
            {
                return laserBehaviour.Object.HasInputAuthority;
            }
            return false;
        }
    }

    private void Start()
    {
        laserBehaviour = GetComponent<NetworkLaserBehaviour>();
        TryInitialize();
    }

    /// <summary>
    /// Attempts to initialize the local player flag. Retried each frame if Object
    /// is not yet valid or if input authority has not synced yet (common during reconnection).
    /// </summary>
    private void TryInitialize()
    {
        if (hasInitialized) return;
        if (laserBehaviour == null) laserBehaviour = GetComponent<NetworkLaserBehaviour>();
        if (laserBehaviour == null || laserBehaviour.Object == null) return;

        if (IsLocalPlayer)
        {
            isLocalPlayer = true;
            hasInitialized = true;
            SetupUIButtons();
        }
    }

    private void SetupUIButtons()
    {
        // Use the same throw button as pistol shooting through NetworkUIManager
        // The NetworkUIManager.OnThrowButtonPressed() now handles laser shooting
        // So we don't need to set up separate button listeners here
        
        // Debug.Log("[LaserInputHandler] Using shared throw button through NetworkUIManager for laser shooting");
    }

    private void Update()
    {
        // Retry initialization if Object wasn't ready at Start time (reconnection)
        if (!hasInitialized)
        {
            TryInitialize();
            return; // Wait until next frame after initialization
        }

        if (!isLocalPlayer || laserBehaviour == null)
        {
            return;
        }

        // Mouse button input DISABLED for laser gun.
        // Laser gun can only be fired via the throw button (UI or T key)
        // through NetworkUIManager.OnThrowButtonPressed() -> OnShootButtonPressed().

        // Reload input works on all platforms (for laser, this could be for emergency cooldown reset)
        if (Input.GetKeyDown(reloadKey))
        {
            // For laser weapons, reload could serve as an emergency cooldown reset
            // Debug.Log("[LaserInputHandler] Emergency cooldown reset requested");
        }
    }

    public void OnShootButtonPressed()
    {
        // Debug.Log("[LaserInputHandler] OnShootButtonPressed called");
        if (laserBehaviour != null)
        {
            // Debug.Log("[LaserInputHandler] Calling laserBehaviour.RequestShoot()");
            laserBehaviour.RequestShoot();
        }
        else
        {
            // Debug.LogError("[LaserInputHandler] laserBehaviour is null!");
        }
    }

    public void OnReloadButtonPressed()
    {
        if (laserBehaviour != null)
        {
            // For laser weapons, this could trigger emergency cooldown reset
            // Debug.Log("[LaserInputHandler] Emergency cooldown reset via UI");
        }
    }

    private void OnDestroy()
    {
        // No button listeners to clean up since we use NetworkUIManager's shared button
    }

    /// <summary>
    /// Called when team changes to re-evaluate if laser controls should be enabled
    /// </summary>
    public void OnTeamChanged()
    {
        // Prefab separation handles team restrictions - no action needed
        // Debug.Log("[LaserInputHandler] Team changed - prefab separation handles restrictions");
    }
}
