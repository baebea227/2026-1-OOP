using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    [Networked] private NetworkObject HolderObject { get; set; }

    public void OnPickup(PlayerGrabHandler grabber)
    {
        if (Object.HasStateAuthority) ApplyPickup(grabber.Object);
        else RPC_Pickup(grabber.Object);
    }

    public void OnThrow(PlayerGrabHandler thrower, Vector3 velocity)
    {
        if (Object.HasStateAuthority) ApplyThrow(thrower.Object, velocity);
        else RPC_Throw(thrower.Object, velocity);
    }

    public void OnDrop(PlayerGrabHandler dropper)
    {
        if (Object.HasStateAuthority) ApplyDrop(dropper.Object);
        else RPC_Drop(dropper.Object);
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (HolderObject != null) return;
        if (Object.HasStateAuthority) ApplyPush(force);
        else RPC_Push(force);
    }

    // 이미 잡힌 상태면 무시 — 동시 잡기 race에서 늦게 도착한 RPC 차단
    private void ApplyPickup(NetworkObject holder)
    {
        if (HolderObject != null) return;
        HolderObject = holder;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // 본인이 현재 holder일 때만 해제 — 남의 손에 있는 물체 강제 해제 차단
    private void ApplyThrow(NetworkObject thrower, Vector3 velocity)
    {
        if (HolderObject != thrower) return;
        HolderObject = null;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyDrop(NetworkObject dropper)
    {
        if (HolderObject != dropper) return;
        HolderObject = null;
    }

    // StateAuthority측 재검증 — 호출 시점과 RPC 도착 시점 사이 잡힘 상태 변화 대응
    private void ApplyPush(Vector3 force)
    {
        if (HolderObject != null) return;
        rb.AddForce(force, ForceMode.Impulse);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Pickup(NetworkObject holder) => ApplyPickup(holder);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Throw(NetworkObject thrower, Vector3 velocity) => ApplyThrow(thrower, velocity);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Drop(NetworkObject dropper) => ApplyDrop(dropper);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Push(Vector3 force) => ApplyPush(force);

    public override void FixedUpdateNetwork()
    {
        bool isHeld = HolderObject != null;
        rb.isKinematic = isHeld;

        if (!isHeld) return;

        var holder = HolderObject.GetComponent<PlayerGrabHandler>();
        if (holder != null)
            rb.MovePosition(holder.HoldPoint.position);
    }
}
