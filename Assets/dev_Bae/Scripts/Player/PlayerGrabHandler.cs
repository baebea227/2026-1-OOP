using Fusion;
using UnityEngine;

public class PlayerGrabHandler : NetworkBehaviour
{
    [Header("Grab Settings")]
    public float grabRange = 3f;
    public float throwSpeed = 10f;

    [Header("References")]
    public Transform holdPoint;
    public Transform cameraTransform;

    public Transform HoldPoint => holdPoint;

    // 호스트가 ApplyPickup/Drop/Throw에서 함께 갱신 — resim 시 스냅샷으로 정확히 복원되어 토글 뒤집힘 방지
    [Networked] public NetworkObject HeldGrabbable { get; set; }

    void Awake()
    {
        // 인스펙터 미할당 시 자식 카메라로 폴백 — 누락된 참조로 잡기/던지기가 조용히 실패하는 것 방지
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;
        if (!GetInput(out PlayerNetworkInput input)) return;

        if (input.isGrab)
        {
            if (HeldGrabbable == null)
                TryGrab();
            else
                HeldGrabbable.GetComponent<GrabbableObject>().OnDrop(this);
        }

        if (input.isThrow && HeldGrabbable != null)
        {
            if (cameraTransform == null) return;
            Vector3 velocity = cameraTransform.forward * throwSpeed;
            HeldGrabbable.GetComponent<GrabbableObject>().OnThrow(this, velocity);
        }
    }

    private void TryGrab()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, grabRange))
            return;

        var grabbable = hit.collider.GetComponent<GrabbableObject>();
        if (grabbable == null) return;

        grabbable.OnPickup(this);
    }
}
