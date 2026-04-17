using UnityEngine;

public class Button : InteractableObject
{
    bool isInteractable;

    public override void Interact()
    {
        // 상호작용 구현
    }

    public override bool InteractCheck()
    {
        //temp
        return true;
    }

    void Cooldown()
    {
        // 버튼 상호작용 쿨타임 구현
    }
}
