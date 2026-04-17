using UnityEngine;

public abstract class InteractableObject : MonoBehaviour
{
    public Collider interactableArea;
    public ActObject connectedObj;
    bool interactState;

    public abstract void Interact();

    public abstract bool InteractCheck();
}
