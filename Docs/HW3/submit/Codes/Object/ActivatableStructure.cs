using Fusion;
using UnityEngine;

public class ActivatableStructure : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
    [SerializeField] bool isDisposable;
    [SerializeField] int triggerCnt;
    int curTriggerCnt;

    void Awake()
    {
        IsActive = firstState;
        gameObject.SetActive(firstState);
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
            if(curTriggerCnt == triggerCnt)
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
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        IsActive = false;
        gameObject.SetActive(false);
    }
}
