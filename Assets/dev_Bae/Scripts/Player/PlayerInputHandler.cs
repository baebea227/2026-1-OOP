using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : NetworkBehaviour
{
    internal static bool IsGameplayInputBlocked { get; set; }

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction grabAction;
    private InputAction interactAction;
    private InputAction throwAction;

    public float CameraYaw => localYaw;
    public float CameraPitch => localPitch;

    private bool localJumpPressed;
    private bool localGrabPressed;
    private bool localInteractPressed;
    private bool localThrowPressed;
    private float localYaw;
    private float localPitch;

    [Header("Look Settings")]
    public float lookSensitivity = 0.15f;
    [Range(-80f, 0f)] public float minPitch = -35f;
    [Range(0f, 80f)] public float maxPitch = 55f;

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
            localYaw = transform.eulerAngles.y;
            localPitch = 0f;
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

        if (IsGameplayInputBlocked)
        {
            ClearBufferedButtonInputs();
            return;
        }

        if (jumpAction.WasPressedThisFrame())
            localJumpPressed = true;

        if (grabAction.WasPressedThisFrame())
            localGrabPressed = true;

        if (interactAction.WasPressedThisFrame())
            localInteractPressed = true;

        if (throwAction.WasPressedThisFrame())
            localThrowPressed = true;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        localYaw += lookInput.x * lookSensitivity;
        localPitch -= lookInput.y * lookSensitivity;
        localPitch = Mathf.Clamp(localPitch, minPitch, maxPitch);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (IsGameplayInputBlocked)
        {
            input.Set(new PlayerNetworkInput
            {
                moveInput = Vector2.zero,
                yaw = localYaw,
                pitch = localPitch,
                isSprinting = false,
                isJumping = false,
                isGrab = false,
                isInteract = false,
                isThrow = false
            });

            ClearBufferedButtonInputs();
            return;
        }

        PlayerNetworkInput data = new PlayerNetworkInput
        {
            moveInput = moveAction.ReadValue<Vector2>(),
            yaw = localYaw,
            pitch = localPitch,
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

    private void ClearBufferedButtonInputs()
    {
        localJumpPressed = false;
        localGrabPressed = false;
        localInteractPressed = false;
        localThrowPressed = false;
    }
}
