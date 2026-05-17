using System.Collections;
using UnityEngine;
using Fusion;
using System;

public class Lever : OperatableObject
{
    [SerializeField] float coolTimeSet;
    WaitForSeconds coolTime;
    [SerializeField] Material[] meshSet;
    MeshRenderer mesh;

    protected override void Awake()
    {
        base.Awake();
        mesh = GetComponentInChildren<MeshRenderer>();

        init();
    }
    
    void init()
    {
        coolTime = new WaitForSeconds(coolTimeSet);
        int index = operateState ? 1 : 0;
        mesh.material = meshSet[index];
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

    void ResponseAction()
    {
        if (isDisposable)
        {
            mesh.material = meshSet[2];
            return;
        }
        int index = operateState ? 1 : 0;
        mesh.material = meshSet[index];
    }

    public override void Operate()
    {
        operateState = !operateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
        ResponseAction();
    }

    void OnTriggerEnter(Collider other)
    {
        // 임시 조건
        if (other.CompareTag("Player") && isOperatable)
        {
            Operate();
            StartCoroutine(OperateCooldown());
        }
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
        ResponseAction();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Operate(NetworkObject operatorObject)
    {
        ApplyOperate(operatorObject);
    }
}
