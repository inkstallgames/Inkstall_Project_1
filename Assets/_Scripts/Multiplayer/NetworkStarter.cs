using Fusion;
using Fusion.Sockets;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    [SerializeField] private NetworkRunner _runner;
    [SerializeField] private NetworkSceneManagerDefault _sceneManager;

    private void Start()
    {
        // Get references to required components
        _runner = GetComponent<NetworkRunner>();
        _sceneManager = GetComponent<NetworkSceneManagerDefault>();
        
        if (_runner == null)
        {
            Debug.LogError("NetworkRunner component is missing! Make sure it's attached to the same GameObject.");
            return;
        }
        
        if (_sceneManager == null)
        {
            _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
        
        StartGame();
    }

    private async void StartGame()
    {
        try
        {
            // Configure the runner
            _runner.ProvideInput = true;

            // Initialize the NetworkRunner
            await _runner.StartGame(new StartGameArgs()
            {
                GameMode = Fusion.GameMode.Shared,
                SessionName = "MyRoom",
                SceneManager = _sceneManager,
                PlayerCount = 4,
                Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)
            });

            if (_runner.IsRunning)
            {
                Debug.Log("NetworkRunner started successfully");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start NetworkRunner: {e.Message}\n{e.StackTrace}");
        }
    }
}
