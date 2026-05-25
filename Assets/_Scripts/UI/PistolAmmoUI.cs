using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays pistol ammo count for the local player.
/// Attach to a UI Canvas element.
/// </summary>
public class PistolAmmoUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI reloadText;
    [SerializeField] private Image reloadProgressBar;

    private NetworkPistolBehaviour localPlayerPistol;
    private float reloadStartTime;

    private void Update()
    {
        if (localPlayerPistol == null)
        {
            FindLocalPlayerPistol();
            return;
        }

        UpdateAmmoDisplay();
        UpdateReloadDisplay();
    }

    private void FindLocalPlayerPistol()
    {
        var allPlayers = FindObjectsOfType<NetworkPistolBehaviour>();
        foreach (var pistol in allPlayers)
        {
            if (pistol.Object != null && pistol.Object.HasInputAuthority)
            {
                localPlayerPistol = pistol;
                Debug.Log("[PistolAmmoUI] Found local player pistol");
                break;
            }
        }
    }

    private void UpdateAmmoDisplay()
    {
        if (ammoText != null)
        {
            ammoText.text = $"{localPlayerPistol.CurrentAmmo} / {localPlayerPistol.MaxAmmo}";
        }
    }

    private void UpdateReloadDisplay()
    {
        if (localPlayerPistol.IsReloading)
        {
            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(true);
                reloadText.text = "RELOADING...";
            }

            if (reloadProgressBar != null)
            {
                reloadProgressBar.gameObject.SetActive(true);
            }
        }
        else
        {
            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            if (reloadProgressBar != null)
            {
                reloadProgressBar.gameObject.SetActive(false);
            }
        }
    }
}
