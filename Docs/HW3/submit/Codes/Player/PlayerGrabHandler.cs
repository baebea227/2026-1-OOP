using Fusion;
using UnityEngine;

public class PlayerGrabHandler : NetworkBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 3f;
    public float throwSpeed = 10f;
    public float serverValidationPadding = 0.5f;
    public float holdHeight = 1.1f;
    public float holdDistance = 1.25f;

    [Header("References")]
    public Transform holdPoint;
    public Transform cameraTransform;

    public Transform HoldPoint => holdPoint;

    [Networked] public NetworkObject HeldGrabbable { get; set; }
    [Networked] private Vector3 NetworkedAimDirection { get; set; }

    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private Collider[] ownerColliders;
    private PlayerInputHandler inputHandler;

    void Awake()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }

        ownerColliders = GetComponentsInChildren<Collider>();
        inputHandler = GetComponent<PlayerInputHandler>();
        EnsureHoldPoint();
        UpdateHoldPoint(GetFallbackAimDirection());
    }

    public override void Spawned()
    {
        if (NetworkedAimDirection.sqrMagnitude <= 0.0001f && Object.HasStateAuthority)
            NetworkedAimDirection = GetFallbackAimDirection();

        EnsureHoldPoint();
        UpdateHoldPoint(GetRenderAimDirection());
    }

    public override void Render()
    {
        UpdateHoldPoint(GetRenderAimDirection());
    }

    public override void FixedUpdateNetwork()
    {
        bool hasInput = GetInput(out PlayerNetworkInput input);
        Vector3 aimDirection = hasInput ? GetAimDirection(input.yaw, input.pitch) : GetNetworkAimDirection();

        if (Object.HasStateAuthority)
            NetworkedAimDirection = aimDirection;

        UpdateHoldPoint(aimDirection);

        if (!HasInputAuthority || !hasInput) return;

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
            RequestThrow(aimDirection);
        }
    }

    private NetworkObject FindGrabCandidate()
    {
        if (!TryFindGrabbableHit(out var grabbable))
            return null;

        return grabbable.Object;
    }

    private Vector3 GetAimDirection(float yaw, float pitch)
    {
        Vector3 aimDirection = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        return aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : GetFallbackAimDirection();
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
        if (grabbable == null || !CanReach(candidate))
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

    private bool CanReach(NetworkObject candidate)
    {
        if (candidate == null)
            return false;

        float maxDistance = grabRange + serverValidationPadding;
        Vector3 origin = GetReachOrigin();
        float maxDistanceSqr = maxDistance * maxDistance;

        Collider[] colliders = candidate.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return (candidate.transform.position - origin).sqrMagnitude <= maxDistanceSqr;

        foreach (Collider candidateCollider in colliders)
        {
            if (candidateCollider == null || !candidateCollider.enabled)
                continue;

            Vector3 closestPoint = candidateCollider.ClosestPoint(origin);
            if ((closestPoint - origin).sqrMagnitude <= maxDistanceSqr)
                return true;
        }

        return false;
    }

    private void TryInteract()
    {
        if (!TryFindLeverHit(out var lever))
            return;

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

    private void EnsureHoldPoint()
    {
        if (holdPoint == null)
        {
            GameObject holder = new GameObject("HoldPoint");
            holdPoint = holder.transform;
        }

        if (holdPoint.parent != transform)
            holdPoint.SetParent(transform, false);
    }

    private void UpdateHoldPoint(Vector3 aimDirection)
    {
        if (holdPoint == null)
            return;

        Vector3 forward = aimDirection;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = GetFallbackAimDirection();

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();
        holdPoint.position = transform.position + Vector3.up * holdHeight + forward * holdDistance;
        holdPoint.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private Vector3 GetRenderAimDirection()
    {
        if (HasInputAuthority && inputHandler != null)
            return GetAimDirection(inputHandler.CameraYaw, inputHandler.CameraPitch);

        return GetNetworkAimDirection();
    }

    private Vector3 GetNetworkAimDirection()
    {
        return NetworkedAimDirection.sqrMagnitude > 0.0001f ? NetworkedAimDirection.normalized : GetFallbackAimDirection();
    }

    private Vector3 GetFallbackAimDirection()
    {
        Vector3 fallback = cameraTransform != null ? cameraTransform.forward : transform.forward;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private bool TryFindGrabbableHit(out GrabbableObject nearestGrabbable)
    {
        nearestGrabbable = null;
        if (!TryCollectCameraHits(out int hitCount))
            return false;

        SortHitsByDistance(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (IsOwnerCollider(hit.collider))
                continue;

            var grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
            if (grabbable == null)
                return false;

            if (grabbable.Object == null || !CanReach(grabbable.Object))
                return false;

            nearestGrabbable = grabbable;
            return true;
        }

        return false;
    }

    private bool TryFindLeverHit(out Lever nearestLever)
    {
        nearestLever = null;
        if (!TryCollectCameraHits(out int hitCount))
            return false;

        SortHitsByDistance(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (IsOwnerCollider(hit.collider))
                continue;

            var lever = hit.collider.GetComponentInParent<Lever>();
            if (lever == null)
                return false;

            if (!CanReach(hit.collider))
                return false;

            nearestLever = lever;
            return true;
        }

        return false;
    }

    private bool TryCollectCameraHits(out int hitCount)
    {
        hitCount = 0;
        if (cameraTransform == null)
            return false;

        Vector3 reachOrigin = GetReachOrigin();
        float raycastDistance =
            Vector3.Distance(cameraTransform.position, reachOrigin) +
            grabRange +
            serverValidationPadding;

        hitCount = Runner.GetPhysicsScene().Raycast(
            cameraTransform.position,
            cameraTransform.forward,
            raycastHits,
            raycastDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        return hitCount > 0;
    }

    private void SortHitsByDistance(int hitCount)
    {
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit current = raycastHits[i];
            int j = i - 1;

            while (j >= 0 && raycastHits[j].distance > current.distance)
            {
                raycastHits[j + 1] = raycastHits[j];
                j--;
            }

            raycastHits[j + 1] = current;
        }
    }

    private Vector3 GetReachOrigin()
    {
        return transform.position + Vector3.up * holdHeight;
    }

    private bool CanReach(Collider candidateCollider)
    {
        if (candidateCollider == null || !candidateCollider.enabled)
            return false;

        float maxDistance = grabRange + serverValidationPadding;
        Vector3 origin = GetReachOrigin();
        Vector3 closestPoint = candidateCollider.ClosestPoint(origin);
        return (closestPoint - origin).sqrMagnitude <= maxDistance * maxDistance;
    }

    private bool IsOwnerCollider(Collider candidate)
    {
        if (ownerColliders == null || candidate == null)
            return false;

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider == candidate)
                return true;
        }

        return false;
    }
}
