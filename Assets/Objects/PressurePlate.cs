using System;
using UnityEngine;

public class PressurePlate : OperatableObject
{
    int triggerCnt;
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
            if(triggerCnt++ == 0)
            {
                Operate();
            }
            if(isDisposable)
            {
                isOperatable = false;
            }
        }
    }

    // 시작 시 위에 무언가 올라와 있다면
    // boxcast 이용해 오브젝트 개수 파악 후 triggerCnt 갱신?
    void OnTriggerStay(Collider other)
    {
        if (!operateState)
        {
            triggerCnt++;   
            Operate();
        }
    }

    void OnTriggerExit(Collider other)
    {
        // 임시 조건
        if (other.CompareTag("Player") && isOperatable)
        {
            triggerCnt--;
            if(triggerCnt == 0)
            {
                Operate();
            }
        }
    }
}
