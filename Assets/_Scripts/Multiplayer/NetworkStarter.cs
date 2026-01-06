using Fusion;
using Fusion.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkStarter : MonoBehaviour
{
    private NetworkRunner runner;

    async void Start()
    {
        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared, // or Host
            SessionName = "MyRoom",
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }
}
