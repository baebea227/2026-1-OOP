public class UIController
{
    private UISystem uiSystem;
    private InputController inputCtrl;

    public UIController(UISystem uiSystem, InputController input)
    {
        this.uiSystem = uiSystem;
        this.inputCtrl = input;
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
            uiSystem.refresh();
        }
    }
}