using Fusion;
using NUnit.Framework;
using UnityEngine;

public class PlatformStructure : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
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
            Debug.Log("activate" + IsActive);
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
        Debug.Log("Activate");
    }

    public void Deactivate()
    {
        IsActive = false;
        Debug.Log("Deactivate");
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
        {Debug.Log("Moooooviiiiing...");
            Move();
        }
    }
}
