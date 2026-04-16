public class Character
{
    public string ownerId { get; set; }          // 추가: 이 캐릭터를 소유한 Player ID
    public Vector3 position { get; set; }
    public int health { get; set; }
    public CharacterState state { get; set; }
    public PhysicsObject? heldObject { get; set; }

    public void move(Vector2 direction)
    {
        // 이동 로직 구현
        this.state = CharacterState.Moving;
    }

    public void grab(PhysicsObject obj)
    {
        // 이미 누군가 들고 있으면 잡을 수 없음
        if (obj != null && obj.isGrabbable && obj.ownerId == null)
        {
            this.heldObject = obj;
            this.heldObject.ownerId = this.ownerId;  // 소유권 등록
            this.state = CharacterState.Grabbing;
        }
    }

    public void throwObj(Vector3 force) // C# 키워드 충돌 방지를 위해 throw -> throwObj 명명
    {
        if (this.heldObject != null)
        {
            this.heldObject.ownerId = null;          // 소유권 해제
            this.heldObject.applyForce(force);
            this.heldObject = null;
            this.state = CharacterState.Throwing;
        }
    }

    public void push(HeavyObject obj)
    {
        // HeavyObject는 들지 않고 미는 것이므로 ownerId 변경 없이 힘만 가함
        obj.addForce(new Vector3 { x = 0, y = 0, z = 10 }); // 방향은 실제 로직에 맞게
        this.state = CharacterState.Pushing;
    }
}
