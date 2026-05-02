public class InputController
{
    private InputHandler inputHandler;

    public InputController(InputHandler handler)
    {
        this.inputHandler = handler;
    }

    public void UpdateInput()
    {
        // 매 프레임 입력값 갱신
        inputHandler.processInput();
    }

    // 다른 컨트롤러에서 입력 상태를 읽어갈 수 있도록 Getter 제공
    public InputHandler GetInput() => inputHandler;
}