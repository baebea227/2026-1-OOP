public interface IPickupable
{
    void OnPickup(PlayerGrabHandler grabber);
    void OnThrow(PlayerGrabHandler thrower, UnityEngine.Vector3 velocity);
    void OnDrop(PlayerGrabHandler dropper);
}
