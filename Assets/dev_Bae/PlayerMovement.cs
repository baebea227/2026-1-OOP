using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;
    
    private float currentSpeed;
    private CharacterController controller;
    private Vector2 moveInput;

    [Header("Gravity & Jump")]
    public float gravity = -9.81f;
    public float jumpHeight = 1.5f;
    private float verticalVelocity;
    private Vector3 playerVelocity;

    [Header("Components")]
    private Animator animator;
    private PlayerInput playerInput;
    
    // 입력 시스템 액션
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private bool isSprinting = false;
    private bool isFalling = false; // [추가] 현재 추락 중인지 체크하는 변수

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();

        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];
    }

    void OnEnable()
    {
        moveAction.performed += OnMoveInput;
        moveAction.canceled += OnMoveInput;

        if (sprintAction != null)
        {
            sprintAction.performed += ctx => isSprinting = true;
            sprintAction.canceled += ctx => isSprinting = false;
        }

        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpInput;
        }
    }

    void OnDisable()
    {
        moveAction.performed -= OnMoveInput;
        moveAction.canceled -= OnMoveInput;
        
        if (sprintAction != null)
        {
            sprintAction.performed -= ctx => isSprinting = true;
            sprintAction.canceled -= ctx => isSprinting = false;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpInput;
        }
    }

    private void OnMoveInput(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnJumpInput(InputAction.CallbackContext context)
    {
        if (controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }

    void Update()
    {
        HandleGravity();
        HandleMovement();
        
        controller.Move(playerVelocity * Time.deltaTime);

        UpdateAnimations();
    }

    private void HandleGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        playerVelocity.y = verticalVelocity;
    }

    private void HandleMovement()
    {
        bool actualSprinting = isSprinting && moveInput.y > 0;

        if (actualSprinting)
        {
            currentSpeed = sprintSpeed;
        }
        else if (moveInput.magnitude > 0.5f)
        {
            currentSpeed = runSpeed;
        }
        else if (moveInput.magnitude > 0)
        {
            currentSpeed = walkSpeed;
        }
        else
        {
            currentSpeed = 0f;
        }

        playerVelocity.x = moveInput.x * currentSpeed;
        playerVelocity.z = moveInput.y * currentSpeed;
    }

    private void UpdateAnimations()
    {
        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);
        animator.SetFloat("SpeedMagnitude", moveInput.magnitude); 

        bool actualSprinting = isSprinting && moveInput.y > 0;
        animator.SetBool("IsSprinting", actualSprinting);

        // [수정된 로직 1] 땅에 닿았는지 상태 전달 (Grounded)
        animator.SetBool("IsGrounded", controller.isGrounded);

        // [수정된 로직 2] Any State -> Fall 전환 로직
        // 바닥에서 떨어졌고(공중), 아래로 이동 중일 때 (점프 후 정점 지났을 때 or 절벽에서 떨어질 때)
        if (!controller.isGrounded && verticalVelocity < 0f)
        {
            // 추락이 시작되는 '최초 1프레임'에만 Trigger 발동
            if (!isFalling)
            {
                isFalling = true;
                animator.SetTrigger("Fall"); 
            }
        }
        else if (controller.isGrounded)
        {
            // 땅에 닿으면 추락 상태 초기화
            isFalling = false; 
        }
    }
}