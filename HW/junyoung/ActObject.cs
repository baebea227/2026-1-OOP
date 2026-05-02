using UnityEngine;

public class ActObject : MonoBehaviour
{
    enum ActType {Move, Active};
    public Transform moveStartPos;
    public Transform moveEndPos;
    public float moveSpeed;

    void Interact()
    {
        // InteractableObject를 통해 해당 함수 호출
        // 오브젝트의 움직임을 구현
    }

    void Move()
    {
        // '움직임'을 구현 시 이용
    }

    void Activate()
    {
        // '활성화'를 구현 시 이용
    }
}
