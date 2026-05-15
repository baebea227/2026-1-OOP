using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    [Header("Fusion")]
    [SerializeField] private NetworkRunner runner;

    [Header("Scene Build Index")]
    [SerializeField] private int lobbySceneBuildIndex = 0;
    [SerializeField] private int waitingRoomSceneBuildIndex = 1;
    [SerializeField] private int gameSceneBuildIndex = 2;

    [Header("Option")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private void Awake()
    {
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        FindRunnerIfNull();
    }

    private void FindRunnerIfNull()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();
    }

    public NetworkSceneInfo GetWaitingRoomSceneInfo()
    {
        return CreateSceneInfo(waitingRoomSceneBuildIndex);
    }

    public NetworkSceneInfo GetGameSceneInfo()
    {
        return CreateSceneInfo(gameSceneBuildIndex);
    }

    private NetworkSceneInfo CreateSceneInfo(int sceneBuildIndex)
    {
        SceneRef sceneRef = SceneRef.FromIndex(sceneBuildIndex);

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

        return sceneInfo;
    }

    public bool IsGameSceneLoaded()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == gameSceneBuildIndex;
    }

    public void LoadLobbySceneLocal()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneBuildIndex);
    }

    public void LoadWaitingRoomSceneNetwork()
    {
        LoadNetworkScene(waitingRoomSceneBuildIndex);
    }

    public void LoadGameSceneNetwork()
    {
        LoadNetworkScene(gameSceneBuildIndex);
    }

    private void LoadNetworkScene(int sceneBuildIndex)
    {
        FindRunnerIfNull();

        if (runner == null)
        {
            Debug.LogError("[SceneFlowManager] NetworkRunner not found");
            return;
        }

        if (!runner.IsRunning)
        {
            Debug.LogError("[SceneFlowManager] NetworkRunner is not running");
            return;
        }

        if (!runner.IsSceneAuthority)
        {
            Debug.LogWarning("[SceneFlowManager] Only SceneAuthority can load network scenes");
            return;
        }

        runner.LoadScene(SceneRef.FromIndex(sceneBuildIndex), LoadSceneMode.Single);
    }
}