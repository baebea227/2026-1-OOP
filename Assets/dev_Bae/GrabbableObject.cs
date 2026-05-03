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

    public void OnThrow(Vector3 velocity)
    {
        if (Object.HasStateAuthority) ApplyThrow(velocity);
        else RPC_Throw(velocity);
    }

    public void OnDrop()
    {
        if (Object.HasStateAuthority) HolderObject = null;
        else RPC_Drop();
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (HolderObject != null) return;
        if (Object.HasStateAuthority) rb.AddForce(force, ForceMode.Impulse);
        else RPC_Push(force);
    }

    private void ApplyPickup(NetworkObject holder)
    {
        HolderObject = holder;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyThrow(Vector3 velocity)
    {
        HolderObject = null;
        rb.linearVelocity = velocity;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Pickup(NetworkObject holder) => ApplyPickup(holder);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Throw(Vector3 velocity) => ApplyThrow(velocity);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Drop() => HolderObject = null;

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Push(Vector3 force) => rb.AddForce(force, ForceMode.Impulse);

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
