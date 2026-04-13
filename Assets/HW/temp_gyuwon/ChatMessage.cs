using System;

public class ChatMessage
{
    public Player sender;
    public string content;
    public DateTime sendTime;

    public string FormatMessage()
    {
        // 출력용 문자열 형태로 변환한다.
        return $"{sender.nickname}: {content}";
    }
}