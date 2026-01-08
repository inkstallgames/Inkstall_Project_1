using UnityEngine;
#if UNITY_IOS
using UnityEngine.SignInWithApple;
#endif

public class AppleSignInHandler : MonoBehaviour
{
#if UNITY_IOS
    public void SignIn()
    {
        var siwa = gameObject.AddComponent<SignInWithApple>();
        siwa.Login(OnLogin);
    }

    private void OnLogin(SignInWithApple.CallbackArgs args)
    {
        if (args.error != null)
        {
            Debug.LogError($"[AppleSignInHandler] Apple Sign-In failed: {args.error}");
            return;
        }

        var userInfo = (SignInWithApple.UserInfo)args.userInfo;
        string identityToken = userInfo.idToken;

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
#endif
}
