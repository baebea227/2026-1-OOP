public interface IActivatable
{
    void Activate();
    void Deactivate();
    bool IsActive { get; }
}
