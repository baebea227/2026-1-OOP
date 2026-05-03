using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    [Networked] private NetworkObject HolderObject { get; set; }

    public void OnPickup(PlayerGrabHandler grabber)
    {
        HolderObject = grabber.Object;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void OnThrow(Vector3 velocity)
    {
        HolderObject = null;
        rb.linearVelocity = velocity;
    }

    public void OnDrop()
    {
        HolderObject = null;
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (HolderObject != null) return;
        if (Object.HasStateAuthority)
            rb.AddForce(force, ForceMode.Impulse);
        else
            RPC_Push(force);
    }

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
