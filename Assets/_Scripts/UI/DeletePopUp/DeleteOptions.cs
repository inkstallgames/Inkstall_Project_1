using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Threading.Tasks;

public class DeleteOptions : MonoBehaviour
{
    [Header("UI Notifications")]
    public TextMeshProUGUI notificationText;
    private string accountTypeToDelete;
    private bool isDeletingAccount = false;
    public void OnNotDeleteButtonClicked()
  {
     gameObject.SetActive(false);
  }


  public void SetAccountType(string accountType)
    {
        accountTypeToDelete = accountType;
    }

    public void OnDeleteButtonClicked()
    {
        // Auto-detect account type if not set
        if (string.IsNullOrEmpty(accountTypeToDelete))
        {
            accountTypeToDelete = DetectAccountType();
        }

        if (string.IsNullOrEmpty(accountTypeToDelete))
        {
            StartCoroutine(ShowNotification("Unable to detect account type. Please sign in first.", 3f));
            return;
        }

        Debug.Log("Deletion process started for: " + accountTypeToDelete);

        switch (accountTypeToDelete)
        {
            case "Google":
                DeleteGoogleAccount();
                break;

            case "Apple":
                DeleteAppleAccount();
                break;

            default:
                StartCoroutine(ShowNotification("Unknown account type: " + accountTypeToDelete, 3f));
                break;
        }

        // Reset the account type and close the pop-up
        accountTypeToDelete = null;
        gameObject.SetActive(false);
    }

    private async void DeleteGoogleAccount()
    {
        if (isDeletingAccount)
        {
            StartCoroutine(ShowNotification("Account deletion already in progress.", 3f));
            return;
        }

        isDeletingAccount = true;
        StartCoroutine(ShowNotification("Deleting Google account...", 3f));

        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                StartCoroutine(ShowNotification("User is not signed in.", 3f));
                isDeletingAccount = false;
                return;
            }

            await AuthenticationService.Instance.DeleteAccountAsync();
            StartCoroutine(ShowNotification("Account deleted successfully.", 3f));
        }
        catch (AuthenticationException ex)
        {
            StartCoroutine(ShowNotification($"Error: {ex.Message}", 3f));
        }
        catch (RequestFailedException ex)
        {
            StartCoroutine(ShowNotification($"Request failed: {ex.Message}", 3f));
        }
        finally
        {
            isDeletingAccount = false;
        }
    }

    private async void DeleteAppleAccount()
    {
        if (isDeletingAccount)
        {
            StartCoroutine(ShowNotification("Account deletion already in progress.", 3f));
            return;
        }

        isDeletingAccount = true;
        StartCoroutine(ShowNotification("Deleting Apple account...", 3f));

        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                StartCoroutine(ShowNotification("User is not signed in.", 3f));
                isDeletingAccount = false;
                return;
            }

            await AuthenticationService.Instance.DeleteAccountAsync();
            StartCoroutine(ShowNotification("Account deleted successfully.", 3f));
        }
        catch (AuthenticationException ex)
        {
            StartCoroutine(ShowNotification($"Error: {ex.Message}", 3f));
        }
        catch (RequestFailedException ex)
        {
            StartCoroutine(ShowNotification($"Request failed: {ex.Message}", 3f));
        }
        finally
        {
            isDeletingAccount = false;
        }
    }

    private string DetectAccountType()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("User is not signed in.");
            return null;
        }

        var playerInfo = AuthenticationService.Instance.PlayerInfo;
        
        if (playerInfo != null && playerInfo.Identities != null)
        {
            foreach (var identity in playerInfo.Identities)
            {
#if UNITY_ANDROID
                if (identity.TypeId == "google_play_games")
                {
                    return "Google";
                }
#endif
#if UNITY_IOS
                if (identity.TypeId == "apple.com")
                {
                    return "Apple";
                }
#endif
            }
        }

        Debug.LogWarning("Could not detect account type from identities.");
        return null;
    }

    private IEnumerator ShowNotification(string message, float duration)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.gameObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            notificationText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Notification Text component is not assigned.");
        }
    }
}
