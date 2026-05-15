using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion.Addons.Physics;

public class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion")]
    public static NetworkSessionManager Instance { get; private set; }
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private GameObject runnerObject;

    [Header("Session Setting")]
    [SerializeField] private string lobbyName = "MainLobby";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private SceneFlowManager sceneFlowManager;

    [Header("Option")]
    [SerializeField] private bool dontDestroyOnLoad = true;


    public event Action<List<SessionInfo>> OnSessionListChanged;
    public event Action<string> OnStatusChanged;
    public event Action<bool> OnBusyStateChanged;

    public event Action<PlayerRef> OnPlayerJoinedEvent;
    public event Action<PlayerRef> OnPlayerLeftEvent;
    public event Action OnSessionStartedEvent;
    public event Action OnSessionShutdownEvent;

    private readonly List<SessionInfo> cachedSessions = new List<SessionInfo>();

    private bool isBusy = false;

    public NetworkRunner Runner => runner;
    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;
    public bool IsBusy => isBusy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkSessionManager] Duplicate NetworkCore destroyed: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (sceneFlowManager == null)
            sceneFlowManager = GetComponent<SceneFlowManager>();

        Debug.Log("[NetworkSessionManager] Awake completed: " + gameObject.name);
    }
    
    private void OnDestroy()
    {
        Debug.LogWarning("[NetworkSessionManager] OnDestroy called: " + gameObject.name);

        if (runner != null)
            runner.RemoveCallbacks(this);

        if (Instance == this)
            Instance = null;
    }

        private void SetupRunner()
    {
        if (runner != null)
            return;

        // 혹시 이전 런타임 Runner 오브젝트가 남아있으면 제거
        GameObject oldRuntime = GameObject.Find("NetworkRunner_Runtime");
        if (oldRuntime != null)
        {
            Destroy(oldRuntime);
        }

        runnerObject = new GameObject("NetworkRunner_Runtime");

        // NetworkCore의 자식으로 두지 않는다.
        // 따로 DontDestroyOnLoad 루트 오브젝트로 둔다.
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.AddComponent<NetworkRunner>();
        sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        // 게임 씬 물리 동기화용
        runnerObject.AddComponent<RunnerSimulatePhysics3D>();

        runner.ProvideInput = false;

        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        Debug.Log("[NetworkSessionManager] NetworkRunner_Runtime created");
    }

    public async void JoinLobby()
    {
        await JoinLobbyAsync();
    }

        public async void CreateSession(string sessionName)
    {
        await StartSessionAsync(GameMode.Host, sessionName);
    }

    public async void JoinSession(string sessionName)
    {
        await StartSessionAsync(GameMode.Client, sessionName);
    }

    public void CreateSessionWithRandomCode()
    {
        string roomCode = GenerateRoomCode();

        SetStatus("Room Code: " + roomCode);

        CreateSession(roomCode);
    }

    private async Task JoinLobbyAsync()
    {
        if (isBusy)
            return;

        SetBusy(true);
        SetStatus("Connecting to Photon lobby...");

        try
        {
            SetupRunner();

            var result = await runner.JoinSessionLobby(SessionLobby.Custom, lobbyName);

            if (result.Ok)
            {
                SetStatus("Connected to Photon lobby. Waiting for the session list...");
            }
            else
            {
                SetStatus("Failed to connect to Photon lobby: " + result.ShutdownReason);
                await ResetRunnerAsync();
            }
        }
        catch (Exception e)
        {
            SetStatus("Join lobby error: " + e.Message);
            await ResetRunnerAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        int length = 6;

        System.Text.StringBuilder code = new System.Text.StringBuilder();

        for (int i = 0; i < length; i++)
        {
            int index = UnityEngine.Random.Range(0, chars.Length);
            code.Append(chars[index]);
        }

        return code.ToString();
    }

    private async Task StartSessionAsync(GameMode gameMode, string sessionName)
    {
        if (isBusy)
            return;

        sessionName = NormalizeSessionName(sessionName);

        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Room code is empty");
            return;
        }

        SetBusy(true);

        try
        {
            SetupRunner();

            if (sceneFlowManager == null)
            {
                SetStatus("SceneFlowManager not found");
                return;
            }

            runner.ProvideInput = true;

            if (gameMode == GameMode.Host)
                SetStatus($"Creating room: {sessionName}");
            else
                SetStatus($"Joining room: {sessionName}");

            StartGameArgs args = new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = sessionName,

                CustomLobbyName = lobbyName,

                PlayerCount = maxPlayers,

                IsOpen = true,
                IsVisible = true,

                Scene = sceneFlowManager.GetWaitingRoomSceneInfo(),
                SceneManager = sceneManager
            };

            // Client가 없는 방을 실수로 새로 만들지 못하게 막음
            if (gameMode == GameMode.Client)
            {
                args.EnableClientSessionCreation = false;
            }

            var result = await runner.StartGame(args);

            if (result.Ok)
            {
                if (gameMode == GameMode.Host)
                    SetStatus($"Room created successfully: {sessionName}");
                else
                    SetStatus($"Joined room successfully: {sessionName}");

                OnSessionStartedEvent?.Invoke();
            }
            else
            {
                SetStatus("Failed to create or join room: " + result.ShutdownReason);

                // 중요:
                // StartGame 실패 후 같은 Runner를 재사용하면 다음 시도에서 멈출 수 있음
                await ResetRunnerAsync();
            }
        }
        catch (Exception e)
        {
            SetStatus("Session start error: " + e.Message);

            // 예외가 나도 다음 시도 가능하게 Runner 초기화
            await ResetRunnerAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async void LeaveSession()
    {
        if (isBusy)
            return;

        SetBusy(true);
        SetStatus("Leaving session...");

        try
        {
            await ResetRunnerAsync();

            if (this == null)
                return;

            SetStatus("Session closed");
            OnSessionShutdownEvent?.Invoke();
        }
        catch (Exception e)
        {
            SetStatus("Leave session error: " + e.Message);
        }
        finally
        {
            if (this != null)
                SetBusy(false);
        }
    }

    private string NormalizeSessionName(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return "";

        return sessionName.Trim().ToUpper();
    }

    private void SetStatus(string message)
    {
        Debug.Log("[NetworkSessionManager] " + message);
        OnStatusChanged?.Invoke(message);
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        OnBusyStateChanged?.Invoke(!busy);
    }
    private async Task ResetRunnerAsync()
    {
        NetworkRunner oldRunner = runner;
        GameObject oldRunnerObject = runnerObject;

        runner = null;
        sceneManager = null;
        runnerObject = null;

        if (oldRunner != null)
        {
            oldRunner.RemoveCallbacks(this);

            try
            {
                if (oldRunner.IsRunning)
                {
                    await oldRunner.Shutdown(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NetworkSessionManager] Runner shutdown during reset failed: " + e.Message);
            }
        }

        if (oldRunnerObject != null)
        {
            Destroy(oldRunnerObject);
        }
        else if (oldRunner != null && oldRunner.gameObject != gameObject)
        {
            Destroy(oldRunner.gameObject);
        }

        await Task.Yield();

        if (this == null)
            return;

        Debug.Log("[NetworkSessionManager] Runner reset completed");
    }

    public int GetCurrentPlayerCount()
    {
        if (runner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    public bool IsHost()
    {
        if (runner == null)
            return false;

        return runner.IsServer;
    }

    public string GetCurrentSessionName()
    {
        if (runner == null)
            return "";

        if (runner.SessionInfo == null)
            return "";

        return runner.SessionInfo.Name;
    }

    // ==============================
    // Fusion Callbacks
    // ==============================

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessions.Clear();
        cachedSessions.AddRange(sessionList);

        SetStatus($"Session list updated: {cachedSessions.Count}");

        OnSessionListChanged?.Invoke(cachedSessions);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        SetStatus("Connected to server");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        SetStatus("Failed to connect to server: " + reason);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        SetStatus("Disconnected from server: " + reason);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        int currentCount = GetCurrentPlayerCount();

        SetStatus($"Player joined: {player} / Current players: {currentCount}/{maxPlayers}");

        OnPlayerJoinedEvent?.Invoke(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        int currentCount = GetCurrentPlayerCount();

        SetStatus($"Player left: {player} / Current players: {currentCount}/{maxPlayers}");

        OnPlayerLeftEvent?.Invoke(player);
    }

    public void OnShutdown(NetworkRunner shutdownRunner, ShutdownReason shutdownReason)
    {
        SetStatus("Runner shutdown: " + shutdownReason);

        if (runner != null && runner == shutdownRunner)
            runner.ProvideInput = false;

        // 여기서는 이벤트 호출하지 않음.
        // LeaveSession 또는 StartSession 실패 처리 쪽에서 직접 처리하게 둔다.
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    // INetworkRunnerCallbacks을 상속하고 있어서 필요한 함수들
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 여기서는 입력 처리 안 함.
        // 친구가 만든 NetworkInputManager가 실제 게임맵에서 처리할 예정.
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        SetStatus("Scene loading started");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        SetStatus("Scene loading completed");
    }
}