using System.Collections;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
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
        operateState = !operateState;
        connectedObjController.OnActivate(operateState ? 1 : -1);
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
        if (other.CompareTag("Player") && isOperatable)
        {
            Operate();
            StartCoroutine(OperateCooldown());
        }
    }

    public void TryOperate(NetworkObject operatorObject) {}
}
