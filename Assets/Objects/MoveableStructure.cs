using System.Collections;
using Fusion;
using UnityEngine;

public class MoveableStructure : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
    [SerializeField] int triggerCnt;
    [SerializeField] int curTriggerCnt;

    [SerializeField] float moveTime;
    float curMoveTime;
    Transform startPos;
    Transform endPos;
    [SerializeField] Transform posA;
    [SerializeField] Transform posB;

    void Awake()
    {
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
        }
        else
        {
            curTriggerCnt += n;
            if(curTriggerCnt == triggerCnt)
            {
                Activate();
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
        StartCoroutine(Move());
    }

    public void Deactivate()
    {
        IsActive = false;
        startPos = transform;
        endPos = posA;
        StartCoroutine(Move());
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
}
