public class Character
{
    public Vector3 position { get; set; }
    public int health { get; set; }
    public CharacterState state { get; set; }
    public PhysicsObject? heldObject { get; set; } // 널(Null) 허용

    public void move(Vector2 direction)
    {
        // 이동 로직 구현
        this.state = CharacterState.Moving;
    }

    public void grab(PhysicsObject obj)
    {
        if (obj != null && obj.isGrabbable)
        {
            this.heldObject = obj;
            this.state = CharacterState.Grabbing;
        }
    }

    public void throwObj(Vector3 force) // C# 키워드 충돌 방지를 위해 throw -> throwObj 명명
    {
        if (this.heldObject != null)
        {
            this.heldObject.applyForce(force);
            this.heldObject = null;
            this.state = CharacterState.Throwing;
        }
    }

    public void push(HeavyObject obj)
    {
        this.state = CharacterState.Pushing;
        // 밀기 로직 구현 (예: obj.addForce(...))
    }
}