using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : NetworkBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    public static PlayerInputHandler Local;

    // 로컬 입력값을 임시 저장할 변수
    private bool localJumpPressed;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
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

    void Update()
    {
        if (!HasInputAuthority) return;

        // 점프처럼 '누른 순간'이 중요한 키는 Update에서 캐치해둡니다.
        if (jumpAction.WasPressedThisFrame())
        {
            localJumpPressed = true;
        }
    }

    // 퓨전 엔진이 틱마다 입력을 요구할 때 호출되는 콜백 (Runner 매니저에 등록되어 있어야 함)
    // *주의: NetworkRunner를 관리하는 곳에서 INetworkRunnerCallbacks.OnInput 이벤트를 통해 이 메서드를 호출해주거나,
    // 최신 Fusion 버전에서는 NetworkBehaviour의 OnInput을 오버라이드 할 수 있습니다.
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerNetworkInput data = new PlayerNetworkInput();

        data.moveInput = moveAction.ReadValue<Vector2>();
        data.isSprinting = sprintAction.IsPressed();
        data.isJumping = localJumpPressed;

        // 구조체에 담은 데이터를 Fusion 네트워크로 넘깁니다.
        input.Set(data);

        // 점프 입력을 소모했으니 초기화합니다.
        localJumpPressed = false;
    }
}