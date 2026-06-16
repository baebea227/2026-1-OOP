using Fusion;
using UnityEngine;

public class StructureSet : NetworkBehaviour, IActivatable
{
    public bool IsActive { get; set; }

    [SerializeField] GameObject[] structuresList;
    IActivatable[] activatableList;

    void Awake()
    {
        activatableList = new IActivatable[structuresList.Length];
        for(int i=0; i<structuresList.Length; i++)
        {
            activatableList[i] = structuresList[i].GetComponent<IActivatable>();
        }
    }

    public void OnActivate(int n)
    {
        for(int i=0; i<activatableList.Length; i++)
        {
            activatableList[i].OnActivate(n);
        }
    }

    public void Activate(){}

    public void Deactivate(){}
}
