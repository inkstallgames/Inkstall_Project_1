using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using Firebase.Auth;
using UnityEngine.UI;
using TMPro;
using Google;
using System.Net.Http;

public class GoogleAuthManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text UsernameTxt;
    public TMP_Text UserEmailTxt;
    public GameObject LoginScreen;
    public GameObject ProfileScreen;
    public Image userProfilePic;


    private string GoogleWebAPI = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com";

    private GoogleSignInConfiguration config;

    Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    Firebase.Auth.FirebaseAuth auth;
    Firebase.Auth.FirebaseUser user;

    public TMP_Text statusText;
    public Button signInButton;

    private void Awake()
    {
        config = new GoogleSignInConfiguration()
        {
            WebClientId = GoogleWebAPI,
            RequestIdToken = true,
            RequestEmail = true
        };
    }

    private void Start()
    {
        signInButton.onClick.AddListener(SignInWithGoogle);
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
    }

    private void SignInWithGoogle()
    {
        GoogleSignIn.Configuration = config;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;
        GoogleSignIn.Configuration.RequestEmail = true;

        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleAutheniticatedFinished);
    }

    void OnGoogleAutheniticatedFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            Debug.LogError("Fault");
        }
        else if (task.IsCanceled)
        {
            Debug.LogError("Login Cancel");
        }
        else
        {
            Firebase.Auth.Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(task.Result.IdToken, null);

            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled)
                {
                    Debug.LogError("signInWithCredentialAsync was canceled");
                    return;
                }
                if (task.IsFaulted)
                {
                    Debug.LogError("SignInWithCredentialAsync encountered an error");
                    return;
                }

                user = auth.CurrentUser;

                UsernameTxt.text = user.DisplayName;
                UserEmailTxt.text = user.Email;

                LoginScreen.SetActive(false);
                ProfileScreen.SetActive(true);

                StartCoroutine(LoadImage(CheckImageUrl(user.PhotoUrl.ToString())));
            }

            );
        }
    }

    private string CheckImageUrl(string url)
    {
        return string.IsNullOrEmpty(url) ? "" : url;
    }


    IEnumerator LoadImage(string imageUri)
    {

        WWW www = new WWW(imageUri);
        yield return www;

        userProfilePic.sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0, 0));
    }
}