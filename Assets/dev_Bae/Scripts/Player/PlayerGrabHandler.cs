using Fusion;
using UnityEngine;

public class PlayerGrabHandler : NetworkBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 3f;
    public float throwSpeed = 10f;
    public float serverValidationPadding = 0.5f;

    [Header("References")]
    public Transform holdPoint;
    public Transform cameraTransform;

    public Transform HoldPoint => holdPoint;

    [Networked] public NetworkObject HeldGrabbable { get; set; }

    void Awake()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        if (input.isGrab)
        {
            RequestToggleGrab(FindGrabCandidate());
        }

        if (input.isInteract)
        {
            TryInteract();
        }

        if (input.isThrow)
        {
            RequestThrow(GetAimDirection());
        }
    }

    private NetworkObject FindGrabCandidate()
    {
        if (cameraTransform == null) return null;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, grabRange))
            return null;

        var grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
        return grabbable != null ? grabbable.Object : null;
    }

    private Vector3 GetAimDirection()
    {
        return cameraTransform != null ? cameraTransform.forward : transform.forward;
    }

    private void RequestToggleGrab(NetworkObject candidate)
    {
        if (Object.HasStateAuthority)
        {
            ApplyToggleGrab(candidate);
            return;
        }

        RPC_RequestToggleGrab(candidate);
    }

    private void RequestThrow(Vector3 aimDirection)
    {
        if (Object.HasStateAuthority)
        {
            ApplyThrow(aimDirection);
            return;
        }

        RPC_RequestThrow(aimDirection);
    }

    private void ApplyToggleGrab(NetworkObject candidate)
    {
        if (HeldGrabbable != null)
        {
            DropHeldObject();
            return;
        }

        if (candidate == null)
            return;

        var grabbable = candidate.GetComponent<GrabbableObject>();
        if (grabbable == null || !CanReach(candidate.transform.position))
            return;

        grabbable.TryPickup(Object);
    }

    private void ApplyThrow(Vector3 aimDirection)
    {
        if (HeldGrabbable == null)
            return;

        var grabbable = HeldGrabbable.GetComponent<GrabbableObject>();
        if (grabbable == null)
        {
            HeldGrabbable = null;
            return;
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = transform.forward;

        grabbable.TryThrow(Object, aimDirection.normalized * throwSpeed);
    }

    private void DropHeldObject()
    {
        var grabbable = HeldGrabbable != null ? HeldGrabbable.GetComponent<GrabbableObject>() : null;
        if (grabbable == null)
        {
            HeldGrabbable = null;
            return;
        }

        grabbable.TryDrop(Object);
    }

    private bool CanReach(Vector3 targetPosition)
    {
        float maxDistance = grabRange + serverValidationPadding;
        Vector3 origin = cameraTransform != null ? cameraTransform.position : transform.position;
        return Vector3.Distance(origin, targetPosition) <= maxDistance;
    }

    private void TryInteract()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, grabRange))
            return;

        var lever = hit.collider.GetComponentInParent<Lever>();
        if (lever == null) return;

        lever.TryOperate(Object);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestToggleGrab(NetworkObject candidate)
    {
        ApplyToggleGrab(candidate);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestThrow(Vector3 aimDirection)
    {
        ApplyThrow(aimDirection);
    }
}
