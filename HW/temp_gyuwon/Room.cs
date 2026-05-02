using System.Collections.Generic;

public class Room
{
    public string roomId;
    public string roomName;
    public int maxPlayers;
    public List<Player> players = new List<Player>();
    public bool isPlaying;

    public void AddPlayer(Player player)
    {
        // 방 참가자 목록에 플레이어를 추가한다.
    }

    public void RemovePlayer(Player player)
    {
        // 방 참가자 목록에서 플레이어를 제거한다.
    }

    public bool IsFull()
    {
        // 현재 인원이 최대 인원 이상인지 확인한다.
    }

    public int GetReadyCount()
    {
        // 준비 완료한 플레이어 수를 계산한다.
        return count;
    }
}