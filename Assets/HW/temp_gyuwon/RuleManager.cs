public class RuleManager
{
    public Room currentRoom;

    public bool CheckAllReady(Room room)
    {
        // 방 참가자 전원의 준비 여부를 확인한다.
        foreach (Player player in room.players)
        {
            if (!player.isReady)
            {
                return false;
            }
        }

        return true;
    }

    public bool CanStartGame(Room room)
    {
        // 게임 시작 가능 여부를 확인한다.
        // 참가자가 있는지 확인한다.
        // 전원이 준비 상태인지 확인한다.

        if (room.players.Count == 0)
        {
            return false;
        }

        return CheckAllReady(room);
    }
}