using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;

    private CharacterController controller;

    [Header("Gravity & Jump")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;

    [Header("Components")]
    private Animator animator;

    // 늦게 접속한 클라이언트도 올바른 초기 상태를 받을 수 있도록 [Networked]로 선언
    [Networked] private float VerticalVelocity { get; set; }
    [Networked] private Vector3 PlayerVelocity { get; set; }
    [Networked] private NetworkBool IsFalling { get; set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerNetworkInput input))
        {
            HandleGravity(input);
            HandleMovement(input);

            controller.Move(PlayerVelocity * Runner.DeltaTime);

            UpdateAnimations(input);
        }
    }

    private void HandleGravity(PlayerNetworkInput input)
    {
        float v = VerticalVelocity;

        if (controller.isGrounded && v < 0)
            v = -2f;

        if (input.isJumping && controller.isGrounded)
            v = Mathf.Sqrt(jumpHeight * -2f * gravity);

        v += gravity * Runner.DeltaTime;
        VerticalVelocity = v;

        PlayerVelocity = new Vector3(PlayerVelocity.x, v, PlayerVelocity.z);
    }

    private void HandleMovement(PlayerNetworkInput input)
    {
        bool sprinting = input.isSprinting && input.moveInput.y > 0;

        float speed;
        if (sprinting) speed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f) speed = runSpeed;
        else if (input.moveInput.magnitude > 0) speed = walkSpeed;
        else speed = 0f;

        PlayerVelocity = new Vector3(input.moveInput.x * speed, PlayerVelocity.y, input.moveInput.y * speed);
    }

    private void UpdateAnimations(PlayerNetworkInput input)
    {
        bool sprinting = input.isSprinting && input.moveInput.y > 0;

        animator.SetFloat("InputX", input.moveInput.x);
        animator.SetFloat("InputY", input.moveInput.y);
        animator.SetFloat("SpeedMagnitude", input.moveInput.magnitude);
        animator.SetBool("IsSprinting", sprinting);
        animator.SetBool("IsGrounded", controller.isGrounded);

        if (input.isJumping && controller.isGrounded)
            animator.SetTrigger("Jump");

        if (!controller.isGrounded && VerticalVelocity < 0f)
        {
            if (!IsFalling)
            {
                IsFalling = true;
                animator.SetTrigger("Fall");
            }
        }
        else if (controller.isGrounded)
        {
            IsFalling = false;
        }
    }
}
