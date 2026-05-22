using Fusion;
using UnityEngine;

public abstract class OperatableObject : NetworkBehaviour
{
    protected bool isOperatable = true;
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    protected bool OperateState { get; set; }

    [SerializeField] protected GameObject connectedObj;
    protected IActivatable connectedObjController;
    
    [SerializeField] protected Material[] meshSet;
    protected MeshRenderer mesh;

    protected virtual void Awake()
    {
        connectedObjController = connectedObj.GetComponent<IActivatable>();
        mesh = GetComponent<MeshRenderer>();
    }

    public override void Spawned()
    {
        OperateState = false;
        int index = OperateState ? 1 : 0;
        mesh.material = meshSet[index];
    }

    protected abstract void ApplyOperate(NetworkObject operatorObject);

    protected abstract void OnStateChanged();
}
