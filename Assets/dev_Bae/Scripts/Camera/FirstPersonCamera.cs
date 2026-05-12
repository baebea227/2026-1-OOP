using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("Look Settings")]
    public float sensitivity = 0.15f;
    [Range(-90f, 0f)] public float minPitch = -80f;
    [Range(0f, 90f)] public float maxPitch = 80f;

    private Camera cam;
    private InputAction lookAction;
    private float pitch = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            // PlayerInputHandler가 단일 소스로 lookAction을 보유 — 여기선 참조만 가져옴
            var handler = GetComponentInParent<PlayerInputHandler>();
            if (handler != null) lookAction = handler.LookAction;

            cam.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            cam.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority || lookAction == null) return;

        // Pitch는 로컬 전용 — 네트워크 전송 불필요
        Vector2 delta = lookAction.ReadValue<Vector2>();
        pitch -= delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
