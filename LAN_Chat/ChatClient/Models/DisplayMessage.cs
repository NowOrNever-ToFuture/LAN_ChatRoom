using Chat.Shared.Constants;
using Chat.Shared.Models;

namespace ChatClient.Models;

public class DisplayMessage : ChatMessage
{
    public DisplayMessage()
    {
    }

    public DisplayMessage(string senderName, string content, string currentUsername)
    {
        SenderName = senderName;
        Content = content;
        Timestamp = DateTime.Now;
        IsOwnMessage = string.Equals(senderName, currentUsername, StringComparison.OrdinalIgnoreCase);
        IsSystemMessage = string.Equals(senderName, AppConstants.SystemSenderName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsOwnMessage { get; set; }

    public bool IsSystemMessage { get; set; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public string SenderDisplayName => IsOwnMessage ? $"{SenderName} (You)" : SenderName;
}
