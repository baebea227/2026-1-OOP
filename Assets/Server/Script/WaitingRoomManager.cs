using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 대기방 관리 매니저
/// 
/// 담당 범위:
/// 1. 대기방에 들어온 플레이어 상태 생성
/// 2. 현재 방 코드 / 인원 표시
/// 3. Ready 버튼 처리
/// 4. 두 명 모두 Ready인지 확인
/// 5. Host만 게임 시작 가능하게 처리
/// 6. 게임 시작 시 GameScene으로 네트워크 씬 전환 요청
/// 
/// 주의:
/// 실제 게임맵에서 캐릭터를 스폰하거나 조작하는 기능은 이 스크립트의 책임이 아님.
/// </summary>
public class WaitingRoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Managers")]
    [SerializeField] private NetworkSessionManager networkSessionManager;
    [SerializeField] private SceneFlowManager sceneFlowManager;

    [Header("Fusion")]
    [SerializeField] private NetworkRunner runner;

    // 플레이어마다 Ready 상태를 저장할 네트워크 프리팹
    // 이 프리팹에는 NetworkObject + RoomPlayerState가 붙어 있어야 함
    [SerializeField] private NetworkObject roomPlayerStatePrefab;
    [SerializeField] private NetworkObject networkGameStatePrefab;
    private NetworkGameState gameState;

    [Header("UI")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text player1Text;
    [SerializeField] private TMP_Text player2Text;
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Button CopyButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text startGameButtonText;

    [Header("Setting")]
    // 2인 전용 게임이므로 최대 인원은 2명
    [SerializeField] private int maxPlayers = 2;

    // 현재 로컬 플레이어의 RoomPlayerState
    // 즉, 내가 Ready 버튼을 눌렀을 때 바꿀 대상
    private RoomPlayerState myPlayerState;

    private void Awake()
    {
        // 필요한 매니저와 NetworkRunner를 찾음
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();

        // Fusion 콜백 등록
        // 플레이어 입장, 퇴장, 씬 로딩 완료 같은 이벤트를 받기 위해 필요
        if (runner != null)
            runner.AddCallbacks(this);

        // UI 버튼 이벤트 연결
        if (readyButton != null)
            readyButton.onClick.AddListener(OnClickReady);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnClickStartGame);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnClickLeave);

        if (CopyButton != null)
            CopyButton.onClick.AddListener(OnClickCopyButton);
    }

    private void OnDisable()
    {
        // Fusion 콜백 해제
        if (runner != null)
            runner.RemoveCallbacks(this);

        // UI 버튼 이벤트 해제
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnClickReady);

        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnClickStartGame);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(OnClickLeave);

        if (CopyButton != null)
            CopyButton.onClick.RemoveListener(OnClickCopyButton);
    }

    private void Start()
    {
        FindReferences();

        // Host라면 현재 세션에 들어와 있는 플레이어들의 Ready 상태 오브젝트를 생성
        SpawnRoomPlayerStatesIfHost();
        SpawnNetworkGameStateIfHost();

        // 처음 대기방에 들어왔을 때 UI 갱신
        UpdateAllUI();

        SetStatus("Entered waiting room");
        LogRoomState("Start");
    }

    private void Update()
    {
        // Ready 상태는 네트워크 값이라 언제 바뀔지 모름
        // 그래서 매 프레임 UI를 갱신해서 상대방 Ready 상태도 반영되게 함
        UpdateAllUI();
    }

    /// <summary>
    /// 필요한 참조들을 자동으로 찾는 함수
    /// Inspector에서 직접 넣어도 되고, 없으면 씬에서 찾아옴
    /// </summary>
    private void FindReferences()
    {
        if (networkSessionManager == null)
            networkSessionManager = FindAnyObjectByType<NetworkSessionManager>(FindObjectsInactive.Include);

        if (sceneFlowManager == null)
            sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);

        if (runner == null)
        {
            if (networkSessionManager != null)
                runner = networkSessionManager.Runner;

            if (runner == null)
                runner = FindAnyObjectByType<NetworkRunner>();
        }
    }

    /// <summary>
    /// Host만 실행하는 함수
    /// 현재 세션에 들어와 있는 모든 플레이어에 대해 RoomPlayerState를 생성함
    /// 
    /// RoomPlayerState는 각 플레이어의 Ready 상태를 저장하는 네트워크 오브젝트임
    /// </summary>
    private void SpawnRoomPlayerStatesIfHost()
    {
        if (runner == null)
        {
            SetStatus("NetworkRunner not found");
            return;
        }

        // Client는 Spawn 권한이 없으므로 실행하지 않음
        if (!runner.IsServer)
            return;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            SpawnRoomPlayerStateIfNeeded(player);
        }
    }

    /// <summary>
    /// 특정 플레이어의 RoomPlayerState가 없으면 생성함
    /// 이미 있으면 중복 생성하지 않음
    /// </summary>
    private void SpawnRoomPlayerStateIfNeeded(PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        // 이미 해당 플레이어의 상태 오브젝트가 있으면 생성하지 않음
        if (FindRoomPlayerState(player) != null)
            return;

        if (roomPlayerStatePrefab == null)
        {
            SetStatus("RoomPlayerStatePrefab is not assigned");
            return;
        }

        // Fusion 네트워크 오브젝트 생성
        // 마지막 인자 player는 InputAuthority를 의미함
        // 즉, 해당 플레이어가 이 RoomPlayerState에 대해 Ready 변경 요청을 할 수 있음
        NetworkObject obj = runner.Spawn(
            roomPlayerStatePrefab,
            Vector3.zero,
            Quaternion.identity,
            player
        );

        RoomPlayerState state = obj.GetComponent<RoomPlayerState>();

        if (state == null)
        {
            SetStatus("RoomPlayerState component is missing on the prefab");
            return;
        }

        // 방을 만든 사람, 즉 Host인지 표시
        bool isHostPlayer = player == runner.LocalPlayer;

        // PlayerRef, Ready 초기값, Host 여부 설정
        state.Initialize(player, isHostPlayer);

        SetStatus($"Player state created: {player}");
    }
    
    private NetworkGameState FindNetworkGameState()
    {
        if (gameState != null)
            return gameState;

        gameState = FindAnyObjectByType<NetworkGameState>(FindObjectsInactive.Include);
        return gameState;
    }

    private void SpawnNetworkGameStateIfHost()
    {
        if (runner == null)
            return;

        if (!runner.IsServer)
            return;

        if (FindNetworkGameState() != null)
            return;

        if (networkGameStatePrefab == null)
        {
            SetStatus("NetworkGameStatePrefab is not assigned");
            return;
        }

        NetworkObject obj = runner.Spawn(
            networkGameStatePrefab,
            Vector3.zero,
            Quaternion.identity
        );

        gameState = obj.GetComponent<NetworkGameState>();

        if (gameState == null)
        {
            SetStatus("NetworkGameState component is missing on prefab");
            return;
        }

        gameState.SetWaiting();

        SetStatus("NetworkGameState created");
    }

    /// <summary>
    /// Ready 버튼 클릭 시 실행
    /// 내 RoomPlayerState를 찾아서 Ready 상태를 토글함
    /// </summary>
    private void OnClickReady()
    {
        FindMyPlayerState();

        if (myPlayerState == null)
        {
            SetStatus("My RoomPlayerState not found");
            return;
        }

        myPlayerState.RequestToggleReady();
    }

    /// <summary>
    /// 게임 시작 버튼 클릭 시 실행
    /// 
    /// 조건:
    /// 1. Host만 누를 수 있음
    /// 2. 플레이어가 2명이어야 함
    /// 3. 두 명 모두 Ready 상태여야 함
    /// 
    /// 조건이 맞으면 SceneFlowManager에게 GameScene으로 전환 요청
    /// </summary>
    private void OnClickStartGame()
    {
        NetworkGameState state = FindNetworkGameState();

        if (state == null)
        {
            SetStatus("NetworkGameState not found");
            return;
        }

        state.SetPlaying();
        if (runner == null)
            return;

        // 씬 전환은 Host/SceneAuthority만 가능
        if (!runner.IsSceneAuthority)
        {
            SetStatus("Only the host can start the game");
            return;
        }

        if (!CanStartGame())
        {
            SetStatus("Cannot start the game yet");
            return;
        }

        SetStatus("Starting game");

        // 여기까지만 네 담당 범위
        // 실제 GameScene 입장 후 캐릭터 스폰, 조작, 게임 규칙은 다른 담당자가 처리
        if (sceneFlowManager != null)
            sceneFlowManager.LoadGameSceneNetwork();
        else
            SetStatus("SceneFlowManager not found");
    }

    /// <summary>
    /// 방 나가기 버튼 클릭 시 실행
    /// 세션을 종료하고 로비 씬으로 돌아감
    /// </summary>
    private void OnClickLeave()
    {
        SetStatus("Leaving room");

        if (networkSessionManager != null)
            networkSessionManager.LeaveSession();

        if (sceneFlowManager != null)
            sceneFlowManager.LoadLobbySceneLocal();
    }

    /// <summary>
    /// Copy 버튼 클릭 시 현재 방 코드를 클립보드에 복사함
    /// </summary>
    private void OnClickCopyButton()
    {
        if (runner == null || runner.SessionInfo == null)
        {
            SetStatus("Room code not found");
            return;
        }

        string roomCode = runner.SessionInfo.Name;

        GUIUtility.systemCopyBuffer = roomCode;

        SetStatus("Room code copied: " + roomCode);
    }

    /// <summary>
    /// 현재 로컬 플레이어가 조작 권한을 가진 RoomPlayerState를 찾음
    /// Ready 버튼을 눌렀을 때 이 객체의 Ready 상태를 바꿈
    /// </summary>
    private void FindMyPlayerState()
    {
        if (runner == null)
            return;

        RoomPlayerState[] states = FindObjectsByType<RoomPlayerState>(
            FindObjectsSortMode.None
        );

        foreach (RoomPlayerState state in states)
        {
            if (state.Object == null)
                continue;

            if (state.Object.HasInputAuthority)
            {
                myPlayerState = state;
                return;
            }
        }

        myPlayerState = null;
    }

    /// <summary>
    /// 특정 PlayerRef에 해당하는 RoomPlayerState를 찾음
    /// 중복 생성 방지용으로 사용
    /// </summary>
    private RoomPlayerState FindRoomPlayerState(PlayerRef player)
    {
        RoomPlayerState[] states = FindObjectsByType<RoomPlayerState>(
            FindObjectsSortMode.None
        );

        foreach (RoomPlayerState state in states)
        {
            if (state.Object == null)
                continue;

            if (state.Player == player)
                return state;
        }

        return null;
    }

    /// <summary>
    /// 현재 대기방에 존재하는 모든 RoomPlayerState를 가져옴
    /// UI 표시와 게임 시작 조건 검사에 사용
    /// </summary>
    private List<RoomPlayerState> GetRoomPlayerStates()
    {
        RoomPlayerState[] states = FindObjectsByType<RoomPlayerState>(
            FindObjectsSortMode.None
        );

        List<RoomPlayerState> result = new List<RoomPlayerState>();

        foreach (RoomPlayerState state in states)
        {
            if (state.Object == null)
                continue;

            if (state.Player != PlayerRef.None)
                result.Add(state);
        }

        // 표시 순서를 일정하게 하기 위해 PlayerRef 기준 정렬
        result.Sort((a, b) => a.Player.RawEncoded.CompareTo(b.Player.RawEncoded));

        return result;
    }

    /// <summary>
    /// 게임 시작 가능 여부 검사
    /// 
    /// 조건:
    /// 1. 플레이어 수가 maxPlayers와 같아야 함
    /// 2. 모든 플레이어가 Ready 상태여야 함
    /// </summary>
    private bool CanStartGame()
    {
        List<RoomPlayerState> states = GetRoomPlayerStates();

        if (states.Count != maxPlayers)
            return false;

        foreach (RoomPlayerState state in states)
        {
            if (!state.IsReady)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 대기방 UI 전체 갱신
    /// </summary>
    private void UpdateAllUI()
    {
        FindReferences();
        FindMyPlayerState();

        UpdateRoomInfoUI();
        UpdatePlayerListUI();
        UpdateButtonUI();
    }

    /// <summary>
    /// 방 코드와 현재 인원 UI 갱신
    /// </summary>
    private void UpdateRoomInfoUI()
    {
        if (runner == null)
            return;

        if (roomNameText != null)
        {
            if (runner.SessionInfo != null)
                roomNameText.text = "Room Code: " + runner.SessionInfo.Name;
            else
                roomNameText.text = "Room Code: Unknown";
        }

        if (playerCountText != null)
        {
            int count = GetCurrentPlayerCount();
            playerCountText.text = $"Players: {count}/{maxPlayers}";
        }
    }

    /// <summary>
    /// Player 1, Player 2의 Ready 상태 UI 갱신
    /// </summary>
    private void UpdatePlayerListUI()
    {
        List<RoomPlayerState> states = GetRoomPlayerStates();

        if (player1Text != null)
            player1Text.text = "Player 1: Empty";

        if (player2Text != null)
            player2Text.text = "Player 2: Empty";

        for (int i = 0; i < states.Count; i++)
        {
            RoomPlayerState state = states[i];

            string hostText = state.IsHostPlayer ? "Host" : "Client";
            string readyText = state.IsReady ? "Ready" : "Not Ready";
            string myText = state.Object != null && state.Object.HasInputAuthority ? " / Me" : "";

            string line = $"Player {i + 1}: {hostText} / {readyText}{myText}";

            if (i == 0 && player1Text != null)
                player1Text.text = line;

            if (i == 1 && player2Text != null)
                player2Text.text = line;
        }
    }

    /// <summary>
    /// Ready 버튼과 Start Game 버튼 상태 갱신
    /// </summary>
    private void UpdateButtonUI()
    {
        bool hasMyState = myPlayerState != null;

        if (readyButton != null)
            readyButton.interactable = hasMyState;

        if (readyButtonText != null)
        {
            if (myPlayerState != null && myPlayerState.IsReady)
                readyButtonText.text = "Cancel Ready";
            else
                readyButtonText.text = "Ready";
        }

        // Host만 게임 시작 버튼이 보이게 함
        bool isHost = runner != null && runner.IsSceneAuthority;

        // Host이고, 두 명 모두 Ready이면 시작 가능
        bool canStart = isHost && CanStartGame();

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = canStart;
        }

        if (startGameButtonText != null)
        {
            if (canStart)
                startGameButtonText.text = "Start Game";
            else
                startGameButtonText.text = "Waiting...";
        }
    }

    /// <summary>
    /// 현재 세션에 접속한 플레이어 수 계산
    /// </summary>
    private int GetCurrentPlayerCount()
    {
        if (runner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player in runner.ActivePlayers)
            count++;

        return count;
    }

    /// <summary>
    /// 상태 메시지 출력
    /// Debug.Log와 UI Text에 동시에 표시
    /// </summary>
    private void SetStatus(string message)
    {
        Debug.Log("[WaitingRoomManager] " + message);

        if (statusText != null)
            statusText.text = message;
    }

    private void LogRoomState(string context)
    {
        LogRoomState(context, runner);
    }

    private void LogRoomState(string context, NetworkRunner sourceRunner)
    {
        NetworkRunner activeRunner = sourceRunner != null ? sourceRunner : runner;
        string activeScene = SceneManager.GetActiveScene().name;
        int roomStateCount = FindObjectsByType<RoomPlayerState>(FindObjectsSortMode.None).Length;
        int activePlayerCount = CountActivePlayers(activeRunner);
        string sessionName = activeRunner != null && activeRunner.SessionInfo != null ? activeRunner.SessionInfo.Name : "null";

        Debug.Log(
            $"[WaitingRoomManager][Diagnostics:{context}] " +
            $"runner={(activeRunner != null ? activeRunner.name : "null")}, running={(activeRunner != null && activeRunner.IsRunning)}, " +
            $"server={(activeRunner != null && activeRunner.IsServer)}, sceneAuthority={(activeRunner != null && activeRunner.IsSceneAuthority)}, " +
            $"localPlayer={(activeRunner != null ? activeRunner.LocalPlayer.ToString() : "null")}, session={sessionName}, " +
            $"activePlayers={activePlayerCount}, roomStates={roomStateCount}, hasMyState={myPlayerState != null}, " +
            $"hasGameState={FindNetworkGameState() != null}, activeScene={activeScene}"
        );
    }

    private int CountActivePlayers(NetworkRunner sourceRunner)
    {
        if (sourceRunner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player in sourceRunner.ActivePlayers)
            count++;

        return count;
    }

    // ==============================
    // Fusion Callbacks
    // ==============================

    /// <summary>
    /// 새로운 플레이어가 세션에 들어왔을 때 호출됨
    /// Host라면 해당 플레이어의 RoomPlayerState를 생성함
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        this.runner = runner;

        SetStatus($"Player joined: {player}");
        LogRoomState($"OnPlayerJoined player={player}", runner);

        if (runner.IsServer)
            SpawnRoomPlayerStateIfNeeded(player);

        UpdateAllUI();
    }

    /// <summary>
    /// 플레이어가 나갔을 때 호출됨
    /// 현재는 UI만 갱신함
    /// </summary>
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        this.runner = runner;

        SetStatus($"Player left: {player}");
        LogRoomState($"OnPlayerLeft player={player}", runner);
        UpdateAllUI();
    }

    /// <summary>
    /// 씬 로딩이 끝났을 때 호출됨
    /// 대기방 씬에 들어온 직후 기존 플레이어들의 상태 오브젝트를 생성하기 위해 사용
    /// </summary>
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        this.runner = runner;
        LogRoomState("OnSceneLoadDone before sync", runner);

        SpawnRoomPlayerStatesIfHost();
        SpawnNetworkGameStateIfHost();
        FindNetworkGameState();
        UpdateAllUI();
        LogRoomState("OnSceneLoadDone after sync", runner);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        this.runner = runner;
        LogRoomState("OnSceneLoadStart", runner);
    }

    // 입력 처리는 친구가 만든 NetworkInputManager가 담당하므로 여기서는 사용하지 않음
    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    // 입력 누락 처리. 현재는 사용하지 않음
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    /// <summary>
    /// 세션이 종료되었을 때 호출됨
    /// </summary>
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SetStatus("Session ended: " + shutdownReason);
        LogRoomState($"OnShutdown reason={shutdownReason}", runner);
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        LogRoomState("OnConnectedToServer", runner);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        SetStatus("Disconnected from server: " + reason);
        LogRoomState($"OnDisconnectedFromServer reason={reason}", runner);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        SetStatus("Failed to connect to server: " + reason);
        Debug.Log($"[WaitingRoomManager][Diagnostics:OnConnectFailed] remote={remoteAddress}, reason={reason}");
        LogRoomState("OnConnectFailed", runner);
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
