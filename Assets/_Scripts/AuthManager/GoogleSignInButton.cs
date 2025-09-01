using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GoogleSignInButton : MonoBehaviour
{
    private Button signInButton;
    
    void Start()
    {
        signInButton = GetComponent<Button>();
        signInButton.onClick.AddListener(OnSignInButtonClicked);
    }

    void OnSignInButtonClicked()
    {
        // Make sure AuthManager exists
        if (AuthManager.Instance == null)
        {
            Debug.LogError("AuthManager not found in scene!");
            return;
        }
        
        // Call the sign-in method
        AuthManager.Instance.SignInWithGoogle();
    }
}
