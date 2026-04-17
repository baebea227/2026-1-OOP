public class PhysicsObject
{
    public string objectId { get; set; }
    public Vector3 position { get; set; }
    public Vector3 velocity { get; set; }
    public float mass { get; set; }
    public bool isGrabbable { get; set; }
    public string? ownerId { get; set; }

    public virtual void applyForce(Vector3 v)
    {
        // F = ma 로직에 따른 속도/위치 변화 구현
    }

    public void resetVelocity()
    {
        this.velocity = new Vector3 { x = 0, y = 0, z = 0 };
    }
}