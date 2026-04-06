using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles pistol shooting input from UI buttons or keyboard.
/// Attach to the player prefab alongside NetworkPistolBehaviour.
/// </summary>
public class PistolInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;
    
    [Header("UI Button References (Optional)")]
    [SerializeField] private Button shootButton;
    [SerializeField] private Button reloadButton;

    private NetworkPistolBehaviour pistolBehaviour;
    private bool isLocalPlayer = false;

    private void Start()
    {
        pistolBehaviour = GetComponent<NetworkPistolBehaviour>();
        
        if (pistolBehaviour != null && pistolBehaviour.Object != null)
        {
            isLocalPlayer = pistolBehaviour.Object.HasInputAuthority;
        }

        if (isLocalPlayer)
        {
            SetupUIButtons();
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
        if (!isLocalPlayer || pistolBehaviour == null)
        {
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // For PC/Editor, allow shooting with a mouse click, but not if clicking on a UI element.
        // Using GetMouseButtonDown for a single shot per click.
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            pistolBehaviour.RequestShoot();
        }
#endif

        // Reload input works on all platforms
        if (Input.GetKeyDown(reloadKey))
        {
            pistolBehaviour.RequestReload();
        }
    }

    public void OnShootButtonPressed()
    {
        if (pistolBehaviour != null)
        {
            pistolBehaviour.RequestShoot();
        }
    }

    public void OnReloadButtonPressed()
    {
        if (pistolBehaviour != null)
        {
            pistolBehaviour.RequestReload();
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
}
