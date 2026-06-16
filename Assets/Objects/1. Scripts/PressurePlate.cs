using System;
using UnityEngine;
using Fusion;

public class PressurePlate : OperatableObject
{
    int triggerCnt;

    bool operateState;
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    private NetworkBool NetworkedOperateState { get; set; }

    protected override void Awake()
    {
        base.Awake();

        Init();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedOperateState = operateState;
        }
    }
    
    void Init()
    {
        operateState = false;
        triggerCnt = 0;
        int index = operateState ? 1 : 0;
        mesh.material = meshSet[index];
    }

    void ApplyOperate(int n)
    {
        triggerCnt = n;
        if(triggerCnt > 0 && !operateState)
        {
            operateState = true;
            NetworkedOperateState = true;
            connectedObjController.OnActivate(1);
        }
        else if(triggerCnt == 0 && operateState)
        {
            operateState = false;
            NetworkedOperateState = false;
            connectedObjController.OnActivate(-1);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Operate(int n) => ApplyOperate(n);

    void TriggerCheck()
    {
        int res = 0;
        var triggered = Physics.OverlapBox(transform.position, new Vector3(1.4f, 0.7f, 1.4f), Quaternion.identity);
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
        int index = NetworkedOperateState ? 1 : 0;
        mesh.material = meshSet[index];
    }

    protected override void ApplyOperate(NetworkObject operatorObject){}
    void OnDrawGizmos()  
    {  
        Gizmos.matrix = transform.localToWorldMatrix;  
        Gizmos.color = Color.yellow;  
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(2.8f/3, 1f, 2.8f/3));  
    }
}