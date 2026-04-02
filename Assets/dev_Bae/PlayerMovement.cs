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

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>(); // 애니메이터 연결

        // Player Input 컴포넌트에서 액션들을 찾아 연결합니다.
        // 주의: Input Actions 에 'Sprint'와 'Jump' 액션이 설정되어 있어야 합니다.
        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];
    }

    void OnEnable()
    {
        // 이동 입력
        moveAction.performed += OnMoveInput;
        moveAction.canceled += OnMoveInput;

        // 전력질주(Sprint) 입력
        if (sprintAction != null)
        {
            sprintAction.performed += ctx => isSprinting = true;
            sprintAction.canceled += ctx => isSprinting = false;
        }

        // 점프 입력
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
        // 바닥에 있을 때만 점프 가능
        if (controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger("Jump"); // 점프 애니메이션 트리거 실행
        }
    }

    void Update()
    {
        HandleGravity();
        HandleMovement();

        // [핵심 수정] 수직/수평 이동값을 하나로 합친 뒤, 프레임당 한 번만 Move를 호출합니다.
        controller.Move(playerVelocity * Time.deltaTime);

        UpdateAnimations();
    }

    private void HandleGravity()
    {
        // 바닥 체크 (중력 누적 방지)
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // 바닥에 붙어있도록 약간의 마이너스 값 유지
        }

        // 중력 계산
        verticalVelocity += gravity * Time.deltaTime;
        playerVelocity.y = verticalVelocity;
        
        // 기존에 있던 controller.Move(playerVelocity * Time.deltaTime); 삭제!
    }

    private void HandleMovement()
    {
        // [핵심 로직] 전력질주 버튼을 누르고 있고 && 앞쪽(Y값이 0보다 큼)으로 이동 중일 때만 true
        bool actualSprinting = isSprinting && moveInput.y > 0;

        // 속도 결정
        if (actualSprinting)
        {
            currentSpeed = sprintSpeed;
        }
        else if (moveInput.magnitude > 0.5f) // 아날로그 스틱을 끝까지 밀었을 때
        {
            currentSpeed = runSpeed;
        }
        else if (moveInput.magnitude > 0) // 뒤로 걷거나 옆으로만 갈 때는 걷기/일반 달리기
        {
            currentSpeed = walkSpeed;
        }
        else
        {
            currentSpeed = 0f;
        }

        // 이동 계산 (이전 답변에서 수정한 playerVelocity에 저장하는 방식)
        playerVelocity.x = moveInput.x * currentSpeed;
        playerVelocity.z = moveInput.y * currentSpeed;
    }

    private void UpdateAnimations()
    {
        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);
        animator.SetFloat("SpeedMagnitude", moveInput.magnitude); 

        // [중요] 버튼을 누른 상태(isSprinting)가 아니라, 실제로 전력질주 중인지 확인해서 애니메이터에 전달
        bool actualSprinting = isSprinting && moveInput.y > 0;
        animator.SetBool("IsSprinting", actualSprinting);

        animator.SetBool("IsGrounded", controller.isGrounded);
        animator.SetBool("IsFalling", !controller.isGrounded && verticalVelocity < -3f);
    }
}