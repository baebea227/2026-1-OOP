using System;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public Stage curStage;
    bool clearState;

    public GameObject clearUI;
    public Animator clearUIAnim;

    [SerializeField] SceneFlowManager sceneFlowManager;

    void Awake()
    {
        clearState = false;
        FindReferences();
    }

    void Start()
    {
        curStage.StageStart();
    }

    private void FindReferences()
    {
        if(sceneFlowManager == null)
        {
            sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);
        }            
    }

    void StageClearCheck()
    {
        if (curStage != null && curStage.IsCleared)
        {
            RPC_ClearProcess();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_ClearProcess()
    {
        clearState = curStage.IsCleared;
        curStage.StageEnd();   
        ShowClearUI();
    }

    void ShowClearUI()
    {
        clearUI.SetActive(true);
        clearUIAnim.SetTrigger("doClear");
        Invoke("HideClearUI", 2.9f);
        Invoke("MoveToWaitingRoom", 3);
    }

    void HideClearUI()
    {
        clearUI.SetActive(false);
    }

    void MoveToWaitingRoom()
    {
        if (sceneFlowManager != null && Object.HasStateAuthority)
        {
            sceneFlowManager.LoadWaitingRoomSceneNetwork();
        }
    }

    void Update()
    {
        if (!clearState)
        {
            StageClearCheck();
        }
    }
}
