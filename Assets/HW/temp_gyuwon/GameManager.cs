public class GameManager
{
    public GameSession currentSession = new GameSession();

    public void StartGame(Room room, RuleManager ruleManager)
    {
        // 게임 시작 가능 여부를 확인한다.
        // 가능하면 세션을 시작한다.
        // 방 상태를 플레이 중으로 변경한다.
    }

    public void EndGame(Room room)
    {
        // 게임 세션을 종료한다.
        // 방 상태를 플레이 종료로 변경한다.
    }

    public void ExitGame(Player player)
    {
        // 플레이어의 게임 종료 처리를 수행한다.
    }
}
