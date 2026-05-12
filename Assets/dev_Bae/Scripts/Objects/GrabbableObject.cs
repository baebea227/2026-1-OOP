using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    [Networked] private NetworkObject HolderObject { get; set; }

    private float lastPushTime = -1f;
    private const float pushCooldown = 0.1f;

    public void OnPickup(PlayerGrabHandler grabber)
    {
        if (grabber == null) return;
        TryPickup(grabber.Object);
    }

    public void OnThrow(PlayerGrabHandler thrower, Vector3 velocity)
    {
        if (thrower == null) return;
        TryThrow(thrower.Object, velocity);
    }

    public void OnDrop(PlayerGrabHandler dropper)
    {
        if (dropper == null) return;
        TryDrop(dropper.Object);
    }

    public void TryPickup(NetworkObject holder)
    {
        if (Object.HasStateAuthority)
        {
            ApplyPickup(holder);
            return;
        }

        RPC_Pickup(holder);
    }

    public void TryThrow(NetworkObject thrower, Vector3 velocity)
    {
        if (Object.HasStateAuthority)
        {
            ApplyThrow(thrower, velocity);
            return;
        }

        RPC_Throw(thrower, velocity);
    }

    public void TryDrop(NetworkObject dropper)
    {
        if (Object.HasStateAuthority)
        {
            ApplyDrop(dropper);
            return;
        }

        RPC_Drop(dropper);
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (HolderObject != null) return;
        if (Object.HasStateAuthority) ApplyPush(force);
        else RPC_Push(force);
    }

    private void ApplyPickup(NetworkObject holder)
    {
        if (holder == null || HolderObject != null)
            return;

        var grabber = holder.GetComponent<PlayerGrabHandler>();
        if (grabber == null || grabber.HeldGrabbable != null)
            return;

        HolderObject = holder;
        grabber.HeldGrabbable = Object;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyThrow(NetworkObject thrower, Vector3 velocity)
    {
        if (thrower == null || HolderObject != thrower)
            return;

        var grabber = thrower.GetComponent<PlayerGrabHandler>();
        if (grabber != null && grabber.HeldGrabbable == Object)
            grabber.HeldGrabbable = null;

        HolderObject = null;
        rb.isKinematic = false;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyDrop(NetworkObject dropper)
    {
        if (dropper == null || HolderObject != dropper)
            return;

        var grabber = dropper.GetComponent<PlayerGrabHandler>();
        if (grabber != null && grabber.HeldGrabbable == Object)
            grabber.HeldGrabbable = null;

        HolderObject = null;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyPush(Vector3 force)
    {
        if (HolderObject != null) return;

        float now = Runner.SimulationTime;
        if (now - lastPushTime < pushCooldown) return;
        lastPushTime = now;

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
        if (!Object.HasStateAuthority)
            return;

        bool isHeld = HolderObject != null;
        rb.isKinematic = isHeld;

        if (!isHeld) return;

        var holder = HolderObject.GetComponent<PlayerGrabHandler>();
        if (holder == null || holder.HoldPoint == null)
        {
            HolderObject = null;
            return;
        }

        rb.MovePosition(holder.HoldPoint.position);
    }
}
