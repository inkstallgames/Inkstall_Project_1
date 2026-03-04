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
    
    [Header("UI Button References (Optional)")]
    [SerializeField] private Button shootButton;
    [SerializeField] private Button reloadButton;

    private NetworkLaserBehaviour laserBehaviour;
    private bool isLocalPlayer = false;

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
        if (shootButton != null)
        {
            shootButton.onClick.AddListener(OnShootButtonPressed);
        }

        if (reloadButton != null)
        {
            reloadButton.onClick.AddListener(OnReloadButtonPressed);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || laserBehaviour == null)
        {
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // For PC/Editor, allow shooting with a mouse click, but not if clicking on a UI element.
        // Using GetMouseButtonDown for a single shot per click.
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[LaserInputHandler] Mouse click detected - requesting laser shoot");
            laserBehaviour.RequestShoot();
        }
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
        if (shootButton != null)
        {
            shootButton.onClick.RemoveListener(OnShootButtonPressed);
        }

        if (reloadButton != null)
        {
            reloadButton.onClick.RemoveListener(OnReloadButtonPressed);
        }
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
