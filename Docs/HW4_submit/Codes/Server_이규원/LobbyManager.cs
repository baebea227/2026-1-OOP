using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Status Option")]
    [SerializeField] private float statusHideDelay = 3f;

    private NetworkSessionManager networkSessionManager;
    private Coroutine statusCoroutine;
    private bool isSubscribed;

    private void OnEnable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnClickCreateRoom);

        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnClickJoinRoom);

        ResolveNetworkSessionManager(false);
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnClickCreateRoom);

        if (joinRoomButton != null)
            joinRoomButton.onClick.RemoveListener(OnClickJoinRoom);

        UnsubscribeFromNetworkSessionManager();
    }

    private void Start()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (roomCodeInput == null)
            Debug.LogError("[LobbyManager] roomCodeInput is not assigned");

        if (createRoomButton == null)
            Debug.LogError("[LobbyManager] createRoomButton is not assigned");

        if (joinRoomButton == null)
            Debug.LogError("[LobbyManager] joinRoomButton is not assigned");

        if (statusText == null)
            Debug.LogError("[LobbyManager] statusText is not assigned");

        ResolveNetworkSessionManager(false);

        SetStatus("Lobby ready");
    }

    private bool ResolveNetworkSessionManager(bool showError)
    {
        NetworkSessionManager manager = NetworkSessionManager.Instance;

        if (manager == null)
            manager = FindAnyObjectByType<NetworkSessionManager>(FindObjectsInactive.Include);

        if (manager == null)
        {
            networkSessionManager = null;

            if (showError)
            {
                SetStatus("NetworkSessionManager not found");
                Debug.LogError("[LobbyManager] networkSessionManager is null");
            }

            return false;
        }

        if (networkSessionManager != manager)
        {
            UnsubscribeFromNetworkSessionManager();

            networkSessionManager = manager;

            networkSessionManager.OnStatusChanged += SetStatus;
            networkSessionManager.OnBusyStateChanged += SetButtonsInteractable;
            isSubscribed = true;

            Debug.Log("[LobbyManager] NetworkSessionManager resolved: " + networkSessionManager.name);
        }

        return true;
    }

    private void UnsubscribeFromNetworkSessionManager()
    {
        if (!isSubscribed)
            return;

        if (networkSessionManager != null)
        {
            networkSessionManager.OnStatusChanged -= SetStatus;
            networkSessionManager.OnBusyStateChanged -= SetButtonsInteractable;
        }

        isSubscribed = false;
    }

    private void OnClickCreateRoom()
    {
        Debug.Log("[LobbyManager] Create Room button clicked");

        if (!ResolveNetworkSessionManager(true))
            return;

        networkSessionManager.CreateSessionWithRandomCode();
    }

    private void OnClickJoinRoom()
    {
        Debug.Log("[LobbyManager] Join Room button clicked");

        if (!ResolveNetworkSessionManager(true))
            return;

        string roomCode = GetRoomCode();

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            SetStatus("Please enter a room code to join");
            Debug.LogWarning("[LobbyManager] Room code input is empty");
            return;
        }

        Debug.Log("[LobbyManager] Trying to join room: " + roomCode);

        networkSessionManager.JoinSession(roomCode);
    }

    private string GetRoomCode()
    {
        if (roomCodeInput == null)
            return "";

        return roomCodeInput.text.Trim().ToUpper();
    }

    private void SetStatus(string message)
    {
        Debug.Log("[LobbyManager] " + message);

        if (statusText == null)
            return;

        if (string.IsNullOrWhiteSpace(message))
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
            return;
        }

        statusText.text = message;
        statusText.gameObject.SetActive(true);

        if (statusCoroutine != null)
            StopCoroutine(statusCoroutine);

        if (message.Contains("Room Code"))
            return;

        statusCoroutine = StartCoroutine(HideStatusAfterDelay());
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = interactable;

        if (joinRoomButton != null)
            joinRoomButton.interactable = interactable;
    }

    private IEnumerator HideStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusHideDelay);

        if (statusText != null)
        {
            statusText.text = "";
            statusText.gameObject.SetActive(false);
        }

        statusCoroutine = null;
    }
}