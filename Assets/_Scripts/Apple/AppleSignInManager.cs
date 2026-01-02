using UnityEngine;
using Unity.Services.Authentication;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using AppleAuth.Extensions;
using System.Text;


public class AppleSignInManager : MonoBehaviour
{

    private IAppleAuthManager appleAuthManager;

    void Awake()
    {
        if (AppleAuthManager.IsCurrentPlatformSupported)
        {
            var deserializer = new PayloadDeserializer();
            appleAuthManager = new AppleAuthManager(deserializer);
        }
    }

    void Update()
    {
        appleAuthManager?.Update();
    }

    // ---------------- SIGN IN WITH APPLE ----------------
    public void SignInWithApple()
    {
        if (appleAuthManager == null)
        {
            Debug.LogError("❌ Apple Auth not supported on this platform");
            return;
        }

        var loginArgs = new AppleAuthLoginArgs(
            LoginOptions.IncludeEmail | LoginOptions.IncludeFullName
        );

        appleAuthManager.LoginWithAppleId(
            loginArgs,
            async credential =>
            {
                var appleIdCredential = credential as IAppleIDCredential;
                if (appleIdCredential == null)
                {
                    Debug.LogError("❌ Invalid Apple credential");
                    return;
                }

                // 🔑 THIS is what Unity needs
                string identityToken = Encoding.UTF8.GetString(
                    appleIdCredential.IdentityToken
                );

                try
                {
                    await AuthenticationService.Instance
                        .LinkWithAppleAsync(identityToken);

                    Debug.Log("✅ Apple account linked with Unity Authentication");
                }
                catch (AuthenticationException e)
                {
                    Debug.LogWarning("⚠ Apple already linked or conflict: " + e.Message);
                }
            },
            error =>
            {
                Debug.LogError("❌ Apple Sign-In failed: " + error);
            }
        );
    }

}
