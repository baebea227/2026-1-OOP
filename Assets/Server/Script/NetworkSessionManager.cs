using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Fusion")]
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkSceneManagerDefault sceneManager;

    [Header("Session Setting")]
    [SerializeField] private string lobbyName = "MainLobby";
    [SerializeField] private int maxPlayers = 2;

    [Header("Scene Setting")]
    [Tooltip("Waiting room scene Build Index after creating or joining a session")]
    [SerializeField] private int waitingRoomSceneBuildIndex = 1;

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
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        SetupRunner();
    }

    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }
    }

    private void SetupRunner()
    {
        if (runner == null)
        {
            runner = GetComponent<NetworkRunner>();
        }

        if (runner == null)
        {
            runner = gameObject.AddComponent<NetworkRunner>();
        }

        if (sceneManager == null)
        {
            sceneManager = GetComponent<NetworkSceneManagerDefault>();
        }

        if (sceneManager == null)
        {
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        // 로비에서는 입력 받을 필요 없음.
        // 실제 게임맵에 들어간 뒤 NetworkInputManager가 입력을 담당함.
        runner.ProvideInput = false;
    }

    public async void JoinLobby()
    {
        await JoinLobbyAsync();
    }

    private async Task JoinLobbyAsync()
    {
        if (isBusy)
            return;

        SetBusy(true);
        SetStatus("Connecting to Photon lobby...");

        SetupRunner();

        var result = await runner.JoinSessionLobby(SessionLobby.Custom, lobbyName);

        if (result.Ok)
        {
            SetStatus("Connected to Photon lobby. Waiting for the session list...");
        }
        else
        {
            SetStatus("Failed to connect to Photon lobby: " + result.ShutdownReason);
        }

        SetBusy(false);
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
        SetupRunner();

        runner.ProvideInput = true;

        if (gameMode == GameMode.Host)
        {
            SetStatus($"Creating room: {sessionName}");
        }
        else
        {
            SetStatus($"Joining room: {sessionName}");
        }

        StartGameArgs args = new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,

            // 이 이름이 같아야 같은 로비의 방 목록에서 보임
            CustomLobbyName = lobbyName,

            // 2인 전용
            PlayerCount = maxPlayers,

            // 방 참가 가능 / 로비 목록 표시
            IsOpen = true,
            IsVisible = true,

            // 대기방 씬 로딩
            Scene = CreateSceneInfo(waitingRoomSceneBuildIndex),
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
            {
                SetStatus($"Room created successfully: {sessionName}");
            }
            else
            {
                SetStatus($"Joined room successfully: {sessionName}");
            }

            OnSessionStartedEvent?.Invoke();
        }
        else
        {
            SetStatus("Failed to create or join room: " + result.ShutdownReason);

            // 실패한 NetworkRunner는 재사용하면 꼬일 수 있음.
            // 일단 버튼은 다시 살리고, 개발 중에는 Play 재시작하는 게 안전함.
            runner.ProvideInput = false;
        }

        SetBusy(false);
    }

    public async void LeaveSession()
    {
        if (runner == null)
        {
            SetStatus("No session to leave");
            return;
        }

        if (isBusy)
            return;

        SetBusy(true);
        SetStatus("Leaving session...");

        await runner.Shutdown();

        SetStatus("Session closed");
        SetBusy(false);

        OnSessionShutdownEvent?.Invoke();
    }

    private string NormalizeSessionName(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return "";

        return sessionName.Trim().ToUpper();
    }

    private NetworkSceneInfo CreateSceneInfo(int sceneBuildIndex)
    {
        SceneRef sceneRef = SceneRef.FromIndex(sceneBuildIndex);

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

        return sceneInfo;
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

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SetStatus("Runner shutdown: " + shutdownReason);

        this.runner.ProvideInput = false;

        OnSessionShutdownEvent?.Invoke();
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