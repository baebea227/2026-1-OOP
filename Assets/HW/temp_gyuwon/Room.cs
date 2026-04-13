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
        players.Add(player);
    }

    public void RemovePlayer(Player player)
    {
        // 방 참가자 목록에서 플레이어를 제거한다.
        players.Remove(player);
    }

    public bool IsFull()
    {
        // 현재 인원이 최대 인원 이상인지 확인한다.
        return players.Count >= maxPlayers;
    }

    public int GetReadyCount()
    {
        // 준비 완료한 플레이어 수를 계산한다.
        int count = 0;

        foreach (Player player in players)
        {
            if (player.isReady)
            {
                count++;
            }
        }

        return count;
    }
}