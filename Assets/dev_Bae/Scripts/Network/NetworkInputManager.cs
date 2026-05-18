using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NetworkInputManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;

    private PlayerInputHandler cachedLocalHandler;
    private bool warnedMissingLocalHandler;
    private bool loggedFallbackHandler;
    private NetworkRunner registeredRunner;

    private void OnEnable()
    {
        TryRegisterRunnerCallbacks();
    }

    private void Update()
    {
        if (registeredRunner == null)
            TryRegisterRunnerCallbacks();
    }

    private void OnDisable()
    {
        UnregisterRunnerCallbacks();
    }

    private void FindRunnerIfNull()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);
    }

    private void TryRegisterRunnerCallbacks()
    {
        FindRunnerIfNull();

        if (runner == null)
            return;

        if (registeredRunner == runner)
            return;

        UnregisterRunnerCallbacks();

        registeredRunner = runner;
        registeredRunner.RemoveCallbacks(this);
        registeredRunner.AddCallbacks(this);

        Debug.Log("[NetworkInputManager] Registered runner callbacks: " + registeredRunner.name);
    }

    private void UnregisterRunnerCallbacks()
    {
        if (registeredRunner == null)
            return;

        registeredRunner.RemoveCallbacks(this);
        registeredRunner = null;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var localObj = runner.GetPlayerObject(runner.LocalPlayer);
        PlayerInputHandler handler = null;

        if (localObj != null)
        {
            handler = localObj.GetComponent<PlayerInputHandler>();
            cachedLocalHandler = handler;
        }
        else
        {
            handler = GetCachedOrFindLocalHandler();

            if (handler != null)
                runner.SetPlayerObject(runner.LocalPlayer, handler.Object);
        }

        if (handler != null)
        {
            handler.OnInput(runner, input);
            return;
        }

        if (!warnedMissingLocalHandler)
        {
            Debug.LogWarning("[NetworkInputManager] Local input authority handler not found yet.");
            warnedMissingLocalHandler = true;
        }
    }

    private PlayerInputHandler GetCachedOrFindLocalHandler()
    {
        if (cachedLocalHandler != null &&
            cachedLocalHandler.Object != null &&
            cachedLocalHandler.Object.HasInputAuthority)
        {
            return cachedLocalHandler;
        }

        cachedLocalHandler = null;

        PlayerInputHandler[] handlers = FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);

        foreach (PlayerInputHandler handler in handlers)
        {
            if (handler.Object == null)
                continue;

            if (!handler.Object.HasInputAuthority)
                continue;

            cachedLocalHandler = handler;

            if (!loggedFallbackHandler)
            {
                Debug.Log("[NetworkInputManager] Found local input authority handler by fallback search.");
                loggedFallbackHandler = true;
            }

            return cachedLocalHandler;
        }

        return null;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (cachedLocalHandler != null &&
            cachedLocalHandler.Object != null &&
            cachedLocalHandler.Object.InputAuthority == player)
        {
            cachedLocalHandler = null;
        }
    }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        cachedLocalHandler = null;
        warnedMissingLocalHandler = false;
        loggedFallbackHandler = false;
    }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
