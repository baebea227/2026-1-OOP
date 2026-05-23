using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    [SerializeField] Stage[] stageList;
    int curStageIndex;
    bool stageActive;

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
        stageList[curStageIndex].gameObject.SetActive(true);
        stageList[curStageIndex].StageStart();
        stageActive = true;
    }

    void StageClearCheck()
    {
        if (stageList[curStageIndex].IsCleared)
        {
            RPC_ClearProcess();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_ClearProcess()
    {
        // 임시 클리어 액션
        stageList[curStageIndex].StageEnd();
        stageList[curStageIndex].gameObject.SetActive(false);
        Debug.Log("Clear!!");
    }

    void Update()
    {
        if (stageActive)
        {
            StageClearCheck();
        }
    }
}
