using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Push Settings")]
    public float pushForce = 3f;
    [SerializeField] private LayerMask pushProbeMask = CollisionPolicyBootstrap.PushableMask;
    [SerializeField] private LayerMask heavyPushProbeMask = 1 << CollisionPolicyBootstrap.HeavyPuzzleLayer;
    [SerializeField] private float pushProbeRadius = 0.25f;
    [SerializeField] private float pushProbeDistance = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float blockedPushSlowdown = 0f;
    [SerializeField, Range(0f, 1f)] private float pushingSpeedMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float pushingAnimationInputScale = 0.5f;

    [Header("Movement Settings")]
    public float walkSpeed   = 2f;
    public float runSpeed    = 5f;
    public float sprintSpeed = 8f;
    public float turnSpeed   = 720f;

    [Header("Gravity & Jump")]
    public float gravity    = -9.81f;
    public float jumpHeight = 1.5f;
    [SerializeField] private float stepRiseVelocityTolerance = 0.01f;
    [SerializeField] private float stepGroundCheckDistance = 0.12f;

    private const float minCarryDeltaSqr = 0.00000001f;

    private NetworkCharacterController cc;
    private CharacterController characterController;
    private readonly RaycastHit[] pushProbeHits = new RaycastHit[8];
    private readonly RaycastHit[] groundProbeHits = new RaycastHit[4];

    [Networked] public NetworkBool IsFalling   { get; set; }
    [Networked] private float Yaw              { get; set; }
    [Networked] public Vector2 MoveInput       { get; set; }
    [Networked] public NetworkBool IsSprinting { get; set; }
    [Networked] public NetworkBool IsJumping   { get; set; }
    [Networked] public NetworkBool IsPushing   { get; set; }
    [Networked] public float CameraYaw         { get; private set; }
    [Networked] public float CameraPitch       { get; private set; }

    public bool  IsGrounded      => cc.Grounded;
    public float VerticalVelocity => cc.Velocity.y;

    void Awake()
    {
        cc = GetComponent<NetworkCharacterController>();
        characterController = GetComponent<CharacterController>();
        CollisionPolicyBootstrap.ApplyLayerToColliderOwners(gameObject, CollisionPolicyBootstrap.PlayerBodyLayer);

        int lightPushableMask = CollisionPolicyBootstrap.PushableMask & ~(1 << CollisionPolicyBootstrap.HeavyPuzzleLayer);
        if (pushProbeMask.value == 0)
            pushProbeMask = lightPushableMask;
        else
            pushProbeMask = (pushProbeMask.value | lightPushableMask) & ~(1 << CollisionPolicyBootstrap.HeavyPuzzleLayer);

        if (heavyPushProbeMask.value == 0)
            heavyPushProbeMask = 1 << CollisionPolicyBootstrap.HeavyPuzzleLayer;
        else
            heavyPushProbeMask = heavyPushProbeMask.value | (1 << CollisionPolicyBootstrap.HeavyPuzzleLayer);
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
        if (!GetInput(out PlayerNetworkInput input))
        {
            if (Object != null && Object.HasStateAuthority)
            {
                IsPushing = false;
                IsSprinting = false;
                MoveInput = Vector2.zero;
            }

            return;
        }

        CameraYaw = input.yaw;
        CameraPitch = input.pitch;

        bool jumping = input.isJumping && cc.Grounded;
        IsJumping = jumping;

        if (jumping)
            cc.Jump(overrideImpulse: Mathf.Sqrt(jumpHeight * -2f * gravity));

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
        }

        Yaw = transform.eulerAngles.y;

        Vector3 resolvedMoveDir = ResolvePushProbe(
            moveDir,
            out bool isBlockingHeavyPush,
            out Vector3 heavyBlockNormal,
            out float resolvedMoveSpeedScale,
            out bool isPushingHeavyObject);
        IsPushing = isPushingHeavyObject;

        bool sprinting = !isPushingHeavyObject && input.isSprinting && input.moveInput.sqrMagnitude > 0.0001f;
        IsSprinting = sprinting;

        float desiredMaxSpeed = 0f;
        if (isPushingHeavyObject)                       desiredMaxSpeed = walkSpeed * pushingSpeedMultiplier;
        else if (sprinting)                             desiredMaxSpeed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f)      desiredMaxSpeed = runSpeed;
        else if (input.moveInput.magnitude > 0f)        desiredMaxSpeed = walkSpeed;
        cc.maxSpeed = desiredMaxSpeed;
        if (isBlockingHeavyPush)
            cc.maxSpeed = desiredMaxSpeed * resolvedMoveSpeedScale;

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Vector3 localMoveDirection = transform.InverseTransformDirection(moveDir);
            MoveInput = new Vector2(localMoveDirection.x, localMoveDirection.z);
            if (isPushingHeavyObject)
                MoveInput *= pushingAnimationInputScale;
        }
        else
        {
            MoveInput = Vector2.zero;
        }

        float previousY = transform.position.y;
        float previousStepOffset = characterController != null ? characterController.stepOffset : 0f;
        if (isBlockingHeavyPush && characterController != null)
            characterController.stepOffset = 0f;
        cc.Move(resolvedMoveDir); // NCC 내부에서 normalize + gravity + grounded 처리
        if (isBlockingHeavyPush && characterController != null)
            characterController.stepOffset = previousStepOffset;

        if (isBlockingHeavyPush)
            RemoveVelocityIntoBlock(heavyBlockNormal);

        SuppressStepRiseVelocity(previousY, jumping, isBlockingHeavyPush);

        IsFalling = !cc.Grounded && cc.Velocity.y < 0f;
    }

    public void ApplyHeavyObjectCarry(Vector3 delta)
    {
        if (Object != null && !Object.HasStateAuthority)
            return;

        delta.y = 0f;
        if (delta.sqrMagnitude <= minCarryDeltaSqr)
            return;

        if (characterController != null && characterController.enabled)
            characterController.Move(delta);
        else
            transform.position += delta;
    }

    private Vector3 ResolvePushProbe(
        Vector3 moveDir,
        out bool isBlockingHeavyPush,
        out Vector3 heavyBlockNormal,
        out float resolvedMoveSpeedScale,
        out bool isPushingHeavyObject)
    {
        isBlockingHeavyPush = false;
        heavyBlockNormal = Vector3.zero;
        resolvedMoveSpeedScale = 1f;
        isPushingHeavyObject = false;

        if (moveDir.sqrMagnitude <= 0.0001f)
            return moveDir;

        if (TryFindHeavyObject(moveDir, out RaycastHit heavyHit, out HeavyObject heavyObject))
        {
            bool hasPushIntent = heavyObject.TryResolvePushIntent(
                transform.position,
                moveDir,
                out int directionIndex,
                out Vector3 blockNormal);

            isPushingHeavyObject = hasPushIntent;

            if (hasPushIntent && HasInputAuthority)
                heavyObject.SubmitPushIntent(directionIndex, Object.InputAuthority);

            if (blockNormal.sqrMagnitude <= 0.0001f)
                blockNormal = heavyHit.normal;

            Vector3 blockedMoveDir = RemoveBlockedPushComponent(
                moveDir,
                blockNormal,
                out isBlockingHeavyPush,
                out heavyBlockNormal);

            if (isBlockingHeavyPush)
                resolvedMoveSpeedScale = GetResolvedMoveSpeedScale(moveDir, blockedMoveDir);

            return blockedMoveDir;
        }

        if (TryFindPushable(moveDir, out _, out IPushable pushable) && HasInputAuthority)
        {
            Vector3 force = moveDir.normalized * pushForce;
            force.y = 0f;
            pushable.OnPush(force, Object.InputAuthority);
        }

        return moveDir;
    }

    private float GetResolvedMoveSpeedScale(Vector3 originalMoveDir, Vector3 resolvedMoveDir)
    {
        float originalMagnitude = originalMoveDir.magnitude;
        if (originalMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(resolvedMoveDir.magnitude / originalMagnitude);
    }

    private bool TryFindHeavyObject(Vector3 moveDir, out RaycastHit nearestHit, out HeavyObject nearestHeavyObject)
    {
        nearestHit = default;
        nearestHeavyObject = null;

        if (heavyPushProbeMask.value == 0 || pushProbeDistance <= 0f)
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
            heavyPushProbeMask.value,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = pushProbeHits[i];
            if (hit.collider == null || hit.distance >= nearestDistance)
                continue;

            HeavyObject heavyObject = hit.collider.GetComponentInParent<HeavyObject>();
            if (heavyObject == null)
                continue;

            nearestHit = hit;
            nearestHeavyObject = heavyObject;
            nearestDistance = hit.distance;
        }

        return nearestHeavyObject != null;
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

            if (hit.collider.gameObject.layer == CollisionPolicyBootstrap.HeavyPuzzleLayer)
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

    private Vector3 RemoveBlockedPushComponent(Vector3 moveDir, Vector3 hitNormal, out bool isBlockingHeavyPush, out Vector3 heavyBlockNormal)
    {
        isBlockingHeavyPush = false;
        heavyBlockNormal = Vector3.zero;

        hitNormal.y = 0f;
        if (hitNormal.sqrMagnitude <= 0.0001f)
        {
            isBlockingHeavyPush = true;
            return moveDir * blockedPushSlowdown;
        }

        hitNormal.Normalize();
        heavyBlockNormal = hitNormal;

        float inwardAmount = Vector3.Dot(moveDir, -hitNormal);
        if (inwardAmount <= 0f)
            return moveDir;

        isBlockingHeavyPush = true;
        return moveDir + hitNormal * inwardAmount * (1f - blockedPushSlowdown);
    }

    private void RemoveVelocityIntoBlock(Vector3 blockNormal)
    {
        if (cc == null)
            return;

        blockNormal.y = 0f;
        if (blockNormal.sqrMagnitude <= 0.0001f)
            return;

        blockNormal.Normalize();

        Vector3 velocity = cc.Velocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float inwardSpeed = Vector3.Dot(horizontalVelocity, -blockNormal);
        if (inwardSpeed <= 0f)
            return;

        horizontalVelocity += blockNormal * inwardSpeed;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        cc.Velocity = velocity;
    }

    private void SuppressStepRiseVelocity(float previousY, bool jumpedThisTick, bool preventStepRise)
    {
        if (jumpedThisTick || cc == null)
            return;

        float deltaY = transform.position.y - previousY;
        if (deltaY <= stepRiseVelocityTolerance)
            return;

        if (preventStepRise)
        {
            Vector3 position = transform.position;
            position.y = previousY;
            transform.position = position;

            Vector3 stepVelocity = cc.Velocity;
            if (stepVelocity.y > 0f)
            {
                stepVelocity.y = 0f;
                cc.Velocity = stepVelocity;
            }

            return;
        }

        if (!cc.Grounded && !IsNearGround())
            return;

        Vector3 velocity = cc.Velocity;
        if (velocity.y <= 0f)
            return;

        velocity.y = 0f;
        cc.Velocity = velocity;
    }

    private bool IsNearGround()
    {
        if (cc != null && cc.Grounded)
            return true;

        if (characterController == null || stepGroundCheckDistance <= 0f)
            return false;

        float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth);
        float halfHeight = Mathf.Max(radius, characterController.height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(characterController.center);
        Vector3 bottomSphereCenter = center + Vector3.down * halfHeight;
        Vector3 castOrigin = bottomSphereCenter + Vector3.up * Mathf.Max(characterController.skinWidth, 0.01f);

        int hitCount = Runner.GetPhysicsScene().SphereCast(
            castOrigin,
            radius,
            Vector3.down,
            groundProbeHits,
            stepGroundCheckDistance + Mathf.Max(characterController.skinWidth, 0.01f),
            ~CollisionPolicyBootstrap.PlayerBodyMask,
            QueryTriggerInteraction.Ignore);

        return hitCount > 0;
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
