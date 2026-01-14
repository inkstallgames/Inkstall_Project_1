using UnityEngine;
#if UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

public class AppleSignInHandler : MonoBehaviour
{
#if UNITY_IOS
    private IAppleAuthManager _appleAuthManager;

    void Start()
    {
        if (AppleAuthManager.IsCurrentPlatformSupported)
        {
            var deserializer = new PayloadDeserializer();
            _appleAuthManager = new AppleAuthManager(deserializer);
        }
    }

    void Update()
    {
        if (_appleAuthManager != null)
        {
            _appleAuthManager.Update();
        }
    }

    public void SignIn()
    {
        if (_appleAuthManager == null)
        {
            Debug.LogError("[AppleSignInHandler] Apple Auth is not supported on this platform.");
            return;
        }

        var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);

        _appleAuthManager.LoginWithAppleId(
            loginArgs,
            credential =>
            {
                var appleIdCredential = credential as IAppleIDCredential;
                if (appleIdCredential != null)
                {
                    string identityToken = System.Text.Encoding.UTF8.GetString(appleIdCredential.IdentityToken, 0, appleIdCredential.IdentityToken.Length);
                    if (!string.IsNullOrEmpty(identityToken))
                    {
                        Debug.Log("[AppleSignInHandler] Apple Sign-In successful. Passing token to AuthManager.");
                        AuthManager.Instance.SignInWithApple(identityToken);
                    }
                    else
                    {
                        Debug.LogError("[AppleSignInHandler] Apple Sign-In failed: Identity token is null or empty.");
                    }
                }
                else
                {
                    Debug.LogError("[AppleSignInHandler] Apple Sign-In failed: Credential is not an AppleIDCredential.");
                }
            },
            error =>
            {
                Debug.LogError($"[AppleSignInHandler] Apple Sign-In failed: {error}");
                
                // Hide the "Signing you in..." notification
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.HideNotification();
                    
                    // Show error message if it's not a user cancellation
                    if (error.Code != AuthorizationErrorCode.Canceled)
                    {
                        UIManager.Instance.ShowNotificationMessage("Sign in failed. Please try again.");
                    }
                }
            });
    }
#endif
}