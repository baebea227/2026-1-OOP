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
    public static PlayerInputHandler Local;

    private bool localJumpPressed;
    private Vector2 localLookDelta;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        lookAction = playerInput.actions["Look"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            Local = this;
        }
        else
        {
            playerInput.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority) Local = null;
    }

    public override void Render()
    {
        if (!HasInputAuthority) return;

        if (jumpAction.WasPressedThisFrame())
            localJumpPressed = true;

        localLookDelta += lookAction.ReadValue<Vector2>();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.moveInput = moveAction.ReadValue<Vector2>();
        data.lookDelta = localLookDelta;
        data.isSprinting = sprintAction.IsPressed();
        data.isJumping = localJumpPressed;

        input.Set(data);

        localJumpPressed = false;
        localLookDelta = Vector2.zero;
    }
}