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
    Transform startPos;
    Transform endPos;
    [SerializeField] Transform posA;
    [SerializeField] Transform posB;

    Coroutine coroutine;
    bool applyMove;

    void Awake()
    {
        curMoveTime = 0;
        applyMove = false;

        IsActive = firstState;
        curTriggerCnt = firstState ? triggerCnt : 0;
        transform.position = posA.position;
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
        startPos = transform;
        endPos = posB;
        // if(coroutine != null)
        // {
        //     StopCoroutine(coroutine);
        // }
        // coroutine = StartCoroutine(Move());
        applyMove = true;
        curMoveTime = 0;
    }

    public void Deactivate()
    {
        IsActive = false;
        startPos = transform;
        endPos = posA;
        // if(coroutine != null)
        // {
        //     StopCoroutine(coroutine);
        // }
        // coroutine = StartCoroutine(Move());
        applyMove = true;
        curMoveTime = 0;
    }

    void Update()
    {
        if (applyMove)
        {
            Move2();
        }
    }

    IEnumerator Move()
    {
        curMoveTime = 0;

        while(curMoveTime < moveTime)
        {
            curMoveTime += Time.deltaTime;

            transform.position = Vector3.Lerp(startPos.position, endPos.position, curMoveTime / moveTime);

            yield return null;
        }
    }

    void Move2()
    {
        curMoveTime += Time.deltaTime;

        transform.position = Vector3.Lerp(startPos.position, endPos.position, curMoveTime / moveTime);

        if(curMoveTime >= moveTime)
        {
            applyMove = false;
        }
    }
}
