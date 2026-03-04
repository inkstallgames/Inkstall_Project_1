using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles weapon equipping input from UI buttons or keyboard.
/// Attach to the player prefab alongside NetworkWeaponEquipSystem.
/// </summary>
public class WeaponEquipInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private KeyCode pistolKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode bombKey = KeyCode.Alpha2;
    
    [Header("UI Button References (Optional)")]
    [SerializeField] private Button pistolButton;
    [SerializeField] private Button bombButton;

    private NetworkWeaponEquipSystem equipSystem;
    private bool isLocalPlayer = false;
    private bool hasCheckedLocalPlayer = false;

    private void Start()
    {
        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        Debug.Log($"[WeaponEquipInputHandler] Start called. EquipSystem found: {equipSystem != null}");
    }
    
    private void CheckLocalPlayer()
    {
        if (hasCheckedLocalPlayer) return;
        
        if (equipSystem != null && equipSystem.Object != null && equipSystem.Object.HasInputAuthority)
        {
            isLocalPlayer = true;
            hasCheckedLocalPlayer = true;
            FindUIButtonsAtRuntime();
            SetupUIButtons();
            Debug.Log($"[WeaponEquipInputHandler] Local player detected! Input handling enabled.");
        }
    }

    private void FindUIButtonsAtRuntime()
    {
        // Find buttons by tag if not assigned in Inspector
        if (pistolButton == null)
        {
            GameObject pistolButtonObj = GameObject.FindGameObjectWithTag("PistolButton");
            if (pistolButtonObj != null)
            {
                pistolButton = pistolButtonObj.GetComponent<Button>();
                Debug.Log($"[WeaponEquipInputHandler] Found PistolButton at runtime using tag");
            }
            else
            {
                Debug.LogWarning($"[WeaponEquipInputHandler] PistolButton not found! Make sure a UI button has the tag 'PistolButton'");
            }
        }

        if (bombButton == null)
        {
            GameObject bombButtonObj = GameObject.FindGameObjectWithTag("BombButton");
            if (bombButtonObj != null)
            {
                bombButton = bombButtonObj.GetComponent<Button>();
                Debug.Log($"[WeaponEquipInputHandler] Found BombButton at runtime using tag");
            }
            else
            {
                Debug.LogWarning($"[WeaponEquipInputHandler] BombButton not found! Make sure a UI button has the tag 'BombButton'");
            }
        }
    }

    private void SetupUIButtons()
    {
        if (pistolButton != null)
        {
            pistolButton.onClick.AddListener(OnPistolButtonPressed);
            Debug.Log($"[WeaponEquipInputHandler] Pistol button listener added");
        }

        if (bombButton != null)
        {
            bombButton.onClick.AddListener(OnBombButtonPressed);
            Debug.Log($"[WeaponEquipInputHandler] Bomb button listener added");
        }
    }

    private void Update()
    {
        // Check if this is the local player (may take a frame or two for Fusion to initialize)
        if (!hasCheckedLocalPlayer)
        {
            CheckLocalPlayer();
        }
        
        if (!isLocalPlayer || equipSystem == null)
        {
            return;
        }

        if (Input.GetKeyDown(pistolKey))
        {
            Debug.Log("[WeaponEquipInputHandler] Key '1' pressed - Requesting to equip PISTOL");
            equipSystem.RequestEquipPistol();
        }

        if (Input.GetKeyDown(bombKey))
        {
            Debug.Log("[WeaponEquipInputHandler] Key '2' pressed - Requesting to equip BOMB");
            equipSystem.RequestEquipBomb();
        }
    }

    public void OnPistolButtonPressed()
    {
        if (equipSystem != null)
        {
            Debug.Log("[WeaponEquipInputHandler] Pistol button pressed - Requesting to equip PISTOL");
            equipSystem.RequestEquipPistol();
        }
    }

    public void OnBombButtonPressed()
    {
        if (equipSystem != null)
        {
            Debug.Log("[WeaponEquipInputHandler] Bomb button pressed - Requesting to equip BOMB");
            equipSystem.RequestEquipBomb();
        }
    }

    private void OnDestroy()
    {
        if (pistolButton != null)
        {
            pistolButton.onClick.RemoveListener(OnPistolButtonPressed);
        }

        if (bombButton != null)
        {
            bombButton.onClick.RemoveListener(OnBombButtonPressed);
        }
    }
}
