using System;
using System.Collections.Generic;

public class ChatManager
{
    public List<ChatMessage> messageList = new List<ChatMessage>();

    public void SendMessage(Player player, string content)
    {
        // 새로운 메시지를 생성한다.
        // 발신자, 내용, 시간을 저장한다.
        // 메시지 목록에 추가한다.

        ChatMessage message = new ChatMessage();
        message.sender = player;
        message.content = content;
        message.sendTime = DateTime.Now;

        messageList.Add(message);
    }

    public void ReceiveMessage(ChatMessage message)
    {
        // 전달받은 메시지를 저장한다.
        messageList.Add(message);
    }

    public List<ChatMessage> GetMessages()
    {
        // 전체 메시지 목록을 반환한다.
        return messageList;
    }
}