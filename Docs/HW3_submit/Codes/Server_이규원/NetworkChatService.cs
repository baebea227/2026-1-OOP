using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NetworkChatService : MonoBehaviour, INetworkRunnerCallbacks
{
    private const int ChatKey = 0x43484154;
    private const int Version = 1;
    private const int PacketClientToServer = 1;
    private const int PacketServerBroadcast = 2;
    private const int MaxMessageBytes = 2048;

    public static NetworkChatService Instance { get; private set; }

    public event Action<NetworkChatMessage> OnMessageReceived;
    public event Action<string> OnStatusChanged;

    [SerializeField] private int maxMessageLength = 160;
    [SerializeField] private int maxSenderNameLength = 24;

    private readonly HashSet<long> displayedMessages = new HashSet<long>();
    private readonly HashSet<long> relayedMessages = new HashSet<long>();

    private NetworkRunner registeredRunner;
    private int localSequence;
    private float nextResolveTime;

    public static NetworkChatService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindAnyObjectByType<NetworkChatService>(FindObjectsInactive.Include);

        if (Instance != null)
            return Instance;

        GameObject serviceObject = new GameObject("NetworkChatService");
        Instance = serviceObject.AddComponent<NetworkChatService>();
        DontDestroyOnLoad(serviceObject);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        TryRegisterRunner();
    }

    private void OnDisable()
    {
        UnregisterRunner();
    }

    private void OnDestroy()
    {
        UnregisterRunner();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextResolveTime)
            return;

        nextResolveTime = Time.unscaledTime + 0.5f;
        TryRegisterRunner();
    }

    public bool SendChat(string text, string senderName = null)
    {
        text = NormalizeText(text, maxMessageLength);

        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!TryGetRunner(out NetworkRunner activeRunner) || !activeRunner.IsRunning)
        {
            PublishSystemMessage("Chat is not connected to a network session.");
            return false;
        }

        PlayerRef sender = activeRunner.LocalPlayer;
        int sequence = ++localSequence;
        string normalizedSenderName = NormalizeSenderName(activeRunner, senderName, sender);

        ChatPacket packet = new ChatPacket
        {
            PacketType = activeRunner.IsServer ? PacketServerBroadcast : PacketClientToServer,
            SenderRaw = sender.RawEncoded,
            Sequence = sequence,
            SenderName = normalizedSenderName,
            Text = text,
            SentAtUtcTicks = DateTime.UtcNow.Ticks
        };

        if (activeRunner.IsServer)
        {
            PublishAndRelay(activeRunner, packet);
            return true;
        }

        byte[] payload = SerializePacket(packet);

        if (payload.Length > MaxMessageBytes)
        {
            PublishSystemMessage("Chat message is too long.");
            return false;
        }

        activeRunner.SendReliableDataToServer(CreateReliableKey(packet), payload);
        return true;
    }

    private void TryRegisterRunner()
    {
        if (!TryGetRunner(out NetworkRunner activeRunner))
        {
            UnregisterRunner();
            return;
        }

        if (registeredRunner == activeRunner)
            return;

        UnregisterRunner();

        registeredRunner = activeRunner;
        registeredRunner.RemoveCallbacks(this);
        registeredRunner.AddCallbacks(this);

        displayedMessages.Clear();
        relayedMessages.Clear();
    }

    private bool TryGetRunner(out NetworkRunner activeRunner)
    {
        activeRunner = null;

        NetworkSessionManager sessionManager = NetworkSessionManager.Instance;

        if (sessionManager != null)
            activeRunner = sessionManager.Runner;

        if (activeRunner == null)
            activeRunner = FindAnyObjectByType<NetworkRunner>(FindObjectsInactive.Include);

        return activeRunner != null;
    }

    private void UnregisterRunner()
    {
        if (registeredRunner == null)
            return;

        registeredRunner.RemoveCallbacks(this);
        registeredRunner = null;
    }

    private void PublishAndRelay(NetworkRunner activeRunner, ChatPacket packet)
    {
        packet.PacketType = PacketServerBroadcast;

        long messageKey = GetMessageKey(packet.SenderRaw, packet.Sequence);

        if (!relayedMessages.Add(messageKey))
            return;

        PublishPacket(packet);

        byte[] payload = SerializePacket(packet);
        ReliableKey reliableKey = CreateReliableKey(packet);

        foreach (PlayerRef player in activeRunner.ActivePlayers)
        {
            if (player == activeRunner.LocalPlayer)
                continue;

            activeRunner.SendReliableDataToPlayer(player, reliableKey, payload);
        }
    }

    private void PublishPacket(ChatPacket packet)
    {
        long messageKey = GetMessageKey(packet.SenderRaw, packet.Sequence);

        if (!displayedMessages.Add(messageKey))
            return;

        NetworkChatMessage message = new NetworkChatMessage(
            PlayerRef.FromEncoded(packet.SenderRaw),
            packet.SenderName,
            packet.Text,
            new DateTime(packet.SentAtUtcTicks, DateTimeKind.Utc),
            false);

        OnMessageReceived?.Invoke(message);
    }

    private void PublishSystemMessage(string text)
    {
        OnStatusChanged?.Invoke(text);
        OnMessageReceived?.Invoke(new NetworkChatMessage(PlayerRef.None, "System", text, DateTime.UtcNow, true));
    }

    private static ReliableKey CreateReliableKey(ChatPacket packet)
    {
        return ReliableKey.FromInts(ChatKey, packet.SenderRaw, packet.Sequence, packet.PacketType);
    }

    private static bool IsChatKey(ReliableKey key)
    {
        key.GetInts(out int key0, out _, out _, out _);
        return key0 == ChatKey;
    }

    private byte[] SerializePacket(ChatPacket packet)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(ChatKey);
                writer.Write(Version);
                writer.Write(packet.PacketType);
                writer.Write(packet.SenderRaw);
                writer.Write(packet.Sequence);
                writer.Write(packet.SentAtUtcTicks);
                writer.Write(packet.SenderName ?? "");
                writer.Write(packet.Text ?? "");

                return stream.ToArray();
            }
        }
    }

    private bool TryDeserializePacket(ArraySegment<byte> data, out ChatPacket packet)
    {
        packet = new ChatPacket();

        if (data.Array == null || data.Count <= 0)
            return false;

        try
        {
            using (MemoryStream stream = new MemoryStream(data.Array, data.Offset, data.Count, false))
            {
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    int chatKey = reader.ReadInt32();
                    int version = reader.ReadInt32();

                    if (chatKey != ChatKey || version != Version)
                        return false;

                    packet.PacketType = reader.ReadInt32();
                    packet.SenderRaw = reader.ReadInt32();
                    packet.Sequence = reader.ReadInt32();
                    packet.SentAtUtcTicks = reader.ReadInt64();
                    packet.SenderName = NormalizeText(reader.ReadString(), maxSenderNameLength);
                    packet.Text = NormalizeText(reader.ReadString(), maxMessageLength);

                    return !string.IsNullOrWhiteSpace(packet.Text);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NetworkChatService] Failed to parse chat packet: " + e.Message);
            return false;
        }
    }

    private string NormalizeSenderName(NetworkRunner activeRunner, string senderName, PlayerRef sender)
    {
        senderName = NormalizeText(senderName, maxSenderNameLength);

        if (!string.IsNullOrWhiteSpace(senderName))
            return senderName;

        return ResolvePlayerDisplayName(activeRunner, sender);
    }

    private static string ResolvePlayerDisplayName(NetworkRunner activeRunner, PlayerRef player)
    {
        if (player == PlayerRef.None)
            return "Player";

        if (activeRunner != null)
        {
            List<PlayerRef> activePlayers = new List<PlayerRef>();

            foreach (PlayerRef activePlayer in activeRunner.ActivePlayers)
                activePlayers.Add(activePlayer);

            activePlayers.Sort((a, b) => a.RawEncoded.CompareTo(b.RawEncoded));

            for (int i = 0; i < activePlayers.Count; i++)
            {
                if (activePlayers[i] == player)
                    return "Player " + (i + 1);
            }
        }

        return "Player " + GetReadablePlayerNumber(player);
    }

    private static int GetReadablePlayerNumber(PlayerRef player)
    {
        string playerText = player.ToString();
        int colonIndex = playerText.IndexOf(':');
        int endIndex = playerText.IndexOf(']');

        if (colonIndex >= 0 && endIndex > colonIndex)
        {
            string numberText = playerText.Substring(colonIndex + 1, endIndex - colonIndex - 1);

            if (int.TryParse(numberText, out int playerNumber))
                return playerNumber;
        }

        return Mathf.Max(1, player.RawEncoded);
    }

    private static string NormalizeText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = text.Trim();
        text = text.Replace('\r', ' ');
        text = text.Replace('\n', ' ');

        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        if (text.Length > maxLength)
            text = text.Substring(0, maxLength);

        return text;
    }

    private static long GetMessageKey(int senderRaw, int sequence)
    {
        return ((long)senderRaw << 32) ^ (uint)sequence;
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (!IsChatKey(key))
            return;

        if (!TryDeserializePacket(data, out ChatPacket packet))
            return;

        if (packet.PacketType == PacketClientToServer && runner.IsServer)
        {
            packet.SenderRaw = player.RawEncoded;
            packet.SenderName = ResolvePlayerDisplayName(runner, player);
            packet.PacketType = PacketServerBroadcast;
            PublishAndRelay(runner, packet);
            return;
        }

        if (packet.PacketType == PacketServerBroadcast)
            PublishPacket(packet);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        displayedMessages.Clear();
        relayedMessages.Clear();
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    private struct ChatPacket
    {
        public int PacketType;
        public int SenderRaw;
        public int Sequence;
        public string SenderName;
        public string Text;
        public long SentAtUtcTicks;
    }
}
