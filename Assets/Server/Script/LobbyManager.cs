using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Manager")]
    [SerializeField] private NetworkSessionManager networkSessionManager;

    [Header("Lobby UI")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Status Option")]
    [SerializeField] private float statusHideDelay = 3f;

    private Coroutine statusCoroutine;

    private void Awake()
    {
        if (networkSessionManager == null)
        {
            networkSessionManager = FindAnyObjectByType<NetworkSessionManager>();
        }
    }

    private void OnEnable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnClickCreateRoom);

        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnClickJoinRoom);

        if (networkSessionManager != null)
        {
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

        if (networkSessionManager != null)
        {
            networkSessionManager.OnStatusChanged -= SetStatus;
            networkSessionManager.OnBusyStateChanged -= SetButtonsInteractable;
        }
    }

    private void Start()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);

        if (networkSessionManager == null)
        {
            SetStatus("NetworkSessionManager not found");
            SetButtonsInteractable(false);
            return;
        }

        SetStatus("Lobby ready");
    }

    private void OnClickCreateRoom()
    {
        if (networkSessionManager == null)
            return;

        networkSessionManager.CreateSessionWithRandomCode();
    }

    private void OnClickJoinRoom()
    {
        if (networkSessionManager == null)
            return;

        string roomCode = GetRoomCode();

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            SetStatus("Please enter a room code to join");
            return;
        }

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

        // Keep the room code visible so the player can share it with a friend.
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