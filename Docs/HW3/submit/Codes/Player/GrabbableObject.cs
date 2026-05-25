using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    [Header("Release Safety")]
    [SerializeField] private LayerMask releaseBlockMask = CollisionPolicyBootstrap.PlayerBodyMask;
    [SerializeField] private float releasePadding = 0.05f;
    [SerializeField] private float[] releaseCandidateDistances = { 0f, 0.5f, 1f, 1.5f };

    [Header("Player Collision Safety")]
    [SerializeField] private LayerMask playerBlockMask = CollisionPolicyBootstrap.PlayerBodyMask;
    [SerializeField] private float playerBlockPadding = 0.02f;

    [Networked] private NetworkObject HolderObject { get; set; }

    private float lastPushTime = -1f;
    private const float pushCooldown = 0.1f;
    private const float minExtent = 0.01f;
    private const float minVelocitySqr = 0.0001f;
    private Collider[] objectColliders;
    private bool[] defaultColliderEnabled;
    private bool defaultUseGravity;
    private bool defaultDetectCollisions;
    private bool physicsDisabledForHold;
    private readonly Collider[] releaseOverlapResults = new Collider[16];
    private readonly Collider[] playerOverlapResults = new Collider[16];

    protected override void Awake()
    {
        base.Awake();
        ApplyCollisionLayer(isHeld: false);

        if (releaseBlockMask.value == 0)
            releaseBlockMask = CollisionPolicyBootstrap.PlayerBodyMask;

        if (playerBlockMask.value == 0)
            playerBlockMask = CollisionPolicyBootstrap.PlayerBodyMask;

        objectColliders = GetComponentsInChildren<Collider>();
        defaultColliderEnabled = new bool[objectColliders.Length];
        for (int i = 0; i < objectColliders.Length; i++)
            defaultColliderEnabled[i] = objectColliders[i] != null && objectColliders[i].enabled;

        defaultUseGravity = rb.useGravity;
        defaultDetectCollisions = rb.detectCollisions;
    }

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
        if (!CanPush()) return;

        if (Object.HasStateAuthority)
        {
            ApplyPush(force);
            return;
        }

        ApplyPush(force);
        RPC_Push(force);
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
        ClearVelocityIfDynamic();
        SetHeldPhysicsDisabled(true);
    }

    private void ApplyThrow(NetworkObject thrower, Vector3 velocity)
    {
        if (thrower == null || HolderObject != thrower)
            return;

        if (!TryFindThrowReleasePosition(thrower, velocity, out Vector3 releasePosition))
            return;

        ClearHolderReference(thrower);
        SetReleasePosition(releasePosition);
        SetHeldPhysicsDisabled(false);
        rb.WakeUp();
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;
        ClampPlayerDirectedVelocity();
    }

    private void ApplyDrop(NetworkObject dropper)
    {
        if (dropper == null || HolderObject != dropper)
            return;

        if (!TryFindDropReleasePosition(out Vector3 releasePosition))
            return;

        ClearHolderReference(dropper);
        SetReleasePosition(releasePosition);
        SetHeldPhysicsDisabled(false);
        rb.WakeUp();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyPush(Vector3 force)
    {
        if (!CanPush()) return;

        float now = Runner.SimulationTime;
        if (now - lastPushTime < pushCooldown) return;
        lastPushTime = now;

        rb.WakeUp();
        rb.AddForce(force, ForceMode.Impulse);
        ClampPlayerDirectedVelocity();
    }

    private bool CanPush()
    {
        return HolderObject == null && !rb.isKinematic;
    }

    private void ClearHolderReference(NetworkObject holder)
    {
        var grabber = holder != null ? holder.GetComponent<PlayerGrabHandler>() : null;
        if (grabber != null && grabber.HeldGrabbable == Object)
            grabber.HeldGrabbable = null;

        HolderObject = null;
    }

    private void SetReleasePosition(Vector3 releasePosition)
    {
        transform.position = releasePosition;
        rb.position = releasePosition;
    }

    private bool TryFindDropReleasePosition(out Vector3 releasePosition)
    {
        releasePosition = rb.position;
        return IsReleasePositionClear(releasePosition);
    }

    private bool TryFindThrowReleasePosition(NetworkObject thrower, Vector3 velocity, out Vector3 releasePosition)
    {
        Vector3 releaseDirection = velocity;
        if (releaseDirection.sqrMagnitude <= 0.0001f && thrower != null)
            releaseDirection = thrower.transform.forward;

        if (releaseDirection.sqrMagnitude <= 0.0001f)
            releaseDirection = transform.forward;

        releaseDirection.Normalize();

        if (releaseCandidateDistances == null || releaseCandidateDistances.Length == 0)
        {
            releasePosition = rb.position;
            return IsReleasePositionClear(releasePosition);
        }

        Vector3 basePosition = rb.position;
        for (int i = 0; i < releaseCandidateDistances.Length; i++)
        {
            float distance = Mathf.Max(0f, releaseCandidateDistances[i]);
            Vector3 candidatePosition = basePosition + releaseDirection * distance;
            if (!IsReleasePositionClear(candidatePosition))
                continue;

            releasePosition = candidatePosition;
            return true;
        }

        releasePosition = basePosition;
        return false;
    }

    private bool IsReleasePositionClear(Vector3 candidatePosition)
    {
        if (releaseBlockMask.value == 0)
            return true;

        if (objectColliders == null || objectColliders.Length == 0)
            return !HasBlockingOverlap(candidatePosition, Vector3.one * releasePadding, Quaternion.identity);

        for (int i = 0; i < objectColliders.Length; i++)
        {
            Collider objectCollider = objectColliders[i];
            if (objectCollider == null || !WasColliderEnabledByDefault(i))
                continue;

            if (!TryGetOverlapBox(objectCollider, candidatePosition, releasePadding, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
                continue;

            if (HasBlockingOverlap(center, halfExtents, rotation))
                return false;
        }

        return true;
    }

    private bool HasBlockingOverlap(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        halfExtents.x = Mathf.Max(minExtent, halfExtents.x);
        halfExtents.y = Mathf.Max(minExtent, halfExtents.y);
        halfExtents.z = Mathf.Max(minExtent, halfExtents.z);

        int overlapCount = Runner != null
            ? Runner.GetPhysicsScene().OverlapBox(
                center,
                halfExtents,
                releaseOverlapResults,
                rotation,
                releaseBlockMask.value,
                QueryTriggerInteraction.Ignore)
            : Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                releaseOverlapResults,
                rotation,
                releaseBlockMask.value,
                QueryTriggerInteraction.Ignore);

        return overlapCount > 0;
    }

    private void ClampPlayerDirectedVelocity()
    {
        if (HolderObject != null || rb == null || rb.isKinematic || !rb.detectCollisions)
            return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude <= minVelocitySqr || playerBlockMask.value == 0)
            return;

        float deltaTime = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
        Vector3 predictedPosition = rb.position + velocity * deltaTime;

        bool adjusted = RemovePlayerDirectedVelocityAt(predictedPosition, ref velocity);
        adjusted |= RemovePlayerDirectedVelocityAt(rb.position, ref velocity);

        if (adjusted)
            rb.linearVelocity = velocity;
    }

    private bool RemovePlayerDirectedVelocityAt(Vector3 candidatePosition, ref Vector3 velocity)
    {
        if (objectColliders == null || objectColliders.Length == 0)
        {
            return RemovePlayerDirectedVelocityForBox(
                candidatePosition,
                Vector3.one * Mathf.Max(minExtent, playerBlockPadding),
                Quaternion.identity,
                ref velocity);
        }

        bool adjusted = false;
        for (int i = 0; i < objectColliders.Length; i++)
        {
            Collider objectCollider = objectColliders[i];
            if (objectCollider == null || !WasColliderEnabledByDefault(i))
                continue;

            if (!TryGetOverlapBox(objectCollider, candidatePosition, playerBlockPadding, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
                continue;

            if (RemovePlayerDirectedVelocityForBox(center, halfExtents, rotation, ref velocity))
                adjusted = true;
        }

        return adjusted;
    }

    private bool RemovePlayerDirectedVelocityForBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, ref Vector3 velocity)
    {
        halfExtents.x = Mathf.Max(minExtent, halfExtents.x);
        halfExtents.y = Mathf.Max(minExtent, halfExtents.y);
        halfExtents.z = Mathf.Max(minExtent, halfExtents.z);

        int overlapCount = Runner != null
            ? Runner.GetPhysicsScene().OverlapBox(
                center,
                halfExtents,
                playerOverlapResults,
                rotation,
                playerBlockMask.value,
                QueryTriggerInteraction.Ignore)
            : Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                playerOverlapResults,
                rotation,
                playerBlockMask.value,
                QueryTriggerInteraction.Ignore);

        bool adjusted = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider playerCollider = playerOverlapResults[i];
            if (playerCollider == null)
                continue;

            Vector3 directionToPlayer = playerCollider.ClosestPoint(center) - center;
            if (directionToPlayer.sqrMagnitude <= minVelocitySqr)
                directionToPlayer = playerCollider.bounds.center - center;

            if (directionToPlayer.sqrMagnitude <= minVelocitySqr)
                continue;

            directionToPlayer.Normalize();
            float inwardSpeed = Vector3.Dot(velocity, directionToPlayer);
            if (inwardSpeed <= 0f)
                continue;

            velocity -= directionToPlayer * inwardSpeed;
            adjusted = true;
        }

        return adjusted;
    }

    private bool TryGetOverlapBox(Collider sourceCollider, Vector3 candidateRootPosition, float padding, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        center = candidateRootPosition;
        halfExtents = Vector3.one * padding;
        rotation = Quaternion.identity;

        if (sourceCollider is BoxCollider boxCollider)
        {
            center = candidateRootPosition + (boxCollider.transform.TransformPoint(boxCollider.center) - transform.position);
            halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(boxCollider.transform.lossyScale)) + Vector3.one * padding;
            rotation = boxCollider.transform.rotation;
            return true;
        }

        if (sourceCollider is SphereCollider sphereCollider)
        {
            center = candidateRootPosition + (sphereCollider.transform.TransformPoint(sphereCollider.center) - transform.position);
            float radius = sphereCollider.radius * MaxComponent(Abs(sphereCollider.transform.lossyScale)) + padding;
            halfExtents = Vector3.one * radius;
            rotation = Quaternion.identity;
            return true;
        }

        if (sourceCollider is CapsuleCollider capsuleCollider)
        {
            center = candidateRootPosition + (capsuleCollider.transform.TransformPoint(capsuleCollider.center) - transform.position);
            halfExtents = GetCapsuleApproximateHalfExtents(capsuleCollider) + Vector3.one * padding;
            rotation = capsuleCollider.transform.rotation;
            return true;
        }

        if (!sourceCollider.enabled)
            return false;

        Bounds bounds = sourceCollider.bounds;
        if (bounds.size.sqrMagnitude <= 0.0001f)
            return false;

        center = candidateRootPosition + (bounds.center - transform.position);
        halfExtents = bounds.extents + Vector3.one * padding;
        rotation = Quaternion.identity;
        return true;
    }

    private bool WasColliderEnabledByDefault(int index)
    {
        return defaultColliderEnabled == null ||
            index >= defaultColliderEnabled.Length ||
            defaultColliderEnabled[index];
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private static Vector3 GetCapsuleApproximateHalfExtents(CapsuleCollider capsuleCollider)
    {
        Vector3 scale = Abs(capsuleCollider.transform.lossyScale);
        float axisScale = capsuleCollider.direction switch
        {
            0 => scale.x,
            1 => scale.y,
            _ => scale.z
        };

        float radiusScale = capsuleCollider.direction switch
        {
            0 => Mathf.Max(scale.y, scale.z),
            1 => Mathf.Max(scale.x, scale.z),
            _ => Mathf.Max(scale.x, scale.y)
        };

        float radius = capsuleCollider.radius * radiusScale;
        float halfHeight = Mathf.Max(capsuleCollider.height * axisScale * 0.5f, radius);
        Vector3 halfExtents = Vector3.one * radius;

        if (capsuleCollider.direction == 0)
            halfExtents.x = halfHeight;
        else if (capsuleCollider.direction == 1)
            halfExtents.y = halfHeight;
        else
            halfExtents.z = halfHeight;

        return halfExtents;
    }

    private void ClearVelocityIfDynamic()
    {
        if (rb.isKinematic) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void SetKinematic(bool isKinematic)
    {
        if (rb.isKinematic != isKinematic)
            rb.isKinematic = isKinematic;
    }

    private void SetHeldPhysicsDisabled(bool disabled)
    {
        bool expectedDetectCollisions = disabled ? false : defaultDetectCollisions;
        bool expectedUseGravity = disabled ? false : defaultUseGravity;
        if (physicsDisabledForHold == disabled &&
            rb.isKinematic == disabled &&
            rb.detectCollisions == expectedDetectCollisions &&
            rb.useGravity == expectedUseGravity)
        {
            ApplyCollisionLayer(disabled);
            return;
        }

        ApplyCollisionLayer(disabled);

        if (disabled)
        {
            ClearVelocityIfDynamic();
            SetKinematic(true);
            rb.useGravity = false;
            rb.detectCollisions = false;
            SetCollidersEnabled(false);
        }
        else
        {
            SetKinematic(false);
            rb.useGravity = defaultUseGravity;
            rb.detectCollisions = defaultDetectCollisions;
            SetCollidersEnabled(true);
        }

        physicsDisabledForHold = disabled;
    }

    private void ApplyCollisionLayer(bool isHeld)
    {
        int layer = isHeld
            ? CollisionPolicyBootstrap.InteractableNoBodyLayer
            : CollisionPolicyBootstrap.ObjectBodyLayer;

        CollisionPolicyBootstrap.ApplyLayerToColliderOwners(gameObject, layer);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (objectColliders == null)
            return;

        for (int i = 0; i < objectColliders.Length; i++)
        {
            Collider objectCollider = objectColliders[i];
            if (objectCollider == null)
                continue;

            bool wasEnabledByDefault =
                defaultColliderEnabled == null ||
                i >= defaultColliderEnabled.Length ||
                defaultColliderEnabled[i];
            bool targetEnabled = enabled && wasEnabledByDefault;
            if (objectCollider.enabled != targetEnabled)
                objectCollider.enabled = targetEnabled;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Pickup(NetworkObject holder) => ApplyPickup(holder);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Throw(NetworkObject thrower, Vector3 velocity) => ApplyThrow(thrower, velocity);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Drop(NetworkObject dropper) => ApplyDrop(dropper);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Push(Vector3 force) => ApplyPush(force);

    public override void Render()
    {
        ApplyHeldVisualCorrection();
    }

    public override void FixedUpdateNetwork()
    {
        bool isHeld = HolderObject != null;
        SetHeldPhysicsDisabled(isHeld);

        if (!Object.HasStateAuthority)
            return;

        if (!isHeld)
        {
            ClampPlayerDirectedVelocity();
            return;
        }

        var holder = HolderObject.GetComponent<PlayerGrabHandler>();
        if (holder == null || holder.HoldPoint == null)
        {
            HolderObject = null;
            SetHeldPhysicsDisabled(false);
            return;
        }

        rb.MovePosition(holder.HoldPoint.position);
    }

    private void ApplyHeldVisualCorrection()
    {
        if (Object.HasStateAuthority || HolderObject == null)
            return;

        var holder = HolderObject.GetComponent<PlayerGrabHandler>();
        if (holder == null || holder.HoldPoint == null)
            return;

        transform.position = holder.HoldPoint.position;
    }
}
