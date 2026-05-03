using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Push Settings")]
    public float pushForce = 3f;

    [Header("Movement Settings")]
    public float walkSpeed   = 2f;
    public float runSpeed    = 5f;
    public float sprintSpeed = 8f;

    [Header("Gravity & Jump")]
    public float gravity    = -9.81f;
    public float jumpHeight = 1.5f;

    private NetworkCharacterController cc;

    [Networked] public NetworkBool IsFalling   { get; set; }
    [Networked] private float Yaw              { get; set; }
    [Networked] public Vector2 MoveInput       { get; set; }
    [Networked] public NetworkBool IsSprinting { get; set; }
    [Networked] public NetworkBool IsJumping   { get; set; }

    public bool  IsGrounded      => cc.Grounded;
    public float VerticalVelocity => cc.Velocity.y;

    void Awake()
    {
        cc = GetComponent<NetworkCharacterController>();
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

        Yaw = input.yaw;
        transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

        bool sprinting = input.isSprinting && input.moveInput.y > 0;
        bool jumping   = input.isJumping && cc.Grounded;

        MoveInput   = input.moveInput;
        IsSprinting = sprinting;
        IsJumping   = jumping;

        if (jumping)
            cc.Jump(overrideImpulse: Mathf.Sqrt(jumpHeight * -2f * gravity));

        if (sprinting)                              cc.maxSpeed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f) cc.maxSpeed = runSpeed;
        else if (input.moveInput.magnitude > 0f)   cc.maxSpeed = walkSpeed;

        Vector3 right   = transform.right;
        Vector3 forward = transform.forward;
        right.y   = 0f; right.Normalize();
        forward.y = 0f; forward.Normalize();

        Vector3 moveDir = right * input.moveInput.x + forward * input.moveInput.y;
        cc.Move(moveDir); // NCC 내부에서 normalize + gravity + grounded 처리

        IsFalling = !cc.Grounded && cc.Velocity.y < 0f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!HasInputAuthority) return;

        var pushable = hit.collider.GetComponent<IPushable>();
        if (pushable == null) return;

        Vector3 force = hit.moveDirection * pushForce;
        force.y = 0f;
        pushable.OnPush(force, Object.InputAuthority);
    }
}
