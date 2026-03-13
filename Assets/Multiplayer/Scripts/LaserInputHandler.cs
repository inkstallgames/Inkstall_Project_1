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
    private bool isMousePressed = false; // Track mouse state

    private void Start()
    {
        laserBehaviour = GetComponent<NetworkLaserBehaviour>();
        
        // Check if this is the local player
        if (laserBehaviour != null && laserBehaviour.Object != null)
        {
            isLocalPlayer = laserBehaviour.Object.HasInputAuthority;
        }

        // Only enable for local players (prefab separation handles team restrictions)
        if (isLocalPlayer)
        {
            SetupUIButtons();
            Debug.Log("[LaserInputHandler] Laser controls enabled for local player");
        }
        else
        {
            Debug.Log("[LaserInputHandler] Laser controls disabled - not local player");
            this.enabled = false;
        }
    }

    private void SetupUIButtons()
    {
        // Use the same throw button as pistol shooting through NetworkUIManager
        // The NetworkUIManager.OnThrowButtonPressed() now handles laser shooting
        // So we don't need to set up separate button listeners here
        
        Debug.Log("[LaserInputHandler] Using shared throw button through NetworkUIManager for laser shooting");
    }

    private void Update()
    {
        if (!isLocalPlayer || laserBehaviour == null)
        {
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // Track mouse button state for continuous firing
        bool currentMouseState = Input.GetMouseButton(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        
        // Mouse pressed - start shooting
        if (currentMouseState && !isMousePressed)
        {
            Debug.Log("[LaserInputHandler] Mouse pressed - starting continuous laser fire");
            laserBehaviour.RequestShoot();
        }
        // Mouse held - continue shooting every frame
        else if (currentMouseState && isMousePressed)
        {
            laserBehaviour.RequestShoot();
        }
        // Mouse released - stop shooting
        else if (!currentMouseState && isMousePressed)
        {
            Debug.Log("[LaserInputHandler] Mouse released - stopping laser fire");
            laserBehaviour.StopShooting(); // Explicitly stop shooting
        }
        
        isMousePressed = currentMouseState;
#endif

        // Reload input works on all platforms (for laser, this could be for emergency cooldown reset)
        if (Input.GetKeyDown(reloadKey))
        {
            // For laser weapons, reload could serve as an emergency cooldown reset
            Debug.Log("[LaserInputHandler] Emergency cooldown reset requested");
        }
    }

    public void OnShootButtonPressed()
    {
        Debug.Log("[LaserInputHandler] OnShootButtonPressed called");
        if (laserBehaviour != null)
        {
            Debug.Log("[LaserInputHandler] Calling laserBehaviour.RequestShoot()");
            laserBehaviour.RequestShoot();
        }
        else
        {
            Debug.LogError("[LaserInputHandler] laserBehaviour is null!");
        }
    }

    public void OnReloadButtonPressed()
    {
        if (laserBehaviour != null)
        {
            // For laser weapons, this could trigger emergency cooldown reset
            Debug.Log("[LaserInputHandler] Emergency cooldown reset via UI");
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
        Debug.Log("[LaserInputHandler] Team changed - prefab separation handles restrictions");
    }
}
