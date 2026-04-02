using UnityEngine;
using UnityEngine.InputSystem;
using Fusion; // 퓨전 네임스페이스 추가

[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerMovement : NetworkBehaviour // NetworkBehaviour 상속
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;
    
    private float currentSpeed;
    private CharacterController controller;

    [Header("Gravity & Jump")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    private float verticalVelocity;
    private Vector3 playerVelocity;

    [Header("Components")]
    private Animator animator;
    
    private bool isFalling = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    // 퓨전은 Update 대신 이 메서드에서 물리/이동을 처리합니다.
    public override void FixedUpdateNetwork()
    {
        // GetInput을 통해 나 자신 또는 서버로부터 '동기화된 입력값'을 가져옵니다.
        if (GetInput(out PlayerNetworkInput input))
        {
            HandleGravity(input);
            HandleMovement(input);
            
            // Time.deltaTime 대신 Runner.DeltaTime을 반드시 사용해야 합니다.
            controller.Move(playerVelocity * Runner.DeltaTime);

            UpdateAnimations(input);
        }
    }

    private void HandleGravity(PlayerNetworkInput input)
    {
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        // 동기화된 점프 입력 확인
        if (input.isJumping && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }

        verticalVelocity += gravity * Runner.DeltaTime;
        playerVelocity.y = verticalVelocity;
    }

    private void HandleMovement(PlayerNetworkInput input)
    {
        bool actualSprinting = input.isSprinting && input.moveInput.y > 0;

        if (actualSprinting) currentSpeed = sprintSpeed;
        else if (input.moveInput.magnitude > 0.5f) currentSpeed = runSpeed;
        else if (input.moveInput.magnitude > 0) currentSpeed = walkSpeed;
        else currentSpeed = 0f;

        playerVelocity.x = input.moveInput.x * currentSpeed;
        playerVelocity.z = input.moveInput.y * currentSpeed;
    }

    private void UpdateAnimations(PlayerNetworkInput input)
    {
        animator.SetFloat("InputX", input.moveInput.x);
        animator.SetFloat("InputY", input.moveInput.y);
        animator.SetFloat("SpeedMagnitude", input.moveInput.magnitude); 

        bool actualSprinting = input.isSprinting && input.moveInput.y > 0;
        animator.SetBool("IsSprinting", actualSprinting);

        animator.SetBool("IsGrounded", controller.isGrounded);

        if (!controller.isGrounded && verticalVelocity < 0f)
        {
            if (!isFalling)
            {
                isFalling = true;
                animator.SetTrigger("Fall"); 
            }
        }
        else if (controller.isGrounded)
        {
            isFalling = false; 
        }
    }
}