using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Push Settings")]
    public float pushForce = 3f;
    [SerializeField] private LayerMask pushProbeMask = CollisionPolicyBootstrap.PushableMask;
    [SerializeField] private float pushProbeRadius = 0.25f;
    [SerializeField] private float pushProbeDistance = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float blockedPushSlowdown = 0f;

    [Header("Movement Settings")]
    public float walkSpeed   = 2f;
    public float runSpeed    = 5f;
    public float sprintSpeed = 8f;
    public float turnSpeed   = 720f;

    [Header("Gravity & Jump")]
    public float gravity    = -9.81f;
    public float jumpHeight = 1.5f;

    private NetworkCharacterController cc;
    private CharacterController characterController;
    private readonly RaycastHit[] pushProbeHits = new RaycastHit[8];

    [Networked] public NetworkBool IsFalling   { get; set; }
    [Networked] private float Yaw              { get; set; }
    [Networked] public Vector2 MoveInput       { get; set; }
    [Networked] public NetworkBool IsSprinting { get; set; }
    [Networked] public NetworkBool IsJumping   { get; set; }
    [Networked] public float CameraYaw         { get; private set; }
    [Networked] public float CameraPitch       { get; private set; }

    public bool  IsGrounded      => cc.Grounded;
    public float VerticalVelocity => cc.Velocity.y;

    void Awake()
    {
        cc = GetComponent<NetworkCharacterController>();
        characterController = GetComponent<CharacterController>();
        CollisionPolicyBootstrap.ApplyLayerToColliderOwners(gameObject, CollisionPolicyBootstrap.PlayerBodyLayer);

        if (pushProbeMask.value == 0)
            pushProbeMask = CollisionPolicyBootstrap.PushableMask;
    }

    public override void Spawned()
    {
        cc.gravity       = gravity;
        cc.rotationSpeed = 0f;    // Yaw로 직접 제어
        cc.acceleration  = 100f;  // 즉각 반응
        cc.braking       = 100f;
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerNetworkInput input)) return;

        CameraYaw = input.yaw;
        CameraPitch = input.pitch;

        bool sprinting = input.isSprinting && input.moveInput.y > 0;
        bool jumping   = input.isJumping && cc.Grounded;

        IsSprinting = sprinting;
        IsJumping   = jumping;

        if (jumping)
            cc.Jump(overrideImpulse: Mathf.Sqrt(jumpHeight * -2f * gravity));

        if (sprinting)                              cc.maxSpeed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f) cc.maxSpeed = runSpeed;
        else if (input.moveInput.magnitude > 0f)   cc.maxSpeed = walkSpeed;

        Quaternion cameraYaw = Quaternion.Euler(0f, input.yaw, 0f);
        Vector3 right   = cameraYaw * Vector3.right;
        Vector3 forward = cameraYaw * Vector3.forward;
        right.y   = 0f; right.Normalize();
        forward.y = 0f; forward.Normalize();

        Vector3 moveDir = right * input.moveInput.x + forward * input.moveInput.y;
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Runner.DeltaTime);

            Vector3 localMoveDirection = transform.InverseTransformDirection(moveDir);
            MoveInput = new Vector2(localMoveDirection.x, localMoveDirection.z);
        }
        else
        {
            MoveInput = Vector2.zero;
        }

        Yaw = transform.eulerAngles.y;

        Vector3 resolvedMoveDir = ResolvePushProbe(moveDir);
        cc.Move(resolvedMoveDir); // NCC 내부에서 normalize + gravity + grounded 처리

        IsFalling = !cc.Grounded && cc.Velocity.y < 0f;
    }

    private Vector3 ResolvePushProbe(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude <= 0.0001f)
            return moveDir;

        if (!TryFindPushable(moveDir, out RaycastHit hit, out IPushable pushable))
            return moveDir;

        if (HasInputAuthority)
        {
            Vector3 force = moveDir.normalized * pushForce;
            force.y = 0f;
            pushable.OnPush(force, Object.InputAuthority);
        }

        if (hit.collider != null && hit.collider.gameObject.layer == CollisionPolicyBootstrap.HeavyPuzzleLayer)
            return RemoveBlockedPushComponent(moveDir, hit.normal);

        return moveDir;
    }

    private bool TryFindPushable(Vector3 moveDir, out RaycastHit nearestHit, out IPushable nearestPushable)
    {
        nearestHit = default;
        nearestPushable = null;

        if (pushProbeMask.value == 0 || pushProbeDistance <= 0f)
            return false;

        Vector3 direction = moveDir;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        int hitCount = Runner.GetPhysicsScene().SphereCast(
            GetPushProbeOrigin(),
            GetPushProbeRadius(),
            direction,
            pushProbeHits,
            pushProbeDistance,
            pushProbeMask.value,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = pushProbeHits[i];
            if (hit.collider == null)
                continue;

            IPushable pushable = hit.collider.GetComponentInParent<IPushable>();
            if (pushable == null || hit.distance >= nearestDistance)
                continue;

            nearestHit = hit;
            nearestPushable = pushable;
            nearestDistance = hit.distance;
        }

        return nearestPushable != null;
    }

    private Vector3 GetPushProbeOrigin()
    {
        if (characterController != null)
            return transform.TransformPoint(characterController.center);

        return transform.position + Vector3.up * 0.9f;
    }

    private float GetPushProbeRadius()
    {
        if (pushProbeRadius > 0f)
            return pushProbeRadius;

        if (characterController != null)
            return Mathf.Max(0.01f, characterController.radius);

        return 0.25f;
    }

    private Vector3 RemoveBlockedPushComponent(Vector3 moveDir, Vector3 hitNormal)
    {
        hitNormal.y = 0f;
        if (hitNormal.sqrMagnitude <= 0.0001f)
            return moveDir * blockedPushSlowdown;

        hitNormal.Normalize();

        float inwardAmount = Vector3.Dot(moveDir, -hitNormal);
        if (inwardAmount <= 0f)
            return moveDir;

        return moveDir + hitNormal * inwardAmount * (1f - blockedPushSlowdown);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!HasInputAuthority) return;
        if (hit.collider.gameObject.layer == CollisionPolicyBootstrap.InteractableNoBodyLayer ||
            hit.collider.gameObject.layer == CollisionPolicyBootstrap.HeavyPuzzleLayer)
        {
            return;
        }

        var pushable = hit.collider.GetComponentInParent<IPushable>();
        if (pushable == null) return;

        Vector3 force = hit.moveDirection * pushForce;
        force.y = 0f;
        pushable.OnPush(force, Object.InputAuthority);
    }
}
