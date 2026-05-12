using System.Collections;
using UnityEngine;
using Fusion;

public class Lever : OperatableObject
{

    [SerializeField] float coolTimeSet;
    WaitForSeconds coolTime;

    protected override void Awake()
    {
        base.Awake();

        coolTime = new WaitForSeconds(coolTimeSet);
    }

    public override void Operate()
    {
        // operateState = !operateState;
        // connectedObjController.OnActivate(operateState ? 1 : -1);
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

    void OnTriggerEnter(Collider other)
    {
        // 임시 조건
        // if (other.CompareTag("Player") && isOperatable)
        // {
        //     Operate();
        //     StartCoroutine(OperateCooldown());
        // }
    }

    public void TryOperate(NetworkObject operatorObject)
    {
        if (Object.HasStateAuthority)
        {
            ApplyOperate(operatorObject);
            return;
        }

        RPC_Operate(operatorObject);
    }

    private void ApplyOperate(NetworkObject operatorObject)
    {
        if(operatorObject == null)
        {
            return;
        }

        if(!isOperatable)
        {
            return;
        }

        operateState = !operateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
        StartCoroutine(OperateCooldown());
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Operate(NetworkObject operatorObject)
    {
        ApplyOperate(operatorObject);
    }
}
