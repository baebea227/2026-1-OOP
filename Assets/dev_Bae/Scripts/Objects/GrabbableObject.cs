using Fusion;
using UnityEngine;

public class GrabbableObject : InteractableObject, IPickupable, IPushable
{
    [Header("Grab Settings")]
    public float throwSpeed = 10f;

    [Networked] private NetworkObject HolderObject { get; set; }

    private float lastPushTime = -1f;
    private const float pushCooldown = 0.1f;
    private Collider[] objectColliders;
    private bool[] defaultColliderEnabled;
    private bool defaultUseGravity;
    private bool defaultDetectCollisions;
    private bool physicsDisabledForHold;

    protected override void Awake()
    {
        base.Awake();

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

        var grabber = thrower.GetComponent<PlayerGrabHandler>();
        if (grabber != null && grabber.HeldGrabbable == Object)
            grabber.HeldGrabbable = null;

        HolderObject = null;
        SetHeldPhysicsDisabled(false);
        rb.WakeUp();
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
    }

    private bool CanPush()
    {
        return HolderObject == null && !rb.isKinematic;
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
            return;
        }

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

        if (!isHeld) return;

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
