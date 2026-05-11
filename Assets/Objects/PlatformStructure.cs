using Fusion;
using NUnit.Framework;
using UnityEngine;

public class PlatformStructure : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
    [SerializeField] bool isDisposable;
    [SerializeField] int triggerCnt;
    int curTriggerCnt;

    [SerializeField] float moveTime;
    float curMoveTime;
    bool direction;
    [SerializeField] Transform posA;
    [SerializeField] Transform posB;

    void Awake()
    {
        IsActive = firstState;
        curTriggerCnt = firstState ? triggerCnt : 0;
        curMoveTime = 0;
        transform.position = posA.position;
        direction = true;
        
        Debug.Log(IsActive);
    }
    
    public void OnActivate(int n)
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
        else if(triggerCnt == 100)
        {
            return;
        }
        else
        {
            Deactivate();
        }
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    void Move()
    {
        if(curMoveTime >= moveTime)
        {
            curMoveTime = 0;
            direction = !direction;
        }

        curMoveTime += Time.deltaTime;

        transform.position = Vector3.Lerp(direction ? posA.position : posB.position, direction ? posB.position : posA.position, curMoveTime / moveTime);
    }

    void Update()
    {
        if (IsActive)
        {
            Move();
        }
    }
}
