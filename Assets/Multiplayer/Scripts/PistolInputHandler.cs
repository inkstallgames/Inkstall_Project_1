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
        // UI shooting is entirely handled by NetworkUIManager (AKA throw button)

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
        bool isPointerOverUI = false;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                isPointerOverUI = true;
            
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                    isPointerOverUI = true;
            }
        }

        // For PC/Editor, allow shooting with a mouse click, but not if clicking on a UI element.
        if (pistolBehaviour.HasAutoFirePowerup)
        {
            if (Input.GetMouseButton(0) && !isPointerOverUI)
            {
                pistolBehaviour.RequestShoot();
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && !isPointerOverUI)
            {
                pistolBehaviour.RequestShoot();
            }
        }
#endif

        // Reload input works on all platforms
        if (Input.GetKeyDown(reloadKey))
        {
            pistolBehaviour.RequestReload();
        }
    }

    // Shoot method from UI removed, relies on NetworkUIManager relaying to NetworkPistolBehaviour.RequestShoot()

    public void OnReloadButtonPressed()
    {
        if (pistolBehaviour != null)
        {
            pistolBehaviour.RequestReload();
        }
    }

    private void OnDestroy()
    {

        if (reloadButton != null)
        {
            reloadButton.onClick.RemoveListener(OnReloadButtonPressed);
        }
    }
}
