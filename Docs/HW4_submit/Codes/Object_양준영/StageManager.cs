using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    [SerializeField] Stage[] stageList;
    int curStageIndex;
    bool stageActive;

    public GameObject clearUI;

    void Awake()
    {
        stageActive = false;
    }

    // temp
    public int stageIndex;
    void Start()
    {
        SetStage(stageIndex);
    }

    // RPC 처리 필요?
    void SetStage(int index)
    {
        curStageIndex = index;
        stageActive = true;

        if (stageList != null &&
            curStageIndex >= 0 &&
            stageList[curStageIndex] != null)
            {
                stageList[curStageIndex].gameObject.SetActive(stageActive);
        }

        stageList[curStageIndex].StageStart();
    }

    void StageClearCheck()
    {
        if (stageList != null &&
        curStageIndex >= 0 &&
        stageList[curStageIndex] != null &&
        stageList[curStageIndex].IsCleared)
        {
            RPC_ClearProcess();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_ClearProcess()
    {
        // 임시 클리어 액션
        stageList[curStageIndex].StageEnd();
        stageActive = false;
        // stageList[curStageIndex].gameObject.SetActive(stageActive);
        Debug.Log("Clear!!");
        ShowClearUI();
    }

    void ShowClearUI()
    {
        clearUI.SetActive(true);
        Invoke("HideClearUI", 3);
    }

    void HideClearUI()
    {
        clearUI.SetActive(false);
    }

    void Update()
    {
        if (stageActive)
        {
            StageClearCheck();
        }
    }
}
