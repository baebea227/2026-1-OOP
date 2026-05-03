using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : NetworkBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction grabAction;
    private InputAction throwAction;
    public InputAction LookAction => lookAction;

    private bool localJumpPressed;
    private bool localGrabPressed;
    private bool localThrowPressed;
    private Vector2 localLookDelta;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];
        grabAction = playerInput.actions["Grab"];
        throwAction = playerInput.actions["Throw"];
    }

    public override void Spawned()
    {
        if (!HasInputAuthority) 
    {
        playerInput.enabled = false;
        
        // 1. 상대방의 오디오 리스너 끄기
        AudioListener audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null) 
            audioListener.enabled = false;

        // (보너스) 상대방의 카메라도 내 화면에서 렌더링되지 않도록 끄기
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null) 
            cam.gameObject.SetActive(false);
    }
    }

    public override void Render()
    {
        if (!HasInputAuthority) return;

        if (jumpAction.WasPressedThisFrame())
            localJumpPressed = true;
        if (grabAction.WasPressedThisFrame())
            localGrabPressed = true;
        if (throwAction.WasPressedThisFrame())
            localThrowPressed = true;

        localLookDelta += lookAction.ReadValue<Vector2>();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.moveInput = moveAction.ReadValue<Vector2>();
        data.lookDelta = localLookDelta;
        data.isSprinting = sprintAction.IsPressed();
        data.isJumping = localJumpPressed;
        data.isGrab = localGrabPressed;
        data.isThrow = localThrowPressed;

        input.Set(data);

        localJumpPressed = false;
        localGrabPressed = false;
        localThrowPressed = false;
        localLookDelta = Vector2.zero;
    }
}