namespace Chat.Shared.Models;

public class ChatMessage
{
    public string SenderName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
