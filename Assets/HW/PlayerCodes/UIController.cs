public class UIController
{
    private UISystem uiSystem;
    private InputController inputCtrl;

    public UIController(UISystem uiSystem, InputController input)
    {
        this.uiSystem = uiSystem;
        this.inputCtrl = input;
    }

    // 추가: PlayerController가 매 프레임 호출해서 데이터를 밀어넣음
    public void SetStatus(PlayerStatus status)
    {
        uiSystem.statusData = status;
    }

    public void UpdateUI()
    {
        var input = inputCtrl.GetInput();

        if (input.statusKey)
        {
            if (uiSystem.isVisible) uiSystem.hide();
            else uiSystem.show();
        }

        if (uiSystem.isVisible)
        {
            uiSystem.refresh(); // statusData가 채워진 상태로 호출됨
        }
    }
}
