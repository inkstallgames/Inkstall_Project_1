using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles weapon equipping input from UI buttons or keyboard.
/// Team A gets pistol, Team B gets laser as primary weapon.
/// Attach to the player prefab alongside NetworkWeaponEquipSystem.
/// </summary>
public class WeaponEquipInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    [SerializeField] private KeyCode primaryKey = KeyCode.Alpha1; // Changes based on team
    [SerializeField] private KeyCode bombKey = KeyCode.Alpha2;
    
    [Header("UI Button References (Optional)")]
    [SerializeField] private Button primaryButton; // Changes based on team
    [SerializeField] private Button bombButton;

    private NetworkWeaponEquipSystem equipSystem;
    private PlayerNetworkData playerData;
    private bool isLocalPlayer = false;
    private bool hasCheckedLocalPlayer = false;

    private void Start()
    {
        equipSystem = GetComponent<NetworkWeaponEquipSystem>();
        playerData = GetComponent<PlayerNetworkData>();
        // Debug.Log($"[WeaponEquipInputHandler] Start called. EquipSystem found: {equipSystem != null}");
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
            // Debug.Log($"[WeaponEquipInputHandler] Local player detected! Input handling enabled.");
        }
    }

    private void FindUIButtonsAtRuntime()
    {
        // Find buttons by tag if not assigned in Inspector
        if (primaryButton == null)
        {
            // Use the same button for both pistol and laser teams since they share shooting button
            string buttonTag = "PistolButton";
            GameObject primaryButtonObj = GameObject.FindGameObjectWithTag(buttonTag);
            if (primaryButtonObj != null)
            {
                primaryButton = primaryButtonObj.GetComponent<Button>();
                // Debug.Log($"[WeaponEquipInputHandler] Found {buttonTag} at runtime using tag");
            }
            else
            {
                // Debug.LogWarning($"[WeaponEquipInputHandler] {buttonTag} not found! Make sure a UI button has the tag '{buttonTag}'");
            }
        }

        if (bombButton == null)
        {
            GameObject bombButtonObj = GameObject.FindGameObjectWithTag("BombButton");
            if (bombButtonObj != null)
            {
                bombButton = bombButtonObj.GetComponent<Button>();
                // Debug.Log($"[WeaponEquipInputHandler] Found BombButton at runtime using tag");
            }
            else
            {
                // Debug.LogWarning($"[WeaponEquipInputHandler] BombButton not found! Make sure a UI button has the tag 'BombButton'");
            }
        }
    }

    private void SetupUIButtons()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.AddListener(OnPrimaryButtonPressed);
            string weaponName = playerData != null && playerData.TeamId == 1 ? "Laser" : "Pistol";
            // Debug.Log($"[WeaponEquipInputHandler] {weaponName} equip button listener added (shared button)");
        }

        if (bombButton != null)
        {
            bombButton.onClick.AddListener(OnBombButtonPressed);
            // Debug.Log($"[WeaponEquipInputHandler] Bomb button listener added");
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

        if (Input.GetKeyDown(primaryKey))
        {
            string weaponName = playerData != null && playerData.TeamId == 1 ? "LASER" : "PISTOL";
            // Debug.Log($"[WeaponEquipInputHandler] Key '1' pressed - Requesting to equip {weaponName}");
            equipSystem.RequestEquipPrimary();
        }

        if (Input.GetKeyDown(bombKey))
        {
            // Debug.Log("[WeaponEquipInputHandler] Key '2' pressed - Requesting to equip BOMB");
            equipSystem.RequestEquipBomb();
        }
    }

    public void OnPrimaryButtonPressed()
    {
        if (equipSystem != null)
        {
            string weaponName = playerData != null && playerData.TeamId == 1 ? "LASER" : "PISTOL";
            // Debug.Log($"[WeaponEquipInputHandler] {weaponName} button pressed - Requesting to equip {weaponName}");
            equipSystem.RequestEquipPrimary();
        }
    }

    public void OnBombButtonPressed()
    {
        if (equipSystem != null)
        {
            // Debug.Log("[WeaponEquipInputHandler] Bomb button pressed - Requesting to equip BOMB");
            equipSystem.RequestEquipBomb();
        }
    }

    private void OnDestroy()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveListener(OnPrimaryButtonPressed);
        }

        if (bombButton != null)
        {
            bombButton.onClick.RemoveListener(OnBombButtonPressed);
        }
    }
}
