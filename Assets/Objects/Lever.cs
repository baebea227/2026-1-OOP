using System.Collections;
using UnityEngine;
using Fusion;

public class Lever : OperatableObject
{
    [SerializeField] float coolTimeSet;
    WaitForSeconds coolTime;
    bool isDisposable;

    Animator anim;

    protected override void Awake()
    {
        base.Awake();

        anim = GetComponent<Animator>();
        coolTime = new WaitForSeconds(coolTimeSet);
    }

    IEnumerator OperateCooldown()
    {
        isOperatable = false;

        if (isDisposable)
        {
            yield break;
        }
        yield return coolTime;

        isOperatable = true;
    }

    public void TryOperate(NetworkObject operatorObject)
    {        
        RPC_Operate(operatorObject);
    }

    protected override void ApplyOperate(NetworkObject operatorObject)
    {
        if(operatorObject == null)
        {
            return;
        }

        if(!isOperatable)
        {
            return;
        }

        OperateState = !OperateState;
        connectedObjController.OnActivate(OperateState ? 1 : -1);
        StartCoroutine(OperateCooldown());
        // OnStateChanged();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Operate(NetworkObject operatorObject) => ApplyOperate(operatorObject);

    protected override void OnStateChanged()
    {
        if (isDisposable)
        {
            mesh.material = meshSet[2];
            return;
        }
        int index = OperateState ? 1 : 0;
        mesh.material = meshSet[index];
        anim.SetTrigger("Active");
    }
}
