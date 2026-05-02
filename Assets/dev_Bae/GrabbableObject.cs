using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    private PlayerGrabHandler currentHolder;

    public void OnPickup(PlayerGrabHandler grabber)
    {
        currentHolder = grabber;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void OnThrow(Vector3 velocity)
    {
        currentHolder = null;
        rb.isKinematic = false;
        rb.linearVelocity = velocity;
    }

    public void OnDrop()
    {
        currentHolder = null;
        rb.isKinematic = false;
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (currentHolder != null) return;
        rb.AddForce(force, ForceMode.Impulse);
    }

    public override void FixedUpdateNetwork()
    {
        if (currentHolder == null) return;
        rb.MovePosition(currentHolder.HoldPoint.position);
    }
}
