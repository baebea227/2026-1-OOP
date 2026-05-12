using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkRunner runner;
    [SerializeField] private NetworkPrefabRef playerPrefab;
    [SerializeField] private int gameSceneBuildIndex = 2;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    private void OnEnable()
    {
        FindRunnerIfNull();

        if (runner != null)
            runner.AddCallbacks(this);
    }

    private void OnDisable()
    {
        if (runner != null)
            runner.RemoveCallbacks(this);
    }

    private void FindRunnerIfNull()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();
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
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).buildIndex == gameSceneBuildIndex)
                return true;
        }

        return false;
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

        foreach (PlayerRef player in runner.ActivePlayers)
            SpawnPlayerIfNeeded(runner, player);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        spawnedPlayers.Clear();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
