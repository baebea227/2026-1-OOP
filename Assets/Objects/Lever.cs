using System.Collections;
using UnityEngine;
using Fusion;

public class Lever : OperatableObject
{
    [SerializeField] float coolTimeSet;
    WaitForSeconds coolTime;
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

    // void ResponseAction()
    // {
    //     if (isDisposable)
    //     {
    //         mesh.material = meshSet[2];
    //         return;
    //     }
    //     int index = operateState ? 1 : 0;
    //     mesh.material = meshSet[index];
    // }

    public void TryOperate(NetworkObject operatorObject)
    {
        // if (Object.HasStateAuthority)
        // {
        //     ApplyOperate(operatorObject);
        // }
        // else {}
        
        RPC_Operate(operatorObject);
    }

    void ApplyOperate(NetworkObject operatorObject)
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
        NetworkedOperateState = !NetworkedOperateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
        StartCoroutine(OperateCooldown());
        // ResponseAction();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Operate(NetworkObject operatorObject) => ApplyOperate(operatorObject);

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

    public override void Operate()
    {
        // operateState = !operateState;
        // connectedObjController.OnActivate(operateState ? 1 : -1);
        // ResponseAction();
    }

    // void OnTriggerEnter(Collider other)
    // {
    //     // 임시 조건
    //     if (other.CompareTag("Player") && isOperatable)
    //     {
    //         Operate();
    //         StartCoroutine(OperateCooldown());
    //     }
    // }
}
