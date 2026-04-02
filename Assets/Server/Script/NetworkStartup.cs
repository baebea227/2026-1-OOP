using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class NetworkStartup : MonoBehaviour
{
    private NetworkRunner runner;

    async void Start()
    {
        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = "Room1",
            Scene = sceneInfo
        });

        Debug.Log("Fusion 시작 완료");
    }
}