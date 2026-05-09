using Fusion;
using UnityEngine;

public class ActivatableStructure : NetworkBehaviour, IActivatable
{
    
    public bool IsActive { get; set;}
    [SerializeField] bool firstState;
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
        gameObject.SetActive(true);
    }

    public void Deactivate()
    {
        IsActive = false;
        gameObject.SetActive(false);
    }
}
