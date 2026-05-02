public class UISystem
{
    public bool isVisible { get; set; }
    public PlayerStatus statusData { get; set; }
    public StageProgress progressData { get; set; }

    public void show()
    {
        isVisible = true;
        // UI 활성화 로직
    }

    public void hide()
    {
        isVisible = false;
        // UI 비활성화 로직
    }

    public void refresh()
    {
        // statusData, progressData를 기반으로 UI 렌더링 갱신
    }
}