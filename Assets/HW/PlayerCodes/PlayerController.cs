public class PlayerController
{
    private Character playerCharacter;
    private InputController inputCtrl;

    public PlayerController(Character character, InputController input)
    {
        this.playerCharacter = character;
        this.inputCtrl = input;
    }

    public void UpdateAction()
    {
        var input = inputCtrl.GetInput();

        if (input.moveInput.x != 0 || input.moveInput.y != 0)
            playerCharacter.move(input.moveInput);

        if (input.grabKey) /* 잡기 로직 */ ;
        if (input.throwKey) /* 던지기 로직 */ ;
        if (input.pushKey) /* 밀기 로직 */ ;
    }
}