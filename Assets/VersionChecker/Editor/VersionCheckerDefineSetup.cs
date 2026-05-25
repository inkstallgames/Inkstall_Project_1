#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class VersionCheckerDefineSetup
{
    private const string DefineSymbol = "FIREBASE_REMOTE_CONFIG";
    private const string RemoteConfigDllPath = "Firebase/Plugins/Firebase.RemoteConfig.dll";

    [InitializeOnLoadMethod]
    private static void SyncDefineOnLoad()
    {
        SyncDefineSymbol();
    }

    [MenuItem("Tools/Version Checker/Verify Firebase Remote Config")]
    private static void VerifyFirebaseRemoteConfig()
    {
        SyncDefineSymbol();

        if (HasFirebaseRemoteConfig())
        {
            EditorUtility.DisplayDialog(
                "Version Checker",
                "Firebase Remote Config is installed.\n\nThe FIREBASE_REMOTE_CONFIG scripting define has been enabled for Android and iOS.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Version Checker",
            "Firebase Remote Config is NOT installed.\n\n" +
            "1. Download the Firebase Unity SDK\n" +
            "2. Import FirebaseRemoteConfig.unitypackage\n" +
            "3. Add google-services.json (Android) and GoogleService-Info.plist (iOS)\n\n" +
            "Setup guide:\nhttps://firebase.google.com/docs/unity/setup",
            "OK");
    }

    private static void SyncDefineSymbol()
    {
        bool hasFirebaseRemoteConfig = HasFirebaseRemoteConfig();
        SetDefineForTarget(NamedBuildTarget.Android, hasFirebaseRemoteConfig);
        SetDefineForTarget(NamedBuildTarget.iOS, hasFirebaseRemoteConfig);
    }

    private static void SetDefineForTarget(NamedBuildTarget target, bool enableDefine)
    {
        string currentDefines = PlayerSettings.GetScriptingDefineSymbols(target);
        var defineList = currentDefines
            .Split(';')
            .Where(define => !string.IsNullOrWhiteSpace(define))
            .ToList();

        bool containsDefine = defineList.Contains(DefineSymbol);

        if (enableDefine && !containsDefine)
        {
            defineList.Add(DefineSymbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defineList));
            Debug.Log($"[VersionChecker] Added {DefineSymbol} for {target.TargetName}.");
        }
        else if (!enableDefine && containsDefine)
        {
            defineList.Remove(DefineSymbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defineList));
            Debug.Log($"[VersionChecker] Removed {DefineSymbol} for {target.TargetName}.");
        }
    }

    private static bool HasFirebaseRemoteConfig()
    {
        return File.Exists(Path.Combine(Application.dataPath, RemoteConfigDllPath));
    }
}
#endif
