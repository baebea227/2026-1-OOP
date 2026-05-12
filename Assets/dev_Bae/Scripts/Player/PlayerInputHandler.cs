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
    private InputAction interactAction;
    private InputAction throwAction;

    public InputAction LookAction => lookAction;

    private bool localJumpPressed;
    private bool localGrabPressed;
    private bool localInteractPressed;
    private bool localThrowPressed;
    private float localYaw;

    public float lookSensitivity = 0.15f;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];
        grabAction = playerInput.actions["Grab"];
        interactAction = playerInput.actions["Interact"];
        throwAction = playerInput.actions["Throw"];
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            playerInput.enabled = true;

            if (Runner != null && Object != null)
                Runner.SetPlayerObject(Object.InputAuthority, Object);

            AudioListener audioListener = GetComponentInChildren<AudioListener>();
            if (audioListener != null)
                audioListener.enabled = true;

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cam.gameObject.SetActive(true);

            return;
        }

        playerInput.enabled = false;

        AudioListener remoteAudioListener = GetComponentInChildren<AudioListener>();
        if (remoteAudioListener != null)
            remoteAudioListener.enabled = false;

        Camera remoteCamera = GetComponentInChildren<Camera>();
        if (remoteCamera != null)
            remoteCamera.gameObject.SetActive(false);
    }

    public override void Render()
    {
        if (!HasInputAuthority)
            return;

        if (jumpAction.WasPressedThisFrame())
            localJumpPressed = true;

        if (grabAction.WasPressedThisFrame())
            localGrabPressed = true;

        if (interactAction.WasPressedThisFrame())
            localInteractPressed = true;

        if (throwAction.WasPressedThisFrame())
            localThrowPressed = true;

        localYaw += lookAction.ReadValue<Vector2>().x * lookSensitivity;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            yaw = localYaw,
            isSprinting = sprintAction.IsPressed(),
            isJumping = localJumpPressed,
            isGrab = localGrabPressed,
            isInteract = localInteractPressed,
            isThrow = localThrowPressed
        };

        input.Set(data);

        localJumpPressed = false;
        localGrabPressed = false;
        localInteractPressed = false;
        localThrowPressed = false;
    }
}
