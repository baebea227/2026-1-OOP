using System;
using UnityEngine;
using Fusion;

public class PressurePlate : OperatableObject
{
    int triggerCnt;
    [SerializeField] Material[] meshSet;
    MeshRenderer mesh;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    private NetworkBool NetworkedOperateState { get; set; }

    protected override void Awake()
    {
        base.Awake();
        mesh = GetComponent<MeshRenderer>();

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

    void ApplyOperate(int n)
    {
        triggerCnt = n;
        if(triggerCnt > 0 && !operateState)
        {
            operateState = true;
            // 메쉬 변경
            NetworkedOperateState = true;
            connectedObjController.OnActivate(1);
        }
        else if(triggerCnt == 0 && operateState)
        {
            operateState = false;
            //메쉬변경
            NetworkedOperateState = false;
            connectedObjController.OnActivate(-1);
        }

        // operateState = !operateState;
        // connectedObjController.OnActivate(-1);
        // ResponseAction();
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

    void OnStateChanged()
    {
        if (isDisposable)
        {
            mesh.material = meshSet[2];
            return;
        }
        int index = NetworkedOperateState ? 1 : 0;
        mesh.material = meshSet[index];
    }

    // void OnTriggerEnter(Collider other)
    // {
    //     if (other.CompareTag("Player") && isOperatable)
    //     {
    //         if(triggerCnt++ == 0)
    //         {
    //             Operate();
    //         }
    //         if(isDisposable)
    //         {
    //             isOperatable = false;
    //         }
    //     }
    // }

    // void OnTriggerStay(Collider other)
    // {
    //     if (!operateState)
    //     {
    //         triggerCnt++;   
    //         Operate();
    //     }
    // }

    // void OnTriggerExit(Collider other)
    // {
    //     if (other.CompareTag("Player") && isOperatable)
    //     {
    //         triggerCnt--;
    //         if(triggerCnt == 0)
    //         {
    //             Operate();
    //         }
    //     }
    // }

    public override void Operate()
    {}

    void OnDrawGizmos()  
    {  
        Gizmos.matrix = transform.localToWorldMatrix;  
        Gizmos.color = Color.yellow;  
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.8f, 1f, 0.8f));  
    }
}
