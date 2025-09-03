using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;
using Google;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class LoginWithGoogle : MonoBehaviour
{
    public string GoogleAPI = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com";
    [SerializeField] private string databaseCheckUrl = "https://api.inkstall.in/api/auth/student/google-login";
    private GoogleSignInConfiguration configuration;

    //Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    Firebase.Auth.FirebaseAuth auth;
    Firebase.Auth.FirebaseUser user;

    private void Awake()
    {
        configuration = new GoogleSignInConfiguration
        {
            WebClientId = GoogleAPI,
            RequestIdToken = true,
        };
    }

    private void Start()
    {
        InitFirebase();
    }

    void InitFirebase()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
    }

    public void Login()
    {
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            RequestIdToken = true,
            // Copy this value from the google-service.json file.
            // oauth_client with type == 3
            WebClientId = GoogleAPI
        };
        GoogleSignIn.Configuration.RequestEmail = true;

        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();

        TaskCompletionSource<FirebaseUser> signInCompleted = new TaskCompletionSource<FirebaseUser>();
        signIn.ContinueWith(task => {
            if (task.IsCanceled)
            {
                signInCompleted.SetCanceled();
                Debug.Log("Cancelled");
            }
            else if (task.IsFaulted)
            {
                signInCompleted.SetException(task.Exception);

                Debug.Log("Faulted " + task.Exception);
            }
            else
            {
                Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(((Task<GoogleSignInUser>)task).Result.IdToken, null);
                auth.SignInWithCredentialAsync(credential).ContinueWith(authTask => {
                    if (authTask.IsCanceled)
                    {
                        signInCompleted.SetCanceled();
                    }
                    else if (authTask.IsFaulted)
                    {
                        signInCompleted.SetException(authTask.Exception);
                        Debug.Log("Faulted In Auth " + task.Exception);
                    }
                    else
                    {
                        signInCompleted.SetResult(((Task<FirebaseUser>)authTask).Result);
                        Debug.Log("Success");
                        user = auth.CurrentUser;
                    }
                });
            }
        });
    }

}