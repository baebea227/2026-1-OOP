using System;
using System.Collections.Generic;

public class RoomManager
{
    public List<Room> roomList = new List<Room>();

    public Room CreateRoom(string roomName, int maxPlayers)
    {
        // 새로운 방 객체를 생성한다.
        // 방 정보를 설정한다.
        // 방 목록에 추가한다.

        Room newRoom = new Room();
        newRoom.roomId = Guid.NewGuid().ToString();
        newRoom.roomName = roomName;
        newRoom.maxPlayers = maxPlayers;
        newRoom.isPlaying = false;

        roomList.Add(newRoom);
        return newRoom;
    }

    public List<Room> SearchRoom(string keyword)
    {
        // 키워드와 일치하는 방을 검색한다.
        List<Room> result = new List<Room>();

        foreach (Room room in roomList)
        {
            if (room.roomName.Contains(keyword))
            {
                result.Add(room);
            }
        }

        return result;
    }

    public bool JoinRoom(Player player, Room room)
    {
        // 방 참가 가능 여부를 확인한다.
        // 가능하면 플레이어를 방에 추가한다.
        // 플레이어의 현재 방 정보를 갱신한다.

        if (room.IsFull())
        {
            return false;
        }

        room.AddPlayer(player);
        player.JoinRoom(room);
        return true;
    }

    public void LeaveRoom(Player player, Room room)
    {
        // 플레이어를 방에서 제거한다.
        // 플레이어의 현재 방 정보를 초기화한다.

        room.RemovePlayer(player);
        player.LeaveRoom();
    }

    public void RemoveEmptyRoom(Room room)
    {
        // 방이 비어 있으면 목록에서 제거한다.
        if (room.players.Count == 0)
        {
            roomList.Remove(room);
        }
    }
}