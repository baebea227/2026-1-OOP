using System;
using UnityEngine;

public class PressurePlate : OperatableObject
{
    int triggerCnt;

    protected override void Awake()
    {
        base.Awake();
        operateState = false;
        triggerCnt = 0;
    }

    public override void Operate()
    {
        operateState = !operateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
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
