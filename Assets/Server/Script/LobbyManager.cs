using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private NetworkSessionManager networkSessionManager;

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Room List UI")]
    [SerializeField] private Transform roomListParent;
    [SerializeField] private Button roomButtonPrefab;

    private readonly List<Button> roomButtons = new List<Button>();

    private void Awake()
    {
        if (networkSessionManager == null)
        {
            networkSessionManager = FindObjectOfType<NetworkSessionManager>();
        }
    }

    private void OnEnable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnClickCreateRoom);

        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnClickJoinRoom);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnClickRefresh);

        if (networkSessionManager != null)
        {
            networkSessionManager.OnSessionListChanged += UpdateRoomList;
            networkSessionManager.OnStatusChanged += SetStatus;
            networkSessionManager.OnBusyStateChanged += SetButtonsInteractable;
        }
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnClickCreateRoom);

        if (joinRoomButton != null)
            joinRoomButton.onClick.RemoveListener(OnClickJoinRoom);

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnClickRefresh);

        if (networkSessionManager != null)
        {
            networkSessionManager.OnSessionListChanged -= UpdateRoomList;
            networkSessionManager.OnStatusChanged -= SetStatus;
            networkSessionManager.OnBusyStateChanged -= SetButtonsInteractable;
        }
    }

    private void Start()
    {
        if (networkSessionManager == null)
        {
            SetStatus("NetworkSessionManager를 찾을 수 없음");
            SetButtonsInteractable(false);
            return;
        }

        SetStatus("로비 준비 완료");

        // 로비 씬에 들어오면 자동으로 Photon 로비 접속
        networkSessionManager.JoinLobby();
    }

    private void OnClickCreateRoom()
    {
        if (networkSessionManager == null)
            return;

        string roomName = GetRoomName();

        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetStatus("방 이름을 입력해야 함");
            return;
        }

        networkSessionManager.CreateSession(roomName);
    }

    private void OnClickJoinRoom()
    {
        if (networkSessionManager == null)
            return;

        string roomName = GetRoomName();

        if (string.IsNullOrWhiteSpace(roomName))
        {
            SetStatus("참가할 방 이름을 입력해야 함");
            return;
        }

        networkSessionManager.JoinSession(roomName);
    }

    private void OnClickRefresh()
    {
        if (networkSessionManager == null)
            return;

        networkSessionManager.JoinLobby();
    }

    private string GetRoomName()
    {
        if (roomNameInput == null)
            return "";

        return roomNameInput.text.Trim();
    }

    private void UpdateRoomList(List<SessionInfo> sessionList)
    {
        ClearRoomList();

        if (sessionList == null || sessionList.Count == 0)
        {
            SetStatus("현재 생성된 방이 없음");
            return;
        }

        foreach (SessionInfo session in sessionList)
        {
            if (!IsValidRoom(session))
                continue;

            CreateRoomButton(session);
        }

        SetStatus($"방 목록 업데이트 완료: {sessionList.Count}개");
    }

    private bool IsValidRoom(SessionInfo session)
    {
        if (!session.IsValid)
            return false;

        if (!session.IsVisible)
            return false;

        return true;
    }

    private void CreateRoomButton(SessionInfo session)
    {
        if (roomListParent == null || roomButtonPrefab == null)
            return;

        Button button = Instantiate(roomButtonPrefab, roomListParent);
        roomButtons.Add(button);

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();

        if (buttonText != null)
        {
            buttonText.text =
                $"{session.Name}  ({session.PlayerCount}/{session.MaxPlayers})";
        }

        bool canJoin = session.IsOpen && session.PlayerCount < session.MaxPlayers;
        button.interactable = canJoin;

        string selectedRoomName = session.Name;

        button.onClick.AddListener(() =>
        {
            if (roomNameInput != null)
                roomNameInput.text = selectedRoomName;

            if (networkSessionManager != null)
                networkSessionManager.JoinSession(selectedRoomName);
        });
    }

    private void ClearRoomList()
    {
        foreach (Button button in roomButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        roomButtons.Clear();

        if (roomListParent == null)
            return;

        foreach (Transform child in roomListParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void SetStatus(string message)
    {
        Debug.Log("[LobbyManager] " + message);

        if (statusText != null)
            statusText.text = message;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = interactable;

        if (joinRoomButton != null)
            joinRoomButton.interactable = interactable;

        if (refreshButton != null)
            refreshButton.interactable = interactable;
    }
}