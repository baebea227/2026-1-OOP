using Fusion;
using UnityEngine;

public abstract class OperatableObject : NetworkBehaviour
{
    protected bool isOperatable = true;
    [SerializeField] protected bool operateState;
    [SerializeField] protected bool isDisposable;
    // Collider interactArea;
    [SerializeField] protected GameObject connectedObj;
    protected IActivatable connectedObjController;

    protected virtual void Awake()
    {
        // interactArea = GetComponent<Collider>();
        connectedObjController = connectedObj.GetComponent<IActivatable>();
    }

    public abstract void Operate();
}
