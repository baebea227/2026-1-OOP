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
    private const string LobbySceneName = "LobbyScene";

    [Header("Fusion")]
    public static NetworkSessionManager Instance { get; private set; }
    [SerializeField] private NetworkRunner runnerPrefab;
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private GameObject runnerObject;

    [Header("Session Setting")]
    [SerializeField] private string lobbyName = "MainLobby";
    [Min(1)]
    [Tooltip("Room capacity. The game starts when this many players are ready.")]
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
    private bool isSessionActive = false;
    private bool isHandlingRemoteSessionEnd = false;

    public NetworkRunner Runner => runner;
    public IReadOnlyList<SessionInfo> CachedSessions => cachedSessions;
    public bool IsBusy => isBusy;
    public int MaxPlayers => GetConfiguredMaxPlayers();

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

        ResolveSceneFlowManager();

        Debug.Log("[NetworkSessionManager] Awake completed: " + gameObject.name);
    }
    
    private void OnDestroy()
    {
        Debug.Log("[NetworkSessionManager] OnDestroy called: " + gameObject.name);

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

        runnerObject = CreateRunnerObject();

        // NetworkCore의 자식으로 두지 않는다.
        // 따로 DontDestroyOnLoad 루트 오브젝트로 둔다.
        DontDestroyOnLoad(runnerObject);

        runner = runnerObject.GetComponent<NetworkRunner>();
        if (runner == null)
            runner = runnerObject.AddComponent<NetworkRunner>();

        sceneManager = runnerObject.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        // 게임 씬 물리 동기화용
        EnsurePhysicsSimulator(runnerObject);

        runner.ProvideInput = false;

        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        Debug.Log("[NetworkSessionManager] NetworkRunner_Runtime created");
        LogRunnerState("SetupRunner created");
    }

    private GameObject CreateRunnerObject()
    {
        GameObject createdObject;

        if (runnerPrefab != null)
        {
            createdObject = Instantiate(runnerPrefab.gameObject);
            Debug.Log("[NetworkSessionManager] NetworkRunner_Runtime instantiated from prefab: " + runnerPrefab.name);
        }
        else
        {
            createdObject = new GameObject("NetworkRunner_Runtime");
            Debug.LogWarning("[NetworkSessionManager] runnerPrefab is not assigned. Creating NetworkRunner_Runtime from code.");
        }

        createdObject.name = "NetworkRunner_Runtime";
        return createdObject;
    }

    private void EnsurePhysicsSimulator(GameObject targetObject)
    {
        RunnerSimulatePhysics3D physicsSimulator = targetObject.GetComponent<RunnerSimulatePhysics3D>();

        if (physicsSimulator == null)
        {
            physicsSimulator = targetObject.AddComponent<RunnerSimulatePhysics3D>();
            physicsSimulator.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateForward;
            Debug.LogWarning("[NetworkSessionManager] RunnerSimulatePhysics3D missing on runner prefab. Added with SimulateForward.");
        }

        if (physicsSimulator.ClientPhysicsSimulation == ClientPhysicsSimulation.Disabled)
        {
            Debug.LogWarning("[NetworkSessionManager] RunnerSimulatePhysics3D ClientPhysicsSimulation is Disabled. Client-side physics interactions may not raycast or predict correctly.");
        }
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
        int configuredMaxPlayers = GetConfiguredMaxPlayers();

        Debug.Log($"[NetworkSessionManager][Diagnostics:StartSessionBegin] mode={gameMode}, session={sessionName}, lobby={lobbyName}, maxPlayers={configuredMaxPlayers}");

        try
        {
            SetupRunner();

            if (!ResolveSceneFlowManager())
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

                PlayerCount = configuredMaxPlayers,

                IsOpen = true,
                IsVisible = true,

                SceneManager = sceneManager
            };

            if (gameMode != GameMode.Client)
                args.Scene = sceneFlowManager.GetWaitingRoomSceneInfo();

            // Client가 없는 방을 실수로 새로 만들지 못하게 막음
            if (gameMode == GameMode.Client)
            {
                args.EnableClientSessionCreation = false;
            }

            Debug.Log($"[NetworkSessionManager][Diagnostics:BeforeStartGame] mode={gameMode}, session={sessionName}, sceneAssigned={gameMode != GameMode.Client}, clientSessionCreation={args.EnableClientSessionCreation}");
            LogRunnerState("Before StartGame");

            var result = await runner.StartGame(args);

            Debug.Log($"[NetworkSessionManager][Diagnostics:StartGameResult] ok={result.Ok}, reason={result.ShutdownReason}, mode={gameMode}, session={sessionName}");
            LogRunnerState("After StartGame");

            if (result.Ok)
            {
                isSessionActive = true;

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
            Debug.LogException(e);

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
        await LeaveSessionAsync();
    }

    public async Task LeaveSessionAsync()
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

    public void ConfigureMaxPlayers(int playerCount)
    {
        maxPlayers = Mathf.Max(1, playerCount);
    }

    private int GetConfiguredMaxPlayers()
    {
        return Mathf.Max(1, maxPlayers);
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

    private bool ResolveSceneFlowManager()
    {
        if (sceneFlowManager != null)
            return true;

        sceneFlowManager = GetComponent<SceneFlowManager>();

        if (sceneFlowManager == null)
            sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);

        return sceneFlowManager != null;
    }

    private void LogRunnerState(string context)
    {
        LogRunnerState(context, runner);
    }

    private void LogRunnerState(string context, NetworkRunner targetRunner)
    {
        string activeScene = SceneManager.GetActiveScene().name;

        if (targetRunner == null)
        {
            Debug.Log($"[NetworkSessionManager][Diagnostics:{context}] runner=null, activeScene={activeScene}");
            return;
        }

        string sessionName = targetRunner.SessionInfo != null ? targetRunner.SessionInfo.Name : "null";

        Debug.Log(
            $"[NetworkSessionManager][Diagnostics:{context}] " +
            $"runner={targetRunner.name}, running={targetRunner.IsRunning}, server={targetRunner.IsServer}, client={targetRunner.IsClient}, " +
            $"sceneAuthority={targetRunner.IsSceneAuthority}, localPlayer={targetRunner.LocalPlayer}, session={sessionName}, " +
            $"activePlayers={CountActivePlayers(targetRunner)}, activeScene={activeScene}"
        );
    }

    private int CountActivePlayers(NetworkRunner targetRunner)
    {
        if (targetRunner == null)
            return 0;

        if (!targetRunner.IsRunning)
            return 0;

        int count = 0;

        try
        {
            foreach (PlayerRef player in targetRunner.ActivePlayers)
                count++;
        }
        catch (KeyNotFoundException exception)
        {
            Debug.LogWarning(
                $"[NetworkSessionManager] ActivePlayers could not be read while runner state was changing: {exception.Message}"
            );
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning(
                $"[NetworkSessionManager] ActivePlayers changed while being read: {exception.Message}"
            );
            return 0;
        }

        return count;
    }

    private async Task ResetRunnerAsync(bool shutdownRunner = true)
    {
        isSessionActive = false;

        NetworkRunner oldRunner = runner;
        GameObject oldRunnerObject = runnerObject;

        LogRunnerState("ResetRunner begin", oldRunner);

        runner = null;
        sceneManager = null;
        runnerObject = null;

        if (oldRunner != null)
        {
            oldRunner.RemoveCallbacks(this);

            try
            {
                if (shutdownRunner && oldRunner.IsRunning)
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

        return CountActivePlayers(runner);
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

        foreach (SessionInfo session in cachedSessions)
        {
            Debug.Log(
                $"[NetworkSessionManager][Diagnostics:SessionListItem] " +
                $"name={session.Name}, players={session.PlayerCount}/{session.MaxPlayers}, open={session.IsOpen}, visible={session.IsVisible}, valid={session.IsValid}"
            );
        }

        OnSessionListChanged?.Invoke(cachedSessions);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        SetStatus("Connected to server");
        LogRunnerState("OnConnectedToServer", runner);
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        SetStatus("Failed to connect to server: " + reason);
        Debug.Log($"[NetworkSessionManager][Diagnostics:OnConnectFailed] remote={remoteAddress}, reason={reason}");
        LogRunnerState("OnConnectFailed", runner);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        SetStatus("Disconnected from server: " + reason);
        Debug.Log($"[NetworkSessionManager][Diagnostics:OnDisconnectedFromServer] reason={reason}");
        LogRunnerState("OnDisconnectedFromServer", runner);

        HandleRemoteSessionEnd(runner, "server disconnect: " + reason);
    }

    private async void HandleRemoteSessionEnd(NetworkRunner endedRunner, string reason, bool shutdownRunner = true)
    {
        if (isHandlingRemoteSessionEnd)
            return;

        if (runner != null && runner != endedRunner)
            return;

        isHandlingRemoteSessionEnd = true;
        SetBusy(true);

        try
        {
            await ResetRunnerAsync(shutdownRunner);

            if (this == null)
                return;

            SetStatus("Session closed by host: " + reason);
            OnSessionShutdownEvent?.Invoke();

            LoadLobbySceneAfterSessionEnd();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[NetworkSessionManager] Remote disconnect cleanup failed: " + exception.Message);
        }
        finally
        {
            if (this != null)
            {
                isHandlingRemoteSessionEnd = false;
                SetBusy(false);
            }
        }
    }

    private void LoadLobbySceneAfterSessionEnd()
    {
        if (ResolveSceneFlowManager())
        {
            sceneFlowManager.LoadLobbySceneLocal();
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(LobbySceneName))
        {
            SceneManager.LoadScene(LobbySceneName);
            return;
        }

        SceneManager.LoadScene(0);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        int currentCount = GetCurrentPlayerCount();

        SetStatus($"Player joined: {player} / Current players: {currentCount}/{GetConfiguredMaxPlayers()}");
        LogRunnerState($"OnPlayerJoined player={player}", runner);

        OnPlayerJoinedEvent?.Invoke(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        int currentCount = GetCurrentPlayerCount();

        SetStatus($"Player left: {player} / Current players: {currentCount}/{GetConfiguredMaxPlayers()}");
        LogRunnerState($"OnPlayerLeft player={player}", runner);

        OnPlayerLeftEvent?.Invoke(player);
    }

    public void OnShutdown(NetworkRunner shutdownRunner, ShutdownReason shutdownReason)
    {
        SetStatus("Runner shutdown: " + shutdownReason);
        Debug.Log($"[NetworkSessionManager][Diagnostics:OnShutdown] reason={shutdownReason}");
        LogRunnerState("OnShutdown", shutdownRunner);

        if (runner != null && runner == shutdownRunner)
            runner.ProvideInput = false;

        if (isSessionActive)
            HandleRemoteSessionEnd(shutdownRunner, "runner shutdown: " + shutdownReason, false);
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
        LogRunnerState("OnSceneLoadStart", runner);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        SetStatus("Scene loading completed");
        LogRunnerState("OnSceneLoadDone", runner);
    }
}
