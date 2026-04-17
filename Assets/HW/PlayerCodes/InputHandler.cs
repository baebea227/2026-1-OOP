public class InputHandler
{
    public Vector2 moveInput { get; set; }
    public bool grabKey { get; set; }
    public bool throwKey { get; set; }
    public bool pushKey { get; set; }
    public bool statusKey { get; set; }

    public void processInput()
    {
        // 플랫폼에 맞는 입력(키보드, 패드 등)을 받아와서 변수에 할당하는 로직
    }
}