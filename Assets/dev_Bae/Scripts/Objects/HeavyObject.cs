using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HeavyObject : InteractableObject, IPushable
{
    [Header("Heavy Settings")]
    public int requiredPushers = 2;

    // State Authority에서만 유효한 로컬 상태
    private readonly Dictionary<PlayerRef, float> pusherTimestamps = new();
    private const float pushWindow = 0.15f;
    private float lastForcedTime = -1f;
    private const float forceCooldown = 0.1f;

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (Object.HasStateAuthority)
            TryApplyForce(force, pusher);
        else
            RPC_Push(force, pusher);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Push(Vector3 force, PlayerRef pusher)
    {
        TryApplyForce(force, pusher);
    }

    private void TryApplyForce(Vector3 force, PlayerRef pusher)
    {
        float now = Runner.SimulationTime;
        pusherTimestamps[pusher] = now;

        int activePushers = 0;
        foreach (var kvp in pusherTimestamps)
            if (now - kvp.Value <= pushWindow) activePushers++;

        if (activePushers >= requiredPushers && now - lastForcedTime > forceCooldown)
        {
            rb.AddForce(force, ForceMode.Impulse);
            lastForcedTime = now;
        }
    }
}
