public interface IActivatable
{
    bool IsActive { get; }
    void OnActivate(int n);
    void Activate();
    void Deactivate();
}
