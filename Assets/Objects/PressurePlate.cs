using System;
using UnityEngine;
using Fusion;

public class PressurePlate : OperatableObject
{
    int triggerCnt;

    protected override void Awake()
    {
        base.Awake();

        triggerCnt = 0;
    }

    void ApplyOperate(int n)
    {
        triggerCnt = n;
        if(triggerCnt > 0 && !OperateState)
        {
            OperateState = true;
            connectedObjController.OnActivate(1);
        }
        else if(triggerCnt == 0 && OperateState)
        {
            OperateState = false;
            connectedObjController.OnActivate(-1);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Operate(int n) => ApplyOperate(n);

    void TriggerCheck()
    {
        int res = 0;
        var triggered = Physics.OverlapBox(transform.position, new Vector3(0.8f, 0.7f, 0.8f), Quaternion.identity);
        foreach(var entity in triggered)
        {
            if(entity.CompareTag("Player") || entity.name == "GrabbableCube" || entity.name == "HeavyCube")
            {
                res++;
            }
        }
        RPC_Operate(res);
    }

    public override void FixedUpdateNetwork()
    {
        TriggerCheck();
    }

    protected override void OnStateChanged()
    {
        int index = OperateState ? 1 : 0;
        mesh.material = meshSet[index];
    }
    
    protected override void ApplyOperate(NetworkObject operatorObject) {}

    void OnDrawGizmos()  
    {  
        Gizmos.matrix = transform.localToWorldMatrix;  
        Gizmos.color = Color.yellow;  
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.8f, 1f, 0.8f));  
    }
}
