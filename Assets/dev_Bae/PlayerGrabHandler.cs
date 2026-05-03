using Fusion;
using UnityEngine;

public class PlayerGrabHandler : NetworkBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 3f;
    public float throwSpeed = 10f;

    [Header("References")]
    public Transform holdPoint;
    public Transform cameraTransform;

    public Transform HoldPoint => holdPoint;

    private GrabbableObject heldObject;

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        if (input.isGrab)
        {
            if (heldObject == null)
                TryGrab();
            else
                Drop();
        }

        if (input.isThrow && heldObject != null)
            Throw();
    }

    private void TryGrab()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, grabRange))
            return;

        var grabbable = hit.collider.GetComponent<GrabbableObject>();
        if (grabbable == null) return;

        heldObject = grabbable;
        heldObject.OnPickup(this);
    }

    private void Drop()
    {
        heldObject.OnDrop();
        heldObject = null;
    }

    private void Throw()
    {
        if (cameraTransform == null) return;
        Vector3 velocity = cameraTransform.forward * throwSpeed;
        heldObject.OnThrow(velocity);
        heldObject = null;
    }
}
