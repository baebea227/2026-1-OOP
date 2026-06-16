using System.Collections;
using Fusion;
using UnityEngine;

public class MoveableStructure : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
    [SerializeField] bool isDisposable;
    [SerializeField] int triggerCnt;
    int curTriggerCnt;

    [SerializeField] float moveTime;
    float curMoveTime;
    float currentT;
    Vector3 startPos;
    Vector3 endPos;
    [SerializeField] Transform posA;
    [SerializeField] Transform posB;

    Coroutine coroutine;

    void Awake()
    {
        IsActive = firstState;
        curTriggerCnt = firstState ? triggerCnt : 0;
        transform.position = posA.position;
        currentT = 0f;
    }

    public void OnActivate(int n)
    {
        if(triggerCnt <= 1)
        {
            if(!IsActive)
            {
                Activate();
            }
            else
            {
                Deactivate();
            }
            if (isDisposable)
            {
                triggerCnt = 100;
            }
        }
        else if(triggerCnt == 100)
        {
            return;
        }
        else
        {
            curTriggerCnt += n;
            if(curTriggerCnt >= triggerCnt)
            {
                Activate();
                if (isDisposable)
                {
                    triggerCnt = 100;
                }
            }
            else
            {
                Deactivate();
            }
        }
    }

    public void Activate()
    {
        IsActive = true;
        startPos = transform.position;
        endPos = posB.position;

        if (coroutine != null) StopCoroutine(coroutine);
        currentT = 0f;
        coroutine = StartCoroutine(Move());
    }

    public void Deactivate()
    {
        IsActive = false;
        startPos = transform.position;
        endPos = posA.position;

        if (coroutine != null) StopCoroutine(coroutine);
        currentT = 0f;
        coroutine = StartCoroutine(Move());
    }

    IEnumerator Move()
    {
        float remainingDist = (endPos - startPos).magnitude;
        float fullDist = (posB.position - posA.position).magnitude;
        float speed = fullDist / moveTime;
        float actualMoveTime = remainingDist / speed;

        currentT = 0f;
        while (currentT < 1f)
        {
            currentT += Time.deltaTime / actualMoveTime;
            currentT = Mathf.Clamp01(currentT);

            transform.position = Vector3.Lerp(startPos, endPos, currentT);

            yield return null;
        }

        transform.position = endPos;
    }
}
