using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private SceneFlowManager sceneFlowManager;
    [SerializeField] private NetworkPrefabRef playerPrefab;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();
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

    private void FindReferences()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);

        if (sceneFlowManager == null)
            sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);
    }

    private void TryRegisterRunnerCallbacks()
    {
        FindReferences();

        if (runner == null)
            return;

        if (registeredRunner == runner)
            return;

        UnregisterRunnerCallbacks();

        registeredRunner = runner;
        registeredRunner.RemoveCallbacks(this);
        registeredRunner.AddCallbacks(this);

        Debug.Log("[PlayerSpawner] Registered runner callbacks: " + registeredRunner.name);

        if (IsGameSceneLoaded())
            SpawnAllPlayers(registeredRunner);
    }

    private void UnregisterRunnerCallbacks()
    {
        if (registeredRunner == null)
            return;

        registeredRunner.RemoveCallbacks(this);
        registeredRunner = null;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!IsGameSceneLoaded())
            return;

        SpawnPlayerIfNeeded(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out NetworkObject obj) && obj != null)
            runner.Despawn(obj);

        spawnedPlayers.Remove(player);
    }

    private void SpawnPlayerIfNeeded(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (spawnedPlayers.TryGetValue(player, out NetworkObject existing) && existing != null)
            return;

        Vector3 spawnPos = new Vector3(player.RawEncoded * 2f, 1f, 0f);
        NetworkObject obj = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);

        if (obj == null)
            return;

        spawnedPlayers[player] = obj;
        runner.SetPlayerObject(player, obj);
    }

    private bool IsGameSceneLoaded()
    {
        FindReferences();

        return sceneFlowManager != null && sceneFlowManager.IsGameSceneLoaded();
    }

    private void SpawnAllPlayers(NetworkRunner runner)
    {
        foreach (PlayerRef player in runner.ActivePlayers)
            SpawnPlayerIfNeeded(runner, player);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
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

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!IsGameSceneLoaded())
            return;

        SpawnAllPlayers(runner);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        spawnedPlayers.Clear();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
