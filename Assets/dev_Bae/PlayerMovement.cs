using UnityEngine;
using Fusion;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Push Settings")]
    public float pushForce = 3f;
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;

    [Header("Look Settings")]
    public float lookSensitivity = 0.15f;

    [Header("Gravity & Jump")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    private CharacterController controller;

    [Networked] private Vector3 PlayerVelocity { get; set; }
    [Networked] public NetworkBool IsFalling { get; set; }
    [Networked] private float Yaw { get; set; }
    [Networked] public Vector2 MoveInput { get; set; }
    [Networked] public NetworkBool IsSprinting { get; set; }
    [Networked] public NetworkBool IsJumping { get; set; }

    public bool IsGrounded => controller.isGrounded;
    public float VerticalVelocity => PlayerVelocity.y;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerNetworkInput input))
        {
            bool sprinting = input.isSprinting && input.moveInput.y > 0;
            bool jumping = input.isJumping && controller.isGrounded;

            MoveInput = input.moveInput;
            IsSprinting = sprinting;
            IsJumping = jumping;

            Yaw += input.lookDelta.x * lookSensitivity;
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

            HandleGravity(input, jumping);
            HandleMovement(input, sprinting);

            controller.Move(PlayerVelocity * Runner.DeltaTime);

            if (!controller.isGrounded && PlayerVelocity.y < 0f)
                IsFalling = true;
            else if (controller.isGrounded)
                IsFalling = false;
        }
    }

    private void HandleGravity(PlayerNetworkInput input, bool jumping)
    {
        float v = PlayerVelocity.y;

        if (controller.isGrounded && v < 0)
            v = -2f;

        if (jumping)
            v = Mathf.Sqrt(jumpHeight * -2f * gravity);

        v += gravity * Runner.DeltaTime;

        PlayerVelocity = new Vector3(PlayerVelocity.x, v, PlayerVelocity.z);
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

    private void HandleMovement(PlayerNetworkInput input, bool sprinting)
    {
        float speed;
        if (sprinting) speed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f) speed = runSpeed;
        else if (input.moveInput.magnitude > 0) speed = walkSpeed;
        else speed = 0f;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        Vector3 moveDir = right * input.moveInput.x + forward * input.moveInput.y;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        PlayerVelocity = new Vector3(moveDir.x * speed, PlayerVelocity.y, moveDir.z * speed);
    }
}
