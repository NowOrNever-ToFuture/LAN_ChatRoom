using Chat.Shared.Constants;

namespace Chat.Shared.Models;

public enum MessageType
{
    Text,
    Image,
    File
}

/// <summary>
/// Core chat message DTO — shared between server and client.
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string GroupName { get; set; } = AppConstants.DefaultRoom;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public MessageType Type { get; set; } = MessageType.Text;
    
    // For Image/File: Base64 string of the file content
    public string? FileData { get; set; }
    
    // For File: size in bytes
    public long FileSize { get; set; }

    /// <summary>
    /// Snapshot of reactions at cache time: reactionType → count.
    /// Populated by server when serving cached messages; null for live messages.
    /// </summary>
    public Dictionary<string, int>? Reactions { get; set; }
}
