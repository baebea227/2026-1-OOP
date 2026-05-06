using Fusion;
using UnityEngine;

public class RoomPlayerState : NetworkBehaviour
{
    [Header("Networked State")]
    [Networked] public PlayerRef Player { get; private set; }
    [Networked] public NetworkBool IsReady { get; private set; }
    [Networked] public NetworkBool IsHostPlayer { get; private set; }

    public override void Spawned()
    {
        Debug.Log($"[RoomPlayerState] Spawned - Player: {Player}, Ready: {IsReady}");
    }

    public void Initialize(PlayerRef player, bool isHostPlayer)
    {
        if (!Object.HasStateAuthority)
            return;

        Player = player;
        IsReady = false;
        IsHostPlayer = isHostPlayer;
    }

    public void RequestToggleReady()
    {
        if (!Object.HasInputAuthority)
        {
            Debug.LogWarning("[RoomPlayerState] 내 플레이어 상태가 아니므로 Ready 변경 불가");
            return;
        }

        RPC_SetReady(!IsReady);
    }

    public void RequestSetReady(bool ready)
    {
        if (!Object.HasInputAuthority)
        {
            Debug.LogWarning("[RoomPlayerState] 내 플레이어 상태가 아니므로 Ready 변경 불가");
            return;
        }

        RPC_SetReady(ready);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(NetworkBool ready)
    {
        IsReady = ready;

        Debug.Log($"[RoomPlayerState] Ready 변경 - Player: {Player}, Ready: {IsReady}");
    }

    public void ForceSetReady(bool ready)
    {
        if (!Object.HasStateAuthority)
            return;

        IsReady = ready;
    }
}