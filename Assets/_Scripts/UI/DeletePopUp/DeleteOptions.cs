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
        if (string.IsNullOrEmpty(accountTypeToDelete))
        {
            Debug.LogWarning("No account type specified for deletion.");
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
                Debug.LogError("Unknown account type: " + accountTypeToDelete);
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
