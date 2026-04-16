public class GameController
{
    private InputController inputController;
    private PlayerController playerController;
    private UIController uiController;

    public GameController()
    {
        // Entity 초기화
        InputHandler inputHandler = new InputHandler();
        Player player = new Player { playerId = "player_001", name = "Hero" }; // 추가
        Character character = new Character();
        UISystem uiSystem = new UISystem();

        // Controller 조립 (의존성 주입)
        // 순환 참조 방지를 위해 UIController를 PlayerController보다 먼저 생성
        inputController = new InputController(inputHandler);
        uiController    = new UIController(uiSystem, inputController);
        playerController = new PlayerController(player, character,
                                                inputController, uiController);
    }

    // 메인 게임 루프
    public void Update()
    {
        inputController.UpdateInput();     // 1. 입력 업데이트
        playerController.UpdateAction();   // 2. 캐릭터 행동 업데이트 + SetStatus() 실행
        uiController.UpdateUI();           // 3. UI 업데이트 (statusData 이미 채워진 상태)
    }
}
