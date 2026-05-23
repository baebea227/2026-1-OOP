using UnityEngine;

public static class CollisionPolicyBootstrap
{
    public const int PlayerBodyLayer = 8;
    public const int InteractableNoBodyLayer = 9;
    public const int HeavyPuzzleLayer = 10;
    public const int ObjectBodyLayer = 11;

    public const int PlayerBodyMask = 1 << PlayerBodyLayer;
    public const int ObjectBodyMask = 1 << ObjectBodyLayer;
    public const int PushableMask = (1 << InteractableNoBodyLayer) | (1 << HeavyPuzzleLayer) | ObjectBodyMask;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyCollisionPolicy()
    {
        Physics.IgnoreLayerCollision(PlayerBodyLayer, InteractableNoBodyLayer, true);
        Physics.IgnoreLayerCollision(PlayerBodyLayer, HeavyPuzzleLayer, false);
        Physics.IgnoreLayerCollision(PlayerBodyLayer, ObjectBodyLayer, false);
    }

    public static void ApplyLayerToColliderOwners(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider objectCollider in colliders)
        {
            if (objectCollider != null)
                objectCollider.gameObject.layer = layer;
        }
    }
}
