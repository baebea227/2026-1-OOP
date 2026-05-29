using Fusion;
using Unity.VisualScripting;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    [SerializeField] GameObject[] stagePrefabs;
    Stage selectedStage;
    int curStageIndex;
    bool stageActive;

    public GameObject clearUI;

    [SerializeField] SceneFlowManager sceneFlowManager;

    [SerializeField] GameObject[] stageSelect;
    Transform[] stageSelectPos;

    void Awake()
    {
        stageActive = false;
    }

    void Start()
    {
        FindReferences();

        stageSelectPos = new Transform[stageSelect.Length];
        for(int i=0; i<stageSelect.Length; i++)
        {
            stageSelectPos[i] = stageSelect[i].transform;
        }
    }

    private void FindReferences()
    {
        if (sceneFlowManager == null)
            sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);
    }

    void SetStage(int index)
    {
        // curStageIndex = index;

        // if (stageList != null &&
        //     curStageIndex >= 0 &&
        //     stageList[curStageIndex] != null)
        // {
        //     stageActive = true;
        //     stageList[curStageIndex].gameObject.SetActive(stageActive);
        //     stageList[curStageIndex].StageStart();
        // }

        if (Runner.IsServer)
        {
            selectedStage = Runner.Spawn(
                stagePrefabs[index],
                new Vector3(3.5f, -0.5f, 3),
                Quaternion.identity,
                inputAuthority: null
            ).GetComponent<Stage>();

            selectedStage.StageStart();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_SetStage(int i) => SetStage(i);

    void StageClearCheck()
    {
        if (selectedStage != null &&
            curStageIndex >= 0 &&
            selectedStage.IsCleared)
        {
            RPC_ClearProcess();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_ClearProcess()
    {
        // 임시 클리어 액션
        if(selectedStage != null)
        {
            selectedStage.StageEnd();   
        }
        // stageActive = false;
        // stageList[curStageIndex].gameObject.SetActive(stageActive);
        ShowClearUI();
    }

    void ShowClearUI()
    {
        clearUI.SetActive(true);
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
        if (stageActive)
        {
            StageClearCheck();
        }
        else
        {
            SelectStage();
        }
    }

    void SelectStage()
    {
        for(int i=0; i< stageSelectPos.Length; i++)
        {
            int cnt = 0;
            var checker = Physics.OverlapSphere(stageSelectPos[i].position, 1.5f, -1);
            foreach(var entity in checker)
            {
                if (entity.CompareTag("Player"))
                {
                    cnt++;
                }
            }
            if(cnt == 2)
            {
                RPC_HideSelecter();
                SetStage(i);
                stageActive = true;
                break;
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_HideSelecter()
    {
        for(int i=0; i<stageSelect.Length; i++)
        {
            stageSelect[i].SetActive(false);
        }
    }
}
