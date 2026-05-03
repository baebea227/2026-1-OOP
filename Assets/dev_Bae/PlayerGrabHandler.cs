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

    private GrabbableObject heldObject;

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
            if (heldObject == null)
                TryGrab();
            else
                Drop();
        }

        if (input.isThrow && heldObject != null)
            Throw();
    }

    private void TryGrab()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, grabRange))
            return;

        var grabbable = hit.collider.GetComponent<GrabbableObject>();
        if (grabbable == null) return;

        heldObject = grabbable;
        heldObject.OnPickup(this);
    }

    private void Drop()
    {
        heldObject.OnDrop(this);
        heldObject = null;
    }

    private void Throw()
    {
        if (cameraTransform == null) return;
        Vector3 velocity = cameraTransform.forward * throwSpeed;
        heldObject.OnThrow(this, velocity);
        heldObject = null;
    }
}
