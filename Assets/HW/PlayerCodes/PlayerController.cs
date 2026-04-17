public class PlayerController
{
    private Player player;
    private Character playerCharacter;
    private InputController inputCtrl;
    private UIController uiCtrl;          // 추가: 매 프레임 상태 공급 대상

    public PlayerController(Player player, Character character,
                            InputController input, UIController ui)
    {
        this.player = player;
        this.playerCharacter = character;
        this.inputCtrl = input;
        this.uiCtrl = ui;

        // 생성 시점에 Character에 소유자 연결
        this.playerCharacter.ownerId = player.playerId;
    }

    public void UpdateAction()
    {
        var input = inputCtrl.GetInput();

        if (input.moveInput.x != 0 || input.moveInput.y != 0)
            playerCharacter.move(input.moveInput);

        if (input.grabKey && playerCharacter.heldObject == null)
        {
            // 실제 구현: 주변 PhysicsObject 탐색 후 grab 호출
        }

        if (input.throwKey)
            playerCharacter.throwObj(new Vector3 { x = 0, y = 0, z = 15 });

        if (input.pushKey)
        {
            // 실제 구현: 주변 HeavyObject 탐색 후 push 호출
        }

        // 매 프레임 캐릭터 상태를 UI에 공급
        uiCtrl.SetStatus(new PlayerStatus
        {
            state = playerCharacter.state,
            isHoldingObject = playerCharacter.heldObject != null
        });
    }
}
