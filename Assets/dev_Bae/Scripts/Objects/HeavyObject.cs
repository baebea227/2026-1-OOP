using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HeavyObject : InteractableObject, IPushable
{
    [Header("Heavy Settings")]
    public int requiredPushers = 2;

    private struct PushSample
    {
        public float time;
        public int directionIndex;
        public float forceMagnitude;
    }

    private const int PositiveLocalX = 0;
    private const int NegativeLocalX = 1;
    private const int PositiveLocalZ = 2;
    private const int NegativeLocalZ = 3;
    private const int DirectionCount = 4;
    private const float minPushForceSqr = 0.0001f;

    private static readonly RigidbodyConstraints MovementConstraints =
        RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

    private readonly Dictionary<PlayerRef, PushSample> pushSamples = new();
    private const float pushWindow = 0.15f;
    private float lastForcedTime = -1f;
    private const float forceCooldown = 0.1f;

    protected override void Awake()
    {
        base.Awake();
        ApplyMovementConstraints();
    }

    public override void Spawned()
    {
        base.Spawned();
        ApplyMovementConstraints();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        ClampMotionToFaceAxis();
    }

    public void OnPush(Vector3 force, PlayerRef pusher)
    {
        if (Object.HasStateAuthority)
            TryApplyForce(force, pusher);
        else
            RPC_Push(force, pusher);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Push(Vector3 force, PlayerRef pusher)
    {
        TryApplyForce(force, pusher);
    }

    private void TryApplyForce(Vector3 force, PlayerRef pusher)
    {
        if (!TrySnapForce(force, out int directionIndex, out float forceMagnitude))
            return;

        float now = Runner.SimulationTime;
        pushSamples[pusher] = new PushSample
        {
            time = now,
            directionIndex = directionIndex,
            forceMagnitude = forceMagnitude
        };

        if (now - lastForcedTime <= forceCooldown)
            return;

        if (TryGetCooperativePush(now, out int cooperativeDirection, out float cooperativeMagnitude))
        {
            Vector3 pushForce = GetWorldDirection(cooperativeDirection) * cooperativeMagnitude;
            rb.WakeUp();
            rb.AddForce(pushForce, ForceMode.Impulse);
            lastForcedTime = now;
        }
    }

    private bool TrySnapForce(Vector3 force, out int directionIndex, out float forceMagnitude)
    {
        force.y = 0f;
        forceMagnitude = force.magnitude;

        if (force.sqrMagnitude <= minPushForceSqr)
        {
            directionIndex = -1;
            return false;
        }

        Vector3 localForce = transform.InverseTransformDirection(force);
        localForce.y = 0f;

        if (Mathf.Abs(localForce.x) >= Mathf.Abs(localForce.z))
            directionIndex = localForce.x >= 0f ? PositiveLocalX : NegativeLocalX;
        else
            directionIndex = localForce.z >= 0f ? PositiveLocalZ : NegativeLocalZ;

        return true;
    }

    private bool TryGetCooperativePush(float now, out int directionIndex, out float forceMagnitude)
    {
        int[] activeCounts = new int[DirectionCount];
        float[] forceSums = new float[DirectionCount];

        foreach (var sample in pushSamples.Values)
        {
            if (now - sample.time > pushWindow)
                continue;

            activeCounts[sample.directionIndex]++;
            forceSums[sample.directionIndex] += sample.forceMagnitude;
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
            forceMagnitude = 0f;
            return false;
        }

        directionIndex = bestDirection;
        forceMagnitude = forceSums[bestDirection] / bestCount;
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

        if (worldDirection.sqrMagnitude <= minPushForceSqr)
            return Vector3.zero;

        return worldDirection.normalized;
    }

    private void ClampMotionToFaceAxis()
    {
        ApplyMovementConstraints();

        if (rb.isKinematic)
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= minPushForceSqr)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (!TrySnapForce(velocity, out int directionIndex, out _))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        Vector3 allowedDirection = GetWorldDirection(directionIndex);
        rb.linearVelocity = allowedDirection * Vector3.Dot(velocity, allowedDirection);
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyMovementConstraints()
    {
        if (rb.constraints != MovementConstraints)
            rb.constraints = MovementConstraints;
    }
}
