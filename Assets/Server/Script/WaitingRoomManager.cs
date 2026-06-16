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
    [SerializeField] private List<TMP_Text> playerStateTexts = new List<TMP_Text>();
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Button CopyButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text startGameButtonText;

    [Header("Stage Selection")]
    [SerializeField] private GameObject stageSelectionPanel;
    [SerializeField] private Button stageAButton;
    [SerializeField] private Button stageBButton;
    [SerializeField] private TMP_Text stageSelectionTitleText;
    [SerializeField] private TMP_Text stageAButtonText;
    [SerializeField] private TMP_Text stageBButtonText;
    [SerializeField] private string stageAKey = "StageA";
    [SerializeField] private string stageBKey = "StageB";

    [Header("Player State Icons")]
    [SerializeField] private string joinedPlayerIconResourcePath = "WaitingRoomIcons/blue_filled_icon";
    [SerializeField] private string emptyPlayerIconResourcePath = "WaitingRoomIcons/blue_outline_icon";
    [SerializeField] private Vector2 playerStateIconSize = new Vector2(58f, 58f);
    [SerializeField] private float playerStateIconGap = 10f;
    [SerializeField] private List<Image> playerStateIcons = new List<Image>();
    [SerializeField] private Vector2 playerReadyLabelSize = new Vector2(130f, 32f);
    [SerializeField] private float playerReadyLabelGap = 20f;
    [SerializeField] private List<TMP_Text> playerReadyLabels = new List<TMP_Text>();

    private const int DefaultMaxPlayers = 2;
    private const float CopyButtonGap = 8f;
    private const string StageSelectionPanelName = "StageSelectionPanel";
    private const string StageSelectionWindowName = "StageSelectionWindow";
    private const string StageAButtonName = "StageAButton";
    private const string StageBButtonName = "StageBButton";

    private Sprite joinedPlayerIconSprite;
    private Sprite emptyPlayerIconSprite;
    private bool playerStateIconsLoaded;
    private readonly HashSet<PlayerRef> pendingRoomPlayerStateSpawns = new HashSet<PlayerRef>();
    private bool pendingNetworkGameStateSpawn;

    // 현재 로컬 플레이어의 RoomPlayerState
    // 즉, 내가 Ready 버튼을 눌렀을 때 바꿀 대상
    private RoomPlayerState myPlayerState;

    private void Awake()
    {
        // 필요한 매니저와 NetworkRunner를 찾음
        FindReferences();
        EnsureStageSelectionUI();
        HideStageSelectionPanel();
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

        EnsureStageSelectionUI();

        if (stageAButton != null)
            stageAButton.onClick.AddListener(OnClickStageA);

        if (stageBButton != null)
            stageBButton.onClick.AddListener(OnClickStageB);
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

        if (stageAButton != null)
            stageAButton.onClick.RemoveListener(OnClickStageA);

        if (stageBButton != null)
            stageBButton.onClick.RemoveListener(OnClickStageB);
    }

    private void Start()
    {
        FindReferences();

        // Host라면 현재 세션에 들어와 있는 플레이어들의 Ready 상태 오브젝트를 생성
        SpawnRoomPlayerStatesIfHost();
        SpawnNetworkGameStateIfHost();

        // 처음 대기방에 들어왔을 때 UI 갱신
        EnsureStageSelectionUI();
        HideStageSelectionPanel();
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

        List<PlayerRef> activePlayers = GetActivePlayersSnapshot(runner, "SpawnRoomPlayerStatesIfHost");

        foreach (PlayerRef player in activePlayers)
        {
            SpawnRoomPlayerStateIfNeeded(player);
        }
    }

    /// <summary>
    /// 특정 플레이어의 RoomPlayerState가 없으면 생성함
    /// 이미 있으면 중복 생성하지 않음
    /// </summary>
    private async void SpawnRoomPlayerStateIfNeeded(PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        // 이미 해당 플레이어의 상태 오브젝트가 있으면 생성하지 않음
        if (FindRoomPlayerState(player) != null)
            return;

        if (pendingRoomPlayerStateSpawns.Contains(player))
            return;

        if (roomPlayerStatePrefab == null)
        {
            SetStatus("RoomPlayerStatePrefab is not assigned");
            return;
        }

        pendingRoomPlayerStateSpawns.Add(player);

        try
        {
            // Fusion may need a frame to finish loading registered prefabs.
            NetworkObject obj = await runner.SpawnAsync(
                roomPlayerStatePrefab,
                Vector3.zero,
                Quaternion.identity,
                player
            );

            if (obj == null)
            {
                SetStatus("RoomPlayerState spawn failed");
                return;
            }

            RoomPlayerState state = obj.GetComponent<RoomPlayerState>();

            if (state == null)
            {
                SetStatus("RoomPlayerState component is missing on the prefab");
                return;
            }

            // 방을 만든 사람, 즉 Host인지 표시
            bool isHostPlayer = runner != null && player == runner.LocalPlayer;

            // PlayerRef, Ready 초기값, Host 여부 설정
            state.Initialize(player, isHostPlayer);

            SetStatus($"Player state created: {player}");
            UpdateAllUI();
        }
        catch (Exception exception)
        {
            SetStatus("RoomPlayerState spawn failed");
            Debug.LogError($"[WaitingRoomManager] RoomPlayerState spawn failed for {player}: {exception}");
        }
        finally
        {
            pendingRoomPlayerStateSpawns.Remove(player);
        }
    }
    
    private NetworkGameState FindNetworkGameState()
    {
        if (gameState != null)
            return gameState;

        gameState = FindAnyObjectByType<NetworkGameState>(FindObjectsInactive.Include);
        return gameState;
    }

    private NetworkGameState SpawnNetworkGameStateIfHost()
    {
        NetworkGameState existingState = FindNetworkGameState();

        if (existingState != null)
            return existingState;

        if (runner == null)
            return null;

        if (!runner.IsServer)
            return null;

        if (networkGameStatePrefab == null)
        {
            SetStatus("NetworkGameStatePrefab is not assigned");
            return null;
        }

        if (!pendingNetworkGameStateSpawn)
            SpawnNetworkGameStateAsync();

        return FindNetworkGameState();
    }

    private async void SpawnNetworkGameStateAsync()
    {
        if (pendingNetworkGameStateSpawn)
            return;

        if (runner == null || !runner.IsServer)
            return;

        if (networkGameStatePrefab == null)
        {
            SetStatus("NetworkGameStatePrefab is not assigned");
            return;
        }

        pendingNetworkGameStateSpawn = true;

        try
        {
            NetworkObject obj = await runner.SpawnAsync(
                networkGameStatePrefab,
                Vector3.zero,
                Quaternion.identity
            );

            if (obj == null)
            {
                SetStatus("NetworkGameState spawn failed");
                return;
            }

            gameState = obj.GetComponent<NetworkGameState>();

            if (gameState == null)
            {
                SetStatus("NetworkGameState component is missing on prefab");
                return;
            }

            gameState.SetWaiting();

            SetStatus("NetworkGameState created");
            UpdateAllUI();
        }
        catch (Exception exception)
        {
            SetStatus("NetworkGameState spawn failed");
            Debug.LogError($"[WaitingRoomManager] NetworkGameState spawn failed: {exception}");
        }
        finally
        {
            pendingNetworkGameStateSpawn = false;
        }
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
    /// 조건이 맞으면 스테이지 선택 UI를 표시
    /// </summary>
    private void OnClickStartGame()
    {
        FindReferences();

        if (runner == null)
        {
            SetStatus("NetworkRunner not found");
            return;
        }

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

        ShowStageSelectionPanel();
        SetStatus("Select a stage");
    }

    private void OnClickStageA()
    {
        OnClickStage(stageAKey, "Stage A");
    }

    private void OnClickStageB()
    {
        OnClickStage(stageBKey, "Stage B");
    }

    private void OnClickStage(string stageKey, string stageLabel)
    {
        FindReferences();

        if (runner == null)
        {
            SetStatus("NetworkRunner not found");
            return;
        }

        if (!runner.IsSceneAuthority)
        {
            SetStatus("Only the host can select a stage");
            return;
        }

        if (!CanStartGame())
        {
            HideStageSelectionPanel();
            SetStatus("Cannot start the game yet");
            return;
        }

        if (string.IsNullOrWhiteSpace(stageKey))
        {
            SetStatus("Stage scene key is empty");
            return;
        }

        if (sceneFlowManager == null)
        {
            SetStatus("SceneFlowManager not found");
            return;
        }

        NetworkGameState state = SpawnNetworkGameStateIfHost();

        if (state == null)
        {
            SetStatus("NetworkGameState is still loading");
            return;
        }

        state.SetPlaying();
        HideStageSelectionPanel();
        SetStatus($"Starting {stageLabel}");
        sceneFlowManager.LoadStageSceneNetwork(stageKey);
    }

    /// <summary>
    /// 방 나가기 버튼 클릭 시 실행
    /// 세션을 종료하고 로비 씬으로 돌아감
    /// </summary>
    private async void OnClickLeave()
    {
        SetStatus("Leaving room");

        if (networkSessionManager != null)
            await networkSessionManager.LeaveSessionAsync();

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

    private void DespawnRoomPlayerStateIfHost(PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return;

        RoomPlayerState state = FindRoomPlayerState(player);

        if (state == null || state.Object == null)
            return;

        runner.Despawn(state.Object);
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
    /// 1. 플레이어 수가 세션의 최대 인원과 같아야 함
    /// 2. 모든 플레이어가 Ready 상태여야 함
    /// </summary>
    private bool CanStartGame()
    {
        List<RoomPlayerState> states = GetRoomPlayerStates();
        int requiredPlayers = GetRequiredPlayerCount();

        if (states.Count != requiredPlayers)
            return false;

        foreach (RoomPlayerState state in states)
        {
            if (!state.IsReady)
                return false;
        }

        return true;
    }

    private void EnsureStageSelectionUI()
    {
        if (stageSelectionPanel != null)
        {
            CacheStageSelectionUIReferences();
            ConfigureStageSelectionText();
            return;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (canvas == null)
            return;

        Transform existingPanel = canvas.transform.Find(StageSelectionPanelName);

        if (existingPanel != null)
        {
            stageSelectionPanel = existingPanel.gameObject;
            CacheStageSelectionUIReferences();
            ConfigureStageSelectionText();
            return;
        }

        stageSelectionPanel = CreateStageSelectionPanel(canvas);
        CacheStageSelectionUIReferences();
        ConfigureStageSelectionText();
    }

    private GameObject CreateStageSelectionPanel(Canvas canvas)
    {
        GameObject panelObject = new GameObject(
            StageSelectionPanelName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.62f);
        panelImage.raycastTarget = true;

        GameObject windowObject = new GameObject(
            StageSelectionWindowName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        windowObject.layer = canvas.gameObject.layer;
        windowObject.transform.SetParent(panelObject.transform, false);

        RectTransform windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(560f, 300f);
        windowRect.anchoredPosition = Vector2.zero;

        Image windowImage = windowObject.GetComponent<Image>();
        windowImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        windowImage.raycastTarget = true;

        stageSelectionTitleText = CreateStageSelectionText(
            "StageSelectionTitle",
            windowObject.transform,
            "Select Stage",
            38f,
            new Vector2(0f, 82f),
            new Vector2(480f, 62f)
        );

        stageAButton = CreateStageSelectionButton(
            StageAButtonName,
            windowObject.transform,
            "Stage A",
            new Vector2(-120f, -45f)
        );

        stageBButton = CreateStageSelectionButton(
            StageBButtonName,
            windowObject.transform,
            "Stage B",
            new Vector2(120f, -45f)
        );

        stageAButtonText = stageAButton.GetComponentInChildren<TMP_Text>(true);
        stageBButtonText = stageBButton.GetComponentInChildren<TMP_Text>(true);

        panelObject.SetActive(false);
        return panelObject;
    }

    private TMP_Text CreateStageSelectionText(
        string objectName,
        Transform parent,
        string text,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );

        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = anchoredPosition;

        TMP_Text tmpText = textObject.GetComponent<TextMeshProUGUI>();
        CopyStageSelectionFont(tmpText);
        tmpText.text = text;
        tmpText.color = Color.white;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.fontSize = fontSize;
        tmpText.enableAutoSizing = true;
        tmpText.fontSizeMin = Mathf.Max(12f, fontSize * 0.5f);
        tmpText.fontSizeMax = fontSize;
        tmpText.raycastTarget = false;

        return tmpText;
    }

    private Button CreateStageSelectionButton(string objectName, Transform parent, string text, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );

        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(190f, 86f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.45f, 0.82f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.86f, 0.93f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.82f, 0.96f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text label = CreateStageSelectionText(
            "Label",
            buttonObject.transform,
            text,
            28f,
            Vector2.zero,
            new Vector2(170f, 58f)
        );

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private void CopyStageSelectionFont(TMP_Text targetText)
    {
        if (targetText == null)
            return;

        TMP_Text sourceText = startGameButtonText != null
            ? startGameButtonText
            : readyButtonText != null
                ? readyButtonText
                : statusText != null
                    ? statusText
                    : roomNameText;

        if (sourceText != null && sourceText.font != null)
            targetText.font = sourceText.font;
    }

    private void CacheStageSelectionUIReferences()
    {
        if (stageSelectionPanel == null)
            return;

        Transform root = stageSelectionPanel.transform;
        Transform window = root.Find(StageSelectionWindowName);
        Transform searchRoot = window != null ? window : root;

        if (stageAButton == null)
        {
            Transform stageA = searchRoot.Find(StageAButtonName);

            if (stageA != null)
                stageAButton = stageA.GetComponent<Button>();
        }

        if (stageBButton == null)
        {
            Transform stageB = searchRoot.Find(StageBButtonName);

            if (stageB != null)
                stageBButton = stageB.GetComponent<Button>();
        }

        if (stageSelectionTitleText == null)
        {
            Transform title = searchRoot.Find("StageSelectionTitle");

            if (title != null)
                stageSelectionTitleText = title.GetComponent<TMP_Text>();
        }

        if (stageAButtonText == null && stageAButton != null)
            stageAButtonText = stageAButton.GetComponentInChildren<TMP_Text>(true);

        if (stageBButtonText == null && stageBButton != null)
            stageBButtonText = stageBButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void ConfigureStageSelectionText()
    {
        if (stageSelectionTitleText != null)
        {
            CopyStageSelectionFont(stageSelectionTitleText);
            stageSelectionTitleText.text = "Select Stage";
        }

        if (stageAButtonText != null)
        {
            CopyStageSelectionFont(stageAButtonText);
            stageAButtonText.text = "Stage A";
        }

        if (stageBButtonText != null)
        {
            CopyStageSelectionFont(stageBButtonText);
            stageBButtonText.text = "Stage B";
        }
    }

    private void ShowStageSelectionPanel()
    {
        EnsureStageSelectionUI();

        if (stageSelectionPanel == null)
        {
            SetStatus("Stage selection UI not found");
            return;
        }

        SetStageSelectionPanelVisible(true);
        UpdateStageSelectionUI();
    }

    private void HideStageSelectionPanel()
    {
        SetStageSelectionPanelVisible(false);
    }

    private void SetStageSelectionPanelVisible(bool isVisible)
    {
        if (stageSelectionPanel != null && stageSelectionPanel.activeSelf != isVisible)
            stageSelectionPanel.SetActive(isVisible);
    }

    private bool IsStageSelectionPanelVisible()
    {
        return stageSelectionPanel != null && stageSelectionPanel.activeSelf;
    }

    private void UpdateStageSelectionUI()
    {
        bool isHost = runner != null && runner.IsSceneAuthority;
        bool canSelectStage = isHost && CanStartGame();
        bool isPanelVisible = IsStageSelectionPanelVisible();

        if (isPanelVisible && !canSelectStage)
        {
            HideStageSelectionPanel();
            isPanelVisible = false;
        }

        if (stageAButton != null)
            stageAButton.interactable = isPanelVisible && canSelectStage;

        if (stageBButton != null)
            stageBButton.interactable = isPanelVisible && canSelectStage;
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
        UpdateStageSelectionUI();
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

            UpdateRoomCodeCopyButtonLayout();
        }

        if (playerCountText != null)
        {
            int count = GetCurrentPlayerCount();
            playerCountText.text = $"Players: {count}/{GetRequiredPlayerCount()}";
        }
    }

    /// <summary>
    /// Player 1, Player 2의 Ready 상태 UI 갱신
    /// </summary>
    private void UpdateRoomCodeCopyButtonLayout()
    {
        if (roomNameText == null || CopyButton == null)
            return;

        RectTransform textRect = roomNameText.rectTransform;
        RectTransform buttonRect = CopyButton.GetComponent<RectTransform>();

        if (textRect == null || buttonRect == null)
            return;

        Vector4 margin = roomNameText.margin;
        margin.z = 0f;
        roomNameText.margin = margin;

        Canvas.ForceUpdateCanvases();
        roomNameText.ForceMeshUpdate();

        float containerWidth = textRect.rect.width;
        float buttonWidth = buttonRect.rect.width > 1f ? buttonRect.rect.width : 110f;
        float buttonHeight = buttonRect.rect.height > 1f ? buttonRect.rect.height : 40f;

        if (containerWidth <= 0f)
            return;

        float maxTextWidth = Mathf.Max(0f, containerWidth - buttonWidth - CopyButtonGap);
        float textWidth = Mathf.Min(
            Mathf.Ceil(roomNameText.GetPreferredValues(roomNameText.text).x),
            maxTextWidth);

        buttonRect.anchorMin = new Vector2(0f, 0.5f);
        buttonRect.anchorMax = new Vector2(0f, 0.5f);
        buttonRect.pivot = new Vector2(0f, 0.5f);
        buttonRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        buttonRect.anchoredPosition = new Vector2(textWidth + CopyButtonGap, 0f);

        margin.z = containerWidth - textWidth;
        roomNameText.margin = margin;
    }

    private void UpdatePlayerListUI()
    {
        List<RoomPlayerState> states = GetRoomPlayerStates();
        List<TMP_Text> textSlots = GetPlayerTextSlots();
        int requiredPlayers = GetRequiredPlayerCount();
        int presentPlayerCount = Mathf.Max(states.Count, GetCurrentPlayerCount());

        for (int i = 0; i < textSlots.Count; i++)
        {
            TMP_Text textSlot = textSlots[i];
            bool isRequiredSlot = i < requiredPlayers;

            if (textSlot == null)
                continue;

            bool isPlayerPresent = isRequiredSlot && i < presentPlayerCount;

            SetPlayerSlotVisible(textSlot, isPlayerPresent);
            Image icon = SetPlayerStateIcon(i, textSlot, isPlayerPresent, isPlayerPresent);
            SetPlayerReadyLabel(i, textSlot, false, icon);

            textSlot.text = isPlayerPresent ? $"Player {i + 1}" : "";
        }

        for (int i = 0; i < states.Count && i < textSlots.Count; i++)
        {
            RoomPlayerState state = states[i];
            TMP_Text textSlot = textSlots[i];

            if (textSlot == null)
                continue;

            string playerText = $"Player {i + 1}";
            string roleText = state.IsHostPlayer ? "Host" : "Client";
            string localText = state.Object != null && state.Object.HasInputAuthority ? " (You)" : "";

            SetPlayerSlotVisible(textSlot, true);
            Image icon = SetPlayerStateIcon(i, textSlot, true, true);
            textSlot.text = $"{playerText}\n{roleText}{localText}";
            SetPlayerReadyLabel(i, textSlot, state.IsReady, icon);
        }
    }

    /// <summary>
    /// Keeps the player presence icon next to each player state text slot.
    /// </summary>
    private Image SetPlayerStateIcon(int index, TMP_Text textSlot, bool isVisible, bool isPlayerPresent)
    {
        Image icon = GetOrCreatePlayerStateIcon(index, textSlot);

        if (icon == null)
            return null;

        icon.gameObject.SetActive(isVisible);

        if (!isVisible)
            return icon;

        LoadPlayerStateIconsIfNeeded();

        icon.sprite = isPlayerPresent ? joinedPlayerIconSprite : emptyPlayerIconSprite;
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        PositionPlayerStateIcon(icon.rectTransform, textSlot);

        return icon;
    }

    private Image GetOrCreatePlayerStateIcon(int index, TMP_Text textSlot)
    {
        if (textSlot == null)
            return null;

        if (playerStateIcons == null)
            playerStateIcons = new List<Image>();

        while (playerStateIcons.Count <= index)
            playerStateIcons.Add(null);

        Image icon = playerStateIcons[index];
        Transform existingIcon = FindExistingPlayerStateIcon(index, textSlot);

        if (existingIcon != null)
        {
            if (icon != null && icon.transform != existingIcon)
                icon.gameObject.SetActive(false);

            icon = existingIcon.GetComponent<Image>();
        }

        HideDuplicateChildPlayerStateIcon(index, textSlot, icon);

        if (icon != null)
        {
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            PositionPlayerStateIcon(icon.rectTransform, textSlot);
            playerStateIcons[index] = icon;

            return icon;
        }

        if (icon == null)
        {
            GameObject iconObject = new GameObject(
                $"PlayerStateIcon_{index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            iconObject.layer = textSlot.gameObject.layer;
            iconObject.transform.SetParent(textSlot.transform, false);
            icon = iconObject.GetComponent<Image>();
        }

        icon.color = Color.white;
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        PositionPlayerStateIcon(icon.rectTransform, textSlot);
        playerStateIcons[index] = icon;

        return icon;
    }

    private void PositionPlayerStateIcon(RectTransform iconRect, TMP_Text textSlot)
    {
        if (iconRect == null)
            return;

        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = playerStateIconSize.x > 0f && playerStateIconSize.y > 0f
            ? playerStateIconSize
            : new Vector2(58f, 58f);

        if (textSlot != null && iconRect.parent == textSlot.transform)
            iconRect.anchoredPosition = new Vector2(0f, iconRect.sizeDelta.y + playerStateIconGap);
    }

    private void SetPlayerSlotVisible(TMP_Text textSlot, bool isVisible)
    {
        if (textSlot == null)
            return;

        if (textSlot.gameObject.activeSelf != isVisible)
            textSlot.gameObject.SetActive(isVisible);
    }

    private Transform FindExistingPlayerStateIcon(int index, TMP_Text textSlot)
    {
        if (textSlot == null)
            return null;

        string iconName = $"PlayerStateIcon_{index + 1}";

        if (textSlot.transform.parent != null)
        {
            Transform siblingIcon = textSlot.transform.parent.Find(iconName);

            if (siblingIcon != null)
                return siblingIcon;
        }

        return textSlot.transform.Find(iconName);
    }

    private void HideDuplicateChildPlayerStateIcon(int index, TMP_Text textSlot, Image selectedIcon)
    {
        if (textSlot == null)
            return;

        Transform childIcon = textSlot.transform.Find($"PlayerStateIcon_{index + 1}");

        if (childIcon != null && (selectedIcon == null || childIcon != selectedIcon.transform))
            childIcon.gameObject.SetActive(false);
    }

    private void SetPlayerReadyLabel(int index, TMP_Text textSlot, bool isVisible, Image icon)
    {
        TMP_Text readyLabel = GetOrCreatePlayerReadyLabel(index, textSlot, icon);

        if (readyLabel == null)
            return;

        readyLabel.gameObject.SetActive(isVisible);

        if (!isVisible)
            return;

        readyLabel.text = "Ready";
        PositionPlayerReadyLabel(readyLabel.rectTransform, icon);
    }

    private TMP_Text GetOrCreatePlayerReadyLabel(int index, TMP_Text textSlot, Image icon)
    {
        if (textSlot == null)
            return null;

        if (playerReadyLabels == null)
            playerReadyLabels = new List<TMP_Text>();

        while (playerReadyLabels.Count <= index)
            playerReadyLabels.Add(null);

        TMP_Text readyLabel = playerReadyLabels[index];
        string labelName = $"PlayerReadyLabel_{index + 1}";
        Transform labelParent = icon != null && icon.transform.parent != null
            ? icon.transform.parent
            : textSlot.transform.parent;

        if (readyLabel == null && labelParent != null)
        {
            Transform existingLabel = labelParent.Find(labelName);

            if (existingLabel != null)
                readyLabel = existingLabel.GetComponent<TMP_Text>();
        }

        if (readyLabel == null)
        {
            GameObject labelObject = new GameObject(
                labelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

            labelObject.layer = textSlot.gameObject.layer;
            labelObject.transform.SetParent(labelParent != null ? labelParent : textSlot.transform, false);
            readyLabel = labelObject.GetComponent<TextMeshProUGUI>();
        }

        readyLabel.font = textSlot.font;
        readyLabel.fontSize = 24f;
        readyLabel.enableAutoSizing = true;
        readyLabel.fontSizeMin = 14f;
        readyLabel.fontSizeMax = 26f;
        readyLabel.alignment = TextAlignmentOptions.Center;
        readyLabel.color = Color.white;
        readyLabel.raycastTarget = false;
        playerReadyLabels[index] = readyLabel;

        return readyLabel;
    }

    private void PositionPlayerReadyLabel(RectTransform labelRect, Image icon)
    {
        if (labelRect == null || icon == null)
            return;

        RectTransform iconRect = icon.rectTransform;

        if (labelRect.parent != iconRect.parent)
            labelRect.SetParent(iconRect.parent, false);

        labelRect.anchorMin = iconRect.anchorMin;
        labelRect.anchorMax = iconRect.anchorMax;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = playerReadyLabelSize.x > 0f && playerReadyLabelSize.y > 0f
            ? playerReadyLabelSize
            : new Vector2(130f, 32f);
        labelRect.anchoredPosition = iconRect.anchoredPosition
            + new Vector2(0f, iconRect.sizeDelta.y * 0.5f + playerReadyLabelGap);
    }

    private void LoadPlayerStateIconsIfNeeded()
    {
        if (playerStateIconsLoaded)
            return;

        playerStateIconsLoaded = true;
        joinedPlayerIconSprite = LoadPlayerStateIconSprite(
            string.IsNullOrEmpty(joinedPlayerIconResourcePath)
                ? "WaitingRoomIcons/blue_filled_icon"
                : joinedPlayerIconResourcePath
        );

        emptyPlayerIconSprite = LoadPlayerStateIconSprite(
            string.IsNullOrEmpty(emptyPlayerIconResourcePath)
                ? "WaitingRoomIcons/blue_outline_icon"
                : emptyPlayerIconResourcePath
        );
    }

    private Sprite LoadPlayerStateIconSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);

        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);

        if (texture == null)
        {
            Debug.LogWarning($"[WaitingRoomManager] Player state icon not found in Resources: {resourcePath}");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    /// <summary>
    /// Ready button and Start Game button state refresh.
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
        bool isSelectingStage = IsStageSelectionPanelVisible();

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = canStart && !isSelectingStage;
        }

        if (startGameButtonText != null)
        {
            if (isSelectingStage)
                startGameButtonText.text = "Select Stage";
            else if (canStart)
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

        return CountActivePlayers(runner);
    }

    private int GetRequiredPlayerCount()
    {
        FindReferences();

        if (runner != null && runner.SessionInfo != null && runner.SessionInfo.MaxPlayers > 0)
            return runner.SessionInfo.MaxPlayers;

        if (networkSessionManager != null)
            return networkSessionManager.MaxPlayers;

        return DefaultMaxPlayers;
    }

    private List<TMP_Text> GetPlayerTextSlots()
    {
        if (playerStateTexts.Count == 0)
        {
            if (player1Text != null)
                playerStateTexts.Add(player1Text);

            if (player2Text != null && player2Text != player1Text)
                playerStateTexts.Add(player2Text);
        }

        return playerStateTexts;
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

        if (!sourceRunner.IsRunning)
            return 0;

        int count = 0;

        try
        {
            foreach (PlayerRef player in sourceRunner.ActivePlayers)
                count++;
        }
        catch (KeyNotFoundException exception)
        {
            Debug.LogWarning(
                $"[WaitingRoomManager] ActivePlayers could not be read while runner state was changing: {exception.Message}"
            );
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning(
                $"[WaitingRoomManager] ActivePlayers changed while being read: {exception.Message}"
            );
            return 0;
        }

        return count;
    }

    private List<PlayerRef> GetActivePlayersSnapshot(NetworkRunner sourceRunner, string context)
    {
        List<PlayerRef> players = new List<PlayerRef>();

        if (sourceRunner == null || !sourceRunner.IsRunning)
            return players;

        try
        {
            foreach (PlayerRef player in sourceRunner.ActivePlayers)
                players.Add(player);
        }
        catch (KeyNotFoundException exception)
        {
            Debug.LogWarning(
                $"[WaitingRoomManager] ActivePlayers could not be read during {context}: {exception.Message}"
            );
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning(
                $"[WaitingRoomManager] ActivePlayers changed during {context}: {exception.Message}"
            );
        }

        return players;
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
        {
            SpawnRoomPlayerStateIfNeeded(player);
            SpawnNetworkGameStateIfHost();
        }

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
        DespawnRoomPlayerStateIfHost(player);
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
