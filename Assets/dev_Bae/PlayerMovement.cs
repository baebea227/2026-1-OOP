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
        UpdateAnimations();
    }

    private void HandleGravity()
    {
        // 바닥 체크 (중력 누적 방지)
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        // 중력 계산 및 수직 이동
        verticalVelocity += gravity * Time.deltaTime;
        playerVelocity.y = verticalVelocity;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        // 속도 결정 (입력이 작으면 걷기, 크면 뛰기, 버튼 누르면 전력질주)
        if (isSprinting && moveInput.magnitude > 0)
        {
            currentSpeed = sprintSpeed;
        }
        else if (moveInput.magnitude > 0.5f) // 아날로그 스틱을 끝까지 밀었을 때
        {
            currentSpeed = runSpeed;
        }
        else if (moveInput.magnitude > 0) // 살짝 밀었거나 키보드 입력 시
        {
            currentSpeed = walkSpeed;
        }
        else
        {
            currentSpeed = 0f;
        }

        // 이동 계산
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y);
        controller.Move(move * currentSpeed * Time.deltaTime);

        // [중요] 8방향 애니메이션을 제대로 보려면 캐릭터가 진행 방향으로 무조건 회전하면 안 됩니다.
        // 예를 들어 '뒤로 걷기' 모션을 보려면 캐릭터는 앞을 본 상태에서 뒤로 이동해야 합니다.
        // 만약 항상 앞으로 달리는 모션만 원하신다면 아래 주석을 해제하세요.
        /*
        if (move != Vector3.zero)
        {
            transform.forward = move;
        }
        */
    }

    private void UpdateAnimations()
    {
        // 1. 블렌드 트리를 위한 8방향 입력값 전달 (-1.0 ~ 1.0)
        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);

        // 2. 현재 달리기/전력질주 상태 전달
        // Magnitude(벡터의 길이)를 전달하여 Idle(0), Walk/Run 구분
        animator.SetFloat("SpeedMagnitude", moveInput.magnitude); 
        animator.SetBool("IsSprinting", isSprinting);

        // 3. 점프 및 추락 상태 전달
        animator.SetBool("IsGrounded", controller.isGrounded);
        animator.SetBool("IsFalling", !controller.isGrounded && verticalVelocity < -2f);
    }
}