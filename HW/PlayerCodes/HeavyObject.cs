public class HeavyObject : PhysicsObject
{
    public float resistanceThreshold { get; set; }
    public float combinedForce { get; set; }
    public int pusherCount { get; set; }

    public new void addForce(Vector3 v)
    {
        // 힘 누적 로직 (예: combinedForce 증가)
    }

    public bool canMove()
    {
        // 누적된 힘이 저항 한계치를 넘었는지 확인
        return combinedForce > resistanceThreshold;
    }
}