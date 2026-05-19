using System;
using Fusion;

[Serializable]
public struct NetworkChatMessage
{
    public PlayerRef Sender;
    public string SenderName;
    public string Text;
    public DateTime SentAtUtc;
    public bool IsSystem;

    public NetworkChatMessage(PlayerRef sender, string senderName, string text, DateTime sentAtUtc, bool isSystem)
    {
        Sender = sender;
        SenderName = senderName;
        Text = text;
        SentAtUtc = sentAtUtc;
        IsSystem = isSystem;
    }
}
