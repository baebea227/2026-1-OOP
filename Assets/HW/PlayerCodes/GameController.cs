public class GameController
{
    private InputController inputController;
    private PlayerController playerController;
    private UIController uiController;

    public GameController()
    {
        // Entity 초기화
        InputHandler inputHandler = new InputHandler();
        Character character = new Character();
        UISystem uiSystem = new UISystem();

        // Controller 조립 (의존성 주입)
        inputController = new InputController(inputHandler);
        playerController = new PlayerController(character, inputController);
        uiController = new UIController(uiSystem, inputController);
    }

    // 메인 게임 루프
    public void Update()
    {
        inputController.UpdateInput();     // 1. 입력 업데이트
        playerController.UpdateAction();   // 2. 캐릭터 행동 업데이트
        uiController.UpdateUI();           // 3. UI 업데이트
    }
}