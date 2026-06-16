using Fusion;
using UnityEngine;

public class Stage : NetworkBehaviour
{
    [SerializeField] Transform goalPoint;
    public GameObject goalPointMarker;
    public bool IsCleared { get; private set; }
    bool isCurStage;

    void Awake()
    {
        IsCleared = false;
        isCurStage = false;
    }

    public void StageStart()
    {
        IsCleared = false;
        isCurStage = true;
    }

    public void StageEnd()
    {
        isCurStage = false;
        goalPointMarker.SetActive(false);
    }

    void ClearCheck()
    {
        int cnt = 0;
        var hits = Physics.OverlapSphere(goalPoint.position, 1.5f, -1);
        foreach(var entity in hits)
        {
            if (entity.CompareTag("Player"))
            {
                cnt++;
            }
        }
        if(cnt == 2)
        {
            IsCleared = true;
        }
    }

    void Update()
    {
        if (isCurStage)
        {
            ClearCheck();
        }
    }

    private void OnDrawGizmos()
    {
        if (isCurStage)
        {
            // Set the color with custom alpha.
            Gizmos.color = new Color(1f, 0f, 0f, 1f); // Red with custom alpha

            // Draw the sphere.
            Gizmos.DrawSphere(goalPoint.position, 1.5f);

            // Draw wire sphere outline.
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(goalPoint.position, 1.5f);
        }
    }
}
