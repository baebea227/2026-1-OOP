using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public abstract class InteractableObject : NetworkBehaviour
{
    [Header("Object Settings")]
    public float weight = 10f;

    protected Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }
}
