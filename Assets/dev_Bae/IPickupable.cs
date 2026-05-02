public interface IPickupable
{
    void OnPickup(PlayerGrabHandler grabber);
    void OnThrow(UnityEngine.Vector3 velocity);
    void OnDrop();
}
