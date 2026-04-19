using System;

public class Player
{
    public string playerId;
    public string nickname;
    public bool isReady;
    public Room currentRoom;

    public void SetReady(bool ready)
    {
        // 플레이어의 준비 상태를 변경한다.
    }

    public void JoinRoom(Room room)
    {
        // 현재 참가한 방 정보를 저장한다.
    }

    public void LeaveRoom()
    {
        // 현재 참가한 방 정보를 초기화한다.
    }

    public void SendChat(string message)
    {
        // 채팅 매니저에 메시지 전송을 요청한다.
    }
}