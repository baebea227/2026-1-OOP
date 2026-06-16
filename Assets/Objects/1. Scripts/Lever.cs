using System.Collections;
using UnityEngine;
using Fusion;

public class Lever : OperatableObject
{
    [SerializeField] float coolTimeSet;
    WaitForSeconds coolTime;
    [SerializeField] bool isDisposable;
    bool operateState;

    Animator anim;

    protected override void Awake()
    {
        base.Awake();

        anim = GetComponent<Animator>();
        coolTime = new WaitForSeconds(coolTimeSet);
        operateState = false;
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
        operateState = !operateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
        StartCoroutine(OperateCooldown());
        // OnStateChanged();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Operate(NetworkObject operatorObject) => ApplyOperate(operatorObject);

    protected override void OnStateChanged()
    {
        anim.SetTrigger("Active");
        if (isDisposable)
        {
            mesh.material = meshSet[2];
            return;
        }
        int index = OperateState ? 1 : 0;
        mesh.material = meshSet[index];
    }
}
