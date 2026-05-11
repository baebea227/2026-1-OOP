using UnityEngine;
using Fusion;

[RequireComponent(typeof(Animator), typeof(PlayerMovement))]
public class PlayerAnimatorController : NetworkBehaviour
{
    private static readonly int _inputX       = Animator.StringToHash("InputX");
    private static readonly int _inputY       = Animator.StringToHash("InputY");
    private static readonly int _speedMag     = Animator.StringToHash("SpeedMagnitude");
    private static readonly int _isSprinting  = Animator.StringToHash("IsSprinting");
    private static readonly int _isGrounded   = Animator.StringToHash("IsGrounded");
    private static readonly int _jump         = Animator.StringToHash("Jump");
    private static readonly int _fall         = Animator.StringToHash("Fall");

    private Animator animator;
    private PlayerMovement movement;

    private bool prevIsJumping;
    private bool prevIsFalling;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
    }

    public override void Render()
    {
        animator.SetFloat(_inputX,      movement.MoveInput.x);
        animator.SetFloat(_inputY,      movement.MoveInput.y);
        animator.SetFloat(_speedMag,    movement.MoveInput.magnitude);
        animator.SetBool(_isSprinting,  movement.IsSprinting);
        animator.SetBool(_isGrounded,   movement.IsGrounded);

        bool jumping = movement.IsJumping;
        if (jumping && !prevIsJumping) animator.SetTrigger(_jump);
        prevIsJumping = jumping;

        bool falling = movement.IsFalling;
        if (falling && !prevIsFalling) animator.SetTrigger(_fall);
        prevIsFalling = falling;
    }
}
