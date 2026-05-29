using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HeavyObject : InteractableObject
{
    [Header("Heavy Settings")]
    public int requiredPushers = 2;
    public float scriptedMoveSpeed = 1.5f;
    public float pushHoldDuration = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] private float pushIntentDotThreshold = 0.65f;
    [SerializeField] private Transform rangeMinCorner;
    [SerializeField] private Transform rangeMaxCorner;

    private struct PushSample
    {
        public float time;
        public int directionIndex;
    }

    private const int PositiveLocalX = 0;
    private const int NegativeLocalX = 1;
    private const int PositiveLocalZ = 2;
    private const int NegativeLocalZ = 3;
    private const int DirectionCount = 4;
    private const float minDirectionSqr = 0.0001f;

    private static readonly RigidbodyConstraints ScriptedMovementConstraints =
        RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

    private readonly Dictionary<PlayerRef, PushSample> pushSamples = new();
    private const float pushWindow = 0.15f;
    private Collider[] pushFaceColliders;
    private float activeMoveUntil = -1f;
    private int activeDirection = -1;
    private bool warnedIncompleteRange;

    protected override void Awake()
    {
        base.Awake();
        CollisionPolicyBootstrap.ApplyLayerToColliderOwners(gameObject, CollisionPolicyBootstrap.HeavyPuzzleLayer);
        CachePushFaceColliders();
        ApplyScriptedMovementSetup();
    }

    public override void Spawned()
    {
        base.Spawned();
        CachePushFaceColliders();
        ApplyScriptedMovementSetup();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        float now = Runner.SimulationTime;
        if (TryGetCooperativePush(now, out int cooperativeDirection))
            ActivateScriptedMove(cooperativeDirection, now);

        ApplyScriptedMove(now);
    }

    public bool TryResolvePushIntent(
        Vector3 pusherPosition,
        Vector3 moveDirection,
        out int directionIndex,
        out Vector3 blockNormal)
    {
        directionIndex = -1;
        blockNormal = Vector3.zero;

        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude <= minDirectionSqr)
            return false;

        if (!TryResolveNearestPushFace(pusherPosition, out directionIndex, out blockNormal))
            return false;

        Vector3 pushDirection = -blockNormal;
        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude <= minDirectionSqr)
            return false;

        pushDirection.Normalize();
        moveDirection.Normalize();

        return Vector3.Dot(moveDirection, pushDirection) >= pushIntentDotThreshold;
    }

    public void SubmitPushIntent(int directionIndex, PlayerRef pusher)
    {
        if (Object.HasStateAuthority)
            TryRecordPush(directionIndex, pusher);
        else
            RPC_SubmitPushIntent(directionIndex, pusher);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitPushIntent(int directionIndex, PlayerRef pusher)
    {
        TryRecordPush(directionIndex, pusher);
    }

    private void TryRecordPush(int directionIndex, PlayerRef pusher)
    {
        if (!IsValidDirectionIndex(directionIndex))
            return;

        float now = Runner.SimulationTime;
        pushSamples[pusher] = new PushSample
        {
            time = now,
            directionIndex = directionIndex
        };
    }

    private bool TryResolveNearestPushFace(Vector3 pusherPosition, out int directionIndex, out Vector3 blockNormal)
    {
        directionIndex = -1;
        blockNormal = Vector3.zero;

        if (!TryGetLocalColliderBounds(out Bounds localBounds))
            return false;

        Vector3 localPusherPosition = transform.InverseTransformPoint(pusherPosition);

        float distanceToPositiveX = Mathf.Abs(localPusherPosition.x - localBounds.max.x);
        float distanceToNegativeX = Mathf.Abs(localPusherPosition.x - localBounds.min.x);
        float distanceToPositiveZ = Mathf.Abs(localPusherPosition.z - localBounds.max.z);
        float distanceToNegativeZ = Mathf.Abs(localPusherPosition.z - localBounds.min.z);

        float nearestDistance = distanceToPositiveX;
        Vector3 localBlockNormal = Vector3.right;
        directionIndex = NegativeLocalX;

        if (distanceToNegativeX < nearestDistance)
        {
            nearestDistance = distanceToNegativeX;
            localBlockNormal = Vector3.left;
            directionIndex = PositiveLocalX;
        }

        if (distanceToPositiveZ < nearestDistance)
        {
            nearestDistance = distanceToPositiveZ;
            localBlockNormal = Vector3.forward;
            directionIndex = NegativeLocalZ;
        }

        if (distanceToNegativeZ < nearestDistance)
        {
            localBlockNormal = Vector3.back;
            directionIndex = PositiveLocalZ;
        }

        blockNormal = transform.TransformDirection(localBlockNormal);
        blockNormal.y = 0f;
        if (blockNormal.sqrMagnitude <= minDirectionSqr)
            return false;

        blockNormal.Normalize();
        return true;
    }

    private bool TryGetLocalColliderBounds(out Bounds localBounds)
    {
        localBounds = default;

        if (pushFaceColliders == null || pushFaceColliders.Length == 0)
            CachePushFaceColliders();

        bool hasBounds = false;
        if (pushFaceColliders != null)
        {
            foreach (Collider objectCollider in pushFaceColliders)
            {
                if (objectCollider == null || objectCollider.isTrigger)
                    continue;

                EncapsulateWorldBoundsAsLocal(objectCollider.bounds, ref localBounds, ref hasBounds);
            }
        }

        if (hasBounds)
            return true;

        localBounds = new Bounds(Vector3.zero, Vector3.one);
        return true;
    }

    private void EncapsulateWorldBoundsAsLocal(Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        EncapsulateLocalPoint(new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
        EncapsulateLocalPoint(new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
    }

    private void EncapsulateLocalPoint(Vector3 worldPoint, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    private void CachePushFaceColliders()
    {
        pushFaceColliders = GetComponentsInChildren<Collider>();
    }

    private bool IsValidDirectionIndex(int directionIndex)
    {
        return directionIndex >= 0 && directionIndex < DirectionCount;
    }

    private bool TryGetCooperativePush(float now, out int directionIndex)
    {
        int[] activeCounts = new int[DirectionCount];

        foreach (var sample in pushSamples.Values)
        {
            if (now - sample.time > pushWindow)
                continue;

            activeCounts[sample.directionIndex]++;
        }

        int bestDirection = -1;
        int bestCount = 0;
        bool hasTie = false;

        for (int i = 0; i < DirectionCount; i++)
        {
            if (activeCounts[i] <= bestCount)
            {
                if (activeCounts[i] == bestCount && bestCount > 0)
                    hasTie = true;

                continue;
            }

            bestDirection = i;
            bestCount = activeCounts[i];
            hasTie = false;
        }

        if (bestDirection < 0 || bestCount < requiredPushers || hasTie)
        {
            directionIndex = -1;
            return false;
        }

        directionIndex = bestDirection;
        return true;
    }

    private Vector3 GetWorldDirection(int directionIndex)
    {
        Vector3 localDirection = directionIndex switch
        {
            PositiveLocalX => Vector3.right,
            NegativeLocalX => Vector3.left,
            PositiveLocalZ => Vector3.forward,
            NegativeLocalZ => Vector3.back,
            _ => Vector3.zero
        };

        Vector3 worldDirection = transform.TransformDirection(localDirection);
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= minDirectionSqr)
            return Vector3.zero;

        return worldDirection.normalized;
    }

    private void ApplyScriptedMove(float now)
    {
        StopMotion();

        if (!HasAuthorizedMovement(now))
        {
            activeDirection = -1;
            return;
        }

        Vector3 moveDirection = GetWorldDirection(activeDirection);
        if (moveDirection.sqrMagnitude <= minDirectionSqr)
            return;

        float moveDistance = Mathf.Max(0f, scriptedMoveSpeed) * Runner.DeltaTime;
        if (moveDistance <= 0f)
            return;

        Vector3 nextPosition = rb.position + moveDirection * moveDistance;
        if (!TryApplyMovementRange(ref nextPosition))
            return;

        rb.MovePosition(nextPosition);
    }

    private void ActivateScriptedMove(int directionIndex, float now)
    {
        activeDirection = directionIndex;
        activeMoveUntil = now + Mathf.Max(0f, pushHoldDuration);
    }

    private bool HasAuthorizedMovement(float now)
    {
        return activeDirection >= 0 && now <= activeMoveUntil;
    }

    private void StopMotion()
    {
        if (rb == null || rb.isKinematic)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyScriptedMovementSetup()
    {
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        if (rb.constraints != ScriptedMovementConstraints)
            rb.constraints = ScriptedMovementConstraints;

        StopMotion();
    }

    private bool TryApplyMovementRange(ref Vector3 nextPosition)
    {
        if (rangeMinCorner == null && rangeMaxCorner == null)
            return true;

        if (rangeMinCorner == null || rangeMaxCorner == null)
        {
            if (!warnedIncompleteRange)
            {
                Debug.LogWarning($"{name} has an incomplete HeavyObject movement range.", this);
                warnedIncompleteRange = true;
            }

            return false;
        }

        Vector3 a = rangeMinCorner.position;
        Vector3 b = rangeMaxCorner.position;

        nextPosition.x = Mathf.Clamp(nextPosition.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x));
        nextPosition.y = rb.position.y;
        nextPosition.z = Mathf.Clamp(nextPosition.z, Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z));
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (rangeMinCorner == null || rangeMaxCorner == null)
            return;

        Vector3 a = rangeMinCorner.position;
        Vector3 b = rangeMaxCorner.position;
        Vector3 center = new Vector3((a.x + b.x) * 0.5f, transform.position.y, (a.z + b.z) * 0.5f);
        Vector3 size = new Vector3(Mathf.Abs(a.x - b.x), 0.05f, Mathf.Abs(a.z - b.z));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
}
