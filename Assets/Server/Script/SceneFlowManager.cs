using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    [System.Serializable]
    public class SceneEntry
    {
        public string key;
        public int buildIndex;
    }

    [Header("Scene List")]
    [SerializeField] private List<SceneEntry> sceneList = new List<SceneEntry>();

    [Header("Core Scene Keys")]
    [SerializeField] private string lobbyKey = "Lobby";
    [SerializeField] private string waitingRoomKey = "WaitingRoom";
    [SerializeField] private string gameKey = "Game";
    [SerializeField] private List<string> gameplaySceneKeys = new List<string> { "Game", "StageA", "StageB" };
    [SerializeField] private string scenesFolderPath = "Assets/Scenes";

    private Dictionary<string, int> sceneMap;

    private void Awake()
    {
        BuildSceneMap();
    }

    private void BuildSceneMap()
    {
        sceneMap = new Dictionary<string, int>();

        foreach (SceneEntry entry in sceneList)
        {
            if (string.IsNullOrWhiteSpace(entry.key))
            {
                Debug.LogWarning("[SceneFlowManager] Empty scene key found");
                continue;
            }

            string key = entry.key.Trim();

            if (sceneMap.ContainsKey(key))
            {
                Debug.LogWarning("[SceneFlowManager] Duplicate scene key: " + key);
                continue;
            }

            sceneMap.Add(key, entry.buildIndex);
        }

        Debug.Log($"[SceneFlowManager][Diagnostics:BuildSceneMap] count={sceneMap.Count}, keys={string.Join(", ", sceneMap.Keys)}");
    }

    public NetworkSceneInfo GetWaitingRoomSceneInfo()
    {
        return GetSceneInfo(waitingRoomKey);
    }

    public NetworkSceneInfo GetGameSceneInfo()
    {
        return GetSceneInfo(gameKey);
    }

    public NetworkSceneInfo GetSceneInfo(string key)
    {
        int buildIndex = GetBuildIndex(key);
        return CreateSceneInfo(buildIndex);
    }

    public bool IsGameSceneLoaded()
    {
        if (IsSceneLoaded(gameKey))
            return true;

        if (gameplaySceneKeys == null)
            return false;

        foreach (string gameplaySceneKey in gameplaySceneKeys)
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneKey))
                continue;

            if (IsSceneLoaded(gameplaySceneKey))
                return true;
        }

        return false;
    }

    public bool IsSceneLoaded(string key)
    {
        int buildIndex = GetBuildIndex(key);

        if (buildIndex < 0)
            return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).buildIndex == buildIndex)
                return true;
        }

        return false;
    }

    public void LoadLobbySceneLocal()
    {
        LoadSceneLocal(lobbyKey);
    }

    public void LoadSceneLocal(string key)
    {
        int buildIndex = GetBuildIndex(key);

        if (buildIndex < 0)
            return;

        Debug.Log($"[SceneFlowManager][Diagnostics:LoadSceneLocal] key={key}, buildIndex={buildIndex}, activeScene={SceneManager.GetActiveScene().name}");
        SceneManager.LoadScene(buildIndex);
    }

    public void LoadGameSceneNetwork()
    {
        LoadSceneNetwork(gameKey);
    }

    public void LoadStageSceneNetwork(string stageKey)
    {
        LoadSceneNetwork(stageKey);
    }

    public void LoadWaitingRoomSceneNetwork()
    {
        LoadSceneNetwork(waitingRoomKey);
    }

    public void LoadSceneNetwork(string key)
    {
        NetworkRunner runner = ResolveRunner();
        LoadSceneNetwork(runner, key);
    }

    public void LoadSceneNetwork(NetworkRunner runner, string key)
    {
        int buildIndex = GetBuildIndex(key);

        if (buildIndex < 0)
            return;

        Debug.Log(
            $"[SceneFlowManager][Diagnostics:LoadSceneNetwork] key={key}, buildIndex={buildIndex}, " +
            $"runner={(runner != null ? runner.name : "null")}, running={(runner != null && runner.IsRunning)}, " +
            $"sceneAuthority={(runner != null && runner.IsSceneAuthority)}, activeScene={SceneManager.GetActiveScene().name}"
        );

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

        runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
    }

    private int GetBuildIndex(string key)
    {
        if (sceneMap == null)
            BuildSceneMap();

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogError("[SceneFlowManager] Scene key is empty");
            return -1;
        }

        key = key.Trim();

        if (!sceneMap.TryGetValue(key, out int buildIndex))
        {
            buildIndex = GetBuildIndexFromScenePath(key);

            if (buildIndex < 0)
            {
                Debug.LogError("[SceneFlowManager] Scene key not registered: " + key);
                return -1;
            }
        }

        return buildIndex;
    }

    private int GetBuildIndexFromScenePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return -1;

        string scenePath = key.Replace('\\', '/').Trim();

        if (!scenePath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            scenePath += ".unity";

        if (!scenePath.Contains("/"))
        {
            string folderPath = string.IsNullOrWhiteSpace(scenesFolderPath)
                ? "Assets/Scenes"
                : scenesFolderPath.Trim().TrimEnd('/', '\\').Replace('\\', '/');

            scenePath = $"{folderPath}/{scenePath}";
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);

        if (buildIndex < 0)
            Debug.LogWarning($"[SceneFlowManager] Scene path is not enabled in Build Settings: {scenePath}");

        return buildIndex;
    }

    private NetworkSceneInfo CreateSceneInfo(int buildIndex)
    {
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
        return sceneInfo;
    }

    private NetworkRunner ResolveRunner()
    {
        return FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    }
}
