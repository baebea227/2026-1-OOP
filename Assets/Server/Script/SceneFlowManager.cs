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
        {
            DontDestroyOnLoad(gameObject);
        }

        FindRunnerIfNull();
    }

    private void FindRunnerIfNull()
    {
        if (runner == null)
        {
            runner = FindObjectOfType<NetworkRunner>();
        }
    }

    public void LoadLobbySceneLocal()
    {
        // 로비로 돌아갈 때는 보통 세션을 먼저 종료한 뒤 로컬 씬 이동
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
            Debug.LogError("[SceneFlowManager] NetworkRunner를 찾을 수 없음");
            return;
        }

        if (!runner.IsRunning)
        {
            Debug.LogError("[SceneFlowManager] NetworkRunner가 실행 중이 아님");
            return;
        }

        if (!runner.IsSceneAuthority)
        {
            Debug.LogWarning("[SceneFlowManager] 씬 전환은 Scene Authority만 호출해야 함");
            return;
        }

        SceneRef sceneRef = SceneRef.FromIndex(sceneBuildIndex);

        Debug.Log($"[SceneFlowManager] 네트워크 씬 전환: Build Index {sceneBuildIndex}");

        runner.LoadScene(sceneRef, LoadSceneMode.Single);
    }

    public int GetLobbySceneIndex()
    {
        return lobbySceneBuildIndex;
    }

    public int GetWaitingRoomSceneIndex()
    {
        return waitingRoomSceneBuildIndex;
    }

    public int GetGameSceneIndex()
    {
        return gameSceneBuildIndex;
    }
}